# ? 正確的深度採樣實現 - Compute Shader 方案

## ?? **你的問題是對的！**

你完全正確地指出了我之前方案的致命缺陷：

> **真實世界的物品是動態更新的、而且本身沒辦法有Collider（真的又不是Scene中的Virtual GameObject!），是要怎麼Physics Raycast？**

### ? 錯誤的理解（我之前的方案）
- ? 使用 `Physics.Raycast` → **只能檢測 Unity GameObject 的 Collider**
- ? 真實世界的牆壁/桌子**沒有 Collider**
- ? Passthrough 環境是**視頻流**，不是 3D Mesh

---

## ? **正確的解決方案：Compute Shader**

### 為什麼必須使用 Compute Shader？

1. **Depth Texture 是 Texture2DArray**
   ```csharp
   UNITY_DECLARE_TEX2DARRAY(_EnvironmentDepthTexture);  // ← 雙層紋理（左右眼）
   ```
   - C# 無法使用 `ReadPixels` 讀取 `Texture2DArray`
   - 只能在 GPU (Shader) 中採樣

2. **需要完全複製 Meta 的深度轉換邏輯**
   - 重投影矩陣（Reprojection Matrix）
   - Z-Buffer 參數轉換
   - NDC → UV 座標映射

---

## ??? **實現架構**

### 檔案結構

```
Assets/
├─ DepthSamplerCorrect.compute     ← GPU 端深度採樣
└─ AdvancedDepthParticleEmitter.cs ← CPU 端控制邏輯
```

### 工作流程

```
┌─────────────┐
│   Unity C#  │
│  每幀生成   │
│  隨機 UV    │
└──────┬──────┘
       │ SetData()
       ▼
┌──────────────────┐
│  Compute Shader  │
│  GPU 並行採樣   │
│  深度紋理       │
└──────┬───────────┘
       │ GetData()
       ▼
┌─────────────┐
│   Unity C#  │
│  讀取結果   │
│  發射粒子   │
└─────────────┘
```

---

## ?? **Compute Shader 實現細節**

### 核心函數：`SampleEnvironmentDepthLinear`

```hlsl
float SampleEnvironmentDepthLinear(float2 uv, uint eyeIndex)
{
    // 1. 從 Texture2DArray 採樣原始深度
    float inputDepthEye = UNITY_SAMPLE_TEX2DARRAY(
        _EnvironmentDepthTexture, 
        float3(uv, (float)eyeIndex)  // ← 第三個分量是眼睛索引
    ).r;
    
    // 2. 轉換到 NDC 空間
    float inputDepthNdc = inputDepthEye * 2.0 - 1.0;
    
    // 3. 無效深度檢測
    if (inputDepthNdc == 1.0f) {
        return 10000;  // 無限遠
    }
    
    // 4. NDC → 線性深度（使用 Meta 的轉換公式）
    float linearDepth = (1.0f / (inputDepthNdc + _EnvironmentDepthZBufferParams.y)) 
                       * _EnvironmentDepthZBufferParams.x;
    
    return linearDepth;
}
```

**關鍵點**：
- ? 使用 `UNITY_SAMPLE_TEX2DARRAY` 而不是 `tex2D`
- ? UV 是 `float3`，第三個分量是眼睛索引（0=左眼，1=右眼）
- ? 使用 Meta 提供的 `_EnvironmentDepthZBufferParams` 進行深度轉換

---

### 重投影到深度紋理空間

```hlsl
// 1. 計算視線方向（從螢幕 UV → 世界空間射線）
float2 ndc = screenUV * 2.0 - 1.0;  // [0,1] → [-1,1]
ndc.y *= -1.0;  // Unity Y 軸反轉

float3 viewDir = normalize(
    _CameraForward + 
    ndc.x * _TanHalfFOV * _AspectRatio * _CameraRight + 
    ndc.y * _TanHalfFOV * _CameraUp
);

// 2. 假設一個測試深度，計算世界位置
float testDepth = (_MinDepth + _MaxDepth) * 0.5;
float3 testWorldPos = _CameraPosition + viewDir * testDepth;

// 3. 使用重投影矩陣轉換到深度紋理空間
float4 depthSpace = mul(_EnvironmentDepthReprojectionMatrices[eyeIndex], 
                        float4(testWorldPos, 1.0));

// 4. NDC → UV
float2 depthUV = (depthSpace.xy / depthSpace.w + 1.0) * 0.5;

// 5. 採樣深度紋理
float envDepth = SampleEnvironmentDepthLinear(depthUV, eyeIndex);
```

