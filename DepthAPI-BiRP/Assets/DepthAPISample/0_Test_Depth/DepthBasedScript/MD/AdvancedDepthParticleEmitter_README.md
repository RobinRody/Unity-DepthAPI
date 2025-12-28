# AdvancedDepthParticleEmitter - 修改說明

## 1. 主要修改概述

原始代碼使用 `Physics.Raycast` 來檢測場景中的物體深度，這種方法**只能檢測到有碰撞體的虛擬物體**，無法感知真實世界的環境深度。

修改後的代碼直接從 `EnvironmentDepthManager` 獲取 **Meta Quest 3 的環境深度紋理**，能夠檢測到真實世界的物理表面（牆壁、桌子、地板等），使粒子能夠根據真實環境的深度進行發射。

---

## 2. 核心邏輯改動詳解

### 2.1 新增深度紋理相關變量

```csharp
// 新增：用於緩存環境深度紋理和相關參數
private static readonly int DepthTextureID = Shader.PropertyToID("_EnvironmentDepthTexture");
private static readonly int ReprojectionMatricesID = Shader.PropertyToID("_EnvironmentDepthReprojectionMatrices");
private static readonly int ZBufferParamsID = Shader.PropertyToID("_EnvironmentDepthZBufferParams");

private RenderTexture cachedDepthTexture;
private Matrix4x4[] reprojectionMatrices;
private Vector4 zBufferParams;
private float[,] depthGrid;  // 新增：存儲每個網格點的深度值
```

**說明**：
- `_EnvironmentDepthTexture`：Meta XR SDK 全局設置的環境深度紋理
- `_EnvironmentDepthReprojectionMatrices`：用於將世界座標重投影到深度紋理空間的矩陣
- `_EnvironmentDepthZBufferParams`：深度緩衝區參數，用於將非線性深度轉換為線性深度
- `depthGrid`：存儲每個網格採樣點的實際深度值

### 2.2 啟動流程改進

```csharp
private IEnumerator WaitForDepthAndStart()
{
    // 等待深度紋理可用
    while (!depthManager.IsDepthAvailable)
    {
        yield return null;
    }

    StartCoroutine(UpdateDensityGridRoutine());
    StartCoroutine(EmitParticlesRoutine());
}
```

**說明**：
- 深度紋理不是立即可用的，需要等待系統初始化
- 使用協程等待 `IsDepthAvailable` 為 true 才開始工作
- 避免在深度數據未準備好時嘗試訪問，防止錯誤

### 2.3 深度紋理緩存更新

```csharp
private void UpdateDepthTextureCache()
{
    // 從全局 Shader 屬性獲取深度紋理
    cachedDepthTexture = Shader.GetGlobalTexture(DepthTextureID) as RenderTexture;
    
    // 獲取重投影矩陣（用於雙眼立體渲染）
    if (Shader.GetGlobalMatrixArray(ReprojectionMatricesID) != null)
    {
        Shader.GetGlobalMatrixArray(ReprojectionMatricesID, reprojectionMatrices);
    }
    
    // 獲取深度緩衝區參數
    zBufferParams = Shader.GetGlobalVector(ZBufferParamsID);
}
```

**說明**：
- `EnvironmentDepthManager` 會自動將深度紋理設置為全局 Shader 屬性
- 每次更新密度網格前，先緩存這些全局參數
- `reprojectionMatrices` 是一個包含左右眼矩陣的陣列（VR 雙眼渲染）

### 2.4 核心：環境深度採樣

```csharp
private float SampleEnvironmentDepth(float screenX, float screenY)
{
    if (cachedDepthTexture == null || reprojectionMatrices == null || reprojectionMatrices.Length < 2)
    {
        return -1f;
    }

    // 確定使用哪隻眼睛的深度紋理（VR 雙眼）
    int eyeIndex = 0;
#if UNITY_EDITOR
    eyeIndex = 0;
#else
    eyeIndex = (int)UnityEngine.XR.XRSettings.eyeTextureDesc.vrUsage;
#endif
    
    Vector2 uv = new Vector2(screenX, screenY);
    
    // 從 RenderTexture 讀取深度值
    RenderTexture prevRT = RenderTexture.active;
    RenderTexture.active = cachedDepthTexture;
    
    Texture2D tempTex = new Texture2D(1, 1, TextureFormat.RFloat, false);
    tempTex.ReadPixels(new Rect(uv.x * cachedDepthTexture.width, uv.y * cachedDepthTexture.height, 1, 1), 0, 0);
    tempTex.Apply();
    
    float rawDepth = tempTex.GetPixel(0, 0).r;
    Destroy(tempTex);
    
    RenderTexture.active = prevRT;
    
    // 將非線性深度轉換為線性深度
    float linearDepth = LinearizeDepth(rawDepth);
    
    return linearDepth;
}
```

