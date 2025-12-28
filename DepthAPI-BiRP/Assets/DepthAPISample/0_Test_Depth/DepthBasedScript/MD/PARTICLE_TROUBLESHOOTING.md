# ?? 粒子系統無法產生粒子 - 診斷指南

## ?? 檢查清單

### 1. **基本檢查（在 Unity Inspector 中）**

#### ? EnvironmentDepthManager 設置
1. 場景中是否有 `EnvironmentDepthManager` 組件？
2. `EnvironmentDepthManager` 是否啟用（enabled）？
3. `OcclusionShadersMode` 是否設置為 `SoftOcclusion` 或 `HardOcclusion`？
   - ?? 如果設置為 `None`，深度紋理可能不會更新

#### ? AdvancedDepthParticleEmitter 設置
1. `Depth Manager` 引用是否已指定？
2. 參數檢查：
   ```
   Min Depth: 0.3
   Max Depth: 5.0
   Cull Distance: 1.5  ← 粒子只在這個距離內產生
   Base Emission Rate: 100
   Samples Per Frame: 20
   ```

#### ? Particle System 組件
1. 是否有 `Particle System` 組件在同一個 GameObject 上？
2. `Renderer` 模組是否有指定材質？
3. 材質的 Shader 是否正確（推薦：`Meta/Depth/BiRP/Occlusion ParticleStandardUnlit`）？

---

### 2. **運行時檢查（在 Quest 3 設備上）**

#### ?? 查看 Console 日誌

啟用調試日誌後，您應該看到：

```
[DepthParticle] Waiting for depth to become available...
[DepthParticle] Depth is now available! Starting particle emission.
[DepthParticle] Initialized reusable texture. Depth texture size: 640x480
[DepthParticle] Stats - Total Samples: 1200, Valid Depth: 800, Particles: 50, ParticleCount: 45
```

#### ? 如果看到這些錯誤：

**錯誤 1：Depth still not available after 5 seconds**
```
原因：EnvironmentDepthManager 沒有正確初始化
解決：
1. 檢查場景權限：需要 USE_SCENE 權限
2. 檢查 OVRManager 設置：Scene Support = Required
3. 重啟應用程式
```

**錯誤 2：Reprojection matrices not available**
```
原因：深度紋理的重投影矩陣沒有設置
解決：
1. 確認 EnvironmentDepthManager.enabled = true
2. 確認 OcclusionShadersMode 不是 None
3. 檢查 Meta XR SDK 版本（需要 v67.0.0+）
```

**錯誤 3：Depth texture not available**
```
原因：_EnvironmentDepthTexture 全局紋理沒有設置
解決：
1. 確認 EnvironmentDepthManager.IsDepthAvailable = true
2. 檢查 Passthrough 是否啟用
3. 確認在 Quest 3 設備上運行（不支持 Quest 2）
```

---

### 3. **常見問題診斷**

#### 問題：看到 "Valid Depth: 0"

**原因**：所有深度採樣都返回無效值

**可能的情況**：

1. **相機距離問題**
   ```csharp
   // 檢查您與環境的距離
   Min Depth: 0.3  ← 太近的物體會被忽略
   Max Depth: 5.0  ← 太遠的物體會被忽略
   Cull Distance: 1.5  ← 超過這個距離不發射粒子
   ```
   
   **解決**：站在距離牆壁/物體 **0.5-1.5 米**的位置

2. **深度紋理全為 0 或 1**
   ```
   原因：深度數據未正確讀取
   解決：檢查 zBufferParams 是否有效
   ```

#### 問題：看到 "Particles: 0" 但 Valid Depth > 0

**原因**：深度值在範圍內，但密度太低或概率太低

**檢查**：

1. **Density Curve 設置**
   ```csharp
   densityCurve = AnimationCurve.Linear(0, 1, 1, 0);
   // 0 距離（近）：密度 1.0（高）
   // 1 距離（遠）：密度 0.0（無）
   ```
   
   **問題**：如果您在 > cullDistance 的位置，密度為 0
   
   **解決**：調整 Curve 或靠近物體

2. **發射概率太低**
   ```csharp
   // 發射條件
   if (Random.value < density * Time.deltaTime * baseEmissionRate)
   
   // 如果 density = 0.5, baseEmissionRate = 100, deltaTime = 0.016
   // 概率 = 0.5 * 0.016 * 100 = 0.8 (80%)
   ```
   
   **解決**：增加 `baseEmissionRate` 到 200-300

#### 問題：粒子生成但立即消失

**原因**：粒子壽命太短或在相機後方

**檢查**：

1. **Particle Lifetime**
   ```csharp
   particleLifetime = 2f;  // 至少 1.5-2 秒
   ```

2. **粒子位置**
   ```csharp
   // 粒子可能在相機後方
   Vector3 position = ray.origin + ray.direction * (depth + surfaceOffset);
   
   // 如果 depth 值異常，粒子可能在錯誤位置
   ```

