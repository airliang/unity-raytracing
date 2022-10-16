using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BVHTranslator : Singleton<BVHTranslator>
{
    public struct GPUBVHNode
    {
        public Vector3 bboxmin;
        public Vector3 bboxmax;
        public Vector3 LRLeaf;
    };

    GPUBVHNode[] nodes;
    int curNode = 0;
    int curTriIndex = 0;
    Primitive[] primitives;
    List<GPUVertex> gpuVertices = new List<GPUVertex>();
    List<int> bvhRootStartIndices = new List<int>();
    int topLevelIndex = 0;

    int ProcessBLASNodes(LinearBVHNode[] linearNodes, int nodeIndex)
    {
        LinearBVHNode bvhNode = linearNodes[nodeIndex];
        GPUBounds bbox = bvhNode.bounds;

        nodes[curNode].bboxmin = bbox.min;
        nodes[curNode].bboxmax = bbox.max;
        nodes[curNode].LRLeaf.z = 0;

        int index = curNode;

        if (bvhNode.IsLeaf())
        {
            nodes[curNode].LRLeaf.x = curTriIndex + bvhNode.firstPrimOffset;
            nodes[curNode].LRLeaf.y = bvhNode.nPrimitives;
            nodes[curNode].LRLeaf.z = 1;
        }
        else
        {
            curNode++;
            nodes[index].LRLeaf.x = ProcessBLASNodes(linearNodes, bvhNode.leftChildIdx);
            curNode++;
            nodes[index].LRLeaf.y = ProcessBLASNodes(linearNodes, bvhNode.rightChildIdx);
        }
        return index;
    }

    public void ProcessTLAS(LinearBVHNode[] linearNodes, Primitive[] primitives, List<GPUVertex> gpuVertices)
    {

    }

    public void ProcessBLAS(LinearBVHNode[] linearNodes, Primitive[] primitives, MeshInstance[] meshInstances, List<int> bvhOffsets)
    {
        int nodeCnt = 0;

        for (int i = 0; i < bvhOffsets.Count; i++)
            nodeCnt += bvhOffsets[i];
        topLevelIndex = nodeCnt;

        // reserve space for top level nodes
        nodeCnt += 2 * meshInstances.Length;
        nodes = new GPUBVHNode[nodeCnt];

        int bvhRootIndex = 0;
        curTriIndex = 0;
    }

    int ProcessTLASNodes(LinearBVHNode[] linearNodes, int nodeIndex, Primitive[] primitives, MeshInstance[] meshInstances)
    {
        LinearBVHNode bvhNode = linearNodes[nodeIndex];
        GPUBounds bbox = bvhNode.bounds;
        nodes[curNode].bboxmin = bbox.min;
        nodes[curNode].bboxmax = bbox.max;
        nodes[curNode].LRLeaf.z = 0;

        int index = curNode;

        if (bvhNode.IsLeaf())
        {
            Primitive primitive = primitives[bvhNode.firstPrimOffset];
            int instanceIndex = primitive.meshInstIndex;
            int meshIndex = primitive.meshIndex;
            int materialID = meshInstances[instanceIndex].materialIndex;

            nodes[curNode].LRLeaf.x = bvhRootStartIndices[meshIndex];
            nodes[curNode].LRLeaf.y = materialID;
            nodes[curNode].LRLeaf.z = -instanceIndex - 1;
        }

        return 0;
    }
}
