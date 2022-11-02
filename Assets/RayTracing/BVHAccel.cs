using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;


struct BVHPrimitiveInfo
{
	public BVHPrimitiveInfo(int priIndex, GPUBounds bounds)	
	{
		primitiveIndex = priIndex;
		worldBound = bounds;
	}
	//primitive's index in primitive array
	public int primitiveIndex;
	
	public GPUBounds worldBound;
	//worldbound's central point
	//Vector3 centroid;
};
public class BVHBuildNode
{
	public void InitLeaf(int first, int n, GPUBounds b) 
	{
		firstPrimOffset = first;
		nPrimitives = n;
		bounds = b;
		//children[0] = children[1] = nullptr;
		//++leafNodes;
		//++totalLeafNodes;
		//totalPrimitives += n;
	}
	public void InitInterior(int axis, BVHBuildNode c0, BVHBuildNode c1)
	{
		childrenLeft = c0;
		childrenRight = c1;
		//bounds.SetMinMax(Vector3.Min(c0.bounds.min, c1.bounds.min), Vector3.Max(c0.bounds.max, c1.bounds.max));
		bounds = GPUBounds.Union(c0.bounds, c1.bounds);
		splitAxis = axis;
		nPrimitives = 0;
		//++interiorNodes;
	}
	public GPUBounds bounds;
	public BVHBuildNode childrenLeft;
	public BVHBuildNode childrenRight;
	public int splitAxis;
	
	//leaf node才用到的数据
	public int firstPrimOffset;
	//the number of primtives in leaf. 0 is a interior node
	//if is toplevel build node is the bottom bvh offset
	public int nPrimitives;

	public bool IsLeaf()
    {
		return nPrimitives > 0;
    }
};

//use as an array can transfer to the compute shader
public struct LinearBVHNode
{
    public GPUBounds bounds;  //64bytes

    public int leftChildIdx;    // leaf
    public int rightChildIdx;   // interior

	public int firstPrimOffset;
	public int nPrimitives;  // 0 -> interior node

	public bool IsLeaf()
    {
		return nPrimitives > 0;
    }
};

class StackEntry
{
	public LinearBVHNode node;
	public int idx;

	public StackEntry(LinearBVHNode n, int i)
	{
		node = n;
		idx = i;
	}

	//public int EncodeIdx() { return node.IsLeaf() ? ~idx : idx; }
};

public struct HitInfo
{
	public Vector3Int triAddr;
	public int meshInstanceId;
	public int triangleIndexInMesh;
	public float hitT;
	public Vector2 baryCoord;
}

public static class BVHLib
{
	const string BVHLIB_DLL = "BVHLib";
	public struct BVHHandle
    {
        public int numNodes;
        public int numIndices;
		public IntPtr sortIndices;
		public IntPtr bvhBuilder;
    };

    [DllImport(BVHLIB_DLL, EntryPoint = "BuildBVH", CallingConvention = CallingConvention.Cdecl)]
    public static extern BVHHandle BuildBVH([In] GPUBounds[] bounds, [In] int size, [In] int _maxPrimsInNode, [In] bool useSplit);
    [DllImport(BVHLIB_DLL, EntryPoint = "FlattenBVHTree", CallingConvention = CallingConvention.Cdecl)]
    public static extern void FlattenBVHTree(ref BVHHandle handle, [In, Out]LinearBVHNode[] linearBVHNodes);

    [DllImport(BVHLIB_DLL, EntryPoint = "ReleaseBVH", CallingConvention = CallingConvention.Cdecl)]
	public static extern void ReleaseBVH(ref BVHHandle handle);
}

public static class WoopTriangleData
{
	public static List<Vector4> m_woopTriangleVertices = new List<Vector4>();
	public static List<int> m_woopTriangleIndices = new List<int>();
	//woop's triangle transform
	public static Vector4[] m_woop = new Vector4[3];

	public static void UnitTriangle(int triIndex, List<GPUVertex> vertices, Primitive[] primitives)
	{
		Primitive primitive = primitives[triIndex];
		//primitive.triangleOffset 
		Vector3 v0 = vertices[primitive.triIndices.x].position;
		Vector3 v1 = vertices[primitive.triIndices.y].position;
		Vector3 v2 = vertices[primitive.triIndices.z].position;

		Matrix4x4 matrix = new Matrix4x4();
		Vector4 col0 = v0 - v2;
		col0.w = 0;
		matrix.SetColumn(0, col0);
		Vector4 col1 = v1 - v2;
		col1.w = 0;
		matrix.SetColumn(1, col1);
		Vector4 col2 = Vector3.Cross(v0 - v2, v1 - v2);
		col2.w = 0;
		matrix.SetColumn(2, col2);
		Vector4 col3 = v2;
		col3.w = 1;
		matrix.SetColumn(3, col3);
		matrix = Matrix4x4.Inverse(matrix);

		m_woop[0] = matrix.GetRow(0);
		m_woop[1] = matrix.GetRow(1);
		m_woop[2] = matrix.GetRow(2);

		//Vector3 normal = Vector3.Cross(m_woop[0], m_woop[1]);
		//Vector3 normal2 = Vector3.Cross(col0, col1);
		//normal.Normalize();
		//normal2.Normalize();
	}

	public static void Clear()
    {
		m_woopTriangleVertices.Clear();
		m_woopTriangleIndices.Clear();
	}
}

public class BVHAccel
{
	int maxPrimsInNode;
	
	BVHBuildNode root;
	//List<Primitive> primitives;
	//public LinearBVHNode[] linearNodes;
	//BVHBuilder builder = new SplitBVHBuilder();
	public static int maxLeafSize = 4;  //a node can contain leaves
										//scene informations
										//Texture2D bvhNodeTexture;
	//public GPUBVHNode[] m_nodes;
	//gpu中的bvh nodes数组
	public List<GPUBVHNode> m_nodes = new List<GPUBVHNode>();
	public List<RadeonBVH.Node> m_flattenNodes = new List<RadeonBVH.Node>();
	
	public List<GPUVertex> sceneVertices;

	public static bool NVMethod = false;


	public int instBVHNodeAddr = 0;
	public bool buildByCPP = false;
	public bool useRadeonRayBVH = true;
	int bvhTrianglesCount = 0;
	int bvhNodesCount = 0;
	float buildingProgress = 0;
	int totalBuildStep;

    public List<Vector3Int> sortedTriangles = new List<Vector3Int>();
    List<int> instBVHOffset = new List<int> ();

    public void Build(List<Primitive> primitives, List<GPUVertex> vertices, List<Vector3Int> triangles)
    {
		sceneVertices = vertices;
		/*
		primitives = prims;
		maxPrimsInNode = maxPrims;
		List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
		for (int i = 0; i < primitives.Count; ++i)
			primitiveInfos.Add(new BVHPrimitiveInfo(i, ConvertUnityBounds(primitives[i].worldBound)));
		//List<Primitive> orderedPrims = new List<Primitive>();
		root = RecursiveBuild(ref primitiveInfos, 0, prims.Count, ref orderedPrims);
		primitives = orderedPrims;
		*/
		BVHBuilder builder = new SplitBVHBuilder();
		//List<Primitive> orderedPrims = new List<Primitive>();
		GPUBounds[] primBounds = new GPUBounds[primitives.Count];
		for (int i = 0; i < primitives.Count; ++i)
        {
			primBounds[i] = primitives[i].worldBound;
        }
		root = builder.Build(primBounds);
		List<int> orderedPrims = builder.GetOrderedPrimitives();
		//primitives = orderedPrims;
		Primitive[] sortedPrimitives = new Primitive[primitives.Count];
		for (int i = 0; i < primitives.Count; ++i)
        {
			//sortedPrimitives.Add(primitives[orderedPrims[i]]);
			sortedPrimitives[i] = primitives[orderedPrims[i]];

		}
		int offset = 0;
		LinearBVHNode[] linearNodes = new LinearBVHNode[builder.TotalNodes];
		FlattenBVHTree(root, ref offset, linearNodes);
		int totalPrimitives = 0;
		for (int i = 0; i < linearNodes.Length; ++i)
        {
			totalPrimitives += linearNodes[i].nPrimitives;
		}
		CreateCompact(linearNodes, sortedPrimitives, vertices);
		//bvhNodeTexture = new Texture2D(builder.TotalNodes, 1, TextureFormat.RGBAFloat, false);
	}

	public void Clear()
    {
		sceneVertices = null;
		root = null;
        sortedTriangles.Clear();
        //linearNodes = null;
    }
	int FlattenBVHTree(BVHBuildNode node, ref int offset, LinearBVHNode[] linearNodes)
	{
		//LinearBVHNode linearNode = linearNodes[offset];
		int curOffset = offset;
		linearNodes[curOffset].bounds = node.bounds;
		int myOffset = offset++;
		if (node.nPrimitives > 0)
		{
			//是一个叶子节点
			linearNodes[curOffset].nPrimitives = node.nPrimitives;
			linearNodes[curOffset].firstPrimOffset = node.firstPrimOffset;
			linearNodes[curOffset].leftChildIdx = -1;
			linearNodes[curOffset].rightChildIdx = -1;
        }
		else
		{
			//linearNodes[curOffset].axis = (ushort)node.splitAxis;
			linearNodes[curOffset].nPrimitives = 0;
			//这里返回了offset
			linearNodes[curOffset].leftChildIdx = FlattenBVHTree(node.childrenLeft, ref offset, linearNodes);
			linearNodes[curOffset].rightChildIdx = FlattenBVHTree(node.childrenRight, ref offset, linearNodes);
		}

		return myOffset;
	}

