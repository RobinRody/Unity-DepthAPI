using UnityEngine;
using Meta.XR.EnvironmentDepth;
using System.Collections;
using UnityEngine.Rendering;

[RequireComponent(typeof(ParticleSystem))]
public class AdvancedDepthParticleEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentDepthManager depthManager;
    [SerializeField] private ComputeShader depthSamplerShader;

    [Header("Depth Settings")]
    [SerializeField] private float minDepth = 0.2f;
    [SerializeField] private float maxDepth = 5.0f;
    [SerializeField] private float cullDistance = 3.0f;
    [SerializeField] private float surfaceOffset = 0.002f;
    
    // Public accessors for depth visualization sync
    public float MinDepth => minDepth;
    public float CullDistance => cullDistance;

    [Header("Density Settings - Uniform")]
    [SerializeField, Range(0f, 1f)] private float emissionDensity = 0.95f;
    [SerializeField] private int maxParticlesPerFrame = 800;

    [Header("Sampling Settings")]
    [SerializeField] private int samplesPerFrame = 512;
    [SerializeField] private float randomSpread = 0.002f;
    [SerializeField] private bool clampToSurface = true;

    [Header("Particle Settings - Uniform Size")]
    [SerializeField] private float particleLifetime = 0.3f;
    [SerializeField] private float particleSize = 0.02f; // ✅ 單一固定大小
    [SerializeField] private Gradient particleColorGradient;

    [Header("Performance")]
    [SerializeField] private int maxParticles = 20000;
    [SerializeField] private bool useAsyncReadback = true;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private bool showDetailedDebug = false;
    [SerializeField] private bool showGizmos = false;
    [SerializeField] private bool pauseParticleEmission = false; // Pause particle emission for depth debugging

    private ParticleSystem particleSystem;
    private Camera mainCamera;
    private ParticleSystem.EmitParams emitParams;

    private ComputeBuffer resultBuffer;
    private ComputeBuffer uvBuffer;
    private int kernelIndex;
    
    private struct DepthSampleResult
    {
        public Vector3 worldPosition;
        public float depth;
        public float isValid;
    }

    private int totalSamples = 0;
    private int validDepthSamples = 0;
    private int particlesEmittedTotal = 0;
    private float lastDebugTime = 0f;
    
    private DepthSampleResult[] cachedResults;
    private bool isReadbackPending = false;

    // 🆕 診斷狀態追蹤
    private enum InitializationStage
    {
        NotStarted,
        ComponentsFound,
        ComputeShaderInitialized,
        WaitingForDepth,
        DepthAvailable,
        EmissionActive,
        Failed
    }
    
    private InitializationStage currentStage = InitializationStage.NotStarted;
    private string failureReason = "";

    private void Awake()
    {
        LogDebug("=== AWAKE STAGE ===", true);
        currentStage = InitializationStage.NotStarted;

        // Stage 1: 組件檢查
        LogDebug("Stage 1: Checking components...", true);
        particleSystem = GetComponent<ParticleSystem>();
        mainCamera = Camera.main;

        if (particleSystem == null)
        {
            LogError("ParticleSystem component not found!");
            currentStage = InitializationStage.Failed;
            failureReason = "Missing ParticleSystem component";
            return;
        }
        LogDebug("✓ ParticleSystem found", true);

        if (mainCamera == null)
        {
            LogError("Main Camera not found!");
            currentStage = InitializationStage.Failed;
            failureReason = "Main Camera not found";
            return;
        }
        LogDebug($"✓ Main Camera found: {mainCamera.name}", true);

        // Stage 2: DepthManager 檢查
        LogDebug("Stage 2: Checking EnvironmentDepthManager...", true);
        if (depthManager == null)
        {
            LogDebug("DepthManager not assigned, searching in scene...", true);
            depthManager = FindAnyObjectByType<EnvironmentDepthManager>();
        }

        if (depthManager == null)
        {
            LogError("EnvironmentDepthManager not found! Please add it to the scene.");
            currentStage = InitializationStage.Failed;
            failureReason = "EnvironmentDepthManager not found";
            return;
        }
        LogDebug($"✓ DepthManager found: {depthManager.gameObject.name}", true);
        LogDebug($"  - Depth Manager Enabled: {depthManager.enabled}", true);
        LogDebug($"  - Is Supported: {EnvironmentDepthManager.IsSupported}", true);

        currentStage = InitializationStage.ComponentsFound;

        // Stage 3: 粒子系統配置
        LogDebug("Stage 3: Configuring Particle System...", true);
        ConfigureParticleSystem();
        InitializeDefaultGradient();
        LogDebug("✓ Particle System configured", true);
        
        // Stage 4: Compute Shader 初始化
        LogDebug("Stage 4: Initializing Compute Shader...", true);
        if (depthSamplerShader != null)
        {
            InitializeComputeShader();
            if (kernelIndex >= 0)
            {
                currentStage = InitializationStage.ComputeShaderInitialized;
                LogDebug("✓ Compute Shader initialized successfully", true);
            }
            else
            {
                LogError("Compute Shader kernel not found!");
                currentStage = InitializationStage.Failed;
                failureReason = "Compute Shader kernel 'SampleDepthPoints' not found";
            }
        }
        else
        {
            LogError("Compute Shader not assigned!");
            currentStage = InitializationStage.Failed;
            failureReason = "Compute Shader not assigned in Inspector";
            return;
        }
        
        cachedResults = new DepthSampleResult[samplesPerFrame];
        LogDebug($"✓ Sample buffer allocated ({samplesPerFrame} samples)", true);
        LogDebug("=== AWAKE COMPLETE ===\n", true);
    }

    private void InitializeDefaultGradient()
    {
        if (particleColorGradient == null || particleColorGradient.colorKeys.Length == 0)
        {
            particleColorGradient = new Gradient();
            var colors = new GradientColorKey[3];
            colors[0] = new GradientColorKey(new Color(0.4f, 1f, 1f), 0f);
            colors[1] = new GradientColorKey(new Color(0.2f, 0.8f, 1f), 0.5f);
            colors[2] = new GradientColorKey(new Color(0.1f, 0.6f, 1f), 1f);

            var alphas = new GradientAlphaKey[2];
            alphas[0] = new GradientAlphaKey(1.0f, 0f);
            alphas[1] = new GradientAlphaKey(0.8f, 1f);

            particleColorGradient.SetKeys(colors, alphas);
            LogDebug("Default gradient created", showDetailedDebug);
        }
    }

    private void ConfigureParticleSystem()
    {
        var main = particleSystem.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = particleSize;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = false;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;

        var emission = particleSystem.emission;
        emission.enabled = false;

        var colorOverLifetime = particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        var colorKeys = new GradientColorKey[2];
        colorKeys[0] = new GradientColorKey(Color.white, 0f);
        colorKeys[1] = new GradientColorKey(Color.white, 1f);
        
        var alphaKeys = new GradientAlphaKey[4];
        alphaKeys[0] = new GradientAlphaKey(0f, 0f);
        alphaKeys[1] = new GradientAlphaKey(1f, 0.08f);
        alphaKeys[2] = new GradientAlphaKey(1f, 0.75f);
        alphaKeys[3] = new GradientAlphaKey(0f, 1f);
        
        gradient.SetKeys(colorKeys, alphaKeys);
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
    }

    private void Start()
    {
        LogDebug("=== START STAGE ===", true);
        
        if (currentStage == InitializationStage.Failed)
        {
            LogError($"Cannot start - initialization failed: {failureReason}");
            LogError("TROUBLESHOOTING STEPS:");
            LogError("1. Check if EnvironmentDepthManager is in the scene");
            LogError("2. Verify Compute Shader is assigned in Inspector");
            LogError("3. Ensure ParticleSystem component exists");
            LogError("4. Check Console for detailed error messages above");
            return;
        }

        if (depthManager != null)
        {
            LogDebug("Starting depth wait coroutine...", true);
            currentStage = InitializationStage.WaitingForDepth;
            StartCoroutine(WaitForDepthAndStart());
        }
        else
        {
            LogError("EnvironmentDepthManager not found!");
            currentStage = InitializationStage.Failed;
            failureReason = "EnvironmentDepthManager missing at Start";
        }
    }

    private IEnumerator WaitForDepthAndStart()
    {
        LogDebug("=== WAITING FOR DEPTH ===", true);
        LogDebug($"DepthManager.IsDepthAvailable: {depthManager.IsDepthAvailable}", true);
        LogDebug($"DepthManager.enabled: {depthManager.enabled}", true);
        
        float waitTime = 0f;
        int frameCount = 0;
        
        while (!depthManager.IsDepthAvailable)
        {
            waitTime += Time.deltaTime;
            frameCount++;
            
            // 每秒一次詳細檢查
            if (waitTime > 1f)
            {
                LogDebug($"[Frame {frameCount}] Still waiting for depth... ({waitTime:F1}s elapsed)", true);
                LogDebug($"  - DepthManager enabled: {depthManager.enabled}", showDetailedDebug);
                LogDebug($"  - IsSupported: {EnvironmentDepthManager.IsSupported}", showDetailedDebug);
                
                // 🆕 檢查全域深度紋理
                var depthTex = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
                LogDebug($"  - Global Depth Texture: {(depthTex != null ? "Found" : "NULL")}", showDetailedDebug);
                
                if (waitTime > 10f)
                {
                    LogError("DEPTH TIMEOUT (10s)!");
                    LogError("TROUBLESHOOTING:");
                    LogError("1. Is this running on Quest 3?");
                    LogError("2. Is Scene permission granted?");
                    LogError("3. Is Passthrough feature enabled in OVRManager?");
                    LogError("4. Check Project Setup Tool (Meta > Tools > PST)");
                    currentStage = InitializationStage.Failed;
                    failureReason = "Depth timeout after 10 seconds";
                    yield break;
                }
                
                waitTime = 0f;
            }
            
            yield return null;
        }

        LogDebug("✓✓✓ DEPTH AVAILABLE! ✓✓✓", true);
        currentStage = InitializationStage.DepthAvailable;
        
        // 🆕 驗證深度資料
        ValidateDepthResources();

        LogDebug("Starting particle emission...", true);
        currentStage = InitializationStage.EmissionActive;
        StartCoroutine(EmitParticlesRoutine());
    }

    // 🆕 深度資源驗證
    private void ValidateDepthResources()
    {
        LogDebug("=== DEPTH RESOURCES VALIDATION ===", true);
        
        var depthTexture = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
        LogDebug($"Depth Texture: {(depthTexture != null ? $"Valid ({depthTexture.width}x{depthTexture.height})" : "NULL")}", true);
        
        var reprojMatrices = Shader.GetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices");
        LogDebug($"Reprojection Matrices: {(reprojMatrices != null ? $"Valid (count: {reprojMatrices.Length})" : "NULL")}", true);
        
        var zBufferParams = Shader.GetGlobalVector("_EnvironmentDepthZBufferParams");
        LogDebug($"ZBuffer Params: {zBufferParams}", true);
        
        LogDebug("=== VALIDATION COMPLETE ===\n", true);
    }

    private IEnumerator EmitParticlesRoutine()
    {
        LogDebug("Emission loop started", true);
        int emissionFrameCount = 0;
        
        while (true)
        {
            if (!pauseParticleEmission && depthManager != null && depthManager.IsDepthAvailable)
            {
                EmitParticlesFromDepth();
                emissionFrameCount++;
                
                if (showDetailedDebug && emissionFrameCount % 60 == 0)
                {
                    LogDebug($"Emission running... Frame {emissionFrameCount}", true);
                }
            }
            yield return null;
        }
    }

    private void InitializeComputeShader()
    {
        LogDebug("Initializing Compute Shader...", showDetailedDebug);
        
        kernelIndex = depthSamplerShader.FindKernel("SampleDepthPoints");
        
        if (kernelIndex < 0)
        {
            LogError("Kernel 'SampleDepthPoints' not found in Compute Shader!");
            LogError("Make sure you're using the correct Compute Shader file.");
            return;
        }
        
        LogDebug($"✓ Kernel found: index {kernelIndex}", showDetailedDebug);
        
        int stride = sizeof(float) * 5;
        resultBuffer = new ComputeBuffer(samplesPerFrame, stride);
        uvBuffer = new ComputeBuffer(samplesPerFrame, sizeof(float) * 2);
        
        LogDebug($"✓ Buffers created - Samples: {samplesPerFrame}, Max particles/frame: {maxParticlesPerFrame}", true);
    }

    private void EmitParticlesFromDepth()
    {
        if (depthSamplerShader == null || resultBuffer == null)
        {
            if (showDetailedDebug && Time.frameCount % 300 == 0)
            {
                LogDebug("Skipping emission: shader or buffer null", true);
            }
            return;
        }

        if (useAsyncReadback && isReadbackPending)
        {
            return;
        }

        // 🆕 階段性資源檢查
        var depthTexture = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
        if (depthTexture == null)
        {
            if (showDetailedDebug && Time.frameCount % 300 == 0)
            {
                LogDebug("Depth texture not available", true);
            }
            return;
        }

        Vector2[] randomUVs = new Vector2[samplesPerFrame];
        for (int i = 0; i < samplesPerFrame; i++)
        {
            randomUVs[i] = new Vector2(
                Random.Range(0.05f, 0.95f),
                Random.Range(0.05f, 0.95f)
            );
        }
        uvBuffer.SetData(randomUVs);

        depthSamplerShader.SetBuffer(kernelIndex, "_ResultBuffer", resultBuffer);
        depthSamplerShader.SetBuffer(kernelIndex, "_InputUVs", uvBuffer);
        depthSamplerShader.SetTexture(kernelIndex, "_EnvironmentDepthTexture", depthTexture);
        
        var reprojMatrices = Shader.GetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices");
        if (reprojMatrices != null && reprojMatrices.Length >= 2)
        {
            depthSamplerShader.SetMatrixArray("_EnvironmentDepthReprojectionMatrices", reprojMatrices);
        }
        else
        {
            LogDebug("Reprojection matrices not available or insufficient", showDetailedDebug);
        }
        
        var zBufferParams = Shader.GetGlobalVector("_EnvironmentDepthZBufferParams");
        depthSamplerShader.SetVector("_EnvironmentDepthZBufferParams", zBufferParams);
        
        depthSamplerShader.SetVector("_CameraPosition", mainCamera.transform.position);
        depthSamplerShader.SetVector("_CameraForward", mainCamera.transform.forward);
        depthSamplerShader.SetVector("_CameraRight", mainCamera.transform.right);
        depthSamplerShader.SetVector("_CameraUp", mainCamera.transform.up);
        depthSamplerShader.SetFloat("_TanHalfFOV", Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        depthSamplerShader.SetFloat("_AspectRatio", mainCamera.aspect);
        
        int threadGroups = Mathf.CeilToInt(samplesPerFrame / 64f);
        
        try
        {
            depthSamplerShader.Dispatch(kernelIndex, threadGroups, 1, 1);
        }
        catch (System.Exception e)
        {
            LogError($"Dispatch failed: {e.Message}");
            LogError($"Stack trace: {e.StackTrace}");
            return;
        }
        
        if (useAsyncReadback)
        {
            isReadbackPending = true;
            AsyncGPUReadback.Request(resultBuffer, OnReadbackComplete);
        }
        else
        {
            resultBuffer.GetData(cachedResults);
            ProcessResults();
        }
    }

    private void OnReadbackComplete(AsyncGPUReadbackRequest request)
    {
        isReadbackPending = false;
        
        if (request.hasError)
        {
            LogError("GPU readback error!");
            return;
        }
        
        var data = request.GetData<DepthSampleResult>();
        data.CopyTo(cachedResults);
        ProcessResults();
    }

    private void ProcessResults()
    {
        int particlesEmitted = 0;
        int validSamplesThisFrame = 0;
        int depthInRangeSamples = 0; // 🆕 追蹤深度範圍內的樣本
        
        for (int i = 0; i < samplesPerFrame && particlesEmitted < maxParticlesPerFrame; i++)
        {
            totalSamples++;
            
            if (cachedResults[i].isValid > 0.5f)
            {
                validDepthSamples++;
                validSamplesThisFrame++;
                
                float envDepth = cachedResults[i].depth;
                
                if (envDepth >= minDepth && envDepth <= cullDistance)
                {
                    depthInRangeSamples++;
                    float normalizedDepth = Mathf.InverseLerp(minDepth, cullDistance, envDepth);
                    
                    if (Random.value < emissionDensity)
                    {
                        Vector3 worldPos = cachedResults[i].worldPosition;
                        
                        if (clampToSurface)
                        {
                            Vector3 toCamera = (mainCamera.transform.position - worldPos).normalized;
                            worldPos += toCamera * surfaceOffset;
                        }
                        
                        EmitParticleAt(worldPos, normalizedDepth);
                        particlesEmitted++;
                        particlesEmittedTotal++;
                    }
                }
            }
        }

        if (enableDebugLogs && Time.time - lastDebugTime > 1f)
        {
            float validRate = totalSamples > 0 ? (validDepthSamples / (float)totalSamples) * 100f : 0f;
            LogDebug($"[Stats] Valid: {validRate:F1}% | In Range: {depthInRangeSamples}/{validSamplesThisFrame} | Emitted: {particlesEmitted} | Active: {particleSystem.particleCount}", true);
            
            if (showDetailedDebug)
            {
                LogDebug($"  - Total particles emitted: {particlesEmittedTotal}", true);
                LogDebug($"  - Depth range: {minDepth:F2}m to {cullDistance:F2}m", true);
                LogDebug($"  - Current stage: {currentStage}", true);
            }
            
            // 🆕 警告訊息
            if (particlesEmitted == 0 && validSamplesThisFrame > 0)
            {
                LogDebug("⚠ WARNING: Valid samples found but no particles emitted!", true);
                LogDebug($"  Check if depth values are in range ({minDepth:F2}m - {cullDistance:F2}m)", true);
            }
            
            lastDebugTime = Time.time;
        }
    }

    private void EmitParticleAt(Vector3 worldPosition, float normalizedDepth)
    {
        Vector3 randomOffset = Vector3.zero;
        
        if (randomSpread > 0.001f)
        {
            randomOffset = Random.insideUnitSphere * randomSpread;
        }
        
        Vector3 position = worldPosition + randomOffset;

        Color color = particleColorGradient.Evaluate(normalizedDepth);

        emitParams.position = position;
        emitParams.velocity = Vector3.zero;
        emitParams.startLifetime = particleLifetime;
        emitParams.startSize = particleSize;
        emitParams.startColor = color;

        particleSystem.Emit(emitParams, 1);
    }

    private void OnDestroy()
    {
        LogDebug("Cleaning up resources...", true);
        
        if (resultBuffer != null)
        {
            resultBuffer.Release();
            resultBuffer = null;
        }
        
        if (uvBuffer != null)
        {
            uvBuffer.Release();
            uvBuffer = null;
        }
        
        LogDebug("Resources released", true);
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !showGizmos || mainCamera == null || cachedResults == null)
            return;

        for (int i = 0; i < Mathf.Min(30, cachedResults.Length); i++)
        {
            if (cachedResults[i].isValid > 0.5f && cachedResults[i].depth <= cullDistance)
            {
                float normalizedDepth = Mathf.InverseLerp(minDepth, cullDistance, cachedResults[i].depth);
                Gizmos.color = new Color(1f - normalizedDepth, normalizedDepth, 0f, 0.8f);
                Gizmos.DrawWireSphere(cachedResults[i].worldPosition, particleSize * 0.5f);
            }
        }
    }
    
    private void OnValidate()
    {
        if (cullDistance > maxDepth)
        {
            cullDistance = maxDepth;
        }
        
        if (minDepth > cullDistance)
        {
            minDepth = cullDistance * 0.5f;
        }
        
        if (samplesPerFrame % 64 != 0)
        {
            samplesPerFrame = Mathf.CeilToInt(samplesPerFrame / 64f) * 64;
        }
        
        if (Application.isPlaying && particleSystem != null)
        {
            var main = particleSystem.main;
            main.startLifetime = particleLifetime;
            main.startSize = particleSize;
            main.maxParticles = maxParticles;
        }
    }

    // 🆕 統一的日誌系統
    private void LogDebug(string message, bool condition)
    {
        if (condition)
        {
            Debug.Log($"[DepthParticle] {message}");
        }
    }

    private void LogError(string message)
    {
        Debug.LogError($"[DepthParticle] {message}");
    }

    // 🆕 公開診斷方法
    public string GetDiagnosticInfo()
    {
        string info = "=== DEPTH PARTICLE DIAGNOSTICS ===\n";
        info += $"Current Stage: {currentStage}\n";
        info += $"Failure Reason: {(string.IsNullOrEmpty(failureReason) ? "None" : failureReason)}\n";
        info += $"Depth Manager: {(depthManager != null ? "Found" : "NULL")}\n";
        info += $"Depth Available: {(depthManager != null ? depthManager.IsDepthAvailable.ToString() : "N/A")}\n";
        info += $"Compute Shader: {(depthSamplerShader != null ? "Assigned" : "NULL")}\n";
        info += $"Kernel Index: {kernelIndex}\n";
        info += $"Total Particles Emitted: {particlesEmittedTotal}\n";
        info += $"Active Particles: {(particleSystem != null ? particleSystem.particleCount : 0)}\n";
        info += $"Valid Sample Rate: {(totalSamples > 0 ? (validDepthSamples / (float)totalSamples * 100f).ToString("F1") : "0")}%\n";
        return info;
    }
}