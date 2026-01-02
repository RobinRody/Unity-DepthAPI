using UnityEngine;
using Meta.XR.EnvironmentDepth;
using System.Collections;
using UnityEngine.Rendering;
using System.Reflection; // 🆕 新增

[RequireComponent(typeof(ParticleSystem))
]
public class AdvancedDepthParticleEmitter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentDepthManager depthManager;
    [SerializeField] private ComputeShader depthSamplerShader;

    [Header("Depth Settings")]
    [SerializeField] private float minDepth = 0.2f;
    [SerializeField] private float maxDepth = 3.0f; // 🔄 合併 cullDistance
    [SerializeField] private float surfaceOffset = 0.002f;

    [Header("Height Filtering")]
    [SerializeField] private bool enableHeightFilter = true;
    [SerializeField] private HeightFilterMode heightFilterMode = HeightFilterMode.ComputeShader;
    [SerializeField] private bool useRelativeHeight = true; // 🆕 預設啟用相對高度
    [SerializeField] private float minRelativeHeight = 0.3f; // 頭部以上 0.3m
    [SerializeField] private float maxRelativeHeight = 1.5f; // 腳下 1.5m
    [SerializeField] private float minWorldHeight = 0.2f; // 絕對高度 (備用)
    [SerializeField] private float maxWorldHeight = 2.2f;

    public enum HeightFilterMode
    {
        ComputeShader,   // GPU 端過濾 (推薦)
        Hybrid           // GPU + CPU 雙重過濾
    }

    // Public accessors
    public float MinDepth => minDepth;
    public float MaxDepth => maxDepth;


    [Header("Performance")]
    [SerializeField] private int maxParticles = 30000;
    [SerializeField] private bool useAsyncReadback = true;

    // 🆕 新增：基於深度的動態大小
    [Header("Particle Settings")]
    [SerializeField] private float particleLifetime = 0.3f;
    [SerializeField] private bool useDepthBasedSize = true; // 🆕 啟用深度大小
    [SerializeField] private AnimationCurve sizeCurve; // 🆕 大小曲線
    [SerializeField] private float particleSizeMin = 0.01f; // 🆕 最小 1cm
    [SerializeField] private float particleSizeMax = 0.1f; // 
    [SerializeField] private Gradient particleColorGradient;
    [Header("Density Settings")]
    [SerializeField] private bool useDepthBasedDensity = true; // 🆕 深度密度開關
    [SerializeField] private AnimationCurve densityCurve; // 🆕 密度曲線
    [SerializeField, Range(0f, 1f)] private float globalDensityMultiplier = 0.8f;
    [SerializeField] private int maxParticlesPerFrame = 1200;

    [Header("Sampling Settings")]
    [SerializeField] private int samplesPerFrame = 1024;
    [SerializeField] private float randomSpread = 0.002f;
    [SerializeField] private bool clampToSurface = true;

    // 🔧 改為方向性 UV 範圍控制
    [Header("UV Sampling Range (Directional)")]
    [SerializeField, Range(0.05f, 0.45f)] private float uvRangeLeft = 0.3f;   // 左邊界
    [SerializeField, Range(0.55f, 0.95f)] private float uvRangeRight = 0.7f;  // 右邊界
    [SerializeField, Range(0.05f, 0.45f)] private float uvRangeBottom = 0.3f; // 🆕 下邊界 (更嚴格)
    [SerializeField, Range(0.55f, 0.95f)] private float uvRangeTop = 0.8f;     // 🆕 上邊界 (較寬鬆)
    //[SerializeField, Range(0.05f, 0.45f)] private float uvSampleRangeMin = 0.2f; // 🆕 縮小採樣範圍
    //[SerializeField, Range(0.55f, 0.95f)] private float uvSampleRangeMax = 0.8f;
    [SerializeField] private AnimationCurve uvWeightCurve; // 🆕 UV 權重曲線



    // 🆕 非對稱邊緣過濾
    [Header("Edge Filtering (Directional)")]
    [SerializeField] private bool enableEdgeFiltering = true;
    [SerializeField, Range(0f, 0.3f)] private float edgeThresholdHorizontal = 0.15f; // 左右邊緣閾值
    [SerializeField, Range(0f, 0.4f)] private float edgeThresholdBottom = 0.25f;     // 🆕 下方邊緣閾值 (更大)
    [SerializeField, Range(0f, 0.3f)] private float edgeThresholdTop = 0.1f;


    // 🆕 新增底部過濾參數
    [Header("Bottom Region Filtering")]
    [SerializeField] private bool enableBottomDepthCheck = true;
    [SerializeField, Range(0.05f, 0.5f)] private float bottomRegionThreshold = 0.35f;  // 定義「底部」
    //[SerializeField, Range(0.2f, 1.5f)] private float minBottomDepth = 0.4f;
    //[SerializeField, Range(1.0f, 3.0f)] private float maxBottomDepth = 2.0f;
    [SerializeField, Range(0.2f, 3.0f)] private float maxBottomDepth = 1f;         // 底部區域的最小深度
    [SerializeField, Range(0.0f, 1.0f)] private float bottomRegionProbabilityMultiplier = 0.0f; // 0=完全過濾, 0.5=減半機率

    [Header("Edge Depth Correction (Heuristic)")]
    [SerializeField] private bool enableEdgeDepthCorrection = false;
    [SerializeField, Range(0.5f, 1.0f)] private float edgeCorrectionStartRadius = 0.7f;
    [SerializeField, Range(0f, 0.5f)] private float maxCorrectionPercentage = 0.15f;
    [SerializeField, Tooltip("Higher = more aggressive correction")]
    [Range(0.5f, 3.0f)] private float correctionCurveExponent = 1.0f;

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
    private Vector2[] savedUVs; // 🆕 保存 UV 用於 CPU 重新計算
    private bool isReadbackPending = false;

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

    // 🆕 反射相關 (完整版本)
    private static FieldInfo frameDescriptorsField;
    private static FieldInfo fovLeftField;
    private static FieldInfo fovRightField;
    private static FieldInfo fovTopField;
    private static FieldInfo fovDownField;
    private static FieldInfo poseLocationField;
    private static FieldInfo poseRotationField;
    private static bool reflectionFailed = false;

    // 🆕 深度相機 FOV
    private float depthCameraFovLeft = 0f;
    private float depthCameraFovRight = 0f;
    private float depthCameraFovTop = 0f;
    private float depthCameraFovDown = 0f;

    // 🆕 新增深度相機姿態欄位
    private Vector3 depthCameraPoseLocation;
    private Quaternion depthCameraPoseRotation;

    private void Awake()
    {
        LogDebug("=== AWAKE STAGE ===", true);
        currentStage = InitializationStage.NotStarted;


        // ===== 1. 組件檢查 =====
        particleSystem = GetComponent<ParticleSystem>();
        mainCamera = Camera.main;

        if (particleSystem == null || mainCamera == null)
        {
            LogError("Required components not found!");
            currentStage = InitializationStage.Failed;
            return;
        }

        // DepthManager 檢查
        if (depthManager == null)
        {
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

        // ===== 2. 粒子系統配置 =====
        ConfigureParticleSystem();
        // ===== 3. 預設數據初始化 =====
        InitializeDefaultGradient();
        InitializeDefaultDensityCurve(); // 🆕
        InitializeDefaultSizeCurve();
        InitializeUVWeighting(); // 🆕

        // ===== 4. Compute Shader 準備 =====
        if (depthSamplerShader != null)
        {
            InitializeComputeShader();
            if (kernelIndex >= 0)
            {
                currentStage = InitializationStage.ComputeShaderInitialized;
            }
        }
        else
        {
            LogError("Compute Shader not assigned!");
            currentStage = InitializationStage.Failed;
            failureReason = "Compute Shader not assigned in Inspector";
            return;
        }

        // ===== 5. 反射初始化 =====
        if (frameDescriptorsField == null && !reflectionFailed)
        {
            InitializeReflection();
        }

        // ===== 6. 緩衝區分配 =====
        cachedResults = new DepthSampleResult[samplesPerFrame];
        savedUVs = new Vector2[samplesPerFrame]; // 🆕
        LogDebug("=== AWAKE COMPLETE ===\n", true);
    }

    // 🆕 初始化預設密度曲線
    private void InitializeDefaultDensityCurve()
    {
        if (densityCurve == null || densityCurve.keys.Length == 0)
        {
            densityCurve = new AnimationCurve(
                new Keyframe(0f, 1f),      // 近處 100% 密度
                new Keyframe(0.5f, 0.7f),  // 🔄 中距 70% (原 60%)
                new Keyframe(1f, 0.5f)     // 🔄 遠處 40% (原 20%)
            );
            LogDebug("Default density curve created (near-dense, far-moderate)", true);
        }
    }

    // 🆕 初始化預設大小曲線 (透視補償: 視覺大小恆定)
    private void InitializeDefaultSizeCurve()
    {
        if (sizeCurve == null || sizeCurve.keys.Length == 0)
        {
            // 📐 透視補償曲線 - 修正版
            // 
            // 目標: 粒子在螢幕上的視覺大小恆定
            // 原理: 視角大小 (angular size) = 物理大小 / 深度
            //       若要視角恆定,則: 物理大小 ∝ 深度
            // 
            // 但是,完全線性補償 (15x) 會過度補償,因為:
            // 1. 遠處粒子會被遮擋/密度降低
            // 2. 視覺上不需要完全相同大小,略有差異更自然
            // 
            // 解決方案: 使用平方根或對數曲線進行溫和補償
            // 公式: size = baseSize * sqrt(depth / minDepth)
            //      或: size = baseSize * (1 + log(depth / minDepth))
            
            // 計算關鍵點
            float depthRatio = maxDepth / minDepth; // 15.0
            
            // 方案1: 平方根補償 (溫和,推薦)
            float sqrtCompensation = Mathf.Sqrt(depthRatio); // ~3.87x (而非 15x)
            
            // 方案2: 對數補償 (最溫和)
            // float logCompensation = 1f + Mathf.Log(depthRatio, 2f); // ~4.9x
            
            // 方案3: 指數補償 (可調整強度)
            // float expFactor = 0.5f; // 調整補償強度 (0=無補償, 1=完全補償)
            // float customCompensation = Mathf.Pow(depthRatio, expFactor); // ~3.87x (當 expFactor=0.5)
            
            // 使用平方根補償建立曲線
            sizeCurve = new AnimationCurve();
            
            // 在 [0,1] 範圍內建立平方根曲線
            int curvePoints = 5;
            for (int i = 0; i < curvePoints; i++)
            {
                float t = i / (float)(curvePoints - 1); // 0, 0.25, 0.5, 0.75, 1.0
                
                // 計算當前深度
                float currentDepth = Mathf.Lerp(minDepth, maxDepth, t);
                
                // 平方根補償
                float sizeMultiplier = Mathf.Sqrt(currentDepth / minDepth);
                
                // 歸一化到 [0,1] 範圍
                float normalizedSize = (sizeMultiplier - 1f) / (sqrtCompensation - 1f);
                
                sizeCurve.AddKey(t, normalizedSize);
            }
            
            // 設置切線為平滑
            for (int i = 0; i < sizeCurve.keys.Length; i++)
            {
                sizeCurve.SmoothTangents(i, 0f);
            }
            
            // 🔧 自動調整 particleSizeMax
            if (minDepth > 0f && maxDepth > minDepth)
            {
                float recommendedMaxSize = particleSizeMin * sqrtCompensation;
                
                // 如果 particleSizeMax 使用預設值,則自動調整
                if (Mathf.Abs(particleSizeMax - 0.1f) < 0.001f)
                {
                    particleSizeMax = recommendedMaxSize;
                    LogDebug($"✓ Auto-adjusted particleSizeMax to {particleSizeMax:F3}m", true);
                    LogDebug($"  Compensation: sqrt({depthRatio:F1}x) = {sqrtCompensation:F2}x (gentle perspective correction)", true);
                }
                else
                {
                    LogDebug($"Using manual particleSizeMax: {particleSizeMax:F3}m (recommended: {recommendedMaxSize:F3}m)", true);
                }
            }
            
            LogDebug("Perspective-compensated size curve created (sqrt compensation for natural look)", true);
        }
    }

    private void InitializeDefaultGradient()
    {
        // 🆕 改進的預設漸層檢測
        bool isDefaultGradient = false;
        
        if (particleColorGradient != null && particleColorGradient.colorKeys.Length == 2)
        {
            var keys = particleColorGradient.colorKeys;
            isDefaultGradient = 
                keys[0].color == Color.white && keys[0].time == 0f &&
                keys[1].color == Color.black && keys[1].time == 1f;
        }
        
        if (particleColorGradient == null || isDefaultGradient)
        {
            particleColorGradient = new Gradient();
            var colors = new GradientColorKey[5];
            colors[0] = new GradientColorKey(new Color(1f, 0.3f, 0.3f), 0f);    // 近處紅色
            colors[1] = new GradientColorKey(new Color(1f, 0.8f, 0.3f), 0.25f); // 橙色
            colors[2] = new GradientColorKey(new Color(0.3f, 1f, 0.3f), 0.5f);  // 綠色
            colors[3] = new GradientColorKey(new Color(0.3f, 0.8f, 1f), 0.75f); // 青色
            colors[4] = new GradientColorKey(new Color(0.3f, 0.3f, 1f), 1f);    // 遠處藍色

            var alphas = new GradientAlphaKey[2];
            alphas[0] = new GradientAlphaKey(1.0f, 0f);
            alphas[1] = new GradientAlphaKey(0.9f, 1f);

            particleColorGradient.SetKeys(colors, alphas);
            LogDebug("Custom gradient created (replaced Unity default)", true);
        }
    }
    // 在 Awake() 中初始化
    private void InitializeUVWeighting()
    {
        if (uvWeightCurve == null || uvWeightCurve.keys.Length == 0)
        {
            uvWeightCurve = new AnimationCurve();
            // 中心區域權重 1.0
            uvWeightCurve.AddKey(0.5f, 1.0f);
            // 邊緣區域權重 0.3 (仍採樣,但機率降低)
            uvWeightCurve.AddKey(0.0f, 0.3f);
            uvWeightCurve.AddKey(1.0f, 0.3f);
        }
    }

    private void ConfigureParticleSystem()
    {
        var main = particleSystem.main;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = particleSizeMin; // 設置初始大小 (會被動態覆蓋)
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

    // 修改 EmitParticlesFromDepth()
    private void EmitParticlesFromDepth()
    {
        // ===== 1. 資源檢查 =====
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

        var depthTexture = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
        if (depthTexture == null)
        {
            return;
        }

        // ===== 2. 🔧 同時讀取雙眼數據 =====
        // 讀取左眼數據
        bool leftValid = TryGetDepthCameraData(
            0,  // 左眼
            out float leftFovL, out float leftFovR, 
            out float leftFovT, out float leftFovD,
            out Vector3 leftPoseLoc, out Quaternion leftPoseRot
        );
        
        // 讀取右眼數據
        bool rightValid = TryGetDepthCameraData(
            1,  // 右眼
            out float rightFovL, out float rightFovR, 
            out float rightFovT, out float rightFovD,
            out Vector3 rightPoseLoc, out Quaternion rightPoseRot
        );
        
        if (!leftValid && !rightValid)
        {
            LogDebug("Failed to get both eyes' depth camera data", showDetailedDebug);
            return;
        }

        // ===== 3. 生成隨機 UV (分成兩半) =====
        int samplesPerEye = samplesPerFrame / 2;
        
        for (int i = 0; i < samplesPerFrame; i++)
        {
            Vector2 uv;
            float weight = 0f;
            
            // 拒絕採樣: 低權重區域需要更高隨機值才被接受
            do
            {
                uv = new Vector2(
                    Random.Range(uvRangeLeft, uvRangeRight),
                    Random.Range(uvRangeBottom, uvRangeTop)
                );
                
                // 計算 UV 到中心的距離
                float distFromCenter = Vector2.Distance(uv, new Vector2(0.5f, 0.5f)) * 2f;
                weight = uvWeightCurve.Evaluate(distFromCenter);
                
            } while (Random.value > weight);
            
            savedUVs[i] = uv;
        }
        uvBuffer.SetData(savedUVs);

        // ===== 4. 設置共用參數 =====
        depthSamplerShader.SetBuffer(kernelIndex, "_ResultBuffer", resultBuffer);
        depthSamplerShader.SetBuffer(kernelIndex, "_InputUVs", uvBuffer);
        depthSamplerShader.SetTexture(kernelIndex, "_EnvironmentDepthTexture", depthTexture);

        var zBufferParams = Shader.GetGlobalVector("_EnvironmentDepthZBufferParams");
        depthSamplerShader.SetVector("_EnvironmentDepthZBufferParams", zBufferParams);

        // 高度過濾範圍
        float effectiveMinHeight, effectiveMaxHeight;
        CalculateEffectiveHeightRange(out effectiveMinHeight, out effectiveMaxHeight);
        
        // GPU 端高度過濾設定
        if (enableHeightFilter && 
            (heightFilterMode == HeightFilterMode.ComputeShader || heightFilterMode == HeightFilterMode.Hybrid))
        {
            depthSamplerShader.SetFloat("_MinWorldHeight", effectiveMinHeight);
            depthSamplerShader.SetFloat("_MaxWorldHeight", effectiveMaxHeight);
        }
        else
        {
            // 禁用 GPU 端過濾 (設為極端值)
            depthSamplerShader.SetFloat("_MinWorldHeight", -1000f);
            depthSamplerShader.SetFloat("_MaxWorldHeight", 1000f);
        }

        // 🆕 深度校正參數
        depthSamplerShader.SetFloat("_EdgeDepthCorrectionEnabled", enableEdgeDepthCorrection ? 1.0f : 0.0f);
        depthSamplerShader.SetFloat("_EdgeCorrectionStartRadius", edgeCorrectionStartRadius);
        depthSamplerShader.SetFloat("_MaxCorrectionPercentage", maxCorrectionPercentage);
        depthSamplerShader.SetFloat("_CorrectionCurveExponent", correctionCurveExponent);

        // 左眼採樣
        if (leftValid)
        {
            depthSamplerShader.SetFloat("_DepthCameraFovLeft", leftFovL);
            depthSamplerShader.SetFloat("_DepthCameraFovRight", leftFovR);
            depthSamplerShader.SetFloat("_DepthCameraFovTop", leftFovT);
            depthSamplerShader.SetFloat("_DepthCameraFovDown", leftFovD);
            depthSamplerShader.SetVector("_DepthCameraPoseLocation", leftPoseLoc);
            depthSamplerShader.SetVector("_DepthCameraPoseRotation", new Vector4(
                leftPoseRot.x, leftPoseRot.y, leftPoseRot.z, leftPoseRot.w
            ));
            depthSamplerShader.SetInt("_CurrentEyeIndex", 0);
            depthSamplerShader.SetInt("_SampleOffset", 0);
            depthSamplerShader.SetInt("_SampleCount", samplesPerEye);
            
            int threadGroups = Mathf.CeilToInt(samplesPerEye / 64f);
            depthSamplerShader.Dispatch(kernelIndex, threadGroups, 1, 1);
        }

        // ===== 6. 🔧 右眼採樣 =====
        if (rightValid)
        {
            depthSamplerShader.SetFloat("_DepthCameraFovLeft", rightFovL);
            depthSamplerShader.SetFloat("_DepthCameraFovRight", rightFovR);
            depthSamplerShader.SetFloat("_DepthCameraFovTop", rightFovT);
            depthSamplerShader.SetFloat("_DepthCameraFovDown", rightFovD);
            depthSamplerShader.SetVector("_DepthCameraPoseLocation", rightPoseLoc);
            depthSamplerShader.SetVector("_DepthCameraPoseRotation", new Vector4(
                rightPoseRot.x, rightPoseRot.y, rightPoseRot.z, rightPoseRot.w
            ));
            depthSamplerShader.SetInt("_CurrentEyeIndex", 1);
            depthSamplerShader.SetInt("_SampleOffset", samplesPerEye);
            depthSamplerShader.SetInt("_SampleCount", samplesPerEye);
            
            int threadGroups = Mathf.CeilToInt(samplesPerEye / 64f);
            depthSamplerShader.Dispatch(kernelIndex, threadGroups, 1, 1);
        }

        // ===== 7. 回讀結果 =====
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

    // 🆕 計算有效高度範圍 (相對或絕對)
    private void CalculateEffectiveHeightRange(out float minHeight, out float maxHeight)
    {
        // ✅ 絕對高度為基準
        minHeight = minWorldHeight;
        maxHeight = maxWorldHeight;
        
        // ✅ 相對高度進一步縮小範圍 (取交集)
        if (useRelativeHeight && mainCamera != null)
        {
            float headHeight = mainCamera.transform.position.y;
            float relativeMinHeight = headHeight - maxRelativeHeight;
            float relativeMaxHeight = headHeight + minRelativeHeight;
            
            minHeight = Mathf.Max(minHeight, relativeMinHeight);
            maxHeight = Mathf.Min(maxHeight, relativeMaxHeight);
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
        int heightFilteredCount = 0;
        int validSamplesThisFrame = 0;
        int depthInRangeSamples = 0;
        int densityFilteredCount = 0;
        int bottomDepthFilteredCount = 0; // 🆕 底部深度過濾計數

        // 🆕 診斷變量
        float minObservedY = float.MaxValue;
        float maxObservedY = float.MinValue;
        int groundParticleCount = 0;
        int belowMinHeightCount = 0;

        // 🆕 計算有效高度範圍
        float effectiveMinHeight = 0f;
        float effectiveMaxHeight = 0f;
        if (enableHeightFilter)
        {
            CalculateEffectiveHeightRange(out effectiveMinHeight, out effectiveMaxHeight);
        }

        for (int i = 0; i < samplesPerFrame && particlesEmitted < maxParticlesPerFrame; i++)
        {
            totalSamples++;

            // ===== 1. 有效性檢查 =====
            if (cachedResults[i].isValid > 0.5f) // 0或1，但float浮動所以用0.5判斷
            {
                validDepthSamples++;
                validSamplesThisFrame++;

                // ===== 2. CPU 端高度過濾 (Hybrid 模式) =====
                // ✅ 始終使用 GPU 計算的世界座標
                Vector3 worldPos = cachedResults[i].worldPosition;
                float envDepth = cachedResults[i].depth;

                // 🆕 診斷：記錄高度統計
                minObservedY = Mathf.Min(minObservedY, worldPos.y);
                maxObservedY = Mathf.Max(maxObservedY, worldPos.y);

                if (worldPos.y < 0.2f)
                {
                    groundParticleCount++;
                }

                if (worldPos.y < minWorldHeight)
                {
                    belowMinHeightCount++;
                }
                // ===== 2. 🆕 底部深度過濾 (優先執行) =====
                if (enableBottomDepthCheck)
                {
                    // 檢查當前採樣點的 UV 座標
                    Vector2 currentUV = savedUVs[i];

                    // 判斷是否在底部區域 (UV.y < bottomRegionThreshold)
                    if (currentUV.y < bottomRegionThreshold)
                    {
                        // 底部區域：檢查深度
                        if (envDepth >= maxBottomDepth)
                        {
                            // 深度過大，根據機率決定是否過濾
                            if (bottomRegionProbabilityMultiplier <= 0.001f)
                            {
                                // 完全過濾
                                bottomDepthFilteredCount++;
                                continue;
                            }
                            else
                            {
                                // 降低機率
                                if (Random.value > bottomRegionProbabilityMultiplier)
                                {
                                    bottomDepthFilteredCount++;
                                    continue;
                                }
                            }
                        }
                    }
                }

                // ===== 3. CPU 端高度過濾 (Hybrid 模式) =====
                if (enableHeightFilter && heightFilterMode == HeightFilterMode.Hybrid)
                {
                    // 絕對高度過濾
                    if (worldPos.y < minWorldHeight || worldPos.y > maxWorldHeight)
                    {
                        heightFilteredCount++;
                        continue; // 絕對高度不符，直接跳過
                    }
                    
                    // 🆕 【修正】再檢查相對高度 (可選)
                    if (useRelativeHeight && mainCamera != null)
                    {
                        float relativeHeight = worldPos.y - mainCamera.transform.position.y;
                        if (relativeHeight < -maxRelativeHeight || relativeHeight > minRelativeHeight)
                        {
                            heightFilteredCount++;
                            continue; // 相對高度不符，跳過
                        }
                    }
                }

                // ===== 4. 深度範圍檢查 =====
                if (envDepth >= minDepth && envDepth <= maxDepth)
                {
                    depthInRangeSamples++;
                    float normalizedDepth = Mathf.InverseLerp(minDepth, maxDepth, envDepth);
                    
                    // ===== 5. 動態密度過濾 =====
                    float emissionProbability = useDepthBasedDensity 
                        ? densityCurve.Evaluate(normalizedDepth) * globalDensityMultiplier
                        : globalDensityMultiplier;
                    
                    if (Random.value < emissionProbability)
                    {
                        // ===== 6. 表面偏移 =====
                        if (clampToSurface)
                        {
                            Vector3 toCamera = (mainCamera.transform.position - worldPos).normalized;
                            worldPos += toCamera * surfaceOffset;
                        }

                        // ===== 7. 發射粒子 =====
                        EmitParticleAt(worldPos, normalizedDepth);
                        particlesEmitted++;
                        particlesEmittedTotal++;
                    }
                    else
                    {
                        densityFilteredCount++;
                    }
                }
            }
        }

        // ===== 8. 🆕 完整診斷輸出 =====
        if (enableDebugLogs && Time.time - lastDebugTime > 1f)
        {
            float validRate = totalSamples > 0 ? (validDepthSamples / (float)totalSamples) * 100f : 0f;
            LogDebug($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━", true);
            LogDebug($"[Stats] Valid: {validRate:F1}% | In Range: {depthInRangeSamples}/{validSamplesThisFrame} | Emitted: {particlesEmitted} | Active: {particleSystem.particleCount}", true);

            // 🆕 關鍵診斷：高度分佈
            if (validSamplesThisFrame > 0)
            {
                LogDebug($"🔍 [Y-Range] Min: {minObservedY:F3}m | Max: {maxObservedY:F3}m", true);
                LogDebug($"🔍 [Ground] Y<0.2m: {groundParticleCount}/{validSamplesThisFrame} ({groundParticleCount * 100f / validSamplesThisFrame:F1}%)", true);
                LogDebug($"🔍 [BelowMin] Y<{minWorldHeight:F2}m: {belowMinHeightCount}/{validSamplesThisFrame}", true);

                // 🆕 底部深度過濾統計
                if (enableBottomDepthCheck)
                {
                    LogDebug($"🔍 [Bottom Filter] Filtered: {bottomDepthFilteredCount}/{validSamplesThisFrame} (UV.y<{bottomRegionThreshold:F2}, depth>={maxBottomDepth:F1}m)", true);
                }

                // 🆕 致命錯誤檢測
                if (groundParticleCount > 0 && enableHeightFilter)
                {
                    LogDebug($"❌ CRITICAL: {groundParticleCount} particles with Y<0.2m passed GPU filter!", true);
                    LogDebug($"   This means GPU height filtering is NOT working!", true);
                    LogDebug($"   GPU Min/Max Height sent: {effectiveMinHeight:F2}m / {effectiveMaxHeight:F2}m", true);
                }

                if (enableHeightFilter)
                {
                    LogDebug($"🔧 [Filter] Mode: {heightFilterMode} | Effective Range: {effectiveMinHeight:F2}m ~ {effectiveMaxHeight:F2}m", true);
                    LogDebug($"   Absolute Range: {minWorldHeight:F2}m ~ {maxWorldHeight:F2}m", true);
                    LogDebug($"   Use Relative: {useRelativeHeight} | CPU Filtered: {heightFilteredCount}", true);

                    if (mainCamera != null)
                    {
                        LogDebug($"   Camera Y: {mainCamera.transform.position.y:F2}m", true);
                    }
                }
            }

            if (showDetailedDebug)
            {
                LogDebug($"  - Total emitted: {particlesEmittedTotal} | Depth range: {minDepth:F2}m~{maxDepth:F2}m", true);
                LogDebug($"  - Density filtered: {densityFilteredCount}", true);
            }

            LogDebug($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n", true);
            lastDebugTime = Time.time;
        }
    }

    // 🆕 發射粒子 (支援動態大小)
    private void EmitParticleAt(Vector3 worldPosition, float normalizedDepth)
    {
        Vector3 randomOffset = Vector3.zero;
        
        if (randomSpread > 0.001f)
        {
            randomOffset = Random.insideUnitSphere * randomSpread;
        }
        
        Vector3 position = worldPosition + randomOffset;
        Color color = particleColorGradient.Evaluate(normalizedDepth);
        
        // 🆕 動態大小計算 (近小遠大)
        float size = useDepthBasedSize 
            ? Mathf.Lerp(particleSizeMin, particleSizeMax, sizeCurve.Evaluate(normalizedDepth))
            : particleSizeMin;

        emitParams.position = position;
        emitParams.velocity = Vector3.zero;
        emitParams.startLifetime = particleLifetime;
        emitParams.startSize = size; // 🆕 動態大小
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

    // 🔧 修正：Gizmos 繪製大小
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !showGizmos || mainCamera == null || cachedResults == null)
            return;

        for (int i = 0; i < Mathf.Min(30, cachedResults.Length); i++)
        {
            if (cachedResults[i].isValid > 0.5f && cachedResults[i].depth <= maxDepth) // 🔄 使用 maxDepth
            {
                float normalizedDepth = Mathf.InverseLerp(minDepth, maxDepth, cachedResults[i].depth); // 🔄 使用 maxDepth
                Gizmos.color = new Color(1f - normalizedDepth, normalizedDepth, 0f, 0.8f);
                
                // 🔧 修正：使用動態計算的大小
                float gizmoSize = useDepthBasedSize 
                    ? Mathf.Lerp(particleSizeMin, particleSizeMax, sizeCurve != null ? sizeCurve.Evaluate(normalizedDepth) : 0f)
                    : particleSizeMin;
                
                Gizmos.DrawWireSphere(cachedResults[i].worldPosition, gizmoSize * 0.5f);
            }
        }
    }
    
    private void OnValidate()
    {
        if (minDepth > maxDepth)
        {
            minDepth = maxDepth * 0.5f;
        }
        
        if (samplesPerFrame % 64 != 0)
        {
            samplesPerFrame = Mathf.CeilToInt(samplesPerFrame / 64f) * 64;
        }
        
        if (Application.isPlaying && particleSystem != null)
        {
            var main = particleSystem.main;
            main.startLifetime = particleLifetime;
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

    // 🆕 初始化反射以存取 internal 欄位
    private void InitializeReflection()
    {
        try
        {
            LogDebug("Initializing reflection for internal fields...", showDetailedDebug);
            
            var managerType = typeof(EnvironmentDepthManager);
            frameDescriptorsField = managerType.GetField("frameDescriptors", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (frameDescriptorsField == null)
            {
                LogError("Reflection failed: frameDescriptors field not found!");
                reflectionFailed = true;
                return;
            }
            
            // 取得 DepthFrameDesc 的內部成員
            var descType = frameDescriptorsField.FieldType.GetElementType();
            if (descType == null)
            {
                LogError("Reflection failed: Cannot determine DepthFrameDesc type!");
                reflectionFailed = true;
                return;
            }
            
            fovLeftField = descType.GetField("fovLeftAngleTangent", BindingFlags.NonPublic | BindingFlags.Instance);
            fovRightField = descType.GetField("fovRightAngleTangent", BindingFlags.NonPublic | BindingFlags.Instance);
            fovTopField = descType.GetField("fovTopAngleTangent", BindingFlags.NonPublic | BindingFlags.Instance);
            fovDownField = descType.GetField("fovDownAngleTangent", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // 🆕 新增姿態欄位
            poseLocationField = descType.GetField("createPoseLocation", BindingFlags.NonPublic | BindingFlags.Instance);
            poseRotationField = descType.GetField("createPoseRotation", BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (fovLeftField == null || poseLocationField == null)
            {
                LogError("Reflection failed: DepthFrameDesc fields not found!");
                reflectionFailed = true;
                return;
            }
            
            LogDebug("✓ Reflection initialized successfully", true);
        }
        catch (System.Exception e)
        {
            LogError($"Reflection initialization error: {e.Message}");
            reflectionFailed = true;
        }
    }

    //// 🆕 安全取得深度相機 FOV
    //private bool TryGetDepthCameraData(out float left, out float right, out float top, out float down,
    //                                   out Vector3 poseLocation, out Quaternion poseRotation)
    //{
    //    left = right = top = down = 0f;
    //    poseLocation = Vector3.zero;
    //    poseRotation = Quaternion.identity;
        
    //    if (reflectionFailed || frameDescriptorsField == null || depthManager == null)
    //    {
    //        // 降級: 使用預設值
    //        left = Mathf.Tan(45f * Mathf.Deg2Rad);
    //        right = Mathf.Tan(45f * Mathf.Deg2Rad);
    //        top = Mathf.Tan(37.5f * Mathf.Deg2Rad);
    //        down = Mathf.Tan(37.5f * Mathf.Deg2Rad);
    //        return false;
    //    }

    //    try
    //    {
    //        var frameDescs = frameDescriptorsField.GetValue(depthManager) as System.Array;
    //        if (frameDescs == null || frameDescs.Length < 2)
    //        {
    //            LogError($"FrameDescriptors invalid: length={frameDescs?.Length ?? 0}");
    //            return false;
    //        }
            
    //        // 🔧 根據 eyeIndex 選擇對應的描述符
    //        int clampedEyeIndex = Mathf.Clamp(eyeIndex, 0, 1);
    //        object eyeDesc = frameDescs.GetValue(clampedEyeIndex);
            
    //        if (eyeDesc == null)
    //        {
    //            LogError($"FrameDescriptor[{clampedEyeIndex}] is null!");
    //            return false;
    //        }

    //        // 讀取該眼睛的 FOV
    //        left = (float)fovLeftField.GetValue(eyeDesc);
    //        right = (float)fovRightField.GetValue(eyeDesc);
    //        top = (float)fovTopField.GetValue(eyeDesc);
    //        down = (float)fovDownField.GetValue(eyeDesc);
            
    //        // 讀取該眼睛的深度拍攝姿態
    //        poseLocation = (Vector3)poseLocationField.GetValue(eyeDesc);
    //        poseRotation = (Quaternion)poseRotationField.GetValue(eyeDesc);
            
    //        // 🆕 Debug 輸出
    //        if (showDetailedDebug && Time.frameCount % 300 == 0)
    //        {
    //            LogDebug($"[Eye {clampedEyeIndex}] FOV: L={left:F3}, R={right:F3}, T={top:F3}, D={down:F3}", true);
    //            LogDebug($"[Eye {clampedEyeIndex}] Pose: Pos={poseLocation}, Rot={poseRotation.eulerAngles}", true);
    //        }
            
    //        return true;
    //    }
    //    catch (System.Exception e)
    //    {
    //        if (showDetailedDebug)
    //        {
    //            LogError($"Failed to get depth camera data: {e.Message}");
    //        }
    //        return false;
    //    }
    //}


    // 🔧 修改簽名,新增 eyeIndex 參數
    private bool TryGetDepthCameraData(
        int eyeIndex,  // 🆕 0=左眼, 1=右眼
        out float left, out float right, out float top, out float down,
        out Vector3 poseLocation, out Quaternion poseRotation)
    {
        left = right = top = down = 0f;
        poseLocation = Vector3.zero;
        poseRotation = Quaternion.identity;

        if (reflectionFailed || frameDescriptorsField == null || depthManager == null)
        {
            // 降級: 使用預設值
            left = Mathf.Tan(45f * Mathf.Deg2Rad);
            right = Mathf.Tan(45f * Mathf.Deg2Rad);
            top = Mathf.Tan(37.5f * Mathf.Deg2Rad);
            down = Mathf.Tan(37.5f * Mathf.Deg2Rad);
            return false;
        }

        try
        {
            var frameDescs = frameDescriptorsField.GetValue(depthManager) as System.Array;
            if (frameDescs == null || frameDescs.Length < 2)
            {
                LogError($"FrameDescriptors invalid: length={frameDescs?.Length ?? 0}");
                return false;
            }

            // 🔧 根據 eyeIndex 選擇對應的描述符
            int clampedEyeIndex = Mathf.Clamp(eyeIndex, 0, 1);
            object eyeDesc = frameDescs.GetValue(clampedEyeIndex);

            if (eyeDesc == null)
            {
                LogError($"FrameDescriptor[{clampedEyeIndex}] is null!");
                return false;
            }

            // 讀取該眼睛的 FOV
            left = (float)fovLeftField.GetValue(eyeDesc);
            right = (float)fovRightField.GetValue(eyeDesc);
            top = (float)fovTopField.GetValue(eyeDesc);
            down = (float)fovDownField.GetValue(eyeDesc);

            // 讀取該眼睛的深度拍攝姿態
            poseLocation = (Vector3)poseLocationField.GetValue(eyeDesc);
            poseRotation = (Quaternion)poseRotationField.GetValue(eyeDesc);

            // 🆕 Debug 輸出
            if (showDetailedDebug && Time.frameCount % 300 == 0)
            {
                LogDebug($"[Eye {clampedEyeIndex}] FOV: L={left:F3}, R={right:F3}, T={top:F3}, D={down:F3}", true);
                LogDebug($"[Eye {clampedEyeIndex}] Pose: Pos={poseLocation}, Rot={poseRotation.eulerAngles}", true);
            }

            return true;
        }
        catch (System.Exception e)
        {
            LogError($"Failed to get depth camera data: {e.Message}");
            return false;
        }
    }



    // 🔧 改进为方向性边缘检测
    private bool IsNearEdge(Vector2 uv)
    {
        // 計算到各邊界的距離
        float distToLeft = uv.x;
        float distToRight = 1f - uv.x;
        float distToBottom = uv.y;
        float distToTop = 1f - uv.y;
        
        // 🆕 分別檢查各方向
        // 左邊界檢查
        if (distToLeft < edgeThresholdHorizontal)
            return true;
        
        // 右邊界檢查
        if (distToRight < edgeThresholdHorizontal)
            return true;
        
        // 🆕 下邊界檢查 (使用更大的閾值)
        if (distToBottom < edgeThresholdBottom)
            return true;
        
        // 🆕 上邊界檢查 (使用較小的閾值)
        if (distToTop < edgeThresholdTop)
            return true;
        
        return false; // 安全區域
    }
}