	public void DrawDebug(List<MeshInstance> meshInstances, bool instanceBVH = true)
    {
		if (instanceBVH)
        {
			for (int i = instBVHNodeAddr; i < m_nodes.Count; ++i)
			{
				RenderDebug.DrawDebugBound(m_nodes[i].b0min, m_nodes[i].b0max, Color.white);
			}
			return;
			for (int i = 0; i < instBVHNodeAddr; ++i)
            {
				int nodeAddr = MathUtil.SingleToInt32Bits(m_nodes[i].cids.x);
				int meshInstanceIndex = MathUtil.SingleToInt32Bits(m_nodes[i].cids.z);
				MeshInstance meshInst = meshInstances[meshInstanceIndex];
				if (nodeAddr >= 0)
                {
					Vector3 min = m_nodes[i].b0min; //new Vector3(m_nodes[i].b0xy.x, m_nodes[i].b0xy.z, m_nodes[i].b01z.x);
					Vector3 max = m_nodes[i].b0max; //new Vector3(m_nodes[i].b0xy.y, m_nodes[i].b0xy.w, m_nodes[i].b01z.y);
					BoundingUtils.TransformBounds(ref meshInst.localToWorld, ref min, ref max);
					RenderDebug.DrawDebugBound(min, max, Color.white);
				}
				else
                {
					int triAddr = nodeAddr;
					for (int tri = ~triAddr; ; tri += 3)
					{
						Vector4 m0 = WoopTriangleData.m_woopTriangleVertices[tri];     //matrix row 0 

						if (MathUtil.SingleToInt32Bits(m0.x) == 0x7fffffff)
							break;

						RenderDebug.DrawTriangle(meshInst.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[tri]].position),
						meshInst.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[tri + 1]].position),
						meshInst.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[tri + 2]].position), Color.red);
					}
				}
				
                

                nodeAddr = MathUtil.SingleToInt32Bits(m_nodes[i].cids.y);
				meshInstanceIndex = MathUtil.SingleToInt32Bits(m_nodes[i].cids.w);
				meshInst = meshInstances[meshInstanceIndex];
				if (nodeAddr >= 0)
                {
					Vector3 min = m_nodes[i].b0min; //new Vector3(m_nodes[i].b0xy.x, m_nodes[i].b0xy.z, m_nodes[i].b01z.x);
					Vector3 max = m_nodes[i].b0max; //new Vector3(m_nodes[i].b0xy.y, m_nodes[i].b0xy.w, m_nodes[i].b01z.y);
					BoundingUtils.TransformBounds(ref meshInst.localToWorld, ref min, ref max);
					RenderDebug.DrawDebugBound(min, max, Color.white);
				}
				else
                {
					int triAddr = nodeAddr;

					for (int tri = ~triAddr; ; tri += 3)
					{
						Vector4 m0 = WoopTriangleData.m_woopTriangleVertices[tri];     //matrix row 0 

						if (MathUtil.SingleToInt32Bits(m0.x) == 0x7fffffff)
							break;
						RenderDebug.DrawTriangle(meshInst.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[tri]].position),
						meshInst.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[tri + 1]].position),
						meshInst.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[tri + 2]].position), Color.red);
					}
				}
			}
		}
    }


	//create the gpu bvh nodes
	//param meshNode代表是否一个mesh下的bvh划分
	//gpuVertices 
	//bottomLevel
	//bottomLevelOffset   bottomlevel的bvh在m_nodes中的索引
	void CreateCompact(LinearBVHNode[] bvhNodes, Primitive[] primitives, List<GPUVertex> gpuVertices, bool bottomLevel = true, List<int> botomLevelOffset = null)
	{
		//GPUBVHNode[] nodes = new GPUBVHNode[nodesNum];
		List<GPUBVHNode> nodes = new List<GPUBVHNode>(m_nodes);
		nodes.Add(new GPUBVHNode());
		//int nextNodeIdx = 0;
		List<StackEntry> stack = new List<StackEntry>();
		stack.Add(new StackEntry(bvhNodes[0], m_nodes.Count));
		GPUBounds b0 = new GPUBounds();
		GPUBounds b1 = new GPUBounds();
		
		while (stack.Count > 0)
		{
			int c0 = 0;  //bottomlevel bvh: left child vertices offset; toplevel bvh: 
			int c1 = 0;  //bottomlevel bvh: right child vertices offset; toplevel bvh: 
			int c2 = 0;  //bottomlevel bvh: left child primtives num; toplevel bvh: left child bottomlevel bvh offset
			int c3 = 0;  //bottomlevel bvh: right child primitves num
			StackEntry e = stack[stack.Count - 1];
			stack.RemoveAt(stack.Count - 1);

			if (e.node.IsLeaf())
            {
                if (bottomLevel)
                {
                    c0 = ~WoopTriangleData.m_woopTriangleVertices.Count;
                    //BVHBuildNode child = e.node.childrenLeft;
                    //处理三角形
                    for (int i = e.node.firstPrimOffset; i < e.node.firstPrimOffset + e.node.nPrimitives; ++i)
                    {
						//把三角形每个顶点按顺序写入buffer里，保证了每个三角形的索引是连续的
						WoopTriangleData.UnitTriangle(i, gpuVertices, primitives);
                        Primitive primitive = primitives[i];

                        for (int v = 0; v < 3; ++v)
                        {
							WoopTriangleData.m_woopTriangleVertices.Add(WoopTriangleData.m_woop[v]);
							WoopTriangleData.m_woopTriangleIndices.Add(primitive.triIndices[v]);
                            //Vector4 worldPos = gpuVertices[primitive.triIndices[v]].position;
                            //Vector4 uv = gpuVertices[primitive.triIndices[v]].uv;
                            //if (v == 0)
                            //	worldPos.w = Int32BitsToSingle(primitive.materialIndex);

                            //m_vertices.Add(new GPUVertex(worldPos, uv));
                        }
                    }
                    WoopTriangleData.m_woopTriangleVertices.Add(new Vector4(MathUtil.Int32BitsToSingle(int.MaxValue), 0, 0, 0));
					WoopTriangleData.m_woopTriangleIndices.Add(int.MaxValue);
                    //m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
                    //c2 = child.nPrimitives;
                    Primitive primitiveCur = primitives[e.node.firstPrimOffset];
                    c2 = primitiveCur.meshInstIndex;
                }
                else
                {
                    //BVHBuildNode child = e.node.childrenLeft;
                    //处理meshInst

                    Primitive primitive = primitives[e.node.firstPrimOffset];

                    c0 = botomLevelOffset[primitive.meshIndex];
                    c2 = primitive.meshInstIndex;
                }

                GPUBVHNode gpuBVH = new GPUBVHNode();
                b0 = e.node.bounds;//e.node.childrenLeft.bounds;
                b1 = new GPUBounds();//e.node.childrenRight.bounds;

                //gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
                //gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
                //gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);
                gpuBVH.b0min = b0.min;
                gpuBVH.b0max = b0.max;
                gpuBVH.b1min = b1.min;
                gpuBVH.b1max = b1.max;

                gpuBVH.cids = new Vector4(MathUtil.Int32BitsToSingle(c0), MathUtil.Int32BitsToSingle(c1), MathUtil.Int32BitsToSingle(c2), MathUtil.Int32BitsToSingle(c3));
                nodes[e.idx] = gpuBVH;
            }
			else
            {
                //left child
                LinearBVHNode leftChild = bvhNodes[e.node.leftChildIdx];
                if (!leftChild.IsLeaf())
                {
                    c0 = nodes.Count;//++nextNodeIdx;
                    stack.Add(new StackEntry(leftChild, c0));
                    nodes.Add(new GPUBVHNode());
					c2 = -1;  //means that lefchild is not a leaf
                }
				else
                {
                    if (bottomLevel)
                    {
                        c0 = ~WoopTriangleData.m_woopTriangleVertices.Count;
                        //BVHBuildNode child = e.node.childrenLeft;
                        //处理三角形
                        for (int i = leftChild.firstPrimOffset; i < leftChild.firstPrimOffset + leftChild.nPrimitives; ++i)
                        {
							//把三角形每个顶点按顺序写入buffer里，保证了每个三角形的索引是连续的
							WoopTriangleData.UnitTriangle(i, gpuVertices, primitives);
                            Primitive primitive = primitives[i];

                            for (int v = 0; v < 3; ++v)
                            {
                                WoopTriangleData.m_woopTriangleVertices.Add(WoopTriangleData.m_woop[v]);
								WoopTriangleData.m_woopTriangleIndices.Add(primitive.triIndices[v]);
                                //Vector4 worldPos = gpuVertices[primitive.triIndices[v]].position;
                                //Vector4 uv = gpuVertices[primitive.triIndices[v]].uv;
                                //if (v == 0)
                                //	worldPos.w = Int32BitsToSingle(primitive.materialIndex);

                                //m_vertices.Add(new GPUVertex(worldPos, uv));
                            }
                        }
                        WoopTriangleData.m_woopTriangleVertices.Add(new Vector4(MathUtil.Int32BitsToSingle(int.MaxValue), 0, 0, 0));
						WoopTriangleData.m_woopTriangleIndices.Add(int.MaxValue);
                        //m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
                        //c2 = child.nPrimitives;
                        Primitive primitiveCur = primitives[leftChild.firstPrimOffset];
                        c2 = primitiveCur.meshInstIndex;
                    }
                    else
                    {
                        //BVHBuildNode child = e.node.childrenLeft;
                        //处理meshInst

                        Primitive primitive = primitives[leftChild.firstPrimOffset];

                        c0 = botomLevelOffset[primitive.meshIndex];
                        c2 = primitive.meshInstIndex;
                    }

                }

                //right child
                LinearBVHNode rightChild = bvhNodes[e.node.rightChildIdx];
                if (!rightChild.IsLeaf())
                {
                    c1 = nodes.Count;//++nextNodeIdx;
                    stack.Add(new StackEntry(rightChild, c1));
                    nodes.Add(new GPUBVHNode());
					c3 = 1;  //means that rightChild is not leaf node.
                }
				else
                {
                    if (bottomLevel)
                    {
                        c1 = ~WoopTriangleData.m_woopTriangleVertices.Count;
                        //BVHBuildNode child = e.node.childrenRight;
                        //处理三角形
                        for (int i = rightChild.firstPrimOffset; i < rightChild.firstPrimOffset + rightChild.nPrimitives; ++i)
                        {
							//把三角形写入buffer里
							WoopTriangleData.UnitTriangle(i, gpuVertices, primitives);
                            Primitive primitive = primitives[i];
                            for (int v = 0; v < 3; ++v)
                            {
                                WoopTriangleData.m_woopTriangleVertices.Add(WoopTriangleData.m_woop[v]);
								WoopTriangleData.m_woopTriangleIndices.Add(primitive.triIndices[v]);
                                //m_vertices.Add(new GPUVertex(gpuVertices[primitive.triIndices[v]].position, gpuVertices[primitive.triIndices[v]].uv));
                            }

                        }
                        WoopTriangleData.m_woopTriangleVertices.Add(new Vector4(MathUtil.Int32BitsToSingle(int.MaxValue), 0, 0, 0));
						WoopTriangleData.m_woopTriangleIndices.Add(int.MaxValue);
                        //m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
                        Primitive primitiveCur = primitives[rightChild.firstPrimOffset];
                        c3 = primitiveCur.meshInstIndex;
                    }
                    else
                    {
                        //BVHBuildNode child = e.node.childrenRight;
                        //处理meshInst

                        Primitive primitive = primitives[rightChild.firstPrimOffset];

                        c1 = botomLevelOffset[primitive.meshIndex];
                        c3 = primitive.meshInstIndex;
                    }
                }

                //添加结束
                GPUBVHNode gpuBVH = new GPUBVHNode();
                b0 = leftChild.bounds;//e.node.childrenLeft.bounds;
                b1 = rightChild.bounds;//e.node.childrenRight.bounds;

                //gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
                //gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
                //gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);
                gpuBVH.b0min = b0.min;
                gpuBVH.b0max = b0.max;
                gpuBVH.b1min = b1.min;
                gpuBVH.b1max = b1.max;

                gpuBVH.cids = new Vector4(MathUtil.Int32BitsToSingle(c0), MathUtil.Int32BitsToSingle(c1), MathUtil.Int32BitsToSingle(c2), MathUtil.Int32BitsToSingle(c3));
				nodes[e.idx] = gpuBVH;
			}
		}

		m_nodes = nodes;
	}

	//x is left child index
	//y is right child index
	Vector2Int GetNodeChildIndex(Vector4 cids)
    {
		Vector2Int childIndex = new Vector2Int(-1, -1);
		childIndex.x = MathUtil.SingleToInt32Bits(cids.x);
		childIndex.y = MathUtil.SingleToInt32Bits(cids.z);
		return childIndex;
    }

	Vector2Int GetTopLevelLeaveMeshInstance(Vector4 cids)
    {
		Vector2Int childIndex = new Vector2Int(-1, -1);
		childIndex.x = MathUtil.SingleToInt32Bits(cids.y);
		childIndex.y = MathUtil.SingleToInt32Bits(cids.w);
		return childIndex;
	}
	
	public void Prepare(int meshesNum)
	{
        buildingProgress = 0;
        totalBuildStep = meshesNum + 1;
        WoopTriangleData.Clear();
        instBVHNodeAddr = 0;
		sortedTriangles.Clear();
        buildingProgress += 0.001f;
        instBVHOffset.Clear();
    }


	//build 2 types bvh
	//meshinstance bvh and mesh primitive bvh
	//return toplevel bvh在bvhbuffer中的位置
	public IEnumerator Build(List<MeshInstance> meshInstances, List<MeshHandle> meshHandles, List<int> meshHandleIndices, 
		List<GPUVertex> vertices, List<Vector3Int> triangles)
    {
		sceneVertices = vertices;

		while (buildingProgress < 1)
		{
            for (int i = 0; i < meshHandles.Count; ++i)
            {
                MeshHandle meshH = meshHandles[i];

                meshHandles[i] = meshH;
                instBVHOffset.Add(instBVHNodeAddr);
                int nodesNum = BuildMeshBVH(meshHandles[i], vertices, triangles, ref sortedTriangles);
                instBVHNodeAddr = nodesNum;
                buildingProgress = (float)(i + 1) / totalBuildStep;
				int precentage = (int)(buildingProgress * 100.0f);
				RaytracingStates.displayText = "Building BVH... " + precentage + "%";
                yield return null;
            }
			// build the instance bounding box bvh
			buildingProgress = 1;
            BuildInstBVH(meshHandles, meshInstances, meshHandleIndices, instBVHOffset);
            RaytracingStates.displayText = "Building BVH... " + 100 + "%";
            yield return null;
        }
		
		
		yield return null;
	}

	public void BuildInstBVH(List<MeshHandle> meshHandles, List<MeshInstance> meshInstances, List<int> meshHandleIndices, List<int> instBVHOffset)
    {
		List<Primitive> primitives = new List<Primitive>();
		RadeonBVH.MeshInstance[] radeonMeshInstances = new RadeonBVH.MeshInstance[meshInstances.Count];
		for (int i = 0; i < meshInstances.Count; ++i)
		{
			MeshInstance meshInst = meshInstances[i];
			int meshHandleIndex = meshHandleIndices[i];
			MeshHandle meshHandle = meshHandles[meshHandleIndex];
			GPUBounds worldBound = GPUBounds.TransformBounds(ref meshInst.localToWorld, ref meshHandle.localBounds);
			//Renderer renderer = meshTransforms[i].GetComponent<Renderer>();
			Primitive meshInstPrim = new Primitive(worldBound, i, meshHandleIndex);
			primitives.Add(meshInstPrim);

			radeonMeshInstances[i] = new RadeonBVH.MeshInstance()
			{
				meshID = meshHandleIndex,
				materialID = meshInst.materialIndex,
				bvhStartIndex = instBVHOffset[meshHandleIndex]
			};
		}

		bool useNVMethod = buildByCPP ? NVMethod : false;

        if (primitives.Count > 0)
        {
            GPUBounds[] primBounds = new GPUBounds[primitives.Count];
            for (int i = 0; i < primitives.Count; ++i)
            {
                primBounds[i] = primitives[i].worldBound;
            }
            if (buildByCPP)
			{
				//no longer use bvhlib any more
				//if (!useRadeonRayBVH)
    //            {
				//	float timeBegin = Time.realtimeSinceStartup;
				//	BVHLib.BVHHandle bvhHandle = BVHLib.BuildBVH(primBounds, primitives.Count, 1, false);
				//	int[] sortedIndics = new int[bvhHandle.numIndices];
				//	Marshal.Copy(bvhHandle.sortIndices, sortedIndics, 0, bvhHandle.numIndices);
				//	Primitive[] sortedPrims = new Primitive[bvhHandle.numIndices];
				//	for (int i = 0; i < bvhHandle.numIndices; ++i)
				//	{
				//		//sortedPrims.Add(primitives[orderedPrims[i]]);
				//		sortedPrims[i] = primitives[sortedIndics[i]];
				//	}
				//	LinearBVHNode[] linearNodes = new LinearBVHNode[bvhHandle.numNodes];
				//	BVHLib.FlattenBVHTree(ref bvhHandle, linearNodes);
				//	BVHLib.ReleaseBVH(ref bvhHandle);

				//	float timeInterval = Time.realtimeSinceStartup - timeBegin;
				//	Debug.Log("building top level bvh using BVHLib cost time:" + timeInterval);
				//	//CreateCompact(linearNodes, sortedPrims, vertices, false, instBVHOffset);
				//	CompactTLASNodes(linearNodes, sortedPrims, instBVHOffset);
				//}
    //            else
                {
					float timeBegin = Time.realtimeSinceStartup;
					
					RadeonBVH.BVHFlat bvhFlat = RadeonBVH.CreateTLAS(primBounds, new RadeonBVH.BuildParam { cost = 0.125f, miniOverlap = 0.05f, numBins = 64, splitDepth = 48 }, radeonMeshInstances, bvhTrianglesCount, instBVHNodeAddr);
					if (useNVMethod)
                    {
						Primitive[] sortedPrims = new Primitive[bvhFlat.sortedIndices.Length];

						for (int i = 0; i < bvhFlat.sortedIndices.Length; ++i)
						{
							//sortedPrims.Add(primitives[orderedPrims[i]]);
							sortedPrims[i] = primitives[bvhFlat.sortedIndices[i]];
						}

						CompactTLASNodes(bvhFlat.linearBVHNodes, sortedPrims, instBVHOffset);
					}
					else
                    {
						m_flattenNodes.AddRange(bvhFlat.nodes);
                    }
					float timeInterval = Time.realtimeSinceStartup - timeBegin;
					Debug.Log("building bottom level bvh using RadeonRay BVH cost time:" + timeInterval);
				} 
			}
			else
			{
				//instance不需要用split的
				BVHBuilder instBuilder = new BVHBuilder();
				//保证一个leaf一个inst，所以maxPrimsInNode参数是1

                BVHBuildNode instRoot = instBuilder.Build(primBounds, 1);
                //primitives = orderedPrims;
                List<int> orderedPrims = instBuilder.GetOrderedPrimitives();
                Primitive[] sortedPrims = new Primitive[orderedPrims.Count];
                for (int i = 0; i < orderedPrims.Count; ++i)
                {
                    //sortedPrims.Add(primitives[orderedPrims[i]]);
                    sortedPrims[i] = primitives[orderedPrims[i]];
                }
                float timeBegin = Time.realtimeSinceStartup;
                LinearBVHNode[] linearNodes = new LinearBVHNode[instBuilder.TotalNodes];
                int offset = 0;
                FlattenBVHTree(instRoot, ref offset, linearNodes);
                float timeInterval = Time.realtimeSinceStartup - timeBegin;
                Debug.Log("FlattenBVHTree inst bvh cost time:" + timeInterval);
				//CreateCompact(linearNodes, sortedPrims, vertices, false, instBVHOffset);
				CompactTLASNodes(linearNodes, sortedPrims, instBVHOffset);
			}
        }
	}

	//返回m_nodes的数量
	public int BuildMeshBVH(MeshHandle meshHandle, List<GPUVertex> vertices, List<Vector3Int> triangleFaces, ref List<Vector3Int> sortedTriangleFaces)
    {
		//生成MeshHandle的primitives
		List<Primitive> primitives = new List<Primitive>();
		int faceNum = meshHandle.triangleCount / 3;
        GPUBounds[] primBounds = new GPUBounds[faceNum];

        for (int f = 0; f < faceNum; ++f)
		{
			Vector3Int face = triangleFaces[f + meshHandle.triangleOffset];
			int tri0 = face.x;//triangles[f * 3 + meshHandle.triangleOffset];
			int tri1 = face.y;//triangles[f * 3 + 1 + meshHandle.triangleOffset];
			int tri2 = face.z;//triangles[f * 3 + 2 + meshHandle.triangleOffset];
			primitives.Add(new Primitive(tri0, tri1, tri2, vertices[tri0].position, vertices[tri1].position, vertices[tri2].position, 0, 0));
			primBounds[f] = primitives[f].worldBound;
		}
		//List<Primitive> orderedPrims = new List<Primitive>();
		bool useNVMethod = buildByCPP ? NVMethod : false;

        if (buildByCPP)
        {
			float timeBegin = Time.realtimeSinceStartup;
			RadeonBVH.BVHFlat bvhFlat = RadeonBVH.CreateBLAS(primBounds, new RadeonBVH.BuildParam { cost = 0.125f, miniOverlap = 0.05f, numBins = 64, splitDepth = 48 }, bvhTrianglesCount, bvhNodesCount);
			bvhTrianglesCount += bvhFlat.bvhTrianglesNum;
			bvhNodesCount += bvhFlat.bvhNodeNums;
                

            if (useNVMethod)
			{
				Primitive[] sortedPrims = new Primitive[bvhFlat.sortedIndices.Length];

				for (int i = 0; i < bvhFlat.sortedIndices.Length; ++i)
				{
					sortedPrims[i] = primitives[bvhFlat.sortedIndices[i]];
				}

				float timeInterval = Time.realtimeSinceStartup - timeBegin;
				Debug.Log("building bottom level bvh using RadeonRay BVH cost time:" + timeInterval);
				CompactBLASNodes(bvhFlat.linearBVHNodes, sortedPrims, vertices);
				sortedTriangleFaces = triangleFaces;

            }
			else
			{
				m_flattenNodes.AddRange(bvhFlat.nodes);

                for (int j = 0; j < bvhFlat.sortedIndices.Length; ++j)
                {
                    int f = bvhFlat.sortedIndices[j];
                    Vector3Int face = triangleFaces[f + meshHandle.triangleOffset];
                    sortedTriangleFaces.Add(face);
                }
            }
		}
		else
        {
            BVHBuilder builder = new SplitBVHBuilder();
            float timeBegin = Time.realtimeSinceStartup;
            BVHBuildNode meshRoot = builder.Build(primBounds, primitives.Count < 3 ? 1 : maxLeafSize);
            float timeInterval = Time.realtimeSinceStartup - timeBegin;
            Debug.Log("building bottom level mesh bvh cost time:" + timeInterval);

            timeBegin = Time.realtimeSinceStartup;
            LinearBVHNode[] linearNodes = new LinearBVHNode[builder.TotalNodes];
            int offset = 0;
            FlattenBVHTree(meshRoot, ref offset, linearNodes);
            timeInterval = Time.realtimeSinceStartup - timeBegin;
            Debug.Log("FlattenBVHTree mesh bvh cost time:" + timeInterval);
            //CreateCompact生成的m_nodes数组只有inner node的数据，所以这里返回m_nodes.Count
            //primitives = orderedPrims;
            timeBegin = Time.realtimeSinceStartup;
            List<int> orderedPrims = builder.GetOrderedPrimitives();
            Primitive[] sortedPrims = new Primitive[orderedPrims.Count];
            for (int i = 0; i < orderedPrims.Count; ++i)
            {
                //sortedPrims.Add(primitives[orderedPrims[i]]);
                sortedPrims[i] = primitives[orderedPrims[i]];
            }
			//CreateCompact(linearNodes, sortedPrims, vertices, true);
			CompactBLASNodes(linearNodes, sortedPrims, vertices);
			timeInterval = Time.realtimeSinceStartup - timeBegin;
            Debug.Log("building bottom level bvh CreateCompact cost time:" + timeInterval);
        }

		return useNVMethod ? m_nodes.Count : m_flattenNodes.Count;
	}

	bool RayBoundIntersect(GPURay ray, in Vector3 invdir, in Vector3 bmin, in Vector3 bmax, out float tMin)
    {
		var f = (bmax - ray.orig).Mul(invdir);
		var n = (bmin - ray.orig).Mul(invdir);

		var tmax = Vector3.Max(f, n);
		var tmin = Vector3.Min(f, n);

		float t1 = Mathf.Min(Mathf.Min(tmax.x, Mathf.Min(tmax.y, tmax.z)), ray.tMax);
		float t0 = Mathf.Max(Mathf.Max(tmin.x, Mathf.Max(tmin.y, tmin.z)), ray.tmin);

		tMin = (t1 >= t0) ? (t0 > 0.0f ? t0 : t1) : -1.0f;

		return tMin > 0;
	}


	public static bool WoodTriangleRayIntersect(GPURay ray, Vector4 m0, Vector4 m1, Vector4 m2, ref Vector2 uv, out float hitT)
	{
		//uv = Vector2.zero;
		hitT = 0;
		//Oz is a point, must plus w
		float Oz = m2.w + Vector3.Dot(ray.orig, m2);//ray.orig.x * m2.x + ray.orig.y * m2.y + ray.orig.z * m2.z;
											   //Dz is a vector
		float invDz = 1.0f / Vector3.Dot(ray.direction, m2);//(ray.direction.x * m2.x + ray.direction.y * m2.y + ray.direction.z * m2.z);
		float t = -Oz * invDz;
		//t *= 1 + 2 * gamma(3);
		//hitT = tmax;
		//if t is in bounding and less than the ray.tMax
		if (t >= ray.tmin && t < ray.tmax)
		{
			// Compute and check barycentric u.
			float Ox = m0.w + Vector3.Dot(ray.orig, m0);//ray.orig.x * m0.x + ray.orig.y * m0.y + ray.orig.z * m0.z;
			float Dx = Vector3.Dot(ray.direction, m0);//dirx * m0.x + diry * m0.y + dirz * m0.z;
			float u = Ox + t * Dx;

			if (u >= 0.0f)
			{
				// Compute and check barycentric v.
				float Oy = m1.w + Vector3.Dot(ray.orig, m1);//ray.orig.x * m1.x + ray.orig.y * m1.y + ray.orig.z * m1.z;
				float Dy = Vector3.Dot(ray.direction, m1);//dirx * m1.x + diry * m1.y + dirz * m1.z;
				float v = Oy + t * Dy;

				if (v >= 0.0f && u + v <= 1.0f)
				{
					uv = new Vector2(u, v);
					hitT = t;
					return true;
				}
			}
		}
		return false;
	}

	public Vector3 GetInverseDirection(Vector3 rayDir)
	{
		return rayDir.Invert();
	}

	public bool IntersectMeshBVH(GPURay ray, int bvhOffset, MeshInstance meshInstance, ref HitInfo hitInfo, bool anyHit)
	{
		const int INVALID_INDEX = 0x76543210;

		//GPURay TempRay = new GPURay();
		int[] traversalStack = new int[64];
		traversalStack[0] = INVALID_INDEX;
		int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).

		//instBVHOffset >= m_nodes.Count说明没有inst
		int nodeAddr = bvhOffset;

		float tmin = ray.tmin;
		float hitT = ray.tmax;

		Vector3 invDir = GetInverseDirection(ray.direction);
 		int stackIndex = 0;
 		int hitIndex = -1;

 		while (nodeAddr != INVALID_INDEX)
 		{
 			while ((uint)nodeAddr < (uint)INVALID_INDEX)
 			{
				GPUBVHNode curNode = m_nodes[nodeAddr];
 				Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);

				//left child ray-bound intersection test
				float tMin = 0;
				//bool traverseChild0 = RayBoundIntersect(rayOrig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out tMin);
				bool traverseChild0 = RayBoundIntersect(ray, invDir, curNode.b0min, curNode.b0max, out tMin);
				//right child ray-bound intersection test
				float tMin1 = 0;
				//bool traverseChild1 = RayBoundIntersect(rayOrig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out tMin1);
				bool traverseChild1 = RayBoundIntersect(ray, invDir, curNode.b1min, curNode.b1max, out tMin1);

				bool swp = (tMin1 < tMin);

				tmin = Mathf.Min(tMin1, tMin);

				if (!traverseChild0 && !traverseChild1)
				{
					nodeAddr = traversalStack[stackIndex];
					stackIndex--;
				}
				// Otherwise => fetch child pointers.
				else
				{
					nodeAddr = (traverseChild0) ? cnodes.x : cnodes.z;
					//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
					//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
					//if (!swp)
					//	swp = primitivesNum2 > 0 && primitivesNum == 0;
					// Both children were intersected => push the farther one.
					if (traverseChild0 && traverseChild1)
					{
						if (swp)
						{
							//swap(nodeAddr, cnodes.y);
							int tmp = nodeAddr;
							nodeAddr = cnodes.z;
							cnodes.z = tmp;
							//tmp = primitivesNum;
							//primitivesNum = primitivesNum2;
							//primitivesNum2 = tmp;
						}
						//stackPtr += 4;
						//stackIndex++;
						//*(int*)stackPtr = cnodes.y;
						traversalStack[++stackIndex] = cnodes.z;
					}
				}

				// First leaf => postpone and continue traversal.
				if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
				{
					//leafAddr2= leafAddr;          // postpone 2
					leafAddr = nodeAddr;            //leafAddr成了当前要处理的节点
					nodeAddr = traversalStack[stackIndex];  //出栈，nodeAddr这个时候是下一个要访问的node
					stackIndex--;
				}

				if (!(leafAddr >= 0))   //leaf node小于0，需要处理叶子节点，退出循环
					break;
			}

			//遍历叶子
			while (leafAddr < 0)
			{
				int triangleIndex = 0;
				for (int triAddr = ~leafAddr; /*triAddr < ~leafAddr + primitivesNum * 3*/; triAddr += 3)
				{
					Vector4 m0 = WoopTriangleData.m_woopTriangleVertices[triAddr];     //matrix row 0 

					if (MathUtil.SingleToInt32Bits(m0.x) == 0x7fffffff)
						break;

					Vector4 m1 = WoopTriangleData.m_woopTriangleVertices[triAddr + 1]; //matrix row 1 
					Vector4 m2 = WoopTriangleData.m_woopTriangleVertices[triAddr + 2]; //matrix row 2

					//Oz is a point, must plus w
					float Oz = m2.w + Vector3.Dot(ray.orig, m2);//origx * m2.x + origy * m2.y + origz * m2.z;
															   //Dz is a vector
					float invDz = 1.0f / Vector3.Dot(ray.direction, m2);//(dirx * m2.x + diry * m2.y + dirz * m2.z);
					float t = -Oz * invDz;

					Vector3 normal = Vector3.Cross(m0, m1).normalized;

					int vertexIndex0 = WoopTriangleData.m_woopTriangleIndices[triAddr];
					int vertexIndex1 = WoopTriangleData.m_woopTriangleIndices[triAddr + 1];
					int vertexIndex2 = WoopTriangleData.m_woopTriangleIndices[triAddr + 2];
					Vector3 v0 = sceneVertices[vertexIndex0].position;
					Vector3 v1 = sceneVertices[vertexIndex1].position;
					if (vertexIndex2 >= sceneVertices.Count)
                    {
						Debug.LogError("vertexIndex2 >= sceneVertices.Count! vertexIndex2 = " + vertexIndex2);
                    }
					Vector3 v2 = sceneVertices[vertexIndex2].position;

					//local normal
					normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;

					//if (Vector3.Dot(normal, ray.direction) >= 0)
					//{
					//	//RenderDebug.DrawNormal(m_worldVertices[triAddr], m_worldVertices[triAddr + 1], m_worldVertices[triAddr + 2], 0.3f, 0.35f);
					//	continue;
					//}

					//if t is in bounding and less than the ray.tMax
					if (t >= tmin && t < hitT)
					{
						// Compute and check barycentric u.
						float Ox = m0.w + Vector3.Dot(ray.orig, m0);//origx * m0.x + origy * m0.y + origz * m0.z;
						float Dx = Vector3.Dot(ray.direction, m0); //dirx * m0.x + diry * m0.y + dirz * m0.z;
						float u = Ox + t * Dx;

						if (u >= 0.0f)
						{
							// Compute and check barycentric v.
							float Oy = m1.w + Vector3.Dot(ray.orig, m1);//origx * m1.x + origy * m1.y + origz * m1.z;
							float Dy = Vector3.Dot(ray.direction, m1);//dirx * m1.x + diry * m1.y + dirz * m1.z;
							float v = Oy + t * Dy;

							if (v >= 0.0f && u + v <= 1.0f)
							{
								Vector3 v0World = meshInstance.localToWorld.MultiplyPoint(v0);
								Vector3 v1World = meshInstance.localToWorld.MultiplyPoint(v1);
								Vector3 v2World = meshInstance.localToWorld.MultiplyPoint(v2);
								// Record intersection.
								// Closest intersection not required => terminate.
								hitT = t;
								hitInfo.hitT = hitT;
								hitInfo.triAddr = new Vector3Int(vertexIndex0, vertexIndex1, vertexIndex2);
								hitInfo.baryCoord = new Vector2(u, v);
								hitInfo.triangleIndexInMesh = triangleIndex;
								hitIndex = triAddr;

								if (anyHit)
									return true;
							}
						}
					}
					triangleIndex++;
				} // triangle

				// Another leaf was postponed => process it as well.
				leafAddr = nodeAddr;
				if (nodeAddr < 0)
				{
					nodeAddr = traversalStack[stackIndex--];
					//primitivesNum = primitivesNum2;
				}
			} // leaf
		}

		//tRay = hitT;
		//return false;
		if (hitIndex != -1)
		{
			//RenderDebug.DrawTriangle(m_worldVertices[hitIndex], m_worldVertices[hitIndex + 1], m_worldVertices[hitIndex + 2], Color.green);

		}
		return hitIndex != -1;
	}

	static float origin() { return 1.0f / 32.0f; }
	static float float_scale() { return 1.0f / 65536.0f; }
	static float int_scale() { return 256.0f; }

	// Normal points outward for rays exiting the surface, else is flipped.
	public static Vector3 offset_ray(Vector3 p, Vector3 n)
	{
		Vector3Int of_i = new Vector3Int((int)(int_scale() * n.x), (int)(int_scale() * n.y), (int)(int_scale() * n.z));

		Vector3 p_i = new Vector3(
			MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.x) + ((p.x < 0) ? -of_i.x : of_i.x)),
			MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.y) + ((p.y < 0) ? -of_i.y : of_i.y)),
			MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.z) + ((p.z < 0) ? -of_i.z : of_i.z)));

		return new Vector3(Mathf.Abs(p.x) < origin()? p.x + float_scale() * n.x : p_i.x,
					  Mathf.Abs(p.y) < origin()? p.y + float_scale() * n.y : p_i.y,
					  Mathf.Abs(p.z) < origin()? p.z + float_scale() * n.z : p_i.z);
	}

	public bool IntersectInstTest(GPURay ray, List<MeshInstance> meshInstances, int instBVHOffset, ref HitInfo hitInfo)
	{
		const int INVALID_INDEX = 0x76543210;

		int[] traversalStack = new int[64];
		traversalStack[0] = INVALID_INDEX;

		//instBVHOffset >= m_nodes.Count说明没有inst
		int nodeAddr = instBVHOffset >= m_nodes.Count ? 0 : instBVHOffset;

		float tmin = ray.tmin;
		float hitT = ray.tmax;  //tmax
							// Ray origin.
							//float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

		//Vector3 invDir = new Vector3(idirx, idiry, idirz);
		Vector3 invDir = GetInverseDirection(ray.direction);
		Vector3 invWorldDir = invDir;

		int stackIndex = 0;   //当前traversalStack的索引号
		MeshInstance meshInstance = new MeshInstance();
		//MeshInstance meshInstanceDebug = new MeshInstance();
		//signX = signX < 0 ? 1 : 0;
		//signY = signY < 0 ? 1 : 0;
		//signZ = signZ < 0 ? 1 : 0;

		//int meshInstanceIndex = 0;
		int hitIndex = -1;
		int hitBVHNode = -1;
		int hitMeshIndex = -1;

		//只有一个mesh的时候
		if (nodeAddr == 0)
		{
			meshInstance = meshInstances[0];
			GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
			//invDir = GetInverseDirection(ray.direction);
			float bvhHit = hitT;
			if (IntersectMeshBVH(rayTemp, 0, meshInstance, ref hitInfo, false))
			{
				hitMeshIndex = 0;
				if (hitInfo.hitT < hitT)
				{
					hitBVHNode = 0;
					hitT = bvhHit;
					hitIndex = hitInfo.triAddr.x;
					hitInfo.meshInstanceId = 0;
				}
			}
		}
		else
		{
			while (nodeAddr != INVALID_INDEX)
			{

				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);

				float t0 = 0;
				//left child ray-bound intersection test
				//bool traverseChild0 = RayBoundIntersect(ray.orig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out t0);
				bool traverseChild0 = RayBoundIntersect(ray, invDir, curNode.b0min, curNode.b0max, out t0);

				float t1 = 0;
				//right child ray-bound intersection test
				//bool traverseChild1 = RayBoundIntersect(ray.orig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out t1);
				bool traverseChild1 = RayBoundIntersect(ray, invDir, curNode.b1min, curNode.b1max, out t1);

				Vector2Int next = GetNodeChildIndex(curNode.cids); //new Vector2Int(INVALID_INDEX, INVALID_INDEX);
				Vector2Int nextMeshInstanceIds = nodeAddr >= instBVHOffset ? GetTopLevelLeaveMeshInstance(curNode.cids) : new Vector2Int(-1, -1);
				if (!traverseChild0)
				{
					next.x = INVALID_INDEX;
					nextMeshInstanceIds.x = -1;
				}
				if (!traverseChild1)
				{
					next.y = INVALID_INDEX;
					nextMeshInstanceIds.y = -1;
				}

				bool swp = false;
				//3 cases after boundrayintersect
				if (traverseChild0 && traverseChild1)
				{
					//两个都命中
					swp = (t1 < t0);
					if (swp)
					{
						next = new Vector2Int(next.y, next.x);
						nextMeshInstanceIds = new Vector2Int(nextMeshInstanceIds.y, nextMeshInstanceIds.x);
					}

					//next.y入栈
					bool curNodeIsX = false;
					if (next.x >= instBVHNodeAddr)
					{
						nodeAddr = next.x;
						curNodeIsX = true;
					}
					else
					{
						if (next.y >= instBVHNodeAddr)
							nodeAddr = next.y;
						else
							nodeAddr = traversalStack[stackIndex--];
					}
					//这里入栈的可能是bottomlevel bvh leaf
					if (next.y >= instBVHNodeAddr && curNodeIsX)
						traversalStack[++stackIndex] = next.y;
					//if (nodeAddr >= instBVHOffset)
					//            {
					//	meshInstanceIndex = nextMeshInstanceIds.x;
					//	meshInstanceStack[stackIndex] = nextMeshInstanceIds.y;
					//}

					//if (0 <= next.y && YieldInstruction < )
				}
				else if (!traverseChild0 && !traverseChild1)
				{
					//两个都不命中
					//meshInstanceIndex = nodeAddr >= instBVHOffset ? meshInstanceStack[stackIndex + 1] : meshInstanceIndex;
					nodeAddr = traversalStack[stackIndex--];

				}
				else
				{
					//只有其中一个命中
					if (nodeAddr >= instBVHOffset)
					{
						//meshInstanceIndex = traverseChild0 ? nextMeshInstanceIds.x : nextMeshInstanceIds.y;
						int nextNode = traverseChild0 ? next.x : next.y;
						if (nextNode >= instBVHNodeAddr)
						{
							nodeAddr = nextNode;
						}
						else
							nodeAddr = traversalStack[stackIndex--];
					}
				}


				for (int i = 0; i < 2; ++i)
				{
					//如果next是bottom level bvh node
					if (0 <= next[i] && next[i] < instBVHNodeAddr)
					{
						meshInstance = meshInstances[nextMeshInstanceIds[i]];
						GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
						//invDir = GetInverseDirection(ray.direction);
						float bvhHit = hitT;
						int meshHitTriangleIndex = -1;
						GPUInteraction tmpInteraction = new GPUInteraction();
						if (IntersectMeshBVH(rayTemp, next[i], meshInstance, ref hitInfo, false))
						{
							if (hitInfo.hitT < hitT)
							{
								hitT = bvhHit;
								hitIndex = hitInfo.triAddr.x;
								hitBVHNode = next[i];
								hitMeshIndex = nextMeshInstanceIds[i];
								tmpInteraction.meshInstanceID = (uint)hitMeshIndex;
								tmpInteraction.materialID = meshInstance.materialIndex;
								//isect.triangleIndex = (uint)meshHitTriangleIndex;
							}
						}
						//else if (nextMeshInstanceIds[i] == 1)
						//{
						//	//Debug.Log("error happen!");
						//	int a = 0;
						//}
					}
				}
			}
		}

		return hitIndex != -1;
	}
	public bool IntersectBVHTriangleTest(GPURay ray, int bvhOffset, out float tRay)
	{
		const int EntrypointSentinel = 0x76543210;
		//Vector4 rayOrig = ray.orig;
		//Vector4 rayDir = ray.direction;
		int[] traversalStack = new int[64];
		traversalStack[0] = EntrypointSentinel;
		int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).
		int nodeAddr = bvhOffset;
		//int primitivesNum = 0;   //当前节点的primitives数量
		//int primitivesNum2 = 0;
		int triIdx = 0;
		float tmin = ray.tmin;
		float hitT = ray.tmax;  //tmax
		Vector3 invDir = GetInverseDirection(ray.direction);
		//float origx = rayOrig.x;
		//float origy = rayOrig.y;
		//float origz = rayOrig.z;            // Ray origin.
		//float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
		//float idirx = 1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps); // inverse ray direction
		//float idiry = 1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps); // inverse ray direction
		//float idirz = 1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps); // inverse ray direction
		//int signX = (int)Mathf.Sign(idirx);
		//int signY = (int)Mathf.Sign(idiry);
		//int signZ = (int)Mathf.Sign(idirz);
		//signX = signX < 0 ? 1 : 0;
		//signY = signY < 0 ? 1 : 0;
		//signZ = signZ < 0 ? 1 : 0;

		//float dirx = rayDir.x;
		//float diry = rayDir.y;
		//float dirz = rayDir.z;
		//float oodx = rayOrig.x * idirx;  // ray origin / ray direction
		//float oody = rayOrig.y * idiry;  // ray origin / ray direction
		//float oodz = rayOrig.z * idirz;  // ray origin / ray direction
		int stackIndex = 0;   //当前traversalStack的索引号
		int hitIndex = -1;

		//这个nodeAddr从哪里来？
		while (nodeAddr != EntrypointSentinel)
		{
			while ((uint)nodeAddr < (uint)EntrypointSentinel)
			{
				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);

				//left child ray-bound intersection test
				float tMin = 0;
				//bool traverseChild0 = RayBoundIntersect(rayOrig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), idirx, idiry, idirz, hitT, out tMin);
				bool traverseChild0 = RayBoundIntersect(ray, invDir, curNode.b0min, curNode.b0max, out tMin);

				//right child ray-bound intersection test
				float tMin1 = 0;
				//bool traverseChild1 = RayBoundIntersect(rayOrig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), idirx, idiry, idirz, hitT, out tMin1);
				bool traverseChild1 = RayBoundIntersect(ray, invDir, curNode.b1min, curNode.b1max, out tMin1);

				bool swp = (tMin1 < tMin);

				tmin = Mathf.Min(tMin1, tMin);

				if (!traverseChild0 && !traverseChild1)
				{
					nodeAddr = traversalStack[stackIndex];
					stackIndex--;
				}
				// Otherwise => fetch child pointers.
				else
				{
					nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;
					//primitivesNum = (traverseChild0) ? cnodes.z : cnodes.w;
					//primitivesNum2 = (traverseChild0) ? cnodes.w : cnodes.z;
					//if (!swp)
					//	swp = primitivesNum2 > 0 && primitivesNum == 0;
					// Both children were intersected => push the farther one.
					if (traverseChild0 && traverseChild1)
					{
						if (swp)
						{
							//swap(nodeAddr, cnodes.y);
							int tmp = nodeAddr;
							nodeAddr = cnodes.y;
							cnodes.y = tmp;
							//tmp = primitivesNum;
							//primitivesNum = primitivesNum2;
							//primitivesNum2 = tmp;
						}
						//stackPtr += 4;
						//stackIndex++;
						//*(int*)stackPtr = cnodes.y;
						traversalStack[++stackIndex] = cnodes.y;
					}
				}

				// First leaf => postpone and continue traversal.
				if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
				{
					//leafAddr2= leafAddr;          // postpone 2
					leafAddr = nodeAddr;            //leafAddr成了当前要处理的节点
					nodeAddr = traversalStack[stackIndex];  //出栈，nodeAddr这个时候是下一个要访问的node
					stackIndex--;
				}

				if (!(leafAddr >= 0))   //leaf node小于0，需要处理叶子节点，退出循环
					break;
			}

			//遍历叶子
			while (leafAddr < 0)
			{
				for (int triAddr = ~leafAddr; ; triAddr += 3)
				{
					Vector4 m0 = WoopTriangleData.m_woopTriangleVertices[triAddr];     //matrix row 0 

					if (MathUtil.SingleToInt32Bits(m0.x) == 0x7fffffff)
						break;

					Vector4 m1 = WoopTriangleData.m_woopTriangleVertices[triAddr + 1]; //matrix row 1 
					Vector4 m2 = WoopTriangleData.m_woopTriangleVertices[triAddr + 2]; //matrix row 2

					Vector3 normal = Vector3.Cross(m0, m1).normalized;
					if (Vector3.Dot(normal, ray.direction) >= 0)
					{
						//RenderDebug.DrawNormal(m_worldVertices[triAddr], m_worldVertices[triAddr + 1], m_worldVertices[triAddr + 2], 0.3f, 0.35f);
						continue;
					}

					Vector2 berycentric = Vector2.zero;
					float t = 0;
					if (WoodTriangleRayIntersect(ray, m0, m1, m2, ref berycentric, out t))
					{
						hitT = t;
						hitIndex = triAddr;
					}
					

				} // triangle

				// Another leaf was postponed => process it as well.
				leafAddr = nodeAddr;
				if (nodeAddr < 0)
				{
					nodeAddr = traversalStack[stackIndex--];
					//primitivesNum = primitivesNum2;
				}
			} // leaf
		}

		tRay = hitT;
		//return false;
		if (hitIndex != -1)
		{
			RenderDebug.DrawTriangle(sceneVertices[WoopTriangleData.m_woopTriangleIndices[hitIndex]].position,
				sceneVertices[WoopTriangleData.m_woopTriangleIndices[hitIndex + 1]].position,
				sceneVertices[WoopTriangleData.m_woopTriangleIndices[hitIndex + 2]].position, Color.green);

		}
		return hitIndex != -1;
	}

	public bool IntersectInstTestP(GPURay ray, List<MeshInstance> meshInstances, int instBVHOffset, out float hitT, out int meshInstanceIndex)
    {
		const int INVALID_INDEX = 0x76543210;

		int[] traversalStack = new int[64];
		traversalStack[0] = INVALID_INDEX;

		//instBVHOffset >= m_nodes.Count说明没有inst
		int nodeAddr = instBVHOffset >= m_nodes.Count ? 0 : instBVHOffset;

		float tmin = ray.tMin;
		hitT = ray.tMax;  //tmax
							// Ray origin.
							//float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

		//Vector3 invDir = new Vector3(idirx, idiry, idirz);
		Vector3 invDir = GetInverseDirection(ray.direction);
		Vector3 invWorldDir = invDir;

		int stackIndex = 0;   //当前traversalStack的索引号
		MeshInstance meshInstance = new MeshInstance();
		//MeshInstance meshInstanceDebug = new MeshInstance();
		int signX = invDir.x < 0 ? 1 : 0;
		int signY = invDir.y < 0 ? 1 : 0;
		int signZ = invDir.z < 0 ? 1 : 0;
		Vector3Int signs = new Vector3Int(signX, signY, signZ);

		//int meshInstanceIndex = 0;
		int hitIndex = -1;
		int hitBVHNode = -1;
		int hitMeshIndex = -1;

		//只有一个mesh的时候
		if (nodeAddr == 0)
		{
			meshInstance = meshInstances[0];
			GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
			//invDir = GetInverseDirection(ray.direction);
			float bvhHit = hitT;
			int meshHitTriangleIndex = -1;
			//GPUInteraction isect = new GPUInteraction();
			HitInfo hitInfo = new HitInfo();
			if (IntersectMeshBVH(rayTemp, 0, meshInstance, ref hitInfo, false))
			{
				hitMeshIndex = 0;
				if (hitInfo.hitT < hitT)
				{
					hitBVHNode = 0;
					hitT = bvhHit;
					hitIndex = meshHitTriangleIndex;
				}
			}
		}
		else
		{
			while (nodeAddr != INVALID_INDEX)
			{

				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);

				float t0 = 0;
				//left child ray-bound intersection test
				//bool traverseChild0 = RayBoundIntersect(ray.orig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out t0);
				bool traverseChild0 = RayBoundIntersect(ray, invDir, curNode.b0min, curNode.b0max, out t0);
				
				float t1 = 0;
				//right child ray-bound intersection test
				//bool traverseChild1 = RayBoundIntersect(ray.orig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out t1);
				bool traverseChild1 = RayBoundIntersect(ray, invDir, curNode.b1min, curNode.b1max, out t1);

				Vector2Int next = GetNodeChildIndex(curNode.cids); //new Vector2Int(INVALID_INDEX, INVALID_INDEX);
				Vector2Int nextMeshInstanceIds = nodeAddr >= instBVHOffset ? GetTopLevelLeaveMeshInstance(curNode.cids) : new Vector2Int(-1, -1);
				if (!traverseChild0)
				{
					next.x = INVALID_INDEX;
					nextMeshInstanceIds.x = -1;
				}
				if (!traverseChild1)
				{
					next.y = INVALID_INDEX;
					nextMeshInstanceIds.y = -1;
				}

				bool swp = false;
				//3 cases after boundrayintersect
				if (traverseChild0 && traverseChild1)
				{
					//两个都命中
					swp = (t1 < t0);
					if (swp)
					{
						next = new Vector2Int(next.y, next.x);
						nextMeshInstanceIds = new Vector2Int(nextMeshInstanceIds.y, nextMeshInstanceIds.x);
					}

					//next.y入栈
					bool curNodeIsX = false;
					if (next.x >= instBVHNodeAddr)
					{
						nodeAddr = next.x;
						curNodeIsX = true;
					}
					else
					{
						if (next.y >= instBVHNodeAddr)
							nodeAddr = next.y;
						else
							nodeAddr = traversalStack[stackIndex--];
					}
					//这里入栈的可能是bottomlevel bvh leaf
					if (next.y >= instBVHNodeAddr && curNodeIsX)
						traversalStack[++stackIndex] = next.y;
					//if (nodeAddr >= instBVHOffset)
					//            {
					//	meshInstanceIndex = nextMeshInstanceIds.x;
					//	meshInstanceStack[stackIndex] = nextMeshInstanceIds.y;
					//}

					//if (0 <= next.y && YieldInstruction < )
				}
				else if (!traverseChild0 && !traverseChild1)
				{
					//两个都不命中
					//meshInstanceIndex = nodeAddr >= instBVHOffset ? meshInstanceStack[stackIndex + 1] : meshInstanceIndex;
					nodeAddr = traversalStack[stackIndex--];

				}
				else
				{
					//只有其中一个命中
					if (nodeAddr >= instBVHOffset)
					{
						//meshInstanceIndex = traverseChild0 ? nextMeshInstanceIds.x : nextMeshInstanceIds.y;
						int nextNode = traverseChild0 ? next.x : next.y;
						if (nextNode >= instBVHNodeAddr)
						{
							nodeAddr = nextNode;
						}
						else
							nodeAddr = traversalStack[stackIndex--];
					}
				}


				for (int i = 0; i < 2; ++i)
				{
					//如果next是bottom level bvh node
					if (0 <= next[i] && next[i] < instBVHNodeAddr)
					{
						meshInstance = meshInstances[nextMeshInstanceIds[i]];
						GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
						//invDir = GetInverseDirection(ray.direction);
						float bvhHit = hitT;
						int meshHitTriangleIndex = -1;
						GPUInteraction isect;
						//if (IntersectMeshBVH(rayTemp.orig, rayTemp.direction, next[i], meshInstance, out bvhHit, out meshHitTriangleIndex, out isect))
						if (IntersectMeshBVHP(rayTemp, next[i], out bvhHit, out meshHitTriangleIndex))
						{
							if (bvhHit < hitT)
							{
								hitT = bvhHit;
								hitIndex = meshHitTriangleIndex;
								hitBVHNode = next[i];
								hitMeshIndex = nextMeshInstanceIds[i];
							}
						}
					//	else if (nextMeshInstanceIds[i] == 1)
					//	{
					//		//Debug.Log("error happen!");
					//		int a = 0;
					//	}
					//	else
     //                   {
					//		int a = 0;
     //                   }
					}
				}
			}
		}



		//for (int i = 0; i < meshInstances.Count; ++i)
		if (hitBVHNode >= 0)
		{
			MeshInstance meshInstanceTmp = meshInstances[hitMeshIndex];
			//MeshHandle meshHandle = meshHandles[meshInstanceTmp.meshHandleIndex];
			//Vector3 worldBoundMin = Vector3.zero;
			//Vector3 worldBoundMax = Vector3.zero;
			//GPUBounds.TransformBounds(ref meshInstanceTmp.localToWorld, meshHandle.localBounds.min, meshHandle.localBounds.max, out worldBoundMin, out worldBoundMax);
			//RenderDebug.DrawDebugBound(worldBoundMin, worldBoundMax, Color.white);

			if (hitIndex != -1)
			{
				int triAddrDebug = hitIndex;
				RenderDebug.DrawTriangle(meshInstanceTmp.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[triAddrDebug]].position),
					meshInstanceTmp.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[triAddrDebug + 1]].position),
					meshInstanceTmp.localToWorld.MultiplyPoint(sceneVertices[WoopTriangleData.m_woopTriangleIndices[triAddrDebug + 2]].position), Color.green);
			}
		}
		meshInstanceIndex = hitMeshIndex;
		return hitIndex != -1;
	}

	public bool IntersectMeshBVHP(GPURay ray, int bvhOffset, out float hitT, out int hitIndex)
	{
		int INVALID_INDEX = 0x76543210;

		//GPURay TempRay = new GPURay();
		int[] traversalStack = new int[64];
		traversalStack[0] = INVALID_INDEX;
		int leafAddr = 0;               // If negative, then first postponed leaf, non-negative if no leaf (innernode).

		//instBVHOffset >= m_nodes.Count说明没有inst
		int nodeAddr = bvhOffset;
		float tmin = ray.tmin;
		hitT = ray.tmax;

		float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number

		Vector3 invDir = GetInverseDirection(ray.direction);

		int stackIndex = 0;
		hitIndex = -1;

		while (nodeAddr != INVALID_INDEX)
		{
			while ((uint)nodeAddr < (uint)INVALID_INDEX)
			{
				GPUBVHNode curNode = m_nodes[nodeAddr];
				Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);

				//left child ray-bound intersection test
				float tMin = 0;
				//bool traverseChild0 = RayBoundIntersect(rayOrig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out tMin);
				bool traverseChild0 = RayBoundIntersect(ray, invDir, curNode.b0min, curNode.b0max, out tMin);
				//right child ray-bound intersection test
				float tMin1 = 0;
				//bool traverseChild1 = RayBoundIntersect(rayOrig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out tMin1);
				bool traverseChild1 = RayBoundIntersect(ray, invDir, curNode.b1min, curNode.b1max, out tMin1);


				bool swp = (tMin1 < tMin);

				tmin = Mathf.Min(tMin1, tMin);

				if (!traverseChild0 && !traverseChild1)
				{
					nodeAddr = traversalStack[stackIndex];
					stackIndex--;
				}
				// Otherwise => fetch child pointers.
				else
				{
					nodeAddr = (traverseChild0) ? cnodes.x : cnodes.y;

					if (traverseChild0 && traverseChild1)
					{
						if (swp)
						{
							//swap(nodeAddr, cnodes.y);
							int tmp = nodeAddr;
							nodeAddr = cnodes.y;
							cnodes.y = tmp;
						}
						traversalStack[++stackIndex] = cnodes.y;
					}
				}

				// First leaf => postpone and continue traversal.
				if (nodeAddr < 0 && leafAddr >= 0)     // Postpone max 1
				{
					//leafAddr2= leafAddr;          // postpone 2
					leafAddr = nodeAddr;            //leafAddr成了当前要处理的节点
					nodeAddr = traversalStack[stackIndex];  //出栈，nodeAddr这个时候是下一个要访问的node
					stackIndex--;
				}

				if (!(leafAddr >= 0))   //leaf node小于0，需要处理叶子节点，退出循环
					break;
			}

			//遍历叶子
			while (leafAddr < 0)
			{
				for (int triAddr = ~leafAddr; ; triAddr += 3)
				{
					Vector4 m0 = WoopTriangleData.m_woopTriangleVertices[triAddr];     //matrix row 0 

					if (MathUtil.SingleToInt32Bits(m0.x) == 0x7fffffff)
						break;

					Vector4 m1 = WoopTriangleData.m_woopTriangleVertices[triAddr + 1]; //matrix row 1 
					Vector4 m2 = WoopTriangleData.m_woopTriangleVertices[triAddr + 2]; //matrix row 2

					

					Vector3 normal = Vector3.Normalize(Vector3.Cross(m0, m1));
					if (Vector3.Dot(normal, ray.direction) >= 0)
					{
      //                  int vertexIndex0 = m_woodTriangleIndices[triAddr];
      //                  int vertexIndex1 = m_woodTriangleIndices[triAddr + 1];
      //                  int vertexIndex2 = m_woodTriangleIndices[triAddr + 2];
      //                  Vector3 v0 = sceneVertices[vertexIndex0].position;
      //                  Vector3 v1 = sceneVertices[vertexIndex1].position;
      //                  Vector3 v2 = sceneVertices[vertexIndex2].position;
						//normal = Vector3.Cross(v1 - v0, v2 - v0).normalized;
						//if (Vector3.Dot(normal, rayDir) >= 0)
							continue;
					}

					Vector2 uv = Vector2.zero;
					float triangleHitT = 0;
					bool hitTriangle = WoodTriangleRayIntersect(ray, m0, m1, m2, ref uv, out triangleHitT);
					if (hitTriangle)
					{
						hitT = triangleHitT;
						hitIndex = triAddr;
						//int vertexIndex0 = WoodTriangleIndices[triAddr];
						//int vertexIndex1 = WoodTriangleIndices[triAddr + 1];
						//int vertexIndex2 = WoodTriangleIndices[triAddr + 2];
						//const float4 v0 = Vertices[vertexIndex0].position;
						//const float4 v1 = Vertices[vertexIndex1].position;
						//const float4 v2 = Vertices[vertexIndex2].position;
						//hitIndex = vertexIndex0 / 3;
					}

				} // triangle

				// Another leaf was postponed => process it as well.
				leafAddr = nodeAddr;
				if (nodeAddr < 0)
				{
					nodeAddr = traversalStack[stackIndex--];
				}
			} // leaf
		}

		return hitIndex != -1;
	}

	public void CompactTLASNodes(LinearBVHNode[] bvhNodes, Primitive[] primitives, List<int> botomLevelOffset)
	{
		List<GPUBVHNode> nodes = new List<GPUBVHNode>(m_nodes);
		nodes.Add(new GPUBVHNode());
		List<StackEntry> stack = new List<StackEntry>();
		stack.Add(new StackEntry(bvhNodes[0], m_nodes.Count));
		GPUBounds b0 = new GPUBounds();
		GPUBounds b1 = new GPUBounds();

		while (stack.Count > 0)
		{
			int c0 = 0;  //left child offset in nodes array if it's not a leaf, else is the blas node offset
			int c1 = 0;  //meshInstanceIndex of left child if is a leaf, else is -1 
			int c2 = 0;  //right child offset in nodes array if it's not a leaf, else is the blas node offset
			int c3 = 0;  //meshInstanceIndex of right child if is a leaf, else is -1 

			StackEntry e = stack[stack.Count - 1];
			stack.RemoveAt(stack.Count - 1);

			if (e.node.IsLeaf())
			{
                const int EntrypointSentinel = 0x76543210;
                int INVALID_INDEX = EntrypointSentinel;
                Primitive primitive = primitives[e.node.firstPrimOffset];

                c0 = botomLevelOffset[primitive.meshIndex];
                c1 = primitive.meshInstIndex;
                c2 = INVALID_INDEX;
                c3 = 0;

                GPUBVHNode gpuBVH = new GPUBVHNode();
                b0 = e.node.bounds;//e.node.childrenLeft.bounds;
                b1 = GPUBounds.DefaultBounds();//e.node.childrenRight.bounds;

                //gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
                //gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
                //gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);
                gpuBVH.b0min = b0.min;
                gpuBVH.b0max = b0.max;
                gpuBVH.b1min = b1.min;
                gpuBVH.b1max = b1.max;

                gpuBVH.cids = new Vector4(MathUtil.Int32BitsToSingle(c0), MathUtil.Int32BitsToSingle(c1), MathUtil.Int32BitsToSingle(c2), MathUtil.Int32BitsToSingle(c3));
                nodes[e.idx] = gpuBVH;
            }
			else
			{
                //left child
                LinearBVHNode leftChild = bvhNodes[e.node.leftChildIdx];
                if (!leftChild.IsLeaf())
                {
                    c0 = nodes.Count;//++nextNodeIdx;
                    stack.Add(new StackEntry(leftChild, c0));
                    nodes.Add(new GPUBVHNode());
                    c1 = -1;  //means that lefchild is not a leaf
                }
                else
                {
                    Primitive primitive = primitives[leftChild.firstPrimOffset];

                    c0 = botomLevelOffset[primitive.meshIndex];
                    c1 = primitive.meshInstIndex;
                }

                //right child
                LinearBVHNode rightChild = bvhNodes[e.node.rightChildIdx];
                if (!rightChild.IsLeaf())
                {
                    c2 = nodes.Count;//++nextNodeIdx;
                    stack.Add(new StackEntry(rightChild, c2));
                    nodes.Add(new GPUBVHNode());
                    c3 = -1;  //means that rightChild is not leaf node.
                }
                else
                {
                    Primitive primitive = primitives[rightChild.firstPrimOffset];

                    c2 = botomLevelOffset[primitive.meshIndex];
                    c3 = primitive.meshInstIndex;
                }

                GPUBVHNode gpuBVH = new GPUBVHNode();
                b0 = leftChild.bounds;//e.node.childrenLeft.bounds;
                b1 = rightChild.bounds;//e.node.childrenRight.bounds;


                gpuBVH.b0min = b0.min;
                gpuBVH.b0max = b0.max;
                gpuBVH.b1min = b1.min;
                gpuBVH.b1max = b1.max;

                gpuBVH.cids = new Vector4(MathUtil.Int32BitsToSingle(c0), MathUtil.Int32BitsToSingle(c1), MathUtil.Int32BitsToSingle(c2), MathUtil.Int32BitsToSingle(c3));
                nodes[e.idx] = gpuBVH;
            } 
		}

        m_nodes = nodes;
	}

	public void CompactBLASNodes(LinearBVHNode[] bvhNodes, Primitive[] primitives, List<GPUVertex> gpuVertices)
    {
		const int EntrypointSentinel = 0x76543210;
		int INVALID_INDEX = EntrypointSentinel;
		List<GPUBVHNode> nodes = new List<GPUBVHNode>(m_nodes);
		nodes.Add(new GPUBVHNode());
		//int nextNodeIdx = 0;
		List<StackEntry> stack = new List<StackEntry>();
		stack.Add(new StackEntry(bvhNodes[0], m_nodes.Count));
		GPUBounds b0 = new GPUBounds();
		GPUBounds b1 = new GPUBounds();

		while (stack.Count > 0)
		{
			int c0 = 0;  //bottomlevel bvh: left child vertices offset; toplevel bvh: 
			int c1 = 0;  //bottomlevel bvh: right child vertices offset; toplevel bvh: 
			int c2 = 0;  //bottomlevel bvh: left child primtives num; toplevel bvh: left child bottomlevel bvh offset
			int c3 = 0;  //bottomlevel bvh: right child primitves num
			StackEntry e = stack[stack.Count - 1];
			stack.RemoveAt(stack.Count - 1);

			if( e.node.IsLeaf())
			{ 
				c0 = ~WoopTriangleData.m_woopTriangleVertices.Count;
				//BVHBuildNode child = e.node.childrenLeft;
				for (int i = e.node.firstPrimOffset; i < e.node.firstPrimOffset + e.node.nPrimitives; ++i)
				{
					WoopTriangleData.UnitTriangle(i, gpuVertices, primitives);
					Primitive primitive = primitives[i];

					for (int v = 0; v < 3; ++v)
					{
						WoopTriangleData.m_woopTriangleVertices.Add(WoopTriangleData.m_woop[v]);
						WoopTriangleData.m_woopTriangleIndices.Add(primitive.triIndices[v]);
						//Vector4 worldPos = gpuVertices[primitive.triIndices[v]].position;
						//Vector4 uv = gpuVertices[primitive.triIndices[v]].uv;
						//if (v == 0)
						//	worldPos.w = Int32BitsToSingle(primitive.materialIndex);

						//m_vertices.Add(new GPUVertex(worldPos, uv));
					}
				}
				WoopTriangleData.m_woopTriangleVertices.Add(new Vector4(MathUtil.Int32BitsToSingle(int.MaxValue), 0, 0, 0));
				WoopTriangleData.m_woopTriangleIndices.Add(int.MaxValue);
				//m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
				//c2 = child.nPrimitives;
				Primitive primitiveCur = primitives[e.node.firstPrimOffset];
				c1 = -2;
				c2 = INVALID_INDEX;
				c3 = 0;

				GPUBVHNode gpuBVH = new GPUBVHNode();
				b0 = e.node.bounds;//e.node.childrenLeft.bounds;
				b1 = GPUBounds.DefaultBounds();//e.node.childrenRight.bounds;

				//gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
				//gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
				//gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);
				gpuBVH.b0min = b0.min;
				gpuBVH.b0max = b0.max;
				gpuBVH.b1min = b1.min;
				gpuBVH.b1max = b1.max;

				gpuBVH.cids = new Vector4(MathUtil.Int32BitsToSingle(c0), MathUtil.Int32BitsToSingle(c1), MathUtil.Int32BitsToSingle(c2), MathUtil.Int32BitsToSingle(c3));
				nodes[e.idx] = gpuBVH;
			}
			else
			{
				//left child
				if (e.node.leftChildIdx >= bvhNodes.Length || e.node.leftChildIdx < 0)
                {
					Debug.LogError("bvh Size Error!");
                }
				LinearBVHNode leftChild = bvhNodes[e.node.leftChildIdx];
				if (!leftChild.IsLeaf())
				{
					c0 = nodes.Count;//++nextNodeIdx;
					stack.Add(new StackEntry(leftChild, c0));
					nodes.Add(new GPUBVHNode());
					c1 = -1;  //means that lefchild is not a leaf
				}
				else
				{
					c0 = ~WoopTriangleData.m_woopTriangleVertices.Count;
					//BVHBuildNode child = e.node.childrenLeft;
					//处理三角形
					for (int i = leftChild.firstPrimOffset; i < leftChild.firstPrimOffset + leftChild.nPrimitives; ++i)
					{
						//把三角形每个顶点按顺序写入buffer里，保证了每个三角形的索引是连续的
						WoopTriangleData.UnitTriangle(i, gpuVertices, primitives);
						Primitive primitive = primitives[i];

						for (int v = 0; v < 3; ++v)
						{
							WoopTriangleData.m_woopTriangleVertices.Add(WoopTriangleData.m_woop[v]);
							WoopTriangleData.m_woopTriangleIndices.Add(primitive.triIndices[v]);
							//Vector4 worldPos = gpuVertices[primitive.triIndices[v]].position;
							//Vector4 uv = gpuVertices[primitive.triIndices[v]].uv;
							//if (v == 0)
							//	worldPos.w = Int32BitsToSingle(primitive.materialIndex);

							//m_vertices.Add(new GPUVertex(worldPos, uv));
						}
					}
					WoopTriangleData.m_woopTriangleVertices.Add(new Vector4(MathUtil.Int32BitsToSingle(int.MaxValue), 0, 0, 0));
					WoopTriangleData.m_woopTriangleIndices.Add(int.MaxValue);
					//m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
					//c2 = child.nPrimitives;
					Primitive primitiveCur = primitives[leftChild.firstPrimOffset];
					c1 = -2;  //is a 
				}

				//right child
				LinearBVHNode rightChild = bvhNodes[e.node.rightChildIdx];
				if (!rightChild.IsLeaf())
				{
					c2 = nodes.Count;//++nextNodeIdx;
					stack.Add(new StackEntry(rightChild, c2));
					nodes.Add(new GPUBVHNode());
					c3 = -1;  //means that rightChild is not leaf node.
				}
				else
				{
					c2 = ~WoopTriangleData.m_woopTriangleVertices.Count;

					for (int i = rightChild.firstPrimOffset; i < rightChild.firstPrimOffset + rightChild.nPrimitives; ++i)
					{
						WoopTriangleData.UnitTriangle(i, gpuVertices, primitives);
						Primitive primitive = primitives[i];
						for (int v = 0; v < 3; ++v)
						{
							WoopTriangleData.m_woopTriangleVertices.Add(WoopTriangleData.m_woop[v]);
							WoopTriangleData.m_woopTriangleIndices.Add(primitive.triIndices[v]);
							//m_vertices.Add(new GPUVertex(gpuVertices[primitive.triIndices[v]].position, gpuVertices[primitive.triIndices[v]].uv));
						}

					}
					WoopTriangleData.m_woopTriangleVertices.Add(new Vector4(MathUtil.Int32BitsToSingle(int.MaxValue), 0, 0, 0));
					WoopTriangleData.m_woopTriangleIndices.Add(int.MaxValue);
					//m_vertices.Add(new GPUVertex(new Vector4(Int32BitsToSingle(int.MaxValue), 0, 0, 0), Vector4.zero));
					Primitive primitiveCur = primitives[rightChild.firstPrimOffset];
					c3 = -2;
				}

				//添加结束
				GPUBVHNode gpuBVH = new GPUBVHNode();
				b0 = leftChild.bounds;//e.node.childrenLeft.bounds;
				b1 = rightChild.bounds;//e.node.childrenRight.bounds;

				//gpuBVH.b0xy = new Vector4(b0.min.x, b0.max.x, b0.min.y, b0.max.y);
				//gpuBVH.b1xy = new Vector4(b1.min.x, b1.max.x, b1.min.y, b1.max.y);
				//gpuBVH.b01z = new Vector4(b0.min.z, b0.max.z, b1.min.z, b1.max.z);
				gpuBVH.b0min = b0.min;
				gpuBVH.b0max = b0.max;
				gpuBVH.b1min = b1.min;
				gpuBVH.b1max = b1.max;

				gpuBVH.cids = new Vector4(MathUtil.Int32BitsToSingle(c0), MathUtil.Int32BitsToSingle(c1), MathUtil.Int32BitsToSingle(c2), MathUtil.Int32BitsToSingle(c3));
				nodes[e.idx] = gpuBVH;
			}
		}

		m_nodes = nodes;
	}

	public bool BVHHit(GPURay ray, ref HitInfo hitInfo, bool anyHit, List<MeshInstance> meshInstances, int instBVHOffset)
	{
		float hitT = float.MaxValue;
		hitInfo.triAddr = new Vector3Int(-1, -1, -1);
		const int EntrypointSentinel = 0x76543210;
		int INVALID_INDEX = EntrypointSentinel;

		//GPURay TempRay = new GPURay();
		int[] traversalStack = new int[64];
		int stackIndex = 0;
		traversalStack[stackIndex++] = INVALID_INDEX;
		int curIndex = instBVHOffset >= m_nodes.Count ? 0 : instBVHOffset;
		bool blas = false;
		float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
		Vector3 rayDir = ray.direction;
		Vector3 invDir = new Vector3(1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps),
			1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps),
			1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps)
			);
		
		int hitIndex = -1;

		while (curIndex != INVALID_INDEX)
		{
			GPUBVHNode curNode = m_nodes[curIndex];
			curIndex = INVALID_INDEX;
			Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);
			int leftNode = cnodes.x;
			int leftNodeMask = cnodes.y;
			int rightNode = cnodes.z;
			int rightNodeMask = cnodes.w;
			float tLeftChildHit = 0;
			//left child ray-bound intersection test
			//bool traverseChild0 = RayBoundIntersect(ray.orig, curNode.b0xy, new Vector2(curNode.b01z.x, curNode.b01z.y), invDir.x, invDir.y, invDir.z, hitT, out t0);
			bool traverseChild0 = RayBoundIntersect(ray, invDir, curNode.b0min, curNode.b0max, out tLeftChildHit);

			float tRightChildHit = 0;
			//right child ray-bound intersection test
			//bool traverseChild1 = RayBoundIntersect(ray.orig, curNode.b1xy, new Vector2(curNode.b01z.z, curNode.b01z.w), invDir.x, invDir.y, invDir.z, hitT, out t1);
			bool traverseChild1 = RayBoundIntersect(ray, invDir, curNode.b1min, curNode.b1max, out tRightChildHit);
			bool isLeftChildLeaf = leftNodeMask >= 0;
			bool isRightChildLeaf = rightNodeMask >= 0;
			Vector2Int leafNode = new Vector2Int(INVALID_INDEX, INVALID_INDEX);
			Vector2Int leafNodeMask = new Vector2Int(-1, -1);

			if (tLeftChildHit > 0.0 && tRightChildHit > 0.0)
			{
				int deferred = INVALID_INDEX;
				if (tLeftChildHit > tRightChildHit)
				{
					curIndex = isRightChildLeaf ? INVALID_INDEX : rightNode;
					deferred = leftNode;
					leafNode[0] = isRightChildLeaf ? rightNode : INVALID_INDEX;
					leafNodeMask[0] = isRightChildLeaf ? rightNodeMask : -1;
					leafNode[1] = isLeftChildLeaf ? deferred : INVALID_INDEX;
					leafNodeMask[1] = isLeftChildLeaf ? leftNodeMask : -1;
				}
				else
				{
					curIndex = isLeftChildLeaf ? INVALID_INDEX : leftNode;
					deferred = rightNode;
					leafNode[0] = isLeftChildLeaf ? leftNode : INVALID_INDEX;
					leafNodeMask[0] = isLeftChildLeaf ? leftNodeMask : -1;
					leafNode[1] = isRightChildLeaf ? deferred : INVALID_INDEX;
					leafNodeMask[1] = isRightChildLeaf ? rightNodeMask : -1;
				}

				if (leafNode[1] == INVALID_INDEX)
					traversalStack[stackIndex++] = deferred;
				if (leafNode[0] == INVALID_INDEX && leafNode[1] == INVALID_INDEX)
					continue;
			}
			else if (tLeftChildHit > 0.0f)
			{
				if (!isLeftChildLeaf)
				{
					curIndex = leftNode;
					continue;
				}
				else
				{
					leafNode[0] = leftNode;
					leafNodeMask[0] = leftNodeMask;
				}
			}
			else if (tRightChildHit > 0.0f)
			{
				if (!isRightChildLeaf)
				{
					curIndex = rightNode;
					continue;
				}
				else
				{
					leafNode[0] = rightNode;
					leafNodeMask[0] = rightNodeMask;
				}
			}

			if (curIndex == INVALID_INDEX)
				curIndex = traversalStack[--stackIndex];

			//case 1: tlas leafnode
			for (int i = 0; i < 2; ++i)
			{
				if (leafNode[i] != INVALID_INDEX)
				{
					MeshInstance meshInstance = meshInstances[leafNodeMask[i]];
					GPURay rayTemp = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
					//invDir = GetInverseDirection(ray.direction);
					//float bvhHit = hitT;
					int meshHitTriangleIndex = -1;
					HitInfo tmpHitInfo = new HitInfo();
					//tmpHitInfo.hitT = hitT;
					tmpHitInfo.triAddr = new Vector3Int(-1, -1, -1);
					//GPUInteraction isect = new GPUInteraction();
					if (IntersectMeshBVH(rayTemp, leafNode[i], meshInstance, ref tmpHitInfo, anyHit))
					{
						if (tmpHitInfo.hitT < hitT)
						{
							tmpHitInfo.meshInstanceId = leafNodeMask[i];
							hitT = tmpHitInfo.hitT;
							hitIndex = tmpHitInfo.triAddr.x;
							hitInfo = tmpHitInfo;

							if (anyHit)
								return true;
						}
					}
				}
			}

		}

		return hitIndex > -1;
	}


	public bool BVHHit2(GPURay ray, ref HitInfo hitInfo, bool anyHit, List<MeshInstance> meshInstances, int instBVHOffset)
    {
		const int NODE_PARENT = 0;
		const int NODE_TLAS_LEAF = 1;
		const int NODE_BLAS_LEAF = 2;
		float hitT = ray.tmax;
		//hitInfo = (HitInfo)0;
		hitInfo.triAddr = new Vector3Int(-1, -1, -1);
		const int EntrypointSentinel = 0x76543210;
		int INVALID_INDEX = EntrypointSentinel;

		//GPURay TempRay = new GPURay();
		Vector3Int[] traversalStack = new Vector3Int[64];
		int stackIndex = 0;
		traversalStack[stackIndex++] = new Vector3Int(INVALID_INDEX, NODE_PARENT, -1);
		int curIndex = instBVHOffset;
		//bool blas = false;
		float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
		Vector3 rayDir = ray.direction;
		Vector3 invDir = new Vector3(1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps),
			1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps),
			1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps)
			);

		int hitIndex = -1;
		GPURay rayTrans = ray;
		int meshInstanceID = -1;
		int curNodeCase = NODE_PARENT;
		
		bool blas = false;

		while (curIndex != INVALID_INDEX)
		{
			GPUBVHNode curNode = m_nodes[curIndex];
			curIndex = INVALID_INDEX;
			Vector4Int cnodes = MathUtil.SingleToInt32Bits(curNode.cids);
			int leftNode = cnodes.x;
			int leftNodeMask = cnodes.y;
			int rightNode = cnodes.z;
			int rightNodeMask = cnodes.w;
			Vector2Int leafNode = new Vector2Int(INVALID_INDEX, INVALID_INDEX);
			Vector2Int leafNodeMask = new Vector2Int(-1, -1);

			if (curNodeCase == NODE_TLAS_LEAF)
			{
				MeshInstance meshInstance = meshInstances[meshInstanceID/*leafNodeMask[i]*/];
				rayTrans = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);
				rayDir = rayTrans.direction;
				invDir = new Vector3(1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps),
					1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps),
					1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps)
					);
				traversalStack[stackIndex++] = new Vector3Int(INVALID_INDEX, NODE_PARENT, -1);
				blas = true;
			}

			if (curNodeCase == NODE_PARENT || curNodeCase == NODE_TLAS_LEAF)
            {
				float tLeftChildHit = 0;

				bool traverseChild0 = RayBoundIntersect(rayTrans, invDir, curNode.b0min, curNode.b0max, out tLeftChildHit);

				float tRightChildHit = 0;

				bool traverseChild1 = RayBoundIntersect(rayTrans, invDir, curNode.b1min, curNode.b1max, out tRightChildHit);
				int leftChildCase = leftNodeMask >= 0 ? NODE_TLAS_LEAF : (leftNodeMask == -1 ? NODE_PARENT : NODE_BLAS_LEAF);
				int rightChildCase = rightNodeMask >= 0 ? NODE_TLAS_LEAF : (rightNodeMask == -1 ? NODE_PARENT : NODE_BLAS_LEAF);


				if (tLeftChildHit > 0.0 && tRightChildHit > 0.0)
				{
					int deferred = INVALID_INDEX;
					int deferredCase = NODE_PARENT;
					int deferredMeshInstanceId = -1;
					if (tLeftChildHit > tRightChildHit)
					{
						//right node first
						curIndex = rightChildCase == NODE_BLAS_LEAF ? INVALID_INDEX : rightNode;
						curNodeCase = rightChildCase;
						deferred = leftNode;
						deferredCase = leftChildCase;
						deferredMeshInstanceId = leftNodeMask;
						leafNode[0] = rightChildCase == NODE_BLAS_LEAF ? rightNode : INVALID_INDEX;
						//leafNodeMask[0] = isRightChildLeaf ? rightNodeMask : -1;
						leafNode[1] = leftChildCase == NODE_BLAS_LEAF ? deferred : INVALID_INDEX;
						//leafNodeMask[1] = isLeftChildLeaf ? leftNodeMask : -1;
						if (rightChildCase == NODE_TLAS_LEAF)
							meshInstanceID = rightNodeMask;
					}
					else
					{
						//left node first
						curIndex = leftChildCase == NODE_BLAS_LEAF ? INVALID_INDEX : leftNode;
						curNodeCase = leftChildCase;
						deferred = rightNode;
						deferredCase = rightChildCase;
						deferredMeshInstanceId = rightNodeMask;
						leafNode[0] = leftChildCase == NODE_BLAS_LEAF ? leftNode : INVALID_INDEX;
						//leafNodeMask[0] = isLeftChildLeaf ? leftNodeMask : -1;
						leafNode[1] = rightChildCase == NODE_BLAS_LEAF ? deferred : INVALID_INDEX;
						//leafNodeMask[1] = isRightChildLeaf ? rightNodeMask : -1;
						if (leftChildCase == NODE_TLAS_LEAF)
							meshInstanceID = leftNodeMask;
					}

					if (leafNode[1] == INVALID_INDEX)
						traversalStack[stackIndex++] = new Vector3Int(deferred, deferredCase, deferredMeshInstanceId);
					if (leafNode[0] == INVALID_INDEX && leafNode[1] == INVALID_INDEX)
						continue;
				}
				else if (tLeftChildHit > 0)
				{
					curNodeCase = leftChildCase;
					if (leftChildCase == NODE_BLAS_LEAF)
					{
						leafNode[0] = leftNode;
						//leafNodeMask[0] = leftNodeMask;
					}
					else
					{
						curIndex = leftNode;
						if (leftChildCase == NODE_TLAS_LEAF)
							meshInstanceID = leftNodeMask;
						continue;
					}
				}
				else if (tRightChildHit > 0)
				{
					curNodeCase = rightChildCase;
					if (rightChildCase == NODE_BLAS_LEAF)
					{
						leafNode[0] = rightNode;
						leafNodeMask[0] = rightNodeMask;

					}
					else
					{
						curIndex = rightNode;
						if (rightChildCase == NODE_TLAS_LEAF)
						{
							meshInstanceID = rightNodeMask;
						}
						continue;
					}
				}
			}

			for (int i = 0; i < 2; ++i)
			{
				if (leafNode[i] != INVALID_INDEX)
				{
					//invDir = GetInverseDirection(ray.direction);
					//float bvhHit = hitT;
					int leafAddr = leafNode[i];
					int triangleIndex = 0;
					for (int triAddr = ~leafAddr; ; triAddr += 3)
					{
						Vector4 m0 = WoopTriangleData.m_woopTriangleVertices[triAddr];     //matrix row 0 

						if (MathUtil.SingleToInt32Bits(m0.x) == 0x7fffffff)
							break;

						Vector4 m1 = WoopTriangleData.m_woopTriangleVertices[triAddr + 1]; //matrix row 1 
						Vector4 m2 = WoopTriangleData.m_woopTriangleVertices[triAddr + 2]; //matrix row 2

						//this is the correct local normal, the same as cross(v1- v0, v2 - v0)
						Vector3 normal = Vector3.Normalize(Vector3.Cross(m0, m1));

						Vector2 uv = Vector2.zero;
						float triangleHit = 0;
						bool hitTriangle = WoodTriangleRayIntersect(rayTrans, m0, m1, m2, ref uv, out triangleHit);
						if (hitTriangle && (hitT > triangleHit))
						{
							hitT = triangleHit;
							hitInfo.hitT = hitT;
							//hitInfo.triAddr = triAddr;
							hitInfo.baryCoord = uv;
							hitInfo.triangleIndexInMesh = triangleIndex;
							hitInfo.meshInstanceId = meshInstanceID;
							hitIndex = triAddr;

							if (anyHit)
								return true;
						}
						triangleIndex++;
					} // triangle
				}
			}

			if (curIndex == INVALID_INDEX)
			{
				Vector3Int stack = traversalStack[--stackIndex];
				curIndex = stack.x;
				curNodeCase = stack.y;
				meshInstanceID = stack.z;
			}

			if (blas && curIndex == INVALID_INDEX)
			{
				blas = false;

				Vector3Int stack = traversalStack[--stackIndex];
				curIndex = stack.x;
				curNodeCase = stack.y;
				meshInstanceID = stack.z;

				rayTrans = ray;
				rayDir = rayTrans.direction;
				invDir = new Vector3(1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps),
					1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps),
					1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps)
					);
			}
		}

		return hitIndex > -1;
	}


	public bool BVHHit3(GPURay ray, ref HitInfo hitInfo, bool anyHit, GPUSceneData gpuScene, int instBVHOffset)
	{
		hitInfo = new HitInfo();
		float t = float.PositiveInfinity;
		float d = 0;

		// Intersect BVH and tris
		int[] stack = new int[64];
		int ptr = 0;
		stack[ptr++] = -1;

		int index = instBVHOffset;
		float leftHit = 0.0f;
		float rightHit = 0.0f;

		int currMatID = 0;
		bool BLAS = false;

		Vector3Int triID = new Vector3Int(-1, -1, -1);
		Vector3 bary = Vector3.zero;
		//float4 vert0, vert1, vert2;

		GPURay rTrans = ray;
		//rTrans.orig = ray.orig;
		//rTrans.direction = ray.direction;
		int hitMeshInstanceId = -1;

		while (index != -1)
		{
			RadeonBVH.Node bvhNode = m_flattenNodes[index];

			Vector3 LRLeaf = bvhNode.LRLeaf; //ivec3(texelFetch(BVH, index * 3 + 2).xyz);

			int leftIndex = (int)LRLeaf.x;
			int rightIndex = (int)LRLeaf.y;
			int leaf = (int)LRLeaf.z;

			if (leaf > 0) // Leaf node of BLAS
			{
				for (int i = 0; i < rightIndex; i++) // Loop through tris
				{
					Vector3Int vertIndices = gpuScene.triangles[leftIndex + i];

					GPUVertex v0 = gpuScene.gpuVertices[vertIndices.x];//texelFetch(verticesTex, vertIndices.x);
					GPUVertex v1 = gpuScene.gpuVertices[vertIndices.y];//texelFetch(verticesTex, vertIndices.y);
					GPUVertex v2 = gpuScene.gpuVertices[vertIndices.z];//texelFetch(verticesTex, vertIndices.z);

					Vector3 e0 = v1.position - v0.position;
					Vector3 e1 = v2.position - v0.position;
					Vector3 pv = Vector3.Cross(rTrans.direction, e1);
					float det = Vector3.Dot(e0, pv);

					Vector3 tv = rTrans.orig - v0.position;
					Vector3 qv = Vector3.Cross(tv, e0);

					Vector4 uvt = Vector4.zero;
					uvt.x = Vector3.Dot(tv, pv);
					uvt.y = Vector3.Dot(rTrans.direction, qv);
					uvt.z = Vector3.Dot(e1, qv);
					uvt = uvt.Div(det);
					uvt.w = 1.0f - uvt.x - uvt.y;

					if (uvt.x >= 0 && uvt.y >= 0 && uvt.z >= 0 && uvt.w >= 0 && uvt.z < t)
					{
						t = uvt.z;
						triID = vertIndices;
						//state.matID = currMatID;
						bary = new Vector3(uvt.w, uvt.x, uvt.y);
						//vert0 = v0.position, vert1 = v1, vert2 = v2;
						//transform = transMat;

						if (anyHit)
							return true;
					}
					else
                    {
						int debug = 0;
                    }
				}
			}
			else if (leaf < 0) // Leaf node of TLAS
			{
				//vec4 r1 = texelFetch(transformsTex, ivec2((-leaf - 1) * 4 + 0, 0), 0).xyzw;
				//vec4 r2 = texelFetch(transformsTex, ivec2((-leaf - 1) * 4 + 1, 0), 0).xyzw;
				//vec4 r3 = texelFetch(transformsTex, ivec2((-leaf - 1) * 4 + 2, 0), 0).xyzw;
				//vec4 r4 = texelFetch(transformsTex, ivec2((-leaf - 1) * 4 + 3, 0), 0).xyzw;

				//transMat = mat4(r1, r2, r3, r4);

				//rTrans.origin = vec3(inverse(transMat) * vec4(r.origin, 1.0));
				//rTrans.direction = vec3(inverse(transMat) * vec4(r.direction, 0.0));
				hitMeshInstanceId = -leaf - 1;
				MeshInstance meshInstance = gpuScene.meshInstances[hitMeshInstanceId];
				rTrans = GPURay.TransformRay(ref meshInstance.worldToLocal, ref ray);

				// Add a marker. We'll return to this spot after we've traversed the entire BLAS
				stack[ptr++] = -1;
				index = leftIndex;
				BLAS = true;
				currMatID = rightIndex;
				continue;
			}
			else
			{
				RadeonBVH.Node leftChild = m_flattenNodes[leftIndex];
				RadeonBVH.Node rightChild = m_flattenNodes[rightIndex];
				float ooeps = Mathf.Pow(2, -80.0f);//exp2f(-80.0f); // Avoid div by zero, returns 1/2^80, an extremely small number
				Vector3 rayDir = rTrans.direction;
				Vector3 invDir = new Vector3(1.0f / (Mathf.Abs(rayDir.x) > ooeps ? rayDir.x : Mathf.Sign(rayDir.x) * ooeps),
					1.0f / (Mathf.Abs(rayDir.y) > ooeps ? rayDir.y : Mathf.Sign(rayDir.y) * ooeps),
					1.0f / (Mathf.Abs(rayDir.z) > ooeps ? rayDir.z : Mathf.Sign(rayDir.z) * ooeps)
					);
				RayBoundIntersect(rTrans, invDir, leftChild.min, leftChild.max, out leftHit);
				RayBoundIntersect(rTrans, invDir, rightChild.min, rightChild.max, out rightHit);

				if (leftHit > 0.0 && rightHit > 0.0)
				{
					int deferred = -1;
					if (leftHit > rightHit)
					{
						index = rightIndex;
						deferred = leftIndex;
					}
					else
					{
						index = leftIndex;
						deferred = rightIndex;
					}

					stack[ptr++] = deferred;
					continue;
				}
				else if (leftHit > 0)
				{
					index = leftIndex;
					continue;
				}
				else if (rightHit > 0)
				{
					index = rightIndex;
					continue;
				}
			}
			index = stack[--ptr];

			// If we've traversed the entire BLAS then switch to back to TLAS and resume where we left off
			if (BLAS && index == -1)
			{
				BLAS = false;

				index = stack[--ptr];

				rTrans.orig = ray.orig;
				rTrans.direction = ray.direction;
			}
		}

		// No intersections
		if (t == float.PositiveInfinity)
			return false;

		//state.hitDist = t;
		//state.fhp = r.origin + r.direction * t;
		hitInfo.triAddr = triID;
		hitInfo.hitT = t;
		hitInfo.baryCoord = new Vector2(bary.x, bary.y);
		hitInfo.meshInstanceId = hitMeshInstanceId;

		return true;
	}

	public float BuildingProgress
	{
		get
		{
			return buildingProgress;
		}
	}
}
