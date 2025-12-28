# Depth Visualization 更新說明 (v1.1)

## ?? 新功能

### 1. Quad 跟隨相機 ?
- Debug Quad 現在會自動跟隨相機移動
- 固定在視野右上角，不會遮擋主要視野
- 自動旋轉面向相機

### 2. 自動同步深度範圍 ??
- 自動從 `AdvancedDepthParticleEmitter` 同步深度設定
- 深度顯示範圍與粒子發射範圍一致
- 即時更新，無需手動調整

---

## ?? 新的預設設定

### Quad 位置（相對於相機）
```
Follow Camera: ? (啟用)
Quad Offset From Camera:
  - X (Right):   0.6m  (右側)
  - Y (Up):      0.2m  (稍微向上)
  - Z (Forward): 1.2m  (前方距離)
  
Quad Size: 0.4m × 0.3m (縮小以便於查看)
Rotate With Camera: ? (永遠面向你)
```

### 深度範圍（自動同步）
```
Near Plane: 0.2m  (從 AdvancedDepthParticleEmitter.MinDepth)
Far Plane:  1.5m  (從 AdvancedDepthParticleEmitter.CullDistance)
```

---

## ?? 視覺效果

### 舊版本問題：
- ? Quad 固定在世界空間某處，難以找到
- ? 只有手非常近時才顯示紅色（範圍 0.2m - 5m）
- ? 與粒子系統不同步

### 新版本改進：
- ? Quad 永遠在視野右上角
- ? 深度範圍與粒子系統一致（0.2m - 1.5m）
- ? 更容易看到顏色變化

### 新的顏色映射（0.2m - 1.5m）

| 距離 | 顏色 | 說明 |
|------|------|------|
| **0.2m** | ?? 紅色 | 非常近（minDepth）|
| **0.5m** | ?? 黃色 | 手伸出的距離 |
| **0.8m** | ?? 綠色 | 中等距離 |
| **1.2m** | ?? 青色 | 較遠 |
| **1.5m** | ?? 藍色 | 最遠（cullDistance）|
| **>1.5m** | ? 黑色/無效 | 超出範圍 |

---

## ?? 使用方式

### 自動模式（推薦）
1. 場景中同時有 `AdvancedDepthParticleEmitter` 和 `DepthVisualizationDebugger`
2. 深度範圍會自動同步
3. Quad 自動跟隨相機

**什麼都不用調整！**

### 手動模式
如果您想手動控制：
```
DepthVisualizationDebugger:
  ? Follow Camera: false  (關閉跟隨)
  - Quad Offset From Camera: 手動調整位置
```

---

## ?? 新的 Inspector 設定

### Debug Quad Settings

```
? Show Debug Quad: true

? Follow Camera: true  ← 新增！讓 Quad 跟隨相機
  
Quad Offset From Camera: (0.6, 0.2, 1.2)  ← 新增！
  ├─ X (Right):   0.6   # 右側偏移
  ├─ Y (Up):      0.2   # 上方偏移
  └─ Z (Forward): 1.2   # 前方距離
  
Quad Size: (0.4, 0.3)  ← 縮小！
  ├─ X: 0.4  # 寬度
  └─ Y: 0.3  # 高度
  
? Rotate With Camera: true  ← 新增！永遠面向你
```

---

## ?? 調整建議

### 如果 Quad 太小看不清楚：
```
Quad Size: (0.6, 0.45)  # 增大
```

### 如果 Quad 擋到視線：
```
Quad Offset From Camera:
  - X: 0.8   # 更右邊
  - Y: 0.3   # 更上方
  - Z: 1.5   # 更遠
```

### 如果想要在左側顯示：
```
Quad Offset From Camera:
  - X: -0.6  # 負值 = 左側
  - Y: 0.2
  - Z: 1.2
```

### 如果想要在正前方（VR 中央）：
```
Quad Offset From Camera:
  - X: 0.0   # 中央
  - Y: 0.0
  - Z: 2.0   # 更遠避免太近
  
Quad Size: (0.8, 0.6)  # 可以大一點
```

---

## ?? 同步狀態檢查

### 螢幕左上角顯示：
```
=== DEPTH VISUALIZATION ===
Depth Available: YES
Mode: ColorGradient
Range: 0.20m - 1.50m          ← 與粒子系統同步
Synced with Particle Emitter: YES  ← 新增！
Quad Follow Camera: YES        ← 新增！
Press 'D' to log depth info
```

