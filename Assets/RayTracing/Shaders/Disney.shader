Shader "RayTracing/Disney"
{
    Properties
    {
        [MainTexture] _MainTex("Albedo", 2D) = "white" {}
        [MainColor]   _BaseColor("Color", Color) = (1, 1, 1, 1)
        _NormalTex("NormalMap", 2D) = "bump" {}
        _metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _specular("Specular", Range(0.0, 1.0)) = 0.0
        _roughness("Roughness", Range(0.0, 1.0)) = 0
        _specularTint("SpecularTint", Range(0.0, 1.0)) = 0
        _anisotropy("Anisotropy", Range(0.0, 1.0)) = 0
        _sheen("Sheen", Range(0.0, 1.0)) = 0
        _sheenTint("SheenTint", Range(0.0, 1.0)) = 0
        _clearcoat("Clearcoat", Range(0.0, 1.0)) = 0
        _clearcoatGloss("ClearcoatGloss", Range(0.0, 1.0)) = 0
        _ior("IOR", Range(0.0, 2.0)) = 1.0
        _specularTransmission("SpecularTransmission", Range(0.0, 1.0)) = 0
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0
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
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NormalTex;
            float4 _NormalTex_ST;
            half4  _BaseColor;
            float  _metallic;
            float  _specular;
            float  _roughness;
            float  _specularTint;
            float  _anisotropy;
            float  _sheen;
            float  _sheenTint;
            float  _clearcoat;
            float  _clearcoatGloss;
            float  _ior;
            float  _specularTransmission;
            half _Cutoff;

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


            half4 frag(v2f i) : SV_Target
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
                half diffuseTerm = DisneyDiffuse(nv, nl, lh, _roughness) * nl;
                half3 color = baseColor * (UNITY_LIGHTMODEL_AMBIENT + _LightColor0.rgb * diffuseTerm);
                return half4(color, 1);
            }
            ENDCG
        }
    }
}
