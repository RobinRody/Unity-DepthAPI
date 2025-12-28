Shader "Debug/DepthVisualizationFixed"
{
    Properties
    {
        _NearPlane ("Near Plane", Float) = 0.3
        _FarPlane ("Far Plane", Float) = 2.0
        _VisualizationMode ("Visualization Mode", Int) = 0
        _GradientTex ("Gradient Texture", 2D) = "white" {}
        [Toggle] _UsePreprocessed ("Use Preprocessed Texture", Float) = 1
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
            
            Texture2DArray_half _EnvironmentDepthTexture;
            SamplerState sampler_EnvironmentDepthTexture;
            Texture2DArray_half _PreprocessedEnvironmentDepthTexture;
            SamplerState sampler_PreprocessedEnvironmentDepthTexture;
            float4 _EnvironmentDepthZBufferParams;
            
            float _NearPlane;
            float _FarPlane;
            int _VisualizationMode;
            sampler2D _GradientTex;
            float _UsePreprocessed;
            
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
            
            float3 GrayscaleMapping(float depth)
            {
                float normalized = saturate((depth - _NearPlane) / (_FarPlane - _NearPlane));
                normalized = 1.0 - normalized;
                return float3(normalized, normalized, normalized);
            }
            
            float3 HeatMapMapping(float depth)
            {
                float normalized = saturate((depth - _NearPlane) / (_FarPlane - _NearPlane));
                
                float3 color;
                if (normalized < 0.25)
                {
                    float t = normalized / 0.25;
                    color = lerp(float3(1, 0, 0), float3(1, 1, 0), t);
                }
                else if (normalized < 0.5)
                {
                    float t = (normalized - 0.25) / 0.25;
                    color = lerp(float3(1, 1, 0), float3(0, 1, 0), t);
                }
                else if (normalized < 0.75)
                {
                    float t = (normalized - 0.5) / 0.25;
                    color = lerp(float3(0, 1, 0), float3(0, 1, 1), t);
                }
                else
                {
                    float t = (normalized - 0.75) / 0.25;
                    color = lerp(float3(0, 1, 1), float3(0, 0, 1), t);
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
                
                uint eyeIndex = 0;
                float3 uv = float3(i.uv, eyeIndex);
                
                float depth;
                
                if (_UsePreprocessed > 0.5)
                {
                    // Use preprocessed texture (recommended)
                    fixed4 preprocessed = _PreprocessedEnvironmentDepthTexture.Sample(
                        sampler_PreprocessedEnvironmentDepthTexture, uv);
                    
                    // The preprocessed texture contains depth in a more usable format
                    // Usually normalized to [0,1] or already in meters
                    depth = preprocessed.r;
                    
                    // If depth looks normalized (0-1), scale it
                    // Try different scales to see which works:
                    // depth *= 5.0;  // If max depth is ~5m
                    // depth *= 10.0; // If max depth is ~10m
                    
                    // For now, assume it's already in reasonable units
                    // and just check if it needs scaling based on visual results
                }
                else
                {
                    // Use raw texture (may need more processing)
                    float rawDepth = _EnvironmentDepthTexture.Sample(
                        sampler_EnvironmentDepthTexture, uv).r;
                    
                    // Simple scaling (you may need to adjust this)
                    depth = rawDepth * 5.0;
                }
                
                // Check for invalid depth
                if (depth <= 0.001 || depth > 50.0)
                {
                    return fixed4(1, 0, 1, 1); // Magenta for invalid
                }
                
                // Choose visualization mode
                float3 color;
                
                if (_VisualizationMode == 0)
                {
                    color = GradientMapping(depth);
                }
                else if (_VisualizationMode == 1)
                {
                    color = GrayscaleMapping(depth);
                }
                else if (_VisualizationMode == 2)
                {
                    color = HeatMapMapping(depth);
                }
                else
                {
                    // Debug mode: show raw depth value
                    float normalized = saturate(depth / 5.0);
                    color = float3(normalized, normalized, normalized);
                }
                
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
    
    FallBack "Unlit/Texture"
}
