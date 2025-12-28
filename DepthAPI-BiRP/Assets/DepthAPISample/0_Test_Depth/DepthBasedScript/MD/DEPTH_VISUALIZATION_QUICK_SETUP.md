# Depth Visualization 快速設定指南 (v1.2)

## ?? 最新改進

### 1. **更小的 Quad** ?
```
舊版: 0.4m × 0.3m (太大)
新版: 0.2m × 0.15m (縮小 50%) ?
```

### 2. **更廣的深度顯示範圍** ??
```
舊版: 0.2m - 1.5m (範圍太小，變色不明顯)
新版: 0.3m - 2.0m (涵蓋常用距離) ?
```

### 3. **可選的自動同步** ??
```
新增: Auto Sync With Particle Emitter 開關
  - OFF: 使用自定義範圍 (0.3m - 2.0m) ← 預設
  - ON:  同步粒子發射器範圍
```

---

## ?? 新的預設設定

### Quad 位置與大小
```
Quad Offset From Camera: (0.5, 0.15, 0.8)
  - X (Right):   0.5m   (更靠右一點)
  - Y (Up):      0.15m  (稍低一點)
  - Z (Forward): 0.8m   (更近，避免太遠)

Quad Size: (0.2, 0.15)
  - Width:  0.2m  (20cm 寬)
  - Height: 0.15m (15cm 高)
```

### 深度範圍
```
Display Range: 0.3m - 2.0m
  ? 30cm:  紅色   (手很近)
  ? 50cm:  橙紅   (手伸出)
  ? 70cm:  黃色   (正常距離)
  ? 1.0m:  黃綠   (稍遠)
  ? 1.2m:  綠色   (中距離)
  ? 1.5m:  青綠   (較遠)
  ? 1.7m:  青色   (遠)
  ? 2.0m:  藍色   (最遠)
  ? >2.0m: 黑色/無效
```

---

## ?? 顏色映射表

| 距離 | 顏色 | 情境 | 粒子密度 |
|------|------|------|---------|
| **0.3m** | ?? 鮮紅 | 手幾乎碰到鏡頭 | ????? 超密集 |
| **0.5m** | ?? 橙紅 | 手自然伸出 | ???? 很密集 |
| **0.7m** | ?? 黃色 | 手臂長度 | ??? 密集 |
| **1.0m** | ?? 黃綠 | 舒適距離 | ?? 中等 |
| **1.2m** | ?? 綠色 | 站立手臂展開 | ?? 中等 |
| **1.5m** | ?? 青綠 | 一步之遙 | ? 稀疏 |
| **1.7m** | ?? 青色 | 牆壁距離 | ? 稀疏 |
| **2.0m** | ?? 藍色 | 房間另一側 | (無粒子) |

---

## ?? Inspector 設定

### DepthVisualizationDebugger

```
Visualization Settings:
  ? Enable Visualization: true
  - Visualization Mode: ColorGradient
  - Near Plane: 0.3   ← 更新！
  - Far Plane: 2.0    ← 更新！
  ? Auto Sync With Particle Emitter: false  ← 新增！預設關閉

Debug Quad Settings:
  ? Show Debug Quad: true
  ? Follow Camera: true
  - Quad Offset From Camera: (0.5, 0.15, 0.8)  ← 更新！
  - Quad Size: (0.2, 0.15)   ← 更新！縮小！
  ? Rotate With Camera: true
```

---

## ?? Quad 大小對比

### 視覺大小比較
```
舊版 (0.4 × 0.3):
  ┌────────────────┐
  │                │  ← 佔據約 20% 視野
  │  [Depth Map]   │
  │                │
  └────────────────┘

新版 (0.2 × 0.15):
  ┌────────┐
  │[Depth] │  ← 佔據約 5% 視野
  └────────┘
```

### 實際尺寸
```
舊版: 40cm × 30cm = 1,200 cm? (A3 紙大小)
新版: 20cm × 15cm = 300 cm?  (手機大小) ?
```

---

## ?? 使用場景

### 場景 1: 調試粒子系統
```
設定:
  ? Auto Sync: OFF
  - Near: 0.3m
  - Far: 2.0m

用途: 查看完整的深度範圍變化
```

### 場景 2: 同步粒子範圍
```
設定:
  ? Auto Sync: ON
  - Near: (自動) 0.2m
  - Far: (自動) 1.5m

用途: 確認粒子發射範圍的深度數據
```

### 場景 3: 專注於手部互動
```
設定:
  ? Auto Sync: OFF
  - Near: 0.2m
  - Far: 0.8m

用途: 只看近距離手部的精細深度變化
```

---

## ?? 調整建議

### 如果 Quad 還是太大
```
Quad Size: (0.15, 0.1)  // 更小
Quad Offset: (0.6, 0.2, 0.8)  // 更遠右側
```

### 如果想要更大範圍
```
Near Plane: 0.2m
Far Plane: 3.0m
```

### 如果只關注手部
```
Near Plane: 0.2m
Far Plane: 0.8m
```

### 如果想要在左側顯示
```
Quad Offset From Camera: (-0.5, 0.15, 0.8)  // X 改為負值
```

---

## ?? 深度檢測參考

### 常見距離
```
0.2m - 0.3m: 手幾乎貼近鏡頭
0.3m - 0.5m: 手自然伸出
0.5m - 0.8m: 手臂長度
0.8m - 1.2m: 舒適互動距離
1.2m - 1.5m: 站立伸手可及
1.5m - 2.0m: 房間內的牆壁/家具
>2.0m:       遠處的牆壁
```