### Console 輸出（按 'D'）：
```
[DepthVisualizer] Synced with AdvancedDepthParticleEmitter
=== DEPTH TEXTURE INFO ===
...
```

---

## ?? VR 中的體驗

### 運行時：
1. ? 戴上頭盔
2. ? Quad 自動出現在右上角
3. ? 轉頭時 Quad 跟著轉動
4. ? 伸手時看到即時的深度變化

### 顏色變化：
```
手遠離 → 手靠近 → 手移開

藍色 → 綠色 → 黃色 → 紅色 → 黃色 → 藍色
```

---

## ?? 技術細節

### 自動同步實現
```csharp
// AdvancedDepthParticleEmitter 新增公開屬性
public float MinDepth => minDepth;      // 0.2m
public float CullDistance => cullDistance;  // 1.5m

// DepthVisualizationDebugger 自動讀取
private void SyncWithParticleEmitter()
{
    nearPlane = particleEmitter.MinDepth;      // 0.2m
    farPlane = particleEmitter.CullDistance;   // 1.5m
}
```

### Quad 跟隨相機實現
```csharp
private void UpdateQuadPosition()
{
    Vector3 targetPosition = cameraPosition +
        forward * offsetZ +
        right * offsetX +
        up * offsetY;
    
    debugQuad.transform.position = targetPosition;
    debugQuad.transform.rotation = cameraRotation;
}
```

---

## ?? 故障排除

### 問題 1: Quad 沒有跟隨相機
**解決**：
- 確認 `Follow Camera` 已勾選
- 檢查 `Camera.main` 是否正確

### 問題 2: 深度範圍沒有同步
**解決**：
- 確認場景中有 `AdvancedDepthParticleEmitter`
- 查看 Console 是否有同步訊息
- 檢查螢幕顯示 "Synced: YES"

### 問題 3: Quad 位置不對
**解決**：
- 調整 `Quad Offset From Camera` 參數
- X: 左右，Y: 上下，Z: 前後
- 使用負值可以反向

### 問題 4: 還是只在很近時才顯示紅色
**解決**：
- 檢查 `Near Plane` 和 `Far Plane` 值
- 應該是 0.2 和 1.5，而不是 0.2 和 5.0
- 如果不對，手動設定或重新啟動

---

## ?? 對比

### 舊設定 vs 新設定

| 項目 | 舊版 | 新版 |
|------|------|------|
| Quad 位置 | 固定世界座標 | 跟隨相機 ? |
| Quad 大小 | 1.6 × 1.2 | 0.4 × 0.3 |
| 深度範圍 | 0.2 - 5.0 | 0.2 - 1.5 ? |
| 同步狀態 | 手動 | 自動 ? |
| 易用性 | ?? | ????? |

---

## ?? 最佳實踐

### 開發流程：
1. ? 先設定 `AdvancedDepthParticleEmitter` 的深度範圍
2. ? `DepthVisualizationDebugger` 會自動同步
3. ? 調整 Quad 的偏移位置至舒適位置
4. ? 開始測試粒子效果

### 除錯流程：
1. ? 啟用深度可視化
2. ? Quad 出現在右上角
3. ? 檢查顏色變化是否與粒子發射範圍一致
4. ? 確認深度數據正確後，可以關閉可視化

---

## ?? 視覺化範例

### 手在不同距離的顯示：

```
距離 0.3m（近）:
  Quad: ?????? 紅色
  粒子: ????? 超密集

距離 0.7m（中）:
  Quad: ???? 黃綠色
  粒子: ??? 中等密度

距離 1.3m（遠）:
  Quad: ???? 青藍色
  粒子: ? 稀疏

距離 1.6m（超出）:
  Quad: ? 黑色/無效
  粒子: (無)
```

---

## ?? 更新日誌

### v1.1 (當前版本)
- ? 新增 Quad 跟隨相機功能
- ? 新增自動同步深度範圍
- ? 縮小 Quad 尺寸並移至右側
- ? 新增 `AdvancedDepthParticleEmitter` 公開屬性
- ? 改進 UI 顯示資訊

### v1.0 (舊版本)
- 基礎深度可視化功能
- 固定位置 Quad
- 手動深度範圍設定

---

**版本**: 1.1  
**更新日期**: 2024  
**兼容性**: Unity 2021.3+, .NET Framework 4.7.1
