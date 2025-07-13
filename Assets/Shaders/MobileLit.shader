Shader "Mobile/Lit"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "MobileRenderPipeline"
            "LightMode" = "UniversalForward"
        }
        
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "UnityCG.cginc"
            
            // Lighting properties set by the render pipeline
            float4 _MainLightDirection;
            float4 _MainLightColor;
            float4 _AmbientLight;
            int _LightCount;
            float4 _LightColors[8];
            float4 _LightDirection[8];
            float4 _LightPosition[8];
            
            // Material properties
            sampler2D _BaseMap;
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float _Metallic;
            float _Smoothness;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                
                return o;
            }
            
            float3 CalculateLighting(float3 worldPos, float3 worldNormal, float3 albedo)
            {
                float3 finalColor = _AmbientLight.rgb * albedo;
                
                // Main directional light
                float3 lightDir = normalize(_MainLightDirection.xyz);
                float NdotL = max(0, dot(worldNormal, lightDir));
                finalColor += _MainLightColor.rgb * albedo * NdotL;
                
                // Additional lights
                for (int i = 0; i < _LightCount && i < 8; i++)
                {
                    float3 lightColor = _LightColors[i].rgb;
                    float3 lightDirection = _LightDirection[i].xyz;
                    float4 lightPosition = _LightPosition[i];
                    
                    float3 toLight;
                    float attenuation = 1.0;
                    
                    // Check if it's a directional light (w = 0) or point/spot light (w > 0)
                    if (lightPosition.w == 0.0)
                    {
                        // Directional light
                        toLight = normalize(lightDirection);
                    }
                    else
                    {
                        // Point/Spot light
                        toLight = lightPosition.xyz - worldPos;
                        float distance = length(toLight);
                        toLight = normalize(toLight);
                        
                        // Simple distance attenuation
                        attenuation = 1.0 / (1.0 + lightPosition.w * distance * distance);
                    }
                    
                    float lightNdotL = max(0, dot(worldNormal, toLight));
                    finalColor += lightColor * albedo * lightNdotL * attenuation;
                }
                
                return finalColor;
            }
            
            float4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Sample base texture
                float4 baseMap = tex2D(_BaseMap, i.uv);
                float3 albedo = baseMap.rgb * _BaseColor.rgb;
                
                // Calculate lighting
                float3 finalColor = CalculateLighting(i.worldPos, normalize(i.worldNormal), albedo);
                
                return float4(finalColor, baseMap.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
    
    Fallback "Mobile/Unlit"
} 