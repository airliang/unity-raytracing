Shader "RayTracing/Uber"
{
    Properties
    {
        _MainTex ("Albedo", 2D) = "white" {}
        //[HideInInspector]_MainTex_ST("Texture", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        [Toggle] _UseLinearBaseColor("UseLinearBaseColor", float) = 0
        _BaseColorLinear("Color", Vector) = (1, 1, 1, 1)
        _NormalTex("NormalMap", 2D) = "bump" {}
        _GlossySpecularTex("Glossy Specular Texture", 2D) = "white" {}
        [Linear]_GlossySpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        [KeywordEnum(Matte, Plastic, Metal, Glass, Mirror)] _MaterialType("Material Type(Matte, Plastic, Metal, Glass, Mirror)", float) = 0
        _roughnessU("RoughnessU", Range(0.0, 1.0)) = 0
        _roughnessV("RoughnessU", Range(0.0, 1.0)) = 0
        _eta("Eta", Vector) = (1, 1, 1, 1)
        _k("Metal Absorption", Vector) = (1, 1, 1, 1)
            _t("Transmission", Color) = (1, 1, 1, 1)
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0
        [KeywordEnum(Dielectric, Conductor, Schlick, NoOp)] _FresnelType("Fresnel Type(Dielectric, Conductor, Schlick, NoOp)", int) = 0
        [HideInEditor]_MetalType("Metals", int) = 0
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
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float3 posWorld :TEXCOORD1;
                float3 normalWorld : TEXCOORD2;
                float3 tangentWorld : TEXCOORD3;
                float3 bitangentWorld : TEXCOORD4;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NormalTex;
            float4 _NormalTex_ST;
            
            half4  _BaseColor;
            half4  _BaseColorLinear;
            float _UseLinearBaseColor;
            sampler2D _GlossySpecularTex;
            half4 _GlossySpecularColor;
            int _MaterialType;
            float _roughnessU;
            float _roughnessV;
            float3 _eta;
            float3 _k;   //metal absorption
            float3 _t;   //transmission
            half _Cutoff;
            int _FresnelType;
            int _MetalType;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWorld = UnityObjectToWorldNormal(v.normal);
                o.tangentWorld = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0.0)).xyz);
                o.bitangentWorld = normalize(cross(o.normalWorld, o.tangentWorld) * v.tangent.w);
                //UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // sample the texture
                half4 baseColor = _BaseColor * tex2D(_MainTex, i.uv);
                clip(baseColor.a - _Cutoff);
                float3 posWorld = i.posWorld.xyz;
                float3 normal = normalize(i.normalWorld);
                float3 tangent = i.tangentWorld.xyz;
                float3 binormal = i.bitangentWorld.xyz;
                //half3 normal = i.normalDir.xyz;
                float3 normaltex = UnpackNormal(tex2D(_NormalTex, TRANSFORM_TEX(i.uv, _NormalTex)));
                normal = normalize(tangent * normaltex.x + binormal * normaltex.y + normal * normaltex.z);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float nl = max(dot(normal, lightDir), 0);
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - posWorld);
                float3 halfDir = normalize(lightDir + viewDir);
                float nh = saturate(dot(normal, halfDir));
                float nv = saturate(dot(normal, viewDir));
                half lv = saturate(dot(lightDir, viewDir));
                half lh = saturate(dot(lightDir, halfDir));

                // Diffuse term
                half diffuseTerm = DisneyDiffuse(nv, nl, lh, _roughnessU) * nl;
                half3 color = baseColor * (UNITY_LIGHTMODEL_AMBIENT + _LightColor0.rgb * diffuseTerm);
                return half4(color, 1);
            }
            ENDCG
        }
    }

    SubShader
    {
        Pass
        {
            Name"RayTracing"
            Tags
            {
                "LightMode" = "RayTracing"
            }

            HLSLPROGRAM

            #pragma raytracing test
            #pragma enable_d3d11_debug_symbols
            #include "UnityCG.cginc"
            #include "DXR/TraceRay.hlsl"
            #include "DXR/sampleAreaLights.hlsl"
            #include "DXR/materials.hlsl"
            Texture2D _MainTex;
            float4 _MainTex_ST;
            Texture2D _NormalTex;
            float4 _NormalTex_ST;
            
            half4 _BaseColor;
            half4 _BaseColorLinear;
            float _UseLinearBaseColor;
            Texture2D _GlossySpecularTex;
            half4 _GlossySpecularColor;
            int _MaterialType;
            float _roughnessU;
            float _roughnessV;
            float3 _eta;
            float3 _k; //metal absorption
            float3 _t; //transmission
            half _Cutoff;
            int _FresnelType;
            int _MetalType;

            [shader("closesthit")]
            void ClosestHitShader(inout PathPayload payLoad : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                uint primIndex = PrimitiveIndex();
                float hitT = RayTCurrent();
                float3 direction = WorldRayDirection();
                float3 hitPos = WorldRayOrigin() + direction * hitT;

                Interaction intersect = GetHitInteraction(primIndex, attributeData.barycentrics, WorldRayDirection(), hitPos,
                    ObjectToWorld3x4(), WorldToObject3x4());
                //payLoad.hitSurface = GetHitSurface(primIndex, attributeData.barycentrics, ObjectToWorld3x4(), WorldToObject3x4());
    
                TextureSampleInfo texLod;
                texLod.cosine = dot(intersect.wo, intersect.normal);
    
                texLod.coneWidth = payLoad.rayCone.width == 0 ? _CameraConeSpreadAngle * hitT : payLoad.rayCone.width;
                texLod.screenSpaceArea = intersect.screenSpaceArea;
                texLod.uvArea = intersect.uvArea;
                texLod.uv = intersect.uv;
                float mipmap = ComputeTextureLOD(texLod);
                float4 texColor = _MainTex.SampleLevel(s_linear_repeat_sampler, TRANSFORM_TEX(intersect.uv, _MainTex), mipmap);
    
                payLoad.hitSurface = ConvertFromInteraction(intersect);
                //payLoad.hitSurface.hitT = hitT;
    
                Material material = (Material) 0;
                material.materialType = _MaterialType;
                material.kd = _BaseColor.rgb * texColor.rgb;
                material.ks = _GlossySpecularColor.rgb;
                material.fresnelType = _FresnelType;
                material.k = _k;
                material.transmission = _t;
                material.eta = _eta;
                material.roughness = _roughnessU;
                material.anisotropy = _roughnessV;
                //payLoad.primitiveID = primIndex;
                payLoad.hitResult = HIT_MESH;

                _Materials[payLoad.threadID] = material;
                payLoad.instanceID = InstanceID();

            }

            [shader("anyhit")]
            void AnyHitShader(inout PathPayload payLoad : SV_RayPayload, AttributeData attributeData : SV_IntersectionAttributes)
            {
                payLoad.hitResult = HIT_MESH;
            }

            ENDHLSL
        }
    }

    CustomEditor "BSDFShaderGUI"
}
