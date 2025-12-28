# Depth Visualization Debugger - 使用說明

## ?? 目的
驗證 Environment Depth Texture 是否正確，以確保粒子發射器使用的深度數據正確無誤。

## ?? 新增的檔案

### 1. `DepthVisualizationDebugger.cs`
主要的深度可視化腳本，可以：
- 在場景中創建一個 Quad 顯示深度圖
- 以不同的顏色模式呈現深度
- 不影響現有的粒子發射器邏輯

### 2. `Shaders/DepthVisualization.shader`
增強版深度可視化 Shader，支援：
- 彩色漸層模式
- 灰階模式
- 熱力圖模式
- 原始深度值模式

## ?? 快速開始

### 步驟 1: 創建深度可視化物件

1. 在 Hierarchy 中右鍵 → Create Empty
2. 命名為 "DepthVisualizer"
3. Add Component → `DepthVisualizationDebugger`

### 步驟 2: 設定參數

#### 必要設定：
- **Depth Manager**: 拖入場景中的 EnvironmentDepthManager

#### 建議設定：
```
Visualization Settings:
  ? Enable Visualization: true
  - Visualization Mode: ColorGradient (或選擇其他模式)
  - Near Plane: 0.2
  - Far Plane: 5.0

Debug Quad Settings:
  ? Show Debug Quad: true
  - Quad Position: (0, 1.5, 2)  // 在您面前 2m 處
  - Quad Size: (1.6, 1.2)
```

### 步驟 3: 創建材質

1. Project → Assets → Create → Material
2. 命名為 "DepthVisualizationMaterial"
3. Shader 選擇 "Debug/DepthVisualization"
4. 將此材質拖入 DepthVisualizationDebugger 的 "Depth Visualization Material" 欄位

### 步驟 4: 暫停粒子發射（可選）

如果您想專注於查看深度圖：
1. 選擇場景中的 `AdvancedDepthParticleEmitter` 物件
2. 勾選 `Pause Particle Emission`

## ?? 可視化模式說明

### 1. Color Gradient（彩色漸層）
- **紅色** = 非常近（< 0.5m）
- **黃色** = 近（0.5m - 1m）
- **綠色** = 中等距離（1m - 2m）
- **青色** = 遠（2m - 3m）
- **藍色** = 非常遠（> 3m）
- **洋紅色** = 無效深度

### 2. Grayscale（灰階）
- **白色** = 近
- **灰色** = 中等距離
- **黑色** = 遠

### 3. Heat Map（熱力圖）
- **紅色** → **黃色** → **綠色** → **青色** → **藍色**
- 類似熱成像儀的效果

### 4. Raw Depth（原始深度）
- 顯示正規化後的深度值（0-1）

## ?? 除錯功能

### 按鍵操作
- **按 'D' 鍵**：在 Console 中輸出詳細的深度紋理資訊

### Console 輸出範例
```
=== DEPTH TEXTURE INFO ===
Depth Texture: 256x256, Dimension: Tex2DArray
Preprocessed Depth Texture: 256x256
Reprojection Matrices: Available (2)
ZBuffer Params: (1.0, 0.0, -1.0, 0.0)
========================
```

### 螢幕顯示（左上角）
```
=== DEPTH VISUALIZATION ===
Depth Available: YES
Mode: ColorGradient
Range: 0.20m - 5.00m
Press 'D' to log depth info
```

## ? 驗證深度是否正確

### 正常情況：
1. **手伸出** → 看到紅色/黃色區域
2. **牆壁/遠處物體** → 看到藍色/青色區域
3. **移動手** → 深度圖即時更新
4. **深度連續變化** → 沒有突然的跳躍或閃爍

### 異常情況：
1. **整個畫面都是洋紅色** → 深度紋理未初始化或無效
2. **畫面凍結** → 深度更新停止
3. **深度反轉**（遠處是紅色，近處是藍色）→ 深度計算錯誤
4. **大量黑色區域** → 深度採樣失敗

