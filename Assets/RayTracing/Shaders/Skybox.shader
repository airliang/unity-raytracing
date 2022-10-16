Shader "RayTracing/SkyboxHDR"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Gamma] _Exposure("Exposure", Range(0, 8)) = 1.0
        _Rotation("Rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            //#pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            half4 _MainTex_HDR;
            half _Exposure;
            half _Rotation;
            
            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            inline float2 DirectionToPolar(float3 direction)
            {
                float3 normalizedCoords = normalize(direction);
                float latitude = acos(normalizedCoords.y);
                float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5 / UNITY_PI, 1.0 / UNITY_PI);
                return float2(0.5, 1.0) - sphereCoords;
            }

            v2f vert (appdata v)
            {
                v2f o;
                float3 rotated = RotateAroundYInDegrees(v.vertex, _Rotation);
                o.vertex = UnityObjectToClipPos(rotated);
                o.uv = v.vertex.xyz;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float2 uv = DirectionToPolar(i.uv);
                half4 col = tex2D(_MainTex, uv);
                half3 c = DecodeHDR(col, _MainTex_HDR);
                c *= _Exposure;
                return half4(c, 1);
            }
            ENDCG
        }
    }
}
