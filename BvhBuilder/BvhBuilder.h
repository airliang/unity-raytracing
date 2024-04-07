#pragma once
// Author Humphrey
// A simplified version of RadeomRays
// Modified: 1. Support custom split bins
//           2. Keep build split_bvh and bvh only
//           3. Export CreateBVH(Split or not, SAH-based or not) DestroyBVH, TransferToFlat for GPU access
#ifndef BVH_BUILDER_H
#define BVH_BUILDER_H

#ifdef WIN32
    #ifdef EXPORT_API
        #define RRAPI __declspec(dllexport)
    #else
        #define RRAPI __declspec(dllimport)
    #endif
#elif defined(__GNUC__)
    #ifdef EXPORT_API
        #define RRAPI __attribute__((visibility ("default")))
    #else
        #define RRAPI
    #endif
#endif

#include "RadeonRays/math/bbox.h"
//#include "RadeonRays/log/log.h"

#ifdef __cplusplus
extern "C"
{
#endif

struct Node
{
    RadeonRays::float3 bboxmin;
    RadeonRays::float3 bboxmax;
	RadeonRays::float3 LRLeaf;
	//RadeonRays::float3 pad;
	//int left;
 //   int right;
};

struct MeshInstance
{
	int meshID;
	int materialID;
	int bvhStartIndex;
};

struct BVHHandle
{
	RadeonRays::bbox bounds;
	int numNodes;
	int numIndices;
	int* sortedIndices;
	void* bvh;
};

struct LinearBVHNode
{
	RadeonRays::bbox bounds;  //64bytes

	int leftChildIdx = -1;    // leaf
	int rightChildIdx = -1;   // interior

	int firstPrimOffset = 0;
	int nPrimitives = 0;  // 0 -> interior node

	bool IsLeaf()
	{
		return nPrimitives > 0;
	}
};


RRAPI BVHHandle CreateBVH(const RadeonRays::bbox* bounds, int count, bool useSah, bool useSplit, float traversalCost, int numBins, int splitDepth, float miniOverlap);
RRAPI void DestroyBVH(const BVHHandle* as);
RRAPI int TransferToFlat(Node* nodes, const BVHHandle* as, bool isTLAS, int curTriIndex, int bvhNodeOffset, const MeshInstance* meshInstances);
RRAPI void FlattenBVHTree(const BVHHandle* handle, LinearBVHNode* linearNodes);

typedef void(*FuncCallBack)(const char* message, int color, int size);
static FuncCallBack logCallbackFunc = nullptr;
RRAPI void RegisterLogCallback(FuncCallBack cb);

#ifdef __cplusplus
}
#endif

#endif // BVH_BUILDER_H