## ??? 故障排除

### 問題 1: 看不到 Debug Quad
**解決方案**：
- 確認 `Show Debug Quad` 已勾選
- 調整 `Quad Position` 到您面前
- 檢查材質是否正確指定

### 問題 2: 整個畫面是洋紅色
**解決方案**：
- 檢查 EnvironmentDepthManager 是否啟用
- 確認 `IsDepthAvailable` 為 true
- 按 'D' 檢查深度紋理是否為 NULL

### 問題 3: 深度圖不更新
**解決方案**：
- 確認 `Enable Visualization` 已勾選
- 重新啟動 Play Mode
- 檢查 Console 是否有錯誤訊息

### 問題 4: 深度範圍不對
**解決方案**：
- 調整 `Near Plane` 和 `Far Plane` 參數
- 建議範圍：Near = 0.2, Far = 5.0

## ?? 與粒子發射器的關係

### 深度可視化器
- **獨立運作**：不會影響粒子發射器
- **只讀取深度**：不修改深度紋理
- **可以並行**：可以同時啟用深度顯示和粒子發射

### 粒子發射器
- **可以暫停**：使用 `Pause Particle Emission` 選項
- **獨立控制**：兩個系統完全分離
- **共用深度源**：都使用相同的 `_EnvironmentDepthTexture`

## ?? 最佳實踐

### 除錯流程：
1. ? **先驗證深度** → 啟用深度可視化
2. ? **暫停粒子** → 勾選 `Pause Particle Emission`
3. ? **檢查深度圖** → 確認顏色變化合理
4. ? **測試互動** → 移動手、走動，觀察深度變化
5. ? **恢復粒子** → 取消勾選 `Pause Particle Emission`

### 性能建議：
- 深度可視化對性能影響極小（< 0.5ms）
- 可以在正式版本中移除或禁用
- Debug Quad 使用 Overlay Queue，不影響正常渲染

## ?? 範例場景設定

```
Scene Hierarchy:
├─ EnvironmentDepthManager (必須)
├─ DepthParticles (粒子發射器)
│   └─ AdvancedDepthParticleEmitter
│       └─ Pause Particle Emission: ? (除錯時)
└─ DepthVisualizer (新增)
    └─ DepthVisualizationDebugger
        ├─ Depth Manager: [EnvironmentDepthManager]
        ├─ Depth Visualization Material: [DepthVisualizationMaterial]
        ├─ Enable Visualization: ?
        ├─ Visualization Mode: ColorGradient
        └─ Show Debug Quad: ?
```

## ?? 進階用法

### 自定義顏色漸層
在 Inspector 中調整 `Depth Gradient`：
- 點擊 Gradient 欄位
- 添加/移動色標
- 自定義您喜歡的顏色映射

### 動態調整範圍
Runtime 時可以透過程式碼調整：
```csharp
var debugger = FindObjectOfType<DepthVisualizationDebugger>();
debugger.nearPlane = 0.5f;
debugger.farPlane = 3.0f;
```

### 只使用 Shader（不用 Quad）
如果您想在自己的物件上顯示深度：
1. 創建任何 3D 物件（Cube, Sphere 等）
2. 指定材質使用 "Debug/DepthVisualization" Shader
3. 調整 Shader 參數

## ?? 注意事項

1. **不要刪除現有邏輯**：所有新增功能都是獨立的
2. **記得恢復粒子**：除錯完記得取消 `Pause Particle Emission`
3. **性能監控**：深度可視化對 VR 性能影響很小，可放心使用
4. **雙眼渲染**：Shader 只顯示左眼的深度圖，避免視覺疲勞

## ?? 支援

如果遇到問題，請檢查：
1. Console 中的錯誤訊息
2. 按 'D' 輸出的深度資訊
3. EnvironmentDepthManager 的狀態
4. Unity 版本是否支援 Texture2DArray

---

**版本**: 1.0  
**最後更新**: 2024
