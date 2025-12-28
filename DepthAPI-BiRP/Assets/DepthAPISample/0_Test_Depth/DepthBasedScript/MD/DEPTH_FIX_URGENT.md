# ?? 緊急修復：只顯示粉紅色的問題

## 問題
深度可視化 Quad 只顯示粉紅色（洋紅色），沒有其他顏色變化。

## 快速解決方案（3 個選項）

### 選項 1：使用官方 Shader（最快，推薦） ?

**步驟**：
1. 選擇您的材質 `DepthVisualizationMaterial`
2. Shader 改為：`Unlit/ShowDepthMap`
3. 運行查看

**如果這樣可以顯示深度圖**：
- ? 說明深度紋理本身沒問題
- ? 我們的 Shader 轉換邏輯有誤

---

### 選項 2：使用修正版 Shader（中等難度）

**步驟**：
1. 我已經創建了 `Assets/Shaders/DepthVisualizationFixed.shader`
2. 選擇您的材質
3. Shader 改為：`Debug/DepthVisualizationFixed`
4. 勾選 `Use Preprocessed` (啟用預處理紋理)
5. 運行查看

**這個 Shader 的改進**：
```
? 使用預處理深度紋理（更可靠）
? 簡化深度轉換邏輯
? 添加除錯模式開關
```

---

### 選項 3：手動除錯（進階）

#### 步驟 1: 檢查深度紋理是否存在

**運行場景 → 按 'D' 鍵**

查看 Console 輸出：
```
=== DEPTH TEXTURE DEBUG INFO ===
Depth Texture: 256x256, Dimension: Tex2DArray
```

**如果顯示 NULL**：
- 問題：EnvironmentDepthManager 沒有正確初始化
- 解決：確保 `depthManager.enabled = true`

#### 步驟 2: 查看原始深度值

**修改 Shader**（臨時除錯）：

打開 `Assets/Shaders/DepthVisualization.shader`，找到 `frag` 函數，添加：

```hlsl
fixed4 frag (v2f i) : SV_Target
{
    // ...原有代碼...
    float rawDepth = _EnvironmentDepthTexture.Sample(...).r;
    
    // ? 啟用這行來查看原始值
    return fixed4(rawDepth * 10.0, rawDepth * 10.0, rawDepth * 10.0, 1);
    
    // 暫時註解掉其他邏輯
}
```

**預期結果**：
```
手很近:  看到暗灰色
手遠處:  看到亮灰色
無深度:  看到黑色或白色
```

**如果全是黑色**：
- rawDepth 太小（< 0.1）
- 需要放大顯示：`rawDepth * 20.0`

**如果全是白色或粉紅**：
- rawDepth 無效（= 0 或 = 1）
- 深度紋理未更新

#### 步驟 3: 測試不同的縮放比例

在 Shader 中嘗試不同的縮放：

```hlsl
// 測試 1: 假設最大深度 5m
float depth = rawDepth * 5.0;

// 測試 2: 假設最大深度 10m
float depth = rawDepth * 10.0;

// 測試 3: 假設最大深度 3m
float depth = rawDepth * 3.0;

// 測試 4: 不縮放（rawDepth 可能已經是米）
float depth = rawDepth;
```

哪個看起來最合理？

---

## ?? 最推薦的修復流程

### 流程 1: 驗證深度紋理

```
1. 運行場景
2. 按 'D' 查看深度資訊
3. 確認 Depth Available: YES
4. 確認深度紋理不是 NULL
```

### 流程 2: 使用官方 Shader

```
1. 材質 Shader → Unlit/ShowDepthMap
2. 如果可以顯示 → 深度紋理正常
3. 如果不能顯示 → 深度紋理有問題
```

### 流程 3: 切換到修正版

```
1. 使用 Debug/DepthVisualizationFixed
2. ? Use Preprocessed
3. 調整 Near Plane 和 Far Plane
4. 查看效果
```

---

## ?? 常見症狀與原因

### 症狀 1: 整個畫面粉紅色

**原因**：
```
? 深度值 <= 0.001 (無效)
? 深度值 > 50.0 (太遠)
? 深度轉換失敗
```