**為什麼需要重投影？**
- 深度相機的視角**不等於**主相機的視角
- 必須將主相機的世界座標**轉換到**深度相機的視角
- 這就是 `_EnvironmentDepthReprojectionMatrices` 的作用

---

## ?? **C# 端實現**

### 初始化 Compute Shader

```csharp
private ComputeBuffer resultBuffer;
private ComputeBuffer uvBuffer;
private int kernelIndex;

private void InitializeComputeShader()
{
    kernelIndex = depthSamplerShader.FindKernel("SampleDepthPoints");
    
    // 創建緩衝區
    int stride = sizeof(float) * 5;  // Vector3(12) + float(4) + float(4) = 20 bytes
    resultBuffer = new ComputeBuffer(samplesPerFrame, stride);
    uvBuffer = new ComputeBuffer(samplesPerFrame, sizeof(float) * 2);
}
```

### 每幀執行

```csharp
private void EmitParticlesFromDepth()
{
    // 1. 生成隨機 UV 座標
    Vector2[] randomUVs = new Vector2[samplesPerFrame];
    for (int i = 0; i < samplesPerFrame; i++)
    {
        randomUVs[i] = new Vector2(Random.Range(0.2f, 0.8f), Random.Range(0.2f, 0.8f));
    }
    uvBuffer.SetData(randomUVs);

    // 2. 設置參數
    depthSamplerShader.SetBuffer(kernelIndex, "_ResultBuffer", resultBuffer);
    depthSamplerShader.SetBuffer(kernelIndex, "_InputUVs", uvBuffer);
    depthSamplerShader.SetVector("_CameraPosition", mainCamera.transform.position);
    depthSamplerShader.SetVector("_CameraForward", mainCamera.transform.forward);
    // ... 其他參數

    // 3. 執行 Compute Shader（在 GPU 上並行執行）
    int threadGroups = Mathf.CeilToInt(samplesPerFrame / 64f);
    depthSamplerShader.Dispatch(kernelIndex, threadGroups, 1, 1);
    
    // 4. 讀取結果
    DepthSampleResult[] results = new DepthSampleResult[samplesPerFrame];
    resultBuffer.GetData(results);
    
    // 5. 根據結果發射粒子
    for (int i = 0; i < samplesPerFrame; i++)
    {
        if (results[i].isValid > 0.5f)
        {
            EmitParticleAt(results[i].worldPosition, results[i].depth, ...);
        }
    }
}
```

---

## ?? **性能對比**

| 方法 | CPU 負擔 | GPU 負擔 | 準確性 | 動態環境 |
|------|----------|----------|--------|----------|
| **Compute Shader** | 低（只有 SetData/GetData） | 中（GPU 並行） | ? 高 | ? 支持 |
| Physics Raycast | 高（每幀數十次 Raycast） | 無 | ? 無法檢測真實環境 | ? 不支持 |
| ReadPixels (錯誤) | 極高（GPU→CPU 同步） | 高 | ? 無法使用 Texture2DArray | ? 不支持 |
| 隨機深度 | 極低 | 無 | ? 不真實 | ? 支持 |

---

## ?? **使用步驟**

### 1. 在 Inspector 中設置

```
Advanced Depth Particle Emitter (Script)
├─ Depth Manager: [拖入 EnvironmentDepthManager]
├─ Depth Sampler Shader: [拖入 DepthSamplerCorrect.compute]
├─ Samples Per Frame: 64  ← 必須是 64 的倍數（numthreads）
└─ Enable Debug Logs: ?
```

### 2. Build and Run 到 Quest 3

### 3. 查看日誌

```
[DepthParticle] Compute Shader initialized. Samples: 64
[DepthParticle] ComputeShader | Samples: 3840, Valid: 2100, Particles: 850, Count: 720
```

**理想輸出**：
- `Valid` > 0 表示深度採樣成功
- `Particles` > 0 表示粒子在發射
- `Count` 是場景中的存活粒子數

---

## ?? **Compute Shader 的優勢**