3. **Particle System Renderer**
   - 檢查材質的 Render Queue
   - 檢查材質是否可見（不是完全透明）

---

### 4. **手動測試步驟**

#### 在 Unity Editor 中：

1. **Play Mode**
2. **選中 ParticleEmitter GameObject**
3. **查看 Gizmos**（Scene 視圖）
   - 應該看到黃綠色的小球（顯示採樣點）
   - 如果看不到 → 深度採樣失敗

4. **查看 Particle System 面板**
   - `Particles: X`（當前粒子數）
   - 如果一直是 0 → 沒有發射

#### 在 Quest 3 設備上：

1. **Build and Run**
2. **連接 Oculus Link 或使用 adb logcat**
3. **查看 Console 日誌**：
   ```bash
   adb logcat -s Unity
   ```

4. **站在距離牆壁 1 米處**
5. **移動手部** → 應該看到粒子在手周圍生成

---

### 5. **臨時測試：強制發射粒子**

如果想測試粒子系統本身是否工作，可以暫時修改代碼：

```csharp
private void EmitParticlesFromDepth()
{
    // 測試：強制發射粒子（忽略深度檢查）
    for (int i = 0; i < 10; i++)
    {
        Vector3 testPos = mainCamera.transform.position + mainCamera.transform.forward * 1f;
        testPos += Random.insideUnitSphere * 0.3f;
        
        emitParams.position = testPos;
        emitParams.velocity = Vector3.zero;
        emitParams.startLifetime = 2f;
        emitParams.startSize = 0.02f;
        emitParams.startColor = Color.cyan;
        
        particleSystem.Emit(emitParams, 1);
    }
    
    Debug.Log($"Force emitted 10 particles. Total: {particleSystem.particleCount}");
}
```

**如果強制發射也看不到粒子**：
- 問題在 Particle System 或材質設置
- 檢查 Renderer 模組的材質
- 檢查粒子大小（是否太小）

**如果強制發射可以看到粒子**：
- 問題在深度採樣邏輯
- 繼續檢查深度相關的日誌

---

### 6. **最常見的 5 個原因**

#### 1. **沒有站在正確的距離**
```
? 正確：距離物體 0.5-1.5 米
? 錯誤：太近 (<0.3m) 或太遠 (>1.5m)
```

#### 2. **EnvironmentDepthManager 未啟用**
```
? 正確：Enabled = true, OcclusionShadersMode = SoftOcclusion
? 錯誤：Enabled = false 或 OcclusionShadersMode = None
```

#### 3. **沒有 USE_SCENE 權限**
```
? 正確：OVRManager → Scene Support = Required
? 錯誤：未勾選或權限未授予
```

#### 4. **粒子材質未設置**
```
? 正確：Particle System → Renderer → Material 已指定
? 錯誤：Material 為 None 或使用錯誤的 Shader
```

#### 5. **baseEmissionRate 太低**
```
? 正確：100-300
? 錯誤：< 50（發射概率太低）
```

---

### 7. **調試工具**

#### 在 Inspector 中啟用
```
Advanced Depth Particle Emitter
├─ Debug
│  └─ Enable Debug Logs: ?
```

#### 查看日誌輸出
```
[DepthParticle] Depth is now available! Starting particle emission.
[DepthParticle] Initialized reusable texture. Depth texture size: 640x480
[DepthParticle] Stats - Total Samples: 1200, Valid Depth: 800, Particles: 50
```

**理想的數值**：
- `Total Samples` = 持續增長
- `Valid Depth` / `Total Samples` > 50%（至少一半採樣成功）
- `Particles` > 0（有粒子發射）
- `ParticleCount` > 0（場景中有粒子）

---

### 8. **快速修復步驟**

1. **確認深度可用**
   ```csharp
   // 在 Start() 添加
   Debug.Log($"DepthManager exists: {depthManager != null}");
   Debug.Log($"Depth available: {depthManager?.IsDepthAvailable}");
   ```

2. **降低門檻測試**
   ```csharp
   cullDistance = 5.0f;  // 增大範圍
   baseEmissionRate = 300f;  // 增加發射率
   minDepth = 0.1f;  // 降低最小深度
   ```

3. **檢查粒子系統**
   ```csharp
   // 在 Update() 添加
   if (Input.GetKeyDown(KeyCode.Space))
   {
       Debug.Log($"Particle count: {particleSystem.particleCount}");
       Debug.Log($"Max particles: {particleSystem.main.maxParticles}");
   }
   ```

---

## ?? 預期行為

**正確運行時**：
1. 啟動後 1-2 秒看到日誌：`Depth is now available`
2. 每秒看到統計日誌，`Valid Depth` > 0
3. 在距離物體 1 米處，應該看到青藍色粒子
4. 粒子在手部/物體表面附近生成
5. 粒子壽命 2 秒後淡出

**如果以上都正確，但還是沒有粒子**：
- 請提供完整的 Console 日誌
- 截圖 Inspector 設置
- 確認是在 Quest 3 設備上測試（不是 Quest 2 或模擬器）