### Quest 3 深度範圍
```
最近: ~0.2m  (20cm)
最遠: ~10m   (但精度在 5m 後下降)
建議範圍: 0.3m - 3.0m
```

---

## ?? 螢幕顯示範例

### 新的左上角資訊
```
=== DEPTH VISUALIZATION ===
Depth Available: YES
Mode: ColorGradient
Display Range: 0.30m - 2.00m       ← 更新！
Found Particle Emitter: YES
  - Particle Range: 0.20m - 1.50m
Auto-Sync: OFF                     ← 新增！
Quad Follow Camera: YES
---
Color Guide:                       ← 新增！
  30cm: RED, 70cm: YELLOW
  1.2m: GREEN, 1.7m: CYAN, 2m: BLUE
Press 'D' to log depth info
```

---

## ?? 技術細節

### Quad 大小計算
```
視野中的佔比 = (QuadSize / Distance) × FOV

舊版: (0.4 / 1.2) × 90° ? 30° (太大)
新版: (0.2 / 0.8) × 90° ? 22.5° (合適)
```

### 深度映射精度
```
範圍 0.3m - 2.0m:
  - 總跨度: 1.7m
  - 每 10cm 約佔 5.9% 的顏色變化
  - 可以清楚看到細微的深度差異

範圍 0.2m - 5.0m (舊版):
  - 總跨度: 4.8m
  - 每 10cm 約佔 2.1% 的顏色變化
  - 難以看出細微差異 ?
```

---

## ?? 實際體驗

### 運行後的效果

```
VR 視野:
  ┌───────────────────────────────┐
  │  [小Quad]                     │  ← 右上角
  │                               │
  │                               │
  │         ?? 手                 │
  │     (顏色即時變化)            │
  │                               │
  └───────────────────────────────┘

手部測試:
  手縮回 (2m+):  Quad 顯示藍色/黑色
  手在中距 (1m): Quad 顯示綠色
  手伸出 (50cm): Quad 顯示橙黃色
  手很近 (30cm): Quad 顯示鮮紅色 ?
```

---

## ?? 故障排除

### 問題 1: Quad 還是太大
**解決**:
```csharp
Quad Size: (0.15, 0.1)   // 縮小到 15cm × 10cm
Quad Offset Z: 1.0       // 推遠一點
```

### 問題 2: 顏色變化還是不明顯
**解決**:
```csharp
Near Plane: 0.2
Far Plane: 1.0   // 縮小範圍讓變化更明顯
```

### 問題 3: 想要自動同步
**解決**:
```
? Auto Sync With Particle Emitter: true
範圍會自動變為 0.2m - 1.5m
```

### 問題 4: Quad 位置不理想
**解決**:
```
調整 Quad Offset From Camera:
  X: 左右位置 (正=右，負=左)
  Y: 上下位置 (正=上，負=下)
  Z: 前後距離 (建議 0.6 - 1.2)
```

---

## ?? 版本對比

| 功能 | v1.0 | v1.1 | v1.2 (當前) |
|------|------|------|-------------|
| Quad 大小 | 1.6×1.2 | 0.4×0.3 | **0.2×0.15** ? |
| 深度範圍 | 0.2-5.0 | 0.2-1.5 | **0.3-2.0** ? |
| 跟隨相機 | ? | ? | ? |
| 自動同步 | ? | ? 強制 | **? 可選** ? |
| 位置 | 固定 | 右側 | **優化右側** ? |
| 顏色指南 | ? | ? | **? 顯示** ? |

---

## ?? 最佳設定推薦

### 標準設定（推薦）
```
Visualization Settings:
  ? Enable Visualization: true
  - Visualization Mode: ColorGradient
  - Near Plane: 0.3
  - Far Plane: 2.0
  ? Auto Sync: false

Debug Quad Settings:
  ? Show Debug Quad: true
  ? Follow Camera: true
  - Quad Offset: (0.5, 0.15, 0.8)
  - Quad Size: (0.2, 0.15)
  ? Rotate With Camera: true
```

### 極簡設定
```
Quad Size: (0.15, 0.1)
Quad Offset: (0.6, 0.2, 1.0)
Near: 0.3, Far: 2.0
```

### 詳細設定
```
Quad Size: (0.3, 0.2)
Quad Offset: (0.3, 0, 1.0)  // 中右側
Near: 0.2, Far: 3.0
```

---

## ?? 使用技巧

### 快速驗證深度
1. 伸手靠近 → 看到紅色 ?
2. 手在 50cm → 看到黃色 ?
3. 手在 1m → 看到綠色 ?
4. 手縮回 → 看到藍色 ?

### 調試粒子
1. 關閉 Auto Sync
2. 設定更廣範圍 (0.3 - 2.0)
3. 對比粒子出現的距離與顏色

### 性能測試
1. Quad 很小，性能影響 < 0.3ms
2. 可以一直開啟不影響遊戲

---

## ?? 更新日誌

### v1.2 (當前)
- ? Quad 大小縮小 50% (0.2×0.15)
- ? 深度範圍調整為 0.3m - 2.0m
- ? 新增可選自動同步
- ? 優化 Quad 位置更靠近
- ? 新增螢幕顏色指南

### v1.1
- ? Quad 跟隨相機
- ? 自動同步深度範圍
- ? 縮小 Quad

### v1.0
- 基礎深度可視化

---

**版本**: 1.2  
**更新日期**: 2024  
**推薦設定**: Quad (0.2, 0.15), Range 0.3-2.0m