### 1. **正確讀取 Texture2DArray**
```hlsl
float inputDepthEye = UNITY_SAMPLE_TEX2DARRAY(
    _EnvironmentDepthTexture, 
    float3(uv, eyeIndex)  // ? 可以指定眼睛索引
);
```

### 2. **GPU 並行處理**
```hlsl
[numthreads(64,1,1)]  // ← 64 個線程同時執行
void SampleDepthPoints (uint3 id : SV_DispatchThreadID)
{
    // 每個線程處理一個採樣點
}
```
- 64 個採樣點**同時**在 GPU 執行
- 相比 CPU 循環，速度提升 10-100 倍

### 3. **完整的深度轉換管線**
```hlsl
原始深度 (Texture2DArray)
    ↓ UNITY_SAMPLE_TEX2DARRAY
NDC 深度值
    ↓ _EnvironmentDepthZBufferParams
線性深度 (米)
    ↓ 視線方向 × 深度
世界座標
```

---

## ?? **常見問題**

### Q1: `Valid: 0` - 沒有有效深度

**可能原因**：
1. Compute Shader 沒有正確綁定深度紋理
2. 重投影矩陣未設置
3. 深度紋理未初始化

**解決**：
```csharp
// 檢查深度是否可用
if (!depthManager.IsDepthAvailable)
{
    Debug.LogWarning("Depth not available!");
    return;
}

// Compute Shader 需要自動綁定全局紋理
// Unity 會自動將 _EnvironmentDepthTexture 等全局變量傳遞給 Compute Shader
```

### Q2: Samples Per Frame 不是 64 的倍數

**錯誤**：
```
ArgumentException: Thread group count must be >= 1
```

**原因**：
```hlsl
[numthreads(64,1,1)]  // ← Compute Shader 定義了 64 個線程
```

**解決**：
```csharp
samplesPerFrame = 64;   // ? 正確
samplesPerFrame = 128;  // ? 正確 (64 × 2)
samplesPerFrame = 30;   // ? 錯誤 (不是 64 的倍數)
```

### Q3: 記憶體洩漏

**原因**：
```csharp
ComputeBuffer resultBuffer;  // 需要手動釋放
```

**解決**：
```csharp
private void OnDestroy()
{
    if (resultBuffer != null)
    {
        resultBuffer.Release();  // ? 釋放 GPU 記憶體
        resultBuffer = null;
    }
}
```

---

## ?? **進階優化**

### 1. **異步讀取（減少 CPU 等待）**

目前實現：
```csharp
resultBuffer.GetData(results);  // ? 阻塞 CPU，等待 GPU
```

優化：
```csharp
AsyncGPUReadback.Request(resultBuffer, (request) => {
    if (request.hasError) return;
    var results = request.GetData<DepthSampleResult>();
    // ? 非阻塞，下一幀處理結果
});
```

### 2. **雙緩衝（提升幀率）**

```csharp
ComputeBuffer[] buffers = new ComputeBuffer[2];
int currentBuffer = 0;

void Update()
{
    // 啟動當前幀的 Compute Shader
    Dispatch(buffers[currentBuffer]);
    
    // 讀取上一幀的結果
    int prevBuffer = (currentBuffer + 1) % 2;
    var results = GetResults(buffers[prevBuffer]);
    
    currentBuffer = (currentBuffer + 1) % 2;
}
```

### 3. **LOD（距離衰減採樣）**

```csharp
// 距離越遠，採樣越少
float distanceToCamera = Vector3.Distance(transform.position, mainCamera.transform.position);
int adaptiveSamples = Mathf.Max(16, (int)(64 * (3f / distanceToCamera)));
```

---

## ?? **總結**

### ? 之前的錯誤
- Physics Raycast → **只能檢測 Unity Collider**，無法檢測真實環境
- ReadPixels → **無法讀取 Texture2DArray**

### ? 正確的方案
- **Compute Shader** → 在 GPU 上正確採樣 Texture2DArray
- **完整複製 Meta 的深度轉換邏輯**
- **支持動態真實環境**

### ?? 關鍵概念
1. **Texture2DArray** 必須在 Shader 中採樣
2. **重投影矩陣** 用於座標空間轉換
3. **Compute Shader** 是唯一能從 C# 訪問深度數據的方法

**現在應該能正確檢測真實世界的深度並發射粒子了！** ??
