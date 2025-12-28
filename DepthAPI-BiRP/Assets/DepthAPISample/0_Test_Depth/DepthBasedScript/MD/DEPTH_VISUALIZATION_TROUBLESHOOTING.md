# 深度可視化故障排除指南

## ?? 問題：只顯示粉紅色（洋紅色）

### 原因分析
粉紅色/洋紅色 表示**深度值無效**，有以下幾種可能：

#### 1. 深度紋理未正確初始化
```
症狀: 整個畫面都是粉紅色
原因: _EnvironmentDepthTexture 為空或未更新
```

**檢查方法**：
```
1. 按 'D' 鍵查看 Console
2. 確認 "Depth Available: YES"
3. 確認深度紋理不是 NULL
```

**解決方案**：
```csharp
// 確保 EnvironmentDepthManager 已啟用
depthManager.enabled = true;

// 等待深度可用
while (!depthManager.IsDepthAvailable)
{
    yield return null;
}
```

#### 2. 深度值超出範圍
```
症狀: 大部分是粉紅色，只有極近處有其他顏色
原因: rawDepth >= 0.99 或 <= 0.01
```

**當前 Shader 邏輯**：
```hlsl
// 在 DepthVisualization.shader 中
float RawToLinearDepth(float rawDepth)
{
    // 檢查無效深度
    if (rawDepth >= 0.99 || rawDepth <= 0.01)
    {
        return 1000.0; // 返回超大值 → 顯示粉紅色
    }
    
    // 假設最大深度 10m
    float linearDepth = rawDepth * 10.0;
    return linearDepth;
}
```

**問題**：這個轉換可能不正確！

---

## ?? 修正方案

### 方案 1: 使用 ShowDepthMap Shader（簡單）

您的專案中已經有一個經過驗證的 Shader：

```
Assets\DepthAPISample\Shaders\ShowDepthMap.shader
```

這個 Shader 已經被 Meta 官方測試過，確認可以正確顯示深度。

**修改步驟**：

1. **使用現有的 ShowDepthMap Shader**：
```
材質 Inspector:
  Shader: Unlit/ShowDepthMap (而不是 Debug/DepthVisualization)
```

2. **或者複製其邏輯到我們的 Shader**：

```hlsl
// 從 ShowDepthMap.shader 複製
fixed4 frag (Interpolators i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 uv = float3(i.uv, 0); // 只顯示左眼
    
    // 直接採樣預處理過的深度紋理
    fixed4 col = _PreprocessedEnvironmentDepthTexture.Sample(
        sampler_PreprocessedEnvironmentDepthTexture, uv
    );
    
    col.a = 1;
    return col;
}
```

### 方案 2: 修正深度轉換（進階）

**問題診斷**：

```hlsl
// ? 當前的轉換可能不正確
float linearDepth = rawDepth * 10.0;
```

**為什麼不正確**：
- EnvironmentDepth 的存儲格式可能不是簡單的線性映射
- 不同設備的深度範圍可能不同
- 需要使用 ZBufferParams 進行正確轉換

**正確的轉換**（參考 EnvironmentDepthUtils.cs）：

查看您打開的文件：
```
Library\PackageCache\com.meta.xr.sdk.core@d6da9ab40659\Scripts\EnvironmentDepth\EnvironmentDepthUtils.cs
```

Meta 提供的轉換方法：

```csharp
public static Vector4 ComputeNdcToLinearDepthParameters(float near, float far)
{
    float m22 = -(far + near) / (far - near);
    float m23 = -(2.0f * far * near) / (far - near);
    float m32 = -1.0f;

    float depthScale = -m23;
    float depthOffset = -m22;

    return new Vector4(depthScale, depthOffset, m32, 0);
}
```

**修正後的 Shader**：

```hlsl
float RawToLinearDepth(float rawDepth)
{
    // 預處理過的深度紋理已經是線性的
    // 但需要正確的比例轉換
    
    // 方法 1: 直接使用原始值（最簡單）
    return rawDepth * 5.0; // 假設最大深度 5m
    
    // 方法 2: 使用 ZBuffer 參數（更準確）
    // float ndc = rawDepth * 2.0 - 1.0;
    // float linear = _EnvironmentDepthZBufferParams.x / 
    //                (ndc + _EnvironmentDepthZBufferParams.y);
    // return linear;
}
```

### 方案 3: 除錯模式（推薦先做）

**啟用原始深度顯示**：

在 Shader 中取消註解：

