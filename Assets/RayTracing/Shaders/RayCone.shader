Shader "RayTracing/RayCone"
{
    Properties
    {
        //_MainTex ("Texture", 2D) = "white" {}
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

            //sampler2D _MainTex;
            //float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;//TRANSFORM_TEX(v.uv, _MainTex);
                o.posWorld = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normalWorld = UnityObjectToWorldNormal(v.normal);
                o.tangentWorld = normalize(mul(unity_ObjectToWorld, float4(v.tangent.xyz, 0.0)).xyz);
                o.bitangentWorld = normalize(cross(o.normalWorld, o.tangentWorld) * v.tangent.w);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float dpdx = ddx(i.posWorld);
                float dpdy = ddy(i.posWorld);
                float dndx = ddx(i.normalWorld);
                float dndy = ddy(i.normalWorld);
                float phi = abs(dndx + dndy);   //eq.31
                float chain = dpdx * dndx + dpdy * dndy;
                float s = sign(chain);
                float beta = 2 * s * sqrt(chain);   //eq.32
                return half4(beta, 0, 0, 0);
            }
            ENDCG
        }
    }
}
