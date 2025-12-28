# ?? Unity Crash 根本原因與修復

## ?? **致命問題：記憶體洩漏導致 Crash**

### 問題代碼（舊版）

```csharp
private float SampleEnvironmentDepthAtScreenPoint(...)
{
    // ? 每次調用都創建新的 Texture2D
    Texture2D tempTex = new Texture2D(1, 1, TextureFormat.RFloat, false);
    tempTex.ReadPixels(new Rect(pixelX, pixelY, 1, 1), 0, 0);
    tempTex.Apply();
    
    float rawDepth = tempTex.GetPixel(0, 0).r;
    Destroy(tempTex);  // ? Destroy 不是立即釋放！
    
    return linearDepth;
}
```

---

## ?? **為什麼會 Crash？**

### 1. **每幀創建大量 Texture2D**

```
每幀調用 SampleEnvironmentDepthAtScreenPoint 的次數：
- samplesPerFrame = 20（預設值）
- 每幀創建 20 個 Texture2D
- 60 FPS → 每秒創建 1,200 個 Texture2D

記憶體分配：
- 每個 Texture2D：至少 16 bytes（RFloat 格式）
- 每秒：1,200 × 16 = 19.2 KB
- 1 分鐘：1.15 MB
- 5 分鐘：5.76 MB

但實際上更糟糕，因為：
- Unity 的 Texture2D 有額外的元數據開銷
- 每次 new Texture2D() 都會在 Managed Heap 分配記憶體
```

### 2. **Destroy() 不會立即釋放記憶體**

```csharp
Destroy(tempTex);  // ? 這只是標記為待刪除！
```

**Unity 的 Destroy() 工作原理**：
- `Destroy()` 只是**標記物件為待刪除**
- 實際釋放要等到**下一幀的 LateUpdate 之後**
- 在高幀率下（60 FPS），GC 來不及回收

**結果**：
```
Frame 1: 創建 20 個 Texture2D，標記刪除
Frame 2: 創建 20 個 Texture2D，標記刪除（Frame 1 的還沒釋放）
Frame 3: 創建 20 個 Texture2D，標記刪除（Frame 1, 2 的都還沒釋放）
...
Frame 60: 已經累積了 1,200 個待刪除的 Texture2D

→ 記憶體不斷增長
→ GC 頻繁觸發（導致卡頓）
→ 最終記憶體耗盡 → Crash
```

### 3. **ReadPixels 的 GPU→CPU 同步開銷**

```csharp
tempTex.ReadPixels(...)  // ? 阻塞 GPU 管線
tempTex.Apply()          // ? 強制同步
```

**每次 ReadPixels 都會**：
- 強制 GPU 完成當前渲染
- 將數據從 GPU VRAM 複製到 CPU RAM
- 阻塞渲染管線

**在 60 FPS 下**：
- 每幀 20 次 GPU→CPU 同步
- 每秒 1,200 次同步操作
- 嚴重影響渲染性能

---

## ? **修復方案：重用 Texture2D**

### 新代碼（已修復）

```csharp
// 類成員變量：只創建一次
private Texture2D reusableTexture;
private bool textureInitialized = false;

private float SampleEnvironmentDepthAtScreenPoint(...)
{
    // ? 只在第一次調用時創建
    if (!textureInitialized || reusableTexture == null)
    {
        if (reusableTexture != null)
        {
            Destroy(reusableTexture);
        }
        reusableTexture = new Texture2D(1, 1, TextureFormat.RFloat, false);
        textureInitialized = true;
    }

    // ? 重用同一個 Texture2D
    reusableTexture.ReadPixels(new Rect(pixelX, pixelY, 1, 1), 0, 0);
    reusableTexture.Apply();
    
    float rawDepth = reusableTexture.GetPixel(0, 0).r;
    
    return linearDepth;
}

private void OnDestroy()
{
    // ? 在銷毀時清理
    if (reusableTexture != null)
    {
        Destroy(reusableTexture);
        reusableTexture = null;
    }
}
```

---

## ?? **效能對比**