```hlsl
fixed4 frag (v2f i) : SV_Target
{
    // ...
    float rawDepth = _EnvironmentDepthTexture.Sample(...).r;
    
    // ? 啟用這行來查看原始深度值
    return fixed4(rawDepth, rawDepth, rawDepth, 1);
    
    // 暫時註解掉其他邏輯
    // float linearDepth = RawToLinearDepth(rawDepth);
    // ...
}
```

**預期結果**：
```
手很近 (0.3m):  rawDepth ? 0.03 - 0.1 (暗灰色)
手中距 (1.0m):  rawDepth ? 0.1 - 0.3 (中灰色)
手遠處 (2.0m):  rawDepth ? 0.3 - 0.5 (亮灰色)
無效深度:       rawDepth ? 0.0 或 1.0 (黑色或白色)
```

如果看到的不是上述範圍，說明深度紋理的格式不同。

---

## ?? 快速修正步驟

### 步驟 1: 使用官方 Shader（最快）

```
1. 選擇 DepthVisualizationMaterial
2. Shader → Unlit/ShowDepthMap
3. 運行查看是否顯示正常
```

如果這樣可以正常顯示，說明問題在於我們的深度轉換邏輯。

### 步驟 2: 對比兩個 Shader

如果 ShowDepthMap 可以顯示，對比兩個 Shader 的差異：

**ShowDepthMap.shader**（官方）：
```hlsl
// 直接採樣預處理紋理，不做轉換
fixed4 col = _PreprocessedEnvironmentDepthTexture.Sample(...);
return col;
```

**DepthVisualization.shader**（我們的）：
```hlsl
// 採樣原始紋理 + 複雜轉換
float rawDepth = _EnvironmentDepthTexture.Sample(...).r;
float linearDepth = RawToLinearDepth(rawDepth); // ← 問題可能在這裡
```

### 步驟 3: 簡化我們的 Shader

修改 `Assets/Shaders/DepthVisualization.shader`：

```hlsl
fixed4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
    uint eyeIndex = 0;
    float3 uv = float3(i.uv, eyeIndex);
    
    // ? 改用預處理紋理（已經是正確的深度值）
    float4 preprocessedDepth = _PreprocessedEnvironmentDepthTexture.Sample(
        sampler_PreprocessedEnvironmentDepthTexture, uv
    );
    
    // 預處理紋理可能包含多個通道的深度資訊
    // 通常 r 通道是主要深度
    float depth = preprocessedDepth.r;
    
    // 檢查無效值
    if (depth <= 0.0 || depth >= 1.0)
    {
        return fixed4(1, 0, 1, 1); // 粉紅色
    }
    
    // 映射到視覺化範圍
    // 假設預處理紋理已經正規化到 [0,1]
    // 我們需要將它映射到實際的米數
    float actualDepth = depth * 10.0; // 假設最大 10m
    
    // 正規化到顯示範圍
    float normalized = saturate((actualDepth - _NearPlane) / (_FarPlane - _NearPlane));
    
    // 應用顏色映射
    float3 color = HeatMapMapping(actualDepth);
    
    return fixed4(color, 1.0);
}
```

---

## ?? 除錯檢查清單

### ? 基礎檢查

- [ ] EnvironmentDepthManager.enabled = true
- [ ] depthManager.IsDepthAvailable = true
- [ ] _EnvironmentDepthTexture 不是 NULL
- [ ] 深度紋理尺寸正常（例如 256x256）

### ? Shader 檢查

- [ ] 使用正確的採樣器（sampler_EnvironmentDepthTexture）
- [ ] eyeIndex 設為 0（左眼）
- [ ] UV 座標在 [0,1] 範圍內

### ? 深度值檢查

啟用除錯模式，顯示原始 rawDepth：

```hlsl
return fixed4(rawDepth, rawDepth, rawDepth, 1);
```

**正常範圍**：
- 近處 (0.3m): 0.03 - 0.1
- 中距 (1.0m): 0.1 - 0.3
- 遠處 (2.0m): 0.3 - 0.5

**異常情況**：
- 全是 0.0（黑色）→ 深度紋理未更新
- 全是 1.0（白色）→ 深度無效
- 全是 0.99+（幾乎白）→ 觸發無效檢查

---

## ?? 實際測試步驟

### 測試 1: 驗證深度紋理

