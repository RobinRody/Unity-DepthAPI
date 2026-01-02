using UnityEngine;
using Meta.XR.EnvironmentDepth;
using System.Collections;

/// <summary>
/// Depth Texture Debugger - Visualizes the depth map as a color map
/// This script does not interfere with AdvancedDepthParticleEmitter
/// </summary>
public class DepthVisualizationDebugger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnvironmentDepthManager depthManager;
    [SerializeField] private Material depthVisualizationMaterial;
    
    [Header("Visualization Settings")]
    [SerializeField] private bool enableVisualization = true;
    [SerializeField] private VisualizationMode visualizationMode = VisualizationMode.ColorGradient;
    [SerializeField] private float nearPlane = 0.3f;
    [SerializeField] private float farPlane = 2.0f;
    [SerializeField] private bool autoSyncWithParticleEmitter = false; // Disable auto-sync for wider range
    
    [Header("Color Gradient")]
    [SerializeField] private Gradient depthGradient;
    
    [Header("Debug Quad Settings")]
    [SerializeField] private bool showDebugQuad = true;
    [SerializeField] private bool followCamera = true;
    [SerializeField] private Vector3 quadOffsetFromCamera = new Vector3(0.2f, 0.15f, 0.8f); // Right, Up, Forward
    [SerializeField] private Vector2 quadSize = new Vector2(0.2f, 0.15f); // Much smaller size for corner display
    [SerializeField] private bool rotateWithCamera = true;
    
    private GameObject debugQuad;
    private MeshRenderer quadRenderer;
    private Texture2D gradientTexture;
    private Camera mainCamera;
    private AdvancedDepthParticleEmitter particleEmitter;
    
    [Header("Debug Settings")]
    [SerializeField] private bool showRawDepthValues = true;
    [SerializeField] private Vector2 samplePoint = new Vector2(0.5f, 0.5f);
    
    private float lastSampledRawDepth = 0f;
    private float lastSampledLinearDepth = 0f;
    
    [Header("HMD Debug Controls")]
    [SerializeField] private OVRInput.Button debugLogButton = OVRInput.Button.One; // A 鈕
    [SerializeField] private bool enableHMDDebugControl = true;

    // Track if material was dynamically created
    private bool materialWasDynamicallyCreated = false;

    public enum VisualizationMode
    {
        ColorGradient,      // Near = Red, Far = Blue
        Grayscale,          // Near = White, Far = Black
        HeatMap,            // Near = Red, Mid = Yellow, Far = Blue
        RawDepth            // Raw depth values (0-1)
    }
    
    private void Awake()
    {
        mainCamera = Camera.main;
        
        if (depthManager == null)
        {
            depthManager = FindAnyObjectByType<EnvironmentDepthManager>();
        }
        
        // Try to find and sync with AdvancedDepthParticleEmitter
        particleEmitter = FindAnyObjectByType<AdvancedDepthParticleEmitter>();
        if (particleEmitter != null && autoSyncWithParticleEmitter)
        {
            // Sync depth range settings
            SyncWithParticleEmitter();
            Debug.Log("[DepthVisualizer] Auto-sync enabled, synced with AdvancedDepthParticleEmitter");
        }
        else if (particleEmitter != null)
        {
            Debug.Log("[DepthVisualizer] Found AdvancedDepthParticleEmitter but auto-sync is disabled");
        }
        
        InitializeDefaultGradient();
        CreateGradientTexture();
        
        if (showDebugQuad)
        {
            CreateDebugQuad();
        }
    }
    
    private void InitializeDefaultGradient()
    {
        if (depthGradient == null || depthGradient.colorKeys.Length == 0)
        {
            depthGradient = new Gradient();
            var colors = new GradientColorKey[5];
            colors[0] = new GradientColorKey(Color.red, 0f);        // Very close
            colors[1] = new GradientColorKey(Color.yellow, 0.25f);  // Close
            colors[2] = new GradientColorKey(Color.green, 0.5f);    // Medium
            colors[3] = new GradientColorKey(Color.cyan, 0.75f);    // Far
            colors[4] = new GradientColorKey(Color.blue, 1f);       // Very far
            
            var alphas = new GradientAlphaKey[2];
            alphas[0] = new GradientAlphaKey(1f, 0f);
            alphas[1] = new GradientAlphaKey(1f, 1f);
            
            depthGradient.SetKeys(colors, alphas);
        }
    }
    
    private void CreateGradientTexture()
    {
        gradientTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
        gradientTexture.wrapMode = TextureWrapMode.Clamp;
        
        for (int i = 0; i < 256; i++)
        {
            float t = i / 255f;
            Color color = depthGradient.Evaluate(t);
            gradientTexture.SetPixel(i, 0, color);
        }
        
        gradientTexture.Apply();
    }
    
    private void CreateDebugQuad()
    {
        debugQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        debugQuad.name = "DepthVisualizationQuad";
        debugQuad.transform.localScale = new Vector3(quadSize.x, quadSize.y, 1);
        
        // Destroy collider
        var collider = debugQuad.GetComponent<Collider>();
        if (collider != null)
        {
            if (Application.isPlaying)
            {
                Destroy(collider); // Runtime: 延遲刪除,安全
            }
            else
            {
                DestroyImmediate(collider); // Editor: 立即刪除,必要
            }
        }
        
        quadRenderer = debugQuad.GetComponent<MeshRenderer>();
        
        if (depthVisualizationMaterial != null)
        {
            quadRenderer.material = depthVisualizationMaterial;
            materialWasDynamicallyCreated = false;
        }
        else
        {
            Shader shader = Shader.Find("Debug/DepthVisualization");
            if (shader != null)
            {
                depthVisualizationMaterial = new Material(shader);
                quadRenderer.material = depthVisualizationMaterial;
                materialWasDynamicallyCreated = true; // 🔧 FIX: Mark as dynamically created
            }
        }
        
        UpdateQuadPosition();
        debugQuad.SetActive(enableVisualization && showDebugQuad);
    }
    
    private void Start()
    {
        if (depthManager != null)
        {
            StartCoroutine(WaitForDepthAndVisualize());
        }
        else
        {
            Debug.LogError("[DepthVisualizer] EnvironmentDepthManager not found!");
        }
    }
    
    private IEnumerator WaitForDepthAndVisualize()
    {
        Debug.Log("[DepthVisualizer] Waiting for depth texture...");
        
        while (!depthManager.IsDepthAvailable)
        {
            yield return null;
        }
        
        Debug.Log("[DepthVisualizer] Depth texture available! Starting visualization.");
        
        // Set visualization mode
        UpdateVisualizationMode();
    }
    
    private void Update()
    {
        if (!enableVisualization || depthManager == null || !depthManager.IsDepthAvailable)
            return;
        
        // Update quad position to follow camera
        if (followCamera && debugQuad != null && mainCamera != null)
        {
            UpdateQuadPosition();
        }
        
        // Sync with particle emitter settings (only if auto-sync is enabled)
        if (autoSyncWithParticleEmitter && particleEmitter != null)
        {
            SyncWithParticleEmitter();
        }
        
        // Update material parameters
        if (depthVisualizationMaterial != null)
        {
            depthVisualizationMaterial.SetFloat("_NearPlane", nearPlane);
            depthVisualizationMaterial.SetFloat("_FarPlane", farPlane);
            
            if (gradientTexture != null)
            {
                depthVisualizationMaterial.SetTexture("_GradientTex", gradientTexture);
            }
        }
        
        // 🔧 FIX: Use OVR Input instead of legacy Input
        bool shouldLogDepthInfo = false;
        
        if (enableHMDDebugControl)
        {
            // A 鈕 (右手) 或 X 鈕 (左手)
            shouldLogDepthInfo = OVRInput.GetDown(debugLogButton, OVRInput.Controller.RTouch) ||
                                  OVRInput.GetDown(debugLogButton, OVRInput.Controller.LTouch);
        }
        
        if (shouldLogDepthInfo)
        {
            LogDepthInfo();
        }
        
        // Sample depth at center point for debugging
        if (showRawDepthValues)
        {
            SampleDepthAtPoint();
        }
    }
    
    private void SampleDepthAtPoint()
    {
        var depthTexture = Shader.GetGlobalTexture("_EnvironmentDepthTexture") as RenderTexture;
        if (depthTexture == null)
            return;
        
        // This is a simplified version - actual sampling would need compute shader
        // For now we just show that the feature exists
        // The actual depth values will be shown in the LogDepthInfo
    }
    
    private void UpdateQuadPosition()
    {
        if (debugQuad == null || mainCamera == null)
            return;
        
        // Calculate position relative to camera
        Vector3 forward = mainCamera.transform.forward;
        Vector3 right = mainCamera.transform.right;
        Vector3 up = mainCamera.transform.up;
        
        Vector3 targetPosition = mainCamera.transform.position +
            forward * quadOffsetFromCamera.z +
            right * quadOffsetFromCamera.x +
            up * quadOffsetFromCamera.y;
        
        debugQuad.transform.position = targetPosition;
        
        // Rotate to face camera
        if (rotateWithCamera)
        {
            debugQuad.transform.rotation = mainCamera.transform.rotation;
        }
    }
    
    private void SyncWithParticleEmitter()
    {
        if (particleEmitter == null)
            return;
        
        // Sync depth range from particle emitter
        nearPlane = particleEmitter.MinDepth;
        farPlane = particleEmitter.MaxDepth;
    }
    
    private void UpdateVisualizationMode()
    {
        if (depthVisualizationMaterial == null)
            return;
        
        switch (visualizationMode)
        {
            case VisualizationMode.ColorGradient:
                depthVisualizationMaterial.SetInt("_VisualizationMode", 0);
                break;
            case VisualizationMode.Grayscale:
                depthVisualizationMaterial.SetInt("_VisualizationMode", 1);
                break;
            case VisualizationMode.HeatMap:
                depthVisualizationMaterial.SetInt("_VisualizationMode", 2);
                break;
            case VisualizationMode.RawDepth:
                depthVisualizationMaterial.SetInt("_VisualizationMode", 3);
                break;
        }
    }
    
    private void LogDepthInfo()
    {
        var depthTexture = Shader.GetGlobalTexture("_EnvironmentDepthTexture");
        var preprocessedDepthTexture = Shader.GetGlobalTexture("_PreprocessedEnvironmentDepthTexture");
        var reprojMatrices = Shader.GetGlobalMatrixArray("_EnvironmentDepthReprojectionMatrices");
        var zBufferParams = Shader.GetGlobalVector("_EnvironmentDepthZBufferParams");
        
        Debug.Log("=== DEPTH TEXTURE DEBUG INFO ===");
        
        if (depthTexture != null)
        {
            Debug.Log($"Depth Texture: {depthTexture.width}x{depthTexture.height}, Dimension: {depthTexture.dimension}");
            Debug.Log($"Texture Format: {depthTexture.graphicsFormat}");
        }
        else
        {
            Debug.LogWarning("_EnvironmentDepthTexture is NULL!");
        }
        
        if (preprocessedDepthTexture != null)
        {
            Debug.Log($"Preprocessed Depth Texture: {preprocessedDepthTexture.width}x{preprocessedDepthTexture.height}");
        }
        else
        {
            Debug.LogWarning("_PreprocessedEnvironmentDepthTexture is NULL!");
        }
        
        if (reprojMatrices != null && reprojMatrices.Length >= 2)
        {
            Debug.Log($"Reprojection Matrices: Available ({reprojMatrices.Length})");
        }
        else
        {
            Debug.LogWarning("Reprojection Matrices: NOT AVAILABLE!");
        }
        
        Debug.Log($"ZBuffer Params: {zBufferParams}");
        Debug.Log($"Current Visualization Range: {nearPlane}m - {farPlane}m");
        
        if (particleEmitter != null)
        {
            Debug.Log($"Particle Emitter Range: {particleEmitter.MinDepth}m - {particleEmitter.MaxDepth}m");
        }
        
        Debug.Log("=== TROUBLESHOOTING ===");
        Debug.Log("If you see MAGENTA:");
        Debug.Log("  - Raw depth >= 0.99 or <= 0.01 (invalid)");
        Debug.Log("  - Linear depth > 100m (too far)");
        Debug.Log("If you see BLACK:");
        Debug.Log("  - Depth outside visualization range");
        Debug.Log("If colors don't change:");
        Debug.Log("  - Check if depth texture is updating");
        Debug.Log("  - Try moving hand closer/further");
        Debug.Log("========================");
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateVisualizationMode();
            
            if (gradientTexture != null)
            {
                CreateGradientTexture();
            }
            
            if (debugQuad != null)
            {
                debugQuad.SetActive(enableVisualization && showDebugQuad);
                debugQuad.transform.localScale = new Vector3(quadSize.x, quadSize.y, 1);
                
                if (followCamera && mainCamera != null)
                {
                    UpdateQuadPosition();
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        // 🔧 FIX: Safe cleanup - only destroy runtime objects
        if (debugQuad != null)
        {
            if (Application.isPlaying)
            {
                Destroy(debugQuad);
            }
            else
            {
                DestroyImmediate(debugQuad);
            }
        }
        
        if (gradientTexture != null)
        {
            if (Application.isPlaying)
            {
                Destroy(gradientTexture);
            }
            else
            {
                DestroyImmediate(gradientTexture);
            }
        }
        
        // 🔧 FIX: Only destroy material if it was created at runtime
        if (depthVisualizationMaterial != null && materialWasDynamicallyCreated)
        {
            if (Application.isPlaying)
            {
                Destroy(depthVisualizationMaterial);
            }
            else
            {
                DestroyImmediate(depthVisualizationMaterial);
            }
        }
    }
    
    private void OnGUI()
    {
        if (!enableVisualization)
            return;
        
        GUILayout.BeginArea(new Rect(10, 10, 450, 320));
        GUILayout.Label("=== DEPTH VISUALIZATION DEBUG ===");
        GUILayout.Label($"Depth Available: {(depthManager != null && depthManager.IsDepthAvailable ? "YES" : "NO")}");
        GUILayout.Label($"Mode: {visualizationMode}");
        GUILayout.Label($"Display Range: {nearPlane:F2}m - {farPlane:F2}m");
        
        if (particleEmitter != null)
        {
            GUILayout.Label($"Found Particle Emitter: YES");
            GUILayout.Label($"  - Particle Range: {particleEmitter.MinDepth:F2}m - {particleEmitter.MaxDepth:F2}m");
            GUILayout.Label($"Auto-Sync: {(autoSyncWithParticleEmitter ? "ON" : "OFF")}");
        }
        else
        {
            GUILayout.Label($"Found Particle Emitter: NO");
        }
        
        GUILayout.Label($"Quad Follow Camera: {(followCamera ? "YES" : "NO")}");
        
        GUILayout.Label("---");
        GUILayout.Label("Expected Colors:");
        GUILayout.Label("  0.3m: RED,   0.5m: ORANGE");
        GUILayout.Label("  0.7m: YELLOW, 1.0m: GREEN");
        GUILayout.Label("  1.5m: CYAN,   2.0m: BLUE");
        
        GUILayout.Label("---");
        GUILayout.Label("Troubleshooting:");
        GUILayout.Label("  - Only MAGENTA? Depth invalid");
        GUILayout.Label("  - Only BLACK? Out of range");
        GUILayout.Label("  - Only WHITE? Check shader");
        
        GUILayout.Label("---");
        GUILayout.Label("Press A/X button to log debug info");
        GUILayout.EndArea();
    }
}