**說明**：
- **屏幕坐標到紋理採樣**：將歸一化屏幕坐標（0-1）轉換為深度紋理的像素位置
- **ReadPixels**：從 GPU 的 RenderTexture 讀取單個像素的深度值（這是同步操作，有性能開銷）
- **rawDepth**：從深度紋理讀取的原始非線性深度值（通常是 0-1 範圍）
- **linearDepth**：轉換後的線性世界空間深度（以米為單位）

### 2.5 深度線性化

```csharp
private float LinearizeDepth(float rawDepth)
{
    float nearZ = zBufferParams.z;
    float farZ = zBufferParams.w;
    
    if (nearZ == 0 && farZ == 0)
    {
        nearZ = 0.1f;
        farZ = 10f;
    }
    
    // Unity 的深度線性化公式
    float linear = 1.0f / (zBufferParams.x * rawDepth + zBufferParams.y);
    return linear;
}
```

**說明**：
- GPU 深度緩衝區使用非線性深度（z/w），在近處精度高，遠處精度低
- `zBufferParams` 包含用於反向轉換的參數：
  - `x` = 1 - far/near
  - `y` = far/near
  - `z` = near
  - `w` = far
- 公式 `1.0 / (x * rawDepth + y)` 將非線性深度轉換為線性世界空間距離

### 2.6 網格單元更新邏輯

```csharp
private void UpdateGridCell(int gridX, int gridY, float screenX, float screenY)
{
    Ray ray = mainCamera.ViewportPointToRay(new Vector3(screenX, screenY, 0));
    
    // **關鍵改動**：使用環境深度紋理而非物理射線
    float envDepth = SampleEnvironmentDepth(screenX, screenY);
    
    if (envDepth > 0 && envDepth < maxDepth)
    {
        // 根據深度計算世界空間位置
        Vector3 worldPos = ray.origin + ray.direction * envDepth;
        worldPositionGrid[gridX, gridY] = worldPos + ray.direction * surfaceOffset;
        depthGrid[gridX, gridY] = envDepth;

        // 根據深度計算密度
        if (envDepth > cullDistance)
        {
            densityGrid[gridX, gridY] = 0f;
        }
        else
        {
            float normalizedDepth = Mathf.InverseLerp(minDepth, cullDistance, envDepth);
            densityGrid[gridX, gridY] = densityCurve.Evaluate(normalizedDepth);
        }
    }
    else
    {
        // 無效深度
        densityGrid[gridX, gridY] = 0f;
        worldPositionGrid[gridX, gridY] = ray.origin + ray.direction * maxDepth;
        depthGrid[gridX, gridY] = maxDepth;
    }
}
```

**說明**：
- **原始邏輯**：使用 `Physics.Raycast` 只能檢測虛擬物體
- **新邏輯**：使用 `SampleEnvironmentDepth` 檢測真實環境表面
- `surfaceOffset`：將粒子稍微偏移到表面前方，避免 z-fighting
- 使用 `densityCurve` 根據深度調整粒子密度（近處密度高，遠處密度低）

### 2.7 粒子發射邏輯

```csharp
private void EmitParticleAtGridPosition(int gridX, int gridY)
{
    Vector3 basePosition = worldPositionGrid[gridX, gridY];
    Vector3 randomOffset = Random.insideUnitSphere * 0.05f;
    Vector3 position = basePosition + randomOffset;

    // 使用緩存的深度值，而非重新計算
    float depth = depthGrid[gridX, gridY];
    float normalizedDepth = Mathf.InverseLerp(minDepth, cullDistance, depth);

    Color color = particleColorGradient.Evaluate(normalizedDepth);
    float size = Mathf.Lerp(particleSizeRange.y, particleSizeRange.x, normalizedDepth);

    emitParams.position = position;
    emitParams.velocity = Random.insideUnitSphere * 0.05f;
    emitParams.startLifetime = particleLifetime;
    emitParams.startSize = size;
    emitParams.startColor = color;

    particleSystem.Emit(emitParams, 1);
}
```

**說明**：
- 使用 `depthGrid` 中緩存的深度值，避免重複採樣
- 粒子顏色和大小根據深度動態調整
- 添加隨機偏移使粒子分佈更自然

---

## 3. 性能考量

### 3.1 優化點
1. **批量更新**：深度網格以固定頻率更新（`updateRate`），而非每幀
2. **緩存深度**：將採樣的深度值存儲在 `depthGrid` 中，粒子發射時直接使用
3. **分幀處理**：使用 `maxParticlesPerFrame` 限制每幀發射的粒子數量

### 3.2 性能開銷
1. **ReadPixels**：從 GPU 讀取紋理到 CPU 是同步操作，有一定開銷
2. **建議**：
   - 降低 `gridWidth` 和 `gridHeight`（如 10x8）
   - 增加 `updateRate`（如 0.1 秒）
   - 在設備上測試並調整參數

---

## 4. 使用方法

