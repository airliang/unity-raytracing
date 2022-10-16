
#ifndef WAVEFRONT_QUEUES_HLSL
#define WAVEFRONT_QUEUES_HLSL
#include "GPUStructs.hlsl"
#include "geometry.hlsl"

#define SHADOW_RAY_QUEUE_INDEX 2
#define MISSRAY_QUEUE_INDEX 3
#define HITLIGHT_QUEUE_INDEX 4
#define MATERIAL_SHADING_QUEUE_INDEX 5
#define MAX_QUEUES 6


struct WorkQueueItem
{
	Ray ray;   //32 bytes
	WavefrontPathVertex pathVertex;  //32 bytes
};

struct MaterialQueueItem   //total 56 bytes
{
	//below is hit info
	HitInfo hitInfo;  //32 bytes
	float3  wo;        //12bytes
	float   coneWidth; //4bytes
	float2  pad;
	//float3 position;
	//float3 normal;
	//float2 uv;
};

struct EscapeRayItem   //total 64 bytes
{
	float3 orig;      //12 bytes
	float3 direction;
	int    depth;
	float  pad;
	WavefrontPathVertex pathVertex;  //32 bytes
};

struct HitLightQueueItem  //total 88 bytes
{
	uint   lightId;   //4 bytes
	int    depth;     //4 bytes
	float3 wo;        //12 bytes
	float  pad;       //4 bytes
	HitInfo hitInfo;  //32 bytes
	//uint   triangleIndex;  //hit mesh triangleIndex
	//float  primArea;   //hitlight area
	//float3 wi;
	//float2 pad;
	WavefrontPathVertex pathVertex;  //32 bytes
};

struct ShadowRayQueueItem
{
	float3 p0;
	float3 p1;
	float3 normal;
	float3 ld;
};

//EscapeRayItem ConvertToEscapeRayItem(Interaction surfaceIntersection, int bounce)
//{
//	EscapeRayItem escapeRayItem = (EscapeRayItem)0;
//	escapeRayItem.bounce = bounce;
//	if (bounce > 0)
//	{
//		escapeRayItem.bxdfFlag = 
//	}
//}

#endif



