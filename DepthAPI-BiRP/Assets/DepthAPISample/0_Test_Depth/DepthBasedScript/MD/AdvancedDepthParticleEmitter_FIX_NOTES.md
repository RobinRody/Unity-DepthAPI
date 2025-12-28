# AdvancedDepthParticleEmitter 修復說明

## ?? 已修復的問題

### 問題 1：格狀效果（Grid Pattern）? → ?

**原因**：
- 舊版使用固定的 20x15 網格進行採樣
- 粒子嚴格按照網格點發射，產生明顯的格子排列

**修復方案**：
- ? **移除網格系統**：完全移除 `gridWidth`、`gridHeight`、`densityGrid` 等網格變量
- ? **隨機採樣**：每幀隨機選擇屏幕座標進行深度採樣
- ? **隨機偏移**：在發射位置添加垂直於射線的隨機偏移（`randomSpread`）

**新參數**：
```csharp
[Header("Sampling Settings")]
[SerializeField] private int samplesPerFrame = 20;      // 每幀採樣次數
[SerializeField] private float randomSpread = 0.1f;     // 粒子位置隨機擴散
```

---

### 問題 2：深度採樣不正確 ? → ?

**原因**：
- `_EnvironmentDepthTexture` 是 **Texture2DArray**（包含左右眼），而非 Texture2D
- 舊版直接使用屏幕 UV 座標讀取，沒有考慮重投影變換
- VR 雙眼渲染時，需要使用正確的眼睛索引和重投影矩陣

**修復方案**：
```csharp
private float SampleEnvironmentDepthAtScreenPoint(float screenX, float screenY)
{
    // 1. 從屏幕座標發射射線
    Ray ray = mainCamera.ViewportPointToRay(new Vector3(screenX, screenY, 0));
    
    // 2. 計算世界空間位置
    Vector3 worldPos = ray.origin + ray.direction * cullDistance;
    
    // 3. 獲取重投影矩陣和深度參數
    Matrix4x4[] reprojMatrices = Shader.GetGlobalMatrixArray(ReprojectionMatricesID);
    Vector4 zBufferParams = Shader.GetGlobalVector(ZBufferParamsID);
    
    // 4. 選擇正確的眼睛索引（VR 雙眼）
    int eyeIndex = 0;
#if !UNITY_EDITOR
    if (UnityEngine.XR.XRSettings.enabled)
    {
        eyeIndex = (UnityEngine.XR.XRSettings.stereoRenderingMode == 
                    UnityEngine.XR.XRSettings.StereoRenderingMode.SinglePassInstanced) 
                    ? 0 : (int)UnityEngine.XR.XRSettings.eyeTextureDesc.vrUsage;
    }
#endif

    // 5. 使用重投影矩陣將世界座標轉換為深度紋理空間
    Matrix4x4 reprojMatrix = reprojMatrices[eyeIndex];
    Vector4 reprojPos = reprojMatrix.MultiplyPoint(worldPos);

    // 6. NDC 座標轉 UV（-1~1 -> 0~1）
    float ndcX = reprojPos.x / reprojPos.w;
    float ndcY = reprojPos.y / reprojPos.w;
    float u = ndcX * 0.5f + 0.5f;
    float v = ndcY * 0.5f + 0.5f;

    // 7. 邊界檢查
    if (u < 0 || u > 1 || v < 0 || v > 1)
    {
        return -1f;
    }

    // 8. 從深度紋理讀取原始深度值
    RenderTexture depthTexture = Shader.GetGlobalTexture(DepthTextureID) as RenderTexture;
    // ... ReadPixels ...

    // 9. 線性化深度值
    float linearDepth = 1.0f / (zBufferParams.x * rawDepth + zBufferParams.y);
    
    return linearDepth;
}
```

**關鍵改進**：
- ? 使用 `_EnvironmentDepthReprojectionMatrices` 進行正確的座標轉換
- ? 正確處理 VR 雙眼渲染（左右眼不同的深度紋理）
- ? 世界座標 → NDC 座標 → UV 座標 的完整轉換鏈

---

## ?? 新舊對比

| 項目 | 舊版 | 新版 |
|------|------|------|
| **採樣方式** | 固定網格 (20x15) | 隨機採樣 |
| **粒子位置** | 網格點 | 隨機分佈 + 偏移 |
| **深度座標轉換** | 直接使用屏幕 UV | 世界座標 → 重投影 → UV |
| **VR 支持** | 未正確處理雙眼 | 正確處理左右眼索引 |
| **視覺效果** | 明顯格狀 | 自然分佈 |
| **深度準確性** | 不準確 | 準確 |

---

## ?? 新參數調整指南

### Sampling Settings

