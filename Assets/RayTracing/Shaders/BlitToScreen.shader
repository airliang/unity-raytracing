// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "RayTracing/Blit" {

	Properties{
		_MainTex("Base (RGB)", 2D) = "white" {}
		_Exposure("HDR Exposure", Float) = 1.0
		[KeywordEnum(Default, Filmic, ACE)]_HDRType("HDRType", int) = 0
	}

	HLSLINCLUDE
#include "colorConvert.hlsl"  
#include "UnityCG.cginc"

	struct appdata
	{
		float4 vertex : POSITION;
        half2 uv : TEXCOORD0;
	};

	struct VSOut
	{
		float4 pos : SV_POSITION;
		float2 uv  : TEXCOORD0;
	};


	sampler2D _MainTex;
	//UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);
	//uniform float4 _MainTex_ST;
	float _Exposure;
	int   _HDRType;
#define HDR_DEFAULT 0
#define HDR_FILMIC  1
#define HDR_ACE     2

	VSOut vert(appdata v)
	{
		VSOut o;
		o.pos = UnityObjectToClipPos(v.vertex);//v.vertex;
		o.uv = v.uv;
//#if UNITY_UV_STARTS_AT_TOP
//		o.uv.y = 1.0 - o.uv.y;
//#endif
		return o;
	}
    
	half4 frag(VSOut i) : SV_Target
	{
		half4 color = tex2D(_MainTex, i.uv);
		if (_HDRType == HDR_FILMIC)
		{
			color.rgb = Filmic(color.rgb);
		}
		else if (_HDRType == HDR_ACE)
		{
			color.rgb = ACESToneMapping(color.rgb, _Exposure);
		}
		
	    return color;
	}


	ENDHLSL

	SubShader
	{
		Tags{ "Queue" = "Overlay" }
		Pass
		{
			ZTest Off
			ZWrite Off
			Fog{ Mode Off }

			HLSLPROGRAM
#pragma vertex vert  
#pragma fragment frag 
			ENDHLSL
		}
	}
}
