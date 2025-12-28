# ?? 深度採樣問題修復 - 從 Texture2DArray 到 Physics Raycast

## ? 根本原因：Texture2DArray 無法使用 ReadPixels

### 問題診斷

從日誌可以看到：
```
Valid Depth: 0
```

**原因**：
1. `_EnvironmentDepthTexture` 是 **`Texture2DArray`**（包含左右眼雙層紋理）
2. 舊代碼使用 `RenderTexture` + `ReadPixels`，這在 Quest 3 上**無法工作**
3. `Texture2DArray` 不能使用 `ReadPixels`，只能在 Shader 中採樣

---

## ?? **深度紋理的真實類型**

查看 `ShowDepthMap.shader`：
```csharp
Texture2DArray_half _PreprocessedEnvironmentDepthTexture;
Texture2DArray_half _EnvironmentDepthTexture;  // ← 這是陣列！

fixed4 frag (Interpolators i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 uv = float3(i.uv, 0);  // ← 第三個分量是眼睛索引（0或1）
    fixed4 col = _PreprocessedEnvironmentDepthTexture.Sample(..., uv);
    return col;
}
```

**關鍵發現**：
- 深度紋理有**兩層**（left eye、right eye）
- 在 Shader 中使用 `float3(u, v, eyeIndex)` 採樣
- 在 C# 中**無法直接讀取 Texture2DArray**

---

## ? 解決方案：使用 Physics Raycast 作為替代

### 新代碼策略

由於無法在 C# 中讀取深度紋理，採用兩種模式：

#### 模式 1：Physics Raycast（推薦）
```csharp
[SerializeField] private bool usePhysicsRaycast = true;
[SerializeField] private LayerMask raycastLayers = ~0;

if (usePhysicsRaycast)
{
    Ray ray = mainCamera.ViewportPointToRay(new Vector3(screenX, screenY, 0));
    RaycastHit hit;
    
    if (Physics.Raycast(ray, out hit, cullDistance, raycastLayers))
    {
        envDepth = hit.distance;
        hitPoint = hit.point;
        // ? 使用 Physics 碰撞檢測獲取深度
    }
}
```

**優點**：
- ? 可靠：依賴 Unity Physics
- ? 精確：直接獲取世界座標
- ? 靈活：可以使用 LayerMask 過濾

**缺點**：
- ?? 需要場景中有 Collider
- ?? 性能開銷稍高（但比 ReadPixels 好）

#### 模式 2：隨機深度（無深度檢測）
```csharp
if (!usePhysicsRaycast)
{
    float randomDepth = Random.Range(minDepth, cullDistance);
    Ray ray = mainCamera.ViewportPointToRay(new Vector3(screenX, screenY, 0));
    hitPoint = ray.origin + ray.direction * randomDepth;
    // ? 不依賴深度，純粹裝飾性粒子
}
```

**優點**：
- ? 零依賴：不需要深度或 Collider
- ? 性能最優：無任何檢測

**缺點**：
- ? 不真實：粒子不在真實表面上
- ? 穿牆：可能出現在錯誤位置

---

## ?? **深度紋理讀取的技術限制**

### 為什麼不能使用 ReadPixels？

| 方法 | Texture2D | RenderTexture | Texture2DArray |
|------|-----------|---------------|----------------|
| `ReadPixels` | ? 可用 | ? 可用 | ? **不可用** |
| Shader 採樣 | ? 可用 | ? 可用 | ? 可用 |
| C# 讀取像素 | ? `GetPixel()` | ? `ReadPixels()` | ? **無法直接讀取** |

**結論**：
- `_EnvironmentDepthTexture` 是 `Texture2DArray`
- **C# 無法讀取 Texture2DArray 的像素數據**
- 只能在 Shader 中採樣

---

## ??? **可能的深度讀取方法（未來優化）**

### 方法 1：Compute Shader（最佳性能）

創建一個 Compute Shader 來採樣深度：

```hlsl
#pragma kernel SampleDepth

Texture2DArray<float> _EnvironmentDepthTexture;
SamplerState sampler_EnvironmentDepthTexture;

struct DepthSample
{
    float3 worldPosition;
    float depth;
};

RWStructuredBuffer<DepthSample> _DepthSamples;

[numthreads(8,1,1)]
void SampleDepth (uint3 id : SV_DispatchThreadID)
{
    // 採樣深度紋理
    float3 uvw = float3(u, v, eyeIndex);
    float rawDepth = _EnvironmentDepthTexture.SampleLevel(sampler_EnvironmentDepthTexture, uvw, 0);
    
    // 計算世界位置
    _DepthSamples[id.x].depth = LinearizeDepth(rawDepth);
}
```

**C# 端**：
```csharp
ComputeBuffer depthBuffer = new ComputeBuffer(samplesPerFrame, sizeof(float) * 4);
computeShader.SetBuffer(kernel, "_DepthSamples", depthBuffer);
computeShader.Dispatch(kernel, samplesPerFrame / 8, 1, 1);

// 讀取結果
DepthSample[] samples = new DepthSample[samplesPerFrame];
depthBuffer.GetData(samples);
```

