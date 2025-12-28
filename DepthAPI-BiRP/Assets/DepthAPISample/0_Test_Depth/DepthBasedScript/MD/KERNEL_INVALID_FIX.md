# ?? Compute Shader "Kernel Invalid" 錯誤修復

## ?? **問題症狀**

```
[13:32:32] DepthSamplerCorrect.compute: Kernel at index (0) is invalid
[13:32:32] Valid: 0, Particles: 0
```

---

## ?? **根本原因**

### 1. **未初始化結果緩衝區**

#### ? 錯誤寫法
```hlsl
[numthreads(64,1,1)]
void SampleDepthPoints (uint3 id : SV_DispatchThreadID)
{
    // 沒有初始化！
    float2 screenUV = _InputUVs[id.x];
    
    if (any(depthUV < 0.0) || any(depthUV > 1.0))
    {
        _ResultBuffer[id.x].isValid = 0.0;  // ? 只設置了 isValid
        return;  // ? worldPosition 和 depth 未初始化！
    }
    // ...
}
```

**問題**：
- 如果提前 `return`，`worldPosition` 和 `depth` 是**未定義值**
- GPU 緩衝區可能包含**垃圾數據**
- 導致 Kernel 執行失敗或產生 NaN

#### ? 正確寫法
```hlsl
[numthreads(64,1,1)]
void SampleDepthPoints (uint3 id : SV_DispatchThreadID)
{
    // ? 立即初始化所有欄位
    _ResultBuffer[id.x].worldPosition = float3(0, 0, 0);
    _ResultBuffer[id.x].depth = -1.0;
    _ResultBuffer[id.x].isValid = 0.0;
    
    // 現在可以安全地 return
    if (any(depthUV < 0.0) || any(depthUV > 1.0))
    {
        return;  // ? 已經有有效的預設值
    }
    // ...
}
```

---

### 2. **Kernel Index 檢查缺失**

#### ? 錯誤寫法
```csharp
private void InitializeComputeShader()
{
    kernelIndex = depthSamplerShader.FindKernel("SampleDepthPoints");
    // ? 沒有檢查 kernelIndex 是否有效
    
    resultBuffer = new ComputeBuffer(samplesPerFrame, stride);
    // ...
}
```

**問題**：
- 如果 Kernel 名稱錯誤，`FindKernel` 返回 `-1`
- 後續 `Dispatch` 會失敗

#### ? 正確寫法
```csharp
private void InitializeComputeShader()
{
    kernelIndex = depthSamplerShader.FindKernel("SampleDepthPoints");
    
    // ? 檢查 Kernel 是否找到
    if (kernelIndex < 0)
    {
        Debug.LogError("[DepthParticle] Kernel 'SampleDepthPoints' not found!");
        return;
    }
    
    resultBuffer = new ComputeBuffer(samplesPerFrame, stride);
    // ...
}
```

---

### 3. **缺少異常處理**

#### ? 錯誤寫法
```csharp
depthSamplerShader.Dispatch(kernelIndex, threadGroups, 1, 1);
// ? 如果失敗，沒有錯誤訊息
```

#### ? 正確寫法
```csharp
try
{
    depthSamplerShader.Dispatch(kernelIndex, threadGroups, 1, 1);
}
catch (System.Exception e)
{
    Debug.LogError($"[DepthParticle] Dispatch failed: {e.Message}");
    return;
}
```

---

## ?? **修復對比**

| 問題 | 修復前 | 修復後 |
|------|--------|--------|
| **緩衝區初始化** | ? 未初始化 | ? 立即初始化為預設值 |
| **Kernel Index** | ? 沒有檢查 | ? 檢查並記錄錯誤 |
| **異常處理** | ? 沒有 try-catch | ? 捕獲並記錄異常 |
| **調試信息** | ?? 基本 | ? 詳細（紋理尺寸、矩陣狀態） |

---

## ?? **新的調試輸出**

修復後應該看到：

```
[DepthParticle] Compute Shader initialized. Kernel Index: 0, Samples: 64
[DepthParticle] Depth is now available! Starting particle emission.
[DepthParticle] Depth Texture: 512x512, Dimension: Tex2DArray
[DepthParticle] ComputeShader | Samples: 3840, Valid: 2100, Particles: 450
                                                    ↑
                                            應該 > 0（不再是 0）
```

---

## ?? **關鍵教訓**

### 1. **GPU 緩衝區必須完全初始化**

```hlsl
// ? 危險
struct Result {
    float3 position;
    float value;
    float isValid;
};

RWStructuredBuffer<Result> _Buffer;

void Kernel() {
    if (someCondition)
    {
        _Buffer[id].isValid = 0.0;  // ? 其他欄位未初始化！
        return;
    }
}

// ? 安全
void Kernel() {
    // 立即初始化所有欄位
    _Buffer[id].position = float3(0, 0, 0);
    _Buffer[id].value = 0.0;
    _Buffer[id].isValid = 0.0;
    
    if (someCondition)
    {
        return;  // ? 已有預設值
    }
}
```

### 2. **總是檢查 Kernel Index**

```csharp
int kernelIndex = computeShader.FindKernel("MyKernel");

// ? 必須檢查
if (kernelIndex < 0)
{
    Debug.LogError("Kernel not found!");
    return;
}
```

### 3. **Dispatch 可能失敗**

```csharp
// ? 使用 try-catch
try
{
    computeShader.Dispatch(kernelIndex, x, y, z);
}
catch (Exception e)
{
    Debug.LogError($"Dispatch failed: {e}");
}
```

---

## ?? **測試步驟**

### 1. **Build 並運行**
```bash
# 應該看到 Kernel Index: 0（而非 -1）
[DepthParticle] Compute Shader initialized. Kernel Index: 0
```

### 2. **檢查 Valid 數量**
```bash
# 應該 > 0（不再是 0）
[DepthParticle] Valid: 2100
```

### 3. **檢查粒子數量**
```bash
# 應該有粒子在場景中
[DepthParticle] Particles: 450, Count: 720
```

---

## ?? **總結**

### 修復的核心問題

1. ? **Compute Shader**：添加緩衝區初始化
2. ? **C#**：添加 Kernel Index 檢查
3. ? **C#**：添加異常處理和詳細日誌

### 預期結果

- ? 之前：`Kernel at index (0) is invalid`
- ? 現在：Kernel 正常執行，`Valid > 0`

**現在 Build 測試，應該能看到有效的深度採樣和粒子發射了！** ??
