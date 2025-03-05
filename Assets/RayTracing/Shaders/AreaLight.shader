Shader "RayTracing/AreaLight"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Emission("Emission Color", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Tags { "RenderType"="Opaque" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Emission;
            half4 _Intensity;
            float4 _BaseColor;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);

                return col * _Emission * _Intensity;
            }
            ENDCG
        }
    }

    SubShader
    {
        Pass
        {
            Name "RayTracing"

            Tags
            {
                "LightMode" = "RayTracing"
            }

            HLSLPROGRAM

            #pragma raytracing test
            #pragma enable_d3d11_debug_symbols
            #include "UnityCG.cginc"
            #include "DXR/TraceRay.hlsl"
            Texture2D _MainTex;
            float4 _MainTex_ST;
            CBUFFER_START(UnityPerMaterial)
            half4 _Emission;
            half4 _Intensity;
            CBUFFER_END

            [shader("closesthit")]
            void ClosestHitShader(inout PathPayload payLoad : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                payLoad.hitResult = HIT_LIGHT;
                payLoad.instanceID = InstanceID();
                //payLoad.primitiveID = PrimitiveIndex();
                payLoad.hitSurface = GetHitSurface(PrimitiveIndex(), -payLoad.direction, attributeData, ObjectToWorld3x4(), WorldToObject3x4());
                //payLoad.hitSurface.lightIndex = InstanceID();
    
                Material material = (Material) 0;
                material.kd = 0;
                payLoad.material = material;
            }

            [shader("anyhit")]
            void AnyHitShader(inout PathPayload payLoad : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                payLoad.instanceID = InstanceID();
                payLoad.hitResult = HIT_LIGHT;
            }

            ENDHLSL
        }
    }
}
