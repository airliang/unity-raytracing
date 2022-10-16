#ifndef RTCOMMON_HLSL
#define RTCOMMON_HLSL
#include "geometry.hlsl"
//#include "bxdf.hlsl"
//以后把这些struct放到一个structbuffer.hlsl的文件
struct BXDF
{
	float4 materialParam;
	float4 kd;
	float4 ks;
	float4 kr;
};

//struct MeshHandle
//{
//	int4 offsets;
//	Bounds bounds;
//};



//buffers


//StructuredBuffer<Primitive> Primitives;
//StructuredBuffer<float4x4> WorldMatrices;



//StructuredBuffer<MeshHandle> MeshHandles;


uniform float _time;

uniform float3 testBoundMax;
uniform float3 testBoundMin;


#endif