```csharp
// 在 DepthVisualizationDebugger.cs
private void OnGUI()
{
    var depthTex = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
    if (depthTex != null)
    {
        GUILayout.Label($"Depth Tex: {depthTex.width}x{depthTex.height}");
        GUILayout.Label($"Format: {depthTex.graphicsFormat}");
    }
    else
    {
        GUILayout.Label("Depth Tex: NULL!");
    }
}
```

### 測試 2: 對比官方 Shader

```
1. 創建兩個材質：
   - Material A: Shader = Unlit/ShowDepthMap (官方)
   - Material B: Shader = Debug/DepthVisualization (我們的)

2. 創建兩個 Quad，並排放置

3. 對比顯示結果：
   - 如果 A 正常，B 粉紅色 → 我們的轉換有問題
   - 如果都粉紅色 → 深度紋理本身有問題
   - 如果都正常 → 沒問題！
```

### 測試 3: 逐步除錯

```hlsl
// 步驟 1: 顯示 UV 座標
return fixed4(i.uv.x, i.uv.y, 0, 1);
// 預期: 紅色從左到右漸變，綠色從下到上漸變

// 步驟 2: 顯示原始深度
float rawDepth = _EnvironmentDepthTexture.Sample(...).r;
return fixed4(rawDepth, rawDepth, rawDepth, 1);
// 預期: 手近處暗，手遠處亮

// 步驟 3: 放大顯示
return fixed4(rawDepth * 10.0, rawDepth * 10.0, rawDepth * 10.0, 1);
// 預期: 更容易看到變化

// 步驟 4: 檢查範圍
if (rawDepth < 0.1)
    return fixed4(1, 0, 0, 1); // 紅色: 很近
else if (rawDepth < 0.3)
    return fixed4(0, 1, 0, 1); // 綠色: 中等
else
    return fixed4(0, 0, 1, 1); // 藍色: 遠
```

---

## ?? 最可能的解決方案

根據您的症狀（只有超近才顯示粉紅色，沒有其他顏色），最可能的原因是：

### 原因：深度轉換比例錯誤

```hlsl
// ? 錯誤的比例
float linearDepth = rawDepth * 10.0;
// 如果 rawDepth = 0.03，linearDepth = 0.3m
// 如果 rawDepth = 0.3，linearDepth = 3.0m

// 但實際上 Meta 的深度可能已經是米為單位
// 或者使用不同的編碼方式
```

### 建議的修正

**選項 A：直接使用預處理紋理（最簡單）**

```hlsl
// 使用 _PreprocessedEnvironmentDepthTexture 而不是 _EnvironmentDepthTexture
float depth = _PreprocessedEnvironmentDepthTexture.Sample(...).r;

// 這個紋理已經經過 Meta 的處理，格式更友好
```

**選項 B：複製官方邏輯**

從 `ShowDepthMap.shader` 直接複製：

```hlsl
fixed4 frag (Interpolators i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    float3 uv = float3(i.uv, 0);
    
    // 官方的做法：直接顯示預處理紋理
    fixed4 col = _PreprocessedEnvironmentDepthTexture.Sample(
        sampler_PreprocessedEnvironmentDepthTexture, uv);
    
    col.a = 1;
    return col; // 這應該會顯示正確的深度
}
```

然後再添加我們的顏色映射。

---

## ?? 立即執行的修正

我建議您立即嘗試這個修正：

### 修改 DepthVisualization.shader

將整個 `frag` 函數替換為：

```hlsl
fixed4 frag (v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
    
    uint eyeIndex = 0;
    float3 uv = float3(i.uv, eyeIndex);
    
    // ? 使用預處理紋理（已驗證可用）
    fixed4 preprocessed = _PreprocessedEnvironmentDepthTexture.Sample(
        sampler_PreprocessedEnvironmentDepthTexture, uv);
    
    // 預處理紋理的格式可能是：
    // r, g, b, a = 不同的深度資訊或梯度
    // 先顯示原始值查看
    
    // 測試模式：直接顯示
    // return preprocessed;
    
    // 假設 r 通道是主深度，範圍 [0,1] 對應 [0, max_depth]
    float normalizedDepth = preprocessed.r;
    
    // 映射到實際距離（假設 max 是 5m）
    float actualDepth = normalizedDepth * 5.0;
    
    // 正規化到顯示範圍
    float t = saturate((actualDepth - _NearPlane) / (_FarPlane - _NearPlane));
    
    // 應用熱力圖
    float3 color = HeatMapMapping(actualDepth);
    
    return fixed4(color, 1.0);
}
```

這應該能解決問題！試試看，然後告訴我結果。