### 4.1 場景設置
1. 確保場景中有 `EnvironmentDepthManager` 組件
2. 在粒子系統物體上添加 `AdvancedDepthParticleEmitter` 腳本
3. 在 Inspector 中指定 `depthManager` 引用（或自動查找）

### 4.2 參數調整指南

| 參數 | 建議值 | 說明 |
|------|--------|------|
| `minDepth` | 0.3 | 最小檢測深度（米） |
| `maxDepth` | 5.0 | 最大檢測深度（米） |
| `cullDistance` | 1.5 | 超過此距離的表面不發射粒子 |
| `surfaceOffset` | 0.02 | 粒子距離表面的偏移量（避免穿模） |
| `gridWidth` x `gridHeight` | 20x15 或 10x8 | 採樣網格解析度（越低性能越好） |
| `updateRate` | 0.05-0.1 | 網格更新頻率（秒） |
| `maxParticlesPerFrame` | 30-50 | 每幀最大發射粒子數 |

---

## 5. 搭配的 BRP Shader 建議

粒子需要使用支持環境遮擋的 Shader，推薦使用以下 Shader：

### 5.1 推薦 Shader

```
Meta/Depth/BiRP/Occlusion ParticleStandardUnlit
```

這是 Meta 提供的支持環境深度遮擋的粒子 Shader（BiRP 版本）。

### 5.2 Shader 特性

1. **自動遮擋**：粒子會被真實環境遮擋
2. **軟/硬遮擋模式**：通過 `EnvironmentDepthManager.OcclusionShadersMode` 切換
3. **深度偏移**：支持 `_EnvironmentDepthBias` 參數調整 z-fighting

### 5.3 材質設置步驟

1. 創建新材質
2. 選擇 Shader：`Meta/Depth/BiRP/Occlusion ParticleStandardUnlit`
3. 設置紋理和顏色
4. 調整透明度混合模式（建議使用 Alpha Blend）
5. 將材質指定給粒子系統的 Renderer

### 5.4 代碼中動態調整深度偏移（可選）

```csharp
// 在粒子材質上設置環境深度偏移
Material particleMaterial = GetComponent<ParticleSystemRenderer>().material;
particleMaterial.SetFloat("_EnvironmentDepthBias", 0.05f);
```

### 5.5 其他可用的 BiRP Shader

如果需要更多光照效果，可使用：
- `Meta/Depth/BiRP/Occlusion Standard`（支持 PBR 光照，但粒子系統通常不需要）

---

## 6. 完整工作流程

```
1. EnvironmentDepthManager (Meta XR SDK)
   ↓
   提供環境深度紋理到全局 Shader 屬性

2. AdvancedDepthParticleEmitter.cs
   ↓
   每 updateRate 秒從深度紋理採樣
   ↓
   更新 gridWidth x gridHeight 的密度網格
   ↓
   根據密度決定是否在該位置發射粒子

3. 粒子系統
   ↓
   使用 Occlusion ParticleStandardUnlit Shader
   ↓
   粒子自動被真實環境遮擋
```

---

## 7. 故障排除

### 問題：粒子沒有發射
- 檢查 `EnvironmentDepthManager.IsDepthAvailable` 是否為 true
- 確認設備支持深度 API（需要 Quest 3）
- 查看 Console 是否有錯誤訊息

### 問題：粒子位置不正確
- 調整 `surfaceOffset` 參數
- 檢查 `mainCamera` 是否正確指向主相機
- 確認深度紋理解析度是否足夠

### 問題：性能問題
- 降低 `gridWidth` 和 `gridHeight`
- 增加 `updateRate`
- 減少 `maxParticlesPerFrame`
- 考慮使用 Compute Shader 優化深度採樣

### 問題：粒子沒有被遮擋
- 確認使用了正確的 Occlusion Shader
- 檢查 `EnvironmentDepthManager.OcclusionShadersMode` 設置
- 確認材質的 Render Queue 設置正確

---

## 8. 進階優化建議

### 8.1 使用 Compute Shader
將深度採樣和網格更新移到 GPU 上執行，避免 `ReadPixels` 的 CPU-GPU 同步開銷。

### 8.2 層次細節 (LOD)
根據相機距離動態調整網格解析度和粒子密度。

### 8.3 空間分區
使用八叉樹或其他空間分區結構，只更新視錐體內的網格單元。

### 8.4 異步深度讀取
使用 `AsyncGPUReadback` 異步讀取深度數據，避免阻塞渲染管線。

---

## 9. 結論

修改後的 `AdvancedDepthParticleEmitter` 能夠：
- ? 直接使用 Meta Quest 3 的環境深度數據
- ? 在真實世界表面發射粒子
- ? 根據距離動態調整粒子密度和外觀
- ? 與遮擋 Shader 配合實現真實的 MR 效果

搭配 `Meta/Depth/BiRP/Occlusion ParticleStandardUnlit` Shader，可以創建出能夠感知真實環境、並正確被遮擋的粒子效果。