#### `samplesPerFrame` (預設: 20)
- **作用**：每幀隨機採樣的次數
- **影響**：
  - 越高：粒子密度越大，覆蓋越均勻，但性能開銷越大
  - 越低：性能好，但粒子可能稀疏
- **建議值**：
  - 低性能模式：10-15
  - 平衡模式：20-30
  - 高密度模式：40-50

#### `randomSpread` (預設: 0.1)
- **作用**：粒子在發射位置的隨機擴散半徑（米）
- **影響**：
  - 0：粒子嚴格在深度點上（可能還是會有輕微排列感）
  - 0.05-0.1：自然分佈
  - >0.2：過度擴散，可能穿牆
- **建議值**：0.05 - 0.15

---

## ?? 工作流程

### 舊版（格狀問題）
```
1. 更新密度網格 (每 0.05 秒)
   ↓
2. 固定網格採樣 (20x15 = 300 個點)
   ↓
3. 循環遍歷網格，按密度概率發射
   ↓
4. 粒子嚴格在網格點上
   ↓
結果：格狀排列
```

### 新版（自然分佈）
```
1. 每幀隨機選擇 N 個屏幕座標 (samplesPerFrame)
   ↓
2. 使用重投影矩陣正確採樣深度
   ↓
3. 根據深度決定是否發射
   ↓
4. 添加垂直於射線的隨機偏移
   ↓
結果：自然、連續分佈
```

---

## ??? 深度採樣技術細節

### 為什麼需要重投影矩陣？

Environment Depth 的深度紋理是從 **深度相機視角** 生成的，而非 **主相機視角**。

**錯誤做法**（舊版）：
```csharp
// ? 直接使用屏幕 UV - 錯誤！
Vector2 uv = new Vector2(screenX, screenY);
float depth = SampleTexture(depthTexture, uv);
```

**正確做法**（新版）：
```csharp
// ? 世界座標 → 深度相機空間 → UV
Vector3 worldPos = ray.origin + ray.direction * distance;
Vector4 depthCameraPos = reprojMatrix.MultiplyPoint(worldPos);
Vector2 uv = (depthCameraPos.xy / depthCameraPos.w) * 0.5 + 0.5;
float depth = SampleTexture(depthTexture, uv);
```

### 重投影矩陣的作用

```
重投影矩陣 = 深度相機的 VP 矩陣 * 主相機的逆 VP 矩陣
```

作用：將主相機視角的世界座標轉換為深度相機視角的 NDC 座標

---

## ?? 注意事項

### 性能考量

1. **每幀採樣次數**：
   - `samplesPerFrame` 越高，`ReadPixels` 調用越頻繁
   - 每次 `ReadPixels` 都是 GPU→CPU 同步操作（有開銷）
   - 建議在設備上測試並調整

2. **粒子壽命**：
   - 由於粒子不移動（velocity = 0），壽命決定密度
   - 壽命越長，場景中存在的粒子越多
   - 建議 1-3 秒

### 使用建議

1. **Inspector 參數**：
```
Samples Per Frame: 20-30
Random Spread: 0.08-0.12
Max Particles Per Frame: 40-60
Particle Lifetime: 1.5-2.5
```

2. **Density Curve**：
   - 近處（0）：密度 1.0（高密度）
   - 中間（0.5）：密度 0.5
   - 遠處（1.0）：密度 0（不發射）

3. **Cull Distance**：
   - 設置為您希望粒子出現的最遠距離
   - 建議 1.0-2.0 米

---

## ?? 視覺效果

修復後應該看到：
- ? **自然分佈**：粒子隨機分散，沒有格子感
- ? **正確深度**：粒子確實在真實表面上
- ? **平滑過渡**：從近到遠自然過渡
- ? **符合環境**：粒子跟隨真實環境的幾何形狀

---

## ?? 故障排除

### 問題：粒子還是有格狀
- 增加 `randomSpread` 到 0.15-0.2
- 增加 `samplesPerFrame` 到 30-40

### 問題：粒子位置仍然不對
- 檢查 Console 是否有錯誤
- 確認 `EnvironmentDepthManager.IsDepthAvailable` 為 true
- 在 Scene 視圖檢查 Gizmos（選中物體時可見）

### 問題：性能問題
- 降低 `samplesPerFrame` 到 10-15
- 降低 `maxParticlesPerFrame` 到 30
- 減少 `particleLifetime`

---

## ?? 總結

這次修復解決了兩個核心問題：

1. **格狀效果** → 改用隨機採樣 + 隨機偏移
2. **深度不準** → 正確使用重投影矩陣和 VR 雙眼處理

現在粒子系統能夠：
- ? 自然分佈在真實環境表面
- ? 正確感知深度
- ? 支持 VR 雙眼渲染
- ? 根據距離動態調整密度和外觀
