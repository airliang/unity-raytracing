#include "BvhBuilder.h"
#include "RadeonRays/accelerator/bvh.h"
#include "RadeonRays/accelerator/split_bvh.h"
#include "RadeonRays/log/log.h"

#include <cassert>
#include <stack>
#include <iostream>

typedef intptr_t Handle;

#if defined(BUILD_AS_DLL)
#define BVH_API __declspec(dllexport)
#else
#define BVH_API	__declspec(dllimport)
#endif

using namespace RadeonRays;

static int g_curNode = 0;

int ProcessBLASNodes(Node* nodes, const Bvh::Node* node, const std::vector<int>& packedIndices, int curTriIndex, int bvhNodeOffset)
{
	RadeonRays::bbox bbox = node->bounds;

	int index = g_curNode;

	nodes[index].bboxmin = bbox.pmin;
	nodes[index].bboxmax = bbox.pmax;
	nodes[index].LRLeaf.z = 0;
	if(node->type == RadeonRays::Bvh::NodeType::kLeaf)
	{
		nodes[g_curNode].LRLeaf.x = curTriIndex + node->startidx;
		nodes[g_curNode].LRLeaf.y = node->numprims;
		nodes[g_curNode].LRLeaf.z = 1;
	}
	else
	{
		g_curNode++;
		nodes[index].LRLeaf.x = ProcessBLASNodes(nodes, node->lc, packedIndices, curTriIndex, bvhNodeOffset) + bvhNodeOffset;
		g_curNode++;
		nodes[index].LRLeaf.y = ProcessBLASNodes(nodes, node->rc, packedIndices, curTriIndex, bvhNodeOffset) + bvhNodeOffset;
	}
	return index;
}

int ProcessTLASNodes(Node* nodes, const Bvh::Node* node, const std::vector<int>& packedIndices, int bvhNodeOffset, const MeshInstance* meshInstances)
{
	RadeonRays::bbox bbox = node->bounds;

	nodes[g_curNode].bboxmin = bbox.pmin;
	nodes[g_curNode].bboxmax = bbox.pmax;
	nodes[g_curNode].LRLeaf.z = 0;

	int index = g_curNode;

	if (node->type == RadeonRays::Bvh::NodeType::kLeaf)
	{
		int instanceIndex = packedIndices[node->startidx];
		int meshIndex = meshInstances[instanceIndex].meshID;
		int materialID = meshInstances[instanceIndex].materialID;

		nodes[g_curNode].LRLeaf.x = meshInstances[instanceIndex].bvhStartIndex;
		nodes[g_curNode].LRLeaf.y = materialID;
		nodes[g_curNode].LRLeaf.z = -instanceIndex - 1;
	}
	else
	{
		g_curNode++;
		nodes[index].LRLeaf.x = ProcessTLASNodes(nodes, node->lc, packedIndices, bvhNodeOffset, meshInstances) + bvhNodeOffset;
		g_curNode++;
		nodes[index].LRLeaf.y = ProcessTLASNodes(nodes, node->rc, packedIndices, bvhNodeOffset, meshInstances) + bvhNodeOffset;
	}
	return index;
}

RRAPI BVHHandle CreateBVH(const RadeonRays::bbox* bounds, int count, bool useSah, bool useSplit, float traversalCost, int numBins, int splitDepth, float miniOverlap)
{
	Bvh* bvh = useSplit ? new SplitBvh(traversalCost, numBins, splitDepth, miniOverlap, 0) : new Bvh(traversalCost, numBins, useSah);
	bvh->Build((const RadeonRays::bbox*)bounds, count);
	BVHHandle sbvh;
	sbvh.bvh = bvh;
	sbvh.numNodes = bvh->GetNodeCount();
	sbvh.numIndices = (int)bvh->GetNumIndices();
	sbvh.sortedIndices = (int*)bvh->GetIndices();
	sbvh.bounds = bvh->Bounds();
	return sbvh;
}

RRAPI void DestroyBVH(const BVHHandle* handle)
{
	if(handle != nullptr && handle->bvh != nullptr)
		delete handle->bvh;
}

RRAPI int TransferToFlat(Node* nodes, const BVHHandle* as, bool isTLAS, int curTriIndex, int bvhNodeOffset, const MeshInstance* meshInstances)
{
	Bvh* bvh = (Bvh*)as->bvh;
	g_curNode = 0;
	if (isTLAS)
	{
		ProcessTLASNodes(nodes, bvh->GetRoot(), bvh->GetPackedIndices(), bvhNodeOffset, meshInstances);
	}
	else
		ProcessBLASNodes(nodes, bvh->GetRoot(), bvh->GetPackedIndices(), curTriIndex, bvhNodeOffset);

	return g_curNode + bvhNodeOffset;//curTriIndex + bvh->GetPackedIndices().size();
}

int FlattenBVHTree(const Bvh::Node* node, int& offset, LinearBVHNode* linearNodes)
{
	int curOffset = offset;
	linearNodes[curOffset].bounds = node->bounds;
	int myOffset = offset++;
	if (node->type == Bvh::kLeaf)
	{
		linearNodes[curOffset].nPrimitives = node->numprims;
		linearNodes[curOffset].firstPrimOffset = node->startidx;
		linearNodes[curOffset].leftChildIdx = -1;
		linearNodes[curOffset].rightChildIdx = -1;
	}
	else
	{
		linearNodes[curOffset].nPrimitives = 0;
		linearNodes[curOffset].leftChildIdx = FlattenBVHTree(node->lc, offset, linearNodes);
		linearNodes[curOffset].rightChildIdx = FlattenBVHTree(node->rc, offset, linearNodes);
	}

	return myOffset;
}

RRAPI void FlattenBVHTree(const BVHHandle* handle, LinearBVHNode* linearNodes)
{
	Bvh* bvh = (Bvh*)handle->bvh;
	int offset = 0;
	FlattenBVHTree(bvh->GetRoot(), offset, linearNodes);
}

void RegisterLogCallback(FuncCallBack cb) {
	//logCallbackFunc = cb;
	Logger::logCallbackFunc = cb;
}
