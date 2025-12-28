Shader "Debug/DepthVisualization"
{
    Properties
    {
        _NearPlane ("Near Plane", Float) = 0.2
        _FarPlane ("Far Plane", Float) = 5.0
        _VisualizationMode ("Visualization Mode", Int) = 0
        _GradientTex ("Gradient Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay+1000" }
        LOD 100
        ZTest Always
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            // Global depth textures from EnvironmentDepthManager
            Texture2DArray_half _EnvironmentDepthTexture;
            SamplerState sampler_EnvironmentDepthTexture;
            Texture2DArray_half _PreprocessedEnvironmentDepthTexture;
            SamplerState sampler_PreprocessedEnvironmentDepthTexture;
            float4 _EnvironmentDepthZBufferParams;
            
            // Shader properties
            float _NearPlane;
            float _FarPlane;
            int _VisualizationMode;
            sampler2D _GradientTex;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            
            // Convert raw depth to linear depth (SIMPLIFIED - directly use raw depth as distance)
            float RawToLinearDepth(float rawDepth)
            {
                // The EnvironmentDepthTexture already contains linear depth values in meters
                // No complex conversion needed - just check validity
                
                // Check for invalid depth (raw depth of 1.0 or 0.0 usually means invalid)
                if (rawDepth >= 0.99 || rawDepth <= 0.01)
                {
                    return 1000.0; // Far plane / invalid
                }
                
                // Direct mapping: raw depth is already in meters
                // Typical range: 0.2m - 10m stored as normalized values
                // We need to scale it back to actual meters
                float linearDepth = rawDepth * 10.0; // Assume max depth is ~10m
                
                return linearDepth;
            }
            
            // Color mapping functions
            float3 GrayscaleMapping(float depth)
            {
                float normalized = saturate((depth - _NearPlane) / (_FarPlane - _NearPlane));
                normalized = 1.0 - normalized; // Invert: near = white, far = black
                return float3(normalized, normalized, normalized);
            }
            
            float3 HeatMapMapping(float depth)
            {
                float normalized = saturate((depth - _NearPlane) / (_FarPlane - _NearPlane));
                
                // Heat map: red -> yellow -> green -> cyan -> blue
                float3 color;
                if (normalized < 0.25)
                {
                    float t = normalized / 0.25;
                    color = lerp(float3(1, 0, 0), float3(1, 1, 0), t); // Red to Yellow
                }
                else if (normalized < 0.5)
                {
                    float t = (normalized - 0.25) / 0.25;
                    color = lerp(float3(1, 1, 0), float3(0, 1, 0), t); // Yellow to Green
                }
                else if (normalized < 0.75)
                {
                    float t = (normalized - 0.5) / 0.25;
                    color = lerp(float3(0, 1, 0), float3(0, 1, 1), t); // Green to Cyan
                }
                else
                {
                    float t = (normalized - 0.75) / 0.25;
                    color = lerp(float3(0, 1, 1), float3(0, 0, 1), t); // Cyan to Blue
                }
                
                return color;
            }
            
            float3 GradientMapping(float depth)
            {
                float normalized = saturate((depth - _NearPlane) / (_FarPlane - _NearPlane));
                return tex2D(_GradientTex, float2(normalized, 0.5)).rgb;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                
                // Use left eye only (index 0) for visualization
                uint eyeIndex = 0;
                float3 uv = float3(i.uv, eyeIndex);
                
                // Sample raw depth texture
                float rawDepth = _EnvironmentDepthTexture.Sample(sampler_EnvironmentDepthTexture, uv).r;
                
                // DEBUG: Show raw depth directly for troubleshooting
                // Uncomment to see raw values
                // return fixed4(rawDepth, rawDepth, rawDepth, 1);
                
                // Convert to linear depth
                float linearDepth = RawToLinearDepth(rawDepth);
                
                // Check for invalid depth
                if (linearDepth > 100.0)
                {
                    // Invalid depth - show magenta
                    return fixed4(1, 0, 1, 1);
                }
                
                // DEBUG: Show linear depth range
                float debugNormalized = saturate(linearDepth / 5.0);
                // return fixed4(debugNormalized, debugNormalized, debugNormalized, 1);
                
                // Choose visualization mode
                float3 color;
                
                if (_VisualizationMode == 0)
                {
                    // Color gradient
                    color = GradientMapping(linearDepth);
                }
                else if (_VisualizationMode == 1)
                {
                    // Grayscale
                    color = GrayscaleMapping(linearDepth);
                }
                else if (_VisualizationMode == 2)
                {
                    // Heat map
                    color = HeatMapMapping(linearDepth);
                }
                else
                {
                    // Raw depth (0-1)
                    float normalized = saturate((linearDepth - _NearPlane) / (_FarPlane - _NearPlane));
                    color = float3(normalized, normalized, normalized);
                }
                
                // DEBUG: Show depth value as color bands
                // float band = frac(linearDepth * 2.0);
                // if (band < 0.1) color = float3(1, 0, 0); // Red marker every 0.5m
                
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    
    FallBack "Unlit/Texture"
}