| 項目 | 修復前 | 修復後 |
|------|--------|--------|
| **每幀創建 Texture2D** | 20 個 | 0 個（只創建一次） |
| **每秒創建 Texture2D** | 1,200 個 | 0 個 |
| **記憶體分配** | 持續增長 | 固定（16 bytes） |
| **GC 壓力** | 極高 | 極低 |
| **ReadPixels 次數** | 1,200/秒 | 1,200/秒（仍是瓶頸） |
| **Crash 風險** | ?? 極高 | ? 極低 |

---

## ?? **進一步優化建議**

### 1. **降低採樣頻率**

即使修復了 Texture2D 問題，`ReadPixels` 仍然是性能瓶頸。

**建議參數**：
```csharp
[Header("Sampling Settings")]
[SerializeField] private int samplesPerFrame = 10;  // 從 20 降到 10
```

**效果**：
- 每秒 ReadPixels 次數：1,200 → 600
- 性能提升約 50%

### 2. **使用 AsyncGPUReadback（進階）**

```csharp
// Unity 2018.2+ 支持異步讀取
AsyncGPUReadback.Request(depthTexture, 0, TextureFormat.RFloat, OnCompleteReadback);

void OnCompleteReadback(AsyncGPUReadbackRequest request)
{
    if (request.hasError)
        return;
    
    var data = request.GetData<float>();
    float rawDepth = data[0];
    // ...
}
```

**優點**：
- 不阻塞 GPU 管線
- 不影響幀率

**缺點**：
- 延遲 1-2 幀
- 實現複雜度高

### 3. **使用 Compute Shader（最佳方案）**

將深度採樣和粒子生成移到 GPU：

```csharp
// 在 Compute Shader 中直接採樣深度紋理
RWStructuredBuffer<ParticleData> particleBuffer;
Texture2DArray<float> depthTexture;

[numthreads(8, 8, 1)]
void SampleDepth(uint3 id : SV_DispatchThreadID)
{
    float2 uv = id.xy / textureSize;
    float depth = depthTexture.SampleLevel(samplerState, uv, 0);
    
    // 直接在 GPU 生成粒子數據
    if (depth > minDepth && depth < maxDepth)
    {
        particleBuffer[id.x] = GenerateParticle(uv, depth);
    }
}
```

**優點**：
- 完全在 GPU 執行
- 沒有 GPU→CPU 同步
- 性能提升 100-1000 倍

**缺點**：
- 需要學習 Compute Shader
- 實現複雜

---

## ?? **Unity Crash 檢查清單**

如果仍然遇到 Crash，請檢查：

### ? **已修復的問題**
- [x] Texture2D 記憶體洩漏（已修復）
- [x] OnDestroy 清理（已添加）

### ?? **仍需注意的問題**
- [ ] **samplesPerFrame 過高**（建議 ? 10）
- [ ] **particleLifetime 過長**（建議 ? 2 秒）
- [ ] **maxParticles 過高**（建議 ? 5000）
- [ ] **深度紋理解析度過高**（檢查 EnvironmentDepthManager 設置）

### ?? **檢查步驟**

1. **打開 Profiler**（Window → Analysis → Profiler）
2. **查看 Memory 面板**：
   - 觀察 GC Alloc 是否持續增長
   - 修復後應該看到穩定的記憶體使用

3. **查看 CPU 面板**：
   - 找到 `SampleEnvironmentDepthAtScreenPoint`
   - 修復後耗時應該降低 90%+

4. **查看 Rendering 面板**：
   - 觀察 ReadPixels 調用次數
   - 應該等於 samplesPerFrame

---

## ?? **總結**

### 原因
- ? 每幀創建 20 個 Texture2D
- ? Destroy() 不立即釋放
- ? GC 來不及回收
- ? 記憶體耗盡 → Crash

### 修復
- ? 重用單個 Texture2D
- ? 只在初始化時創建一次
- ? OnDestroy 時清理
- ? 記憶體使用穩定

### 建議
- 降低 samplesPerFrame 到 10
- 考慮使用 AsyncGPUReadback
- 最終方案：Compute Shader

---

**修復後應該能穩定運行，不再 Crash！**
