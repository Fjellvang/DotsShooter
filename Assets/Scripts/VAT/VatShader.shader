Shader "Custom/VAT_Shader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _IdlePositionMap ("Idle Position Map", 2D) = "black" {}
        _IdleNormalMap ("Idle Normal Map", 2D) = "black" {}
        _RunPositionMap ("Run Position Map", 2D) = "black" {}
        _RunNormalMap ("Run Normal Map", 2D) = "black" {}
        
        _IdleFrameCount ("Idle Frame Count", Float) = 30
        _RunFrameCount ("Run Frame Count", Float) = 30
        
        _TextureWidth ("Texture Width", Float) = 512
        _TextureHeight ("Texture Height", Float) = 512
        
//        _Time ("Time", Float) = 0
        _AnimationSpeed ("Animation Speed", Range(0, 2)) = 1
        _BlendFactor ("Blend Factor", Range(0, 1)) = 0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Need to maintain this for instancing
            };
            
            sampler2D _MainTex;
            sampler2D _IdlePositionMap;
            sampler2D _IdleNormalMap;
            sampler2D _RunPositionMap;
            sampler2D _RunNormalMap;
            
            float _IdleFrameCount;
            float _RunFrameCount;
            float _TextureWidth;
            float _TextureHeight;
            float _AnimationSpeed;
            float _BlendFactor;
            
            // UNITY_INSTANCING_BUFFER_START(Props)
            //     UNITY_DEFINE_INSTANCED_PROP(float, _Time)
            // UNITY_INSTANCING_BUFFER_END(Props)
            
            float2 VertexIDToUV(uint vertexID, float frameIndex, float frameCount, float textureWidth, float textureHeight)
            {
                // Calculate which pixel in the texture we need based on vertex ID
                uint pixelIndex = vertexID;
                
                // Calculate x,y position in the texture
                float x = (pixelIndex % (uint)textureWidth);
                float y = (pixelIndex / (uint)textureWidth) + (frameIndex * textureHeight);
                
                // Convert to UVs
                float2 uv = float2(
                    (x + 0.5) / textureWidth,
                    (y + 0.5) / (textureHeight * frameCount)
                );
                
                return uv;
            }
            
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                
                float time = UNITY_ACCESS_INSTANCED_PROP(Props, _Time) * _AnimationSpeed;
                
                // Calculate current frame for each animation
                float idleFrameIndex = fmod(time, _IdleFrameCount);
                float runFrameIndex = fmod(time, _RunFrameCount);
                
                // Get UVs for sampling the texture
                float2 idleUV = VertexIDToUV(v.vertexID, idleFrameIndex, _IdleFrameCount, _TextureWidth, _TextureHeight);
                float2 runUV = VertexIDToUV(v.vertexID, runFrameIndex, _RunFrameCount, _TextureWidth, _TextureHeight);
                
                // Sample position and normal maps for both animations
                float4 idlePosition = tex2Dlod(_IdlePositionMap, float4(idleUV, 0, 0));
                float4 idleNormal = tex2Dlod(_IdleNormalMap, float4(idleUV, 0, 0));
                
                float4 runPosition = tex2Dlod(_RunPositionMap, float4(runUV, 0, 0));
                float4 runNormal = tex2Dlod(_RunNormalMap, float4(runUV, 0, 0));
                
                // Blend between the two animations
                float3 blendedPosition = lerp(idlePosition.xyz, runPosition.xyz, _BlendFactor);
                float3 blendedNormal = normalize(lerp(idleNormal.xyz, runNormal.xyz, _BlendFactor));
                
                // Use the blended position directly as the vertex position
                o.vertex = UnityObjectToClipPos(float4(blendedPosition, 1.0));
                o.normal = UnityObjectToWorldNormal(blendedNormal);
                o.uv = v.uv;
                
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                
                // Basic lighting
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = max(0, dot(i.normal, lightDir));
                fixed4 col = tex2D(_MainTex, i.uv) * ndotl;
                
                return col;
            }
            ENDCG
        }
    }
}