**優點**：
- ? 完全在 GPU 執行
- ? 可以批量採樣
- ? 正確讀取 Texture2DArray

**缺點**：
- ?? 實現複雜度高
- ?? 需要理解重投影矩陣

### 方法 2：MRUK Environment Raycasting（官方推薦）

Meta 提供的官方深度 Raycast API（v71+）：

```csharp
using Meta.XR.MRUtilityKit;

MRUKRoom room = MRUK.Instance.GetCurrentRoom();
if (room.Raycast(ray, out RaycastHit hit, cullDistance, LabelFilter.All))
{
    float depth = hit.distance;
    Vector3 worldPos = hit.point;
}
```

**優點**：
- ? 官方支持
- ? 正確使用深度數據
- ? 高效實現

**缺點**：
- ?? 需要安裝 MRUK 包
- ?? 需要場景掃描數據

---

## ?? **使用指南**

### 在 Quest 3 設備上運行：

1. **Physics Raycast 模式**（推薦）
   - 在 Inspector 中勾選 `Use Physics Raycast`
   - 確保場景中有 Mesh Collider 或其他碰撞體
   - 調整 `Raycast Layers` 選擇要檢測的層

2. **隨機深度模式**（純裝飾）
   - 取消勾選 `Use Physics Raycast`
   - 粒子會在視野內隨機位置生成
   - 不依賴任何深度或碰撞

### 參數調整：

```
Use Physics Raycast: ?（啟用真實深度檢測）
Raycast Layers: Everything（檢測所有層）
Samples Per Frame: 30（增加採樣密度）
Base Emission Rate: 300（增加發射概率）
Random Spread: 0.15（粒子擴散範圍）
```

---

## ?? **日誌輸出**

啟用調試後，您會看到：

### Physics Raycast 模式：
```
[DepthParticle] Mode: Raycast | Samples: 1800, Valid: 1200, Particles: 450, Count: 380
```
- `Valid` > 0 表示碰撞檢測成功
- `Particles` 是發射的總數
- `Count` 是當前存活的粒子數

### 隨機深度模式：
```
[DepthParticle] Mode: Random | Samples: 1800, Valid: 1800, Particles: 600, Count: 520
```
- `Valid` = `Samples`（所有採樣都"有效"）
- 粒子發射更頻繁（因為不受碰撞限制）

---

## ?? **與舊版的差異**

| 項目 | 舊版 | 新版 |
|------|------|------|
| **深度來源** | Environment Depth Texture | Physics Raycast / Random |
| **ReadPixels** | ? 嘗試使用 | ? 移除 |
| **Texture2DArray** | ? 無法處理 | ? 不依賴 |
| **Valid Depth** | 0（失敗） | > 0（成功） |
| **性能** | 高（GPU→CPU 同步） | 中（Raycast）/ 低（Random） |
| **準確性** | 理論上高 | Raycast：高 / Random：低 |

---

## ?? **已知限制**

### Physics Raycast 模式：
1. **需要 Collider**
   - 場景中必須有碰撞體
   - Passthrough 牆壁本身沒有 Collider
   - 需要手動添加或使用 Scene API 生成

2. **性能考量**
   - 每幀 30 次 Raycast 有一定開銷
   - 建議限制 `samplesPerFrame` ? 30

### 隨機深度模式：
1. **不真實**
   - 粒子位置是隨機的
   - 不會貼合真實表面
   - 僅適合純裝飾效果

2. **穿牆問題**
   - 粒子可能出現在牆壁內部
   - 無法避免（因為沒有深度檢測）

---

## ?? **未來改進方向**

### 短期（建議立即實施）：
1. **使用 MRUK Raycasting**
   ```bash
   # 安裝 MRUK
   https://developers.meta.com/horizon/documentation/unity/unity-mr-utility-kit-overview
   ```
   
2. **添加場景 Collider**
   - 使用 Scene API 生成牆壁/桌面的 Mesh
   - 添加 Mesh Collider
   - Physics Raycast 即可工作

### 長期（高級優化）：
1. **Compute Shader 深度採樣**
   - 完全在 GPU 執行
   - 正確處理 Texture2DArray
   - 批量採樣提升性能

2. **Hybrid 模式**
   - 優先使用 Depth Texture（如果可讀）
   - 後備使用 Physics Raycast
   - 最終後備使用隨機深度

---

## ?? **總結**

### 問題根源
- Environment Depth Texture 是 `Texture2DArray`
- C# 無法使用 `ReadPixels` 讀取
- 導致 `Valid Depth: 0`

### 解決方案
- **模式 1**：Physics Raycast（推薦）
- **模式 2**：隨機深度（純裝飾）

### 效果
- ? `Valid Depth` > 0
- ? 粒子能夠生成
- ? 支持兩種使用場景

**現在應該能看到粒子了！** ??