**診斷**：
```csharp
// 在 DepthVisualizationDebugger 按 'D'
if (depthTexture == null)
    → 深度紋理未初始化
else
    → 深度轉換邏輯錯誤
```

### 症狀 2: 只有超近才有顏色

**原因**：
```
? 深度縮放比例錯誤
? 顯示範圍太小
```

**診斷**：
```
Near Plane: 0.3
Far Plane: 2.0

如果只在 < 0.1m 才有顏色
→ 深度轉換係數太大（例如 * 10.0）
```

**修正**：
```hlsl
// 減小縮放係數
float depth = rawDepth * 3.0; // 改為 3 而不是 10
```

### 症狀 3: 完全沒有變化

**原因**：
```
? 深度紋理沒有更新
? 採樣錯誤的紋理層
```

**診斷**：
```hlsl
// 檢查 eyeIndex
uint eyeIndex = 0; // 確保是 0 (左眼)

// 檢查 UV
float3 uv = float3(i.uv, eyeIndex); // 確保正確
```

---

## ?? 實用的除錯代碼

### 在 Shader 中添加除錯模式

```hlsl
// 模式 0: 顯示 UV 座標（驗證採樣）
if (_VisualizationMode == 99)
{
    return fixed4(i.uv.x, i.uv.y, 0, 1);
}

// 模式 1: 顯示原始深度（驗證深度值）
if (_VisualizationMode == 98)
{
    float rawDepth = _EnvironmentDepthTexture.Sample(...).r;
    return fixed4(rawDepth * 5.0, rawDepth * 5.0, rawDepth * 5.0, 1);
}

// 模式 2: 顯示深度範圍標記
if (_VisualizationMode == 97)
{
    float depth = /* 你的深度計算 */;
    
    if (depth < 0.5)
        return fixed4(1, 0, 0, 1); // < 0.5m: 紅色
    else if (depth < 1.0)
        return fixed4(0, 1, 0, 1); // 0.5-1m: 綠色
    else if (depth < 2.0)
        return fixed4(0, 0, 1, 1); // 1-2m: 藍色
    else
        return fixed4(1, 1, 0, 1); // > 2m: 黃色
}
```

### 在 C# 中添加即時日誌

```csharp
// 在 DepthVisualizationDebugger.cs
private void Update()
{
    if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.D))
    {
        // 詳細除錯資訊
        var tex = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
        Debug.Log($"Depth Texture: {(tex != null ? "OK" : "NULL")}");
        Debug.Log($"Manager Enabled: {depthManager.enabled}");
        Debug.Log($"Depth Available: {depthManager.IsDepthAvailable}");
        Debug.Log($"Quad Active: {(debugQuad != null && debugQuad.activeSelf)}");
        Debug.Log($"Material Shader: {(depthVisualizationMaterial != null ? depthVisualizationMaterial.shader.name : "NULL")}");
    }
}
```

---

## ?? 檢查清單

### 在嘗試修復前，確認：

- [ ] EnvironmentDepthManager 存在於場景中
- [ ] depthManager.enabled = true
- [ ] 等待 IsDepthAvailable = true 後才顯示
- [ ] 深度紋理不是 NULL
- [ ] 材質正確指定到 Quad
- [ ] Shader 編譯無錯誤
- [ ] Quad 在相機視野內

### 如果仍然無法修復：

1. **使用官方範例**：
   ```
   打開 DepthAPISample 場景
   查看官方的深度顯示是否正常
   ```

2. **對比設定**：
   ```
   對比官方範例與您的設定
   找出差異
   ```

3. **簡化測試**：
   ```
   只使用 Unlit/ShowDepthMap
   不添加任何顏色映射
   確認基礎深度顯示正常
   ```

---

## ?? 最終建議

如果您需要快速解決，我建議：

### 方案 A：直接使用官方 Shader
```
優點: 立即可用，穩定可靠
缺點: 沒有自定義顏色映射
```

### 方案 B：使用修正版 + 調整
```
優點: 有顏色映射，更直觀
缺點: 可能需要微調參數
```

### 方案 C：深入除錯
```
優點: 完全理解深度格式
缺點: 需要更多時間
```

**我的建議**：先試方案 A，確認深度紋理正常後，再試方案 B。

---

需要更多幫助嗎？告訴我您嘗試哪個方案以及結果如何！
