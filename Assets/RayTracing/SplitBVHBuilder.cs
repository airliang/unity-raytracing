using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

public struct Reference
{
    public int triIdx;
    public GPUBounds bounds;
    public static Reference DefaultReference()
    {
        return new Reference()
        {
            triIdx = -1,
            bounds = GPUBounds.DefaultBounds()
        };
    }
};

//BVH树的节点描述
struct NodeSpec
{
    public int startIdx;   //primitive在总的primitve中的位置
    public int numRef;     //该节点包含的所有子节点的三角形数量
    public GPUBounds bounds;  //节点的AABB盒子
    public GPUBounds centroidBounds;
    public static NodeSpec Default()
    {
        return new NodeSpec()
        {
            startIdx = 0,
            numRef = 0,
            bounds = GPUBounds.DefaultBounds(),
            centroidBounds = GPUBounds.DefaultBounds()
        };
    }
};



struct SahSplit
{
    public int   dim;     //按哪个轴
    public float pos;   //划分的位置
    public float sah;     //消耗的sah
    public float overlap; //overlap的比例，spatial是0

    public static SahSplit Default()
    {
        SahSplit split = new SahSplit();
        split.sah = float.MaxValue;
        split.dim = 0;
        split.pos = float.NaN;
        return split;
    }
};
struct SpatialBin
{
    public GPUBounds bounds;
    public int enter;
    public int exit;

    public static SpatialBin DefaultSpatialBin()
    {
        return new SpatialBin()
        {
            bounds = GPUBounds.DefaultBounds(),
            enter = 0,
            exit = 0
        };
    }
};

public class SplitBVHBuilder : BVHBuilder
{
    //private readonly int maxLevel = 64;
    float m_minOverlap = 0.05f;   //划分空间的最小面积，意思是大于该面积，空间才可继续划分
    float m_splitAlpha = 1.0e-5f;   //what's this mean?
    float m_traversalCost = 0.125f;
    int m_numDuplicates = 0;   //重复在多个节点上的三角形数量
    //List<BVHPrimitiveInfo> primitiveInfos = new List<BVHPrimitiveInfo>();
    //List<int> triangles = new List<int>();
    List<Reference> m_refStack = new List<Reference>();
    //GPUBounds[] m_rightBounds = null;
    int m_sortDim;
    readonly int MaxDepth = 64;
    int MaxSpatialDepth = 48;
    readonly static int NumSpatialBins = 64;
    
    private int innerNodes = 0;
    private int leafNodes = 0;

    SpatialBin[] m_bins = new SpatialBin[3 * NumSpatialBins];
    BucketInfo[] buckets = new BucketInfo[NumSpatialBins];
    GPUBounds[] rightBounds = new GPUBounds[NumSpatialBins - 1];

    static GPUBounds defaultBound = GPUBounds.DefaultBounds();
    static SpatialBin defaultBin = SpatialBin.DefaultSpatialBin();
    static Reference defaultReference = Reference.DefaultReference();
    static SahSplit defaultSahSplit = SahSplit.Default();
    static BucketInfo defaultBucket = BucketInfo.Default();

    public enum SplitType
    {
        kObject,
        kSpatial
    };

    //这个是整个场景的顶点和索引的引用List，不能释放掉
    //int _orderedPrimOffset = 0;
    //List<Primitive> _orderedPrimitives;

    Vector3 ClampVector3(Vector3 v, float min, float max)
    {
        return new Vector3(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max), Mathf.Clamp(v.z, min, max));
    }

    Vector3 ClampVector3(Vector3 v, Vector3 min, Vector3 max)
    {
        return new Vector3(Mathf.Clamp(v.x, min.x, max.x), Mathf.Clamp(v.y, min.y, max.y), Mathf.Clamp(v.z, min.z, max.z));
    }

    void SortSwap(List<Reference> references, int a, int b)
    {
        Reference tmp = references[a];
        references[a] = references[b];
        references[b] = tmp;
    }

    bool compl(float a, float b)
    {
        return a < b;
    }

    bool compge(float a, float b)
    {
        return a >= b;
    }

    delegate bool Cmp(float a, float b);
    //delegate bool cmp2(float a, float b);

    public override BVHBuildNode Build(GPUBounds[] prims, int _maxPrimsInNode = 1)
    {
        //_vertices = vertices;
        //_primitives = prims;
        _orderedPrimitives.Clear();
        //_orderedPrimOffset = 0;
        maxPrimsInNode = _maxPrimsInNode <= 1 ? 2 : _maxPrimsInNode;
        NodeSpec root = NodeSpec.Default(); //new NodeSpec();
        m_refStack.Clear();

        for (int i = 0; i < prims.Length; ++i)
        {
            //primitiveInfos.Add(new BVHPrimitiveInfo(i, prims[i].worldBound));
            Reference reference = new Reference()
            {
                triIdx = i,  //reference's triIdx is the index in _primitives
                bounds = prims[i]
            };
            m_refStack.Add(reference);
        }
        for (int i = 0; i < prims.Length; ++i)
        {
            //root.bounds = GPUBounds.Union(root.bounds, primitiveInfos[i].worldBound);
            //root.centroidBounds = GPUBounds.Union(root.centroidBounds, primitiveInfos[i].worldBound.centroid);
            root.bounds.Union(m_refStack[i].bounds);
            root.centroidBounds.Union(m_refStack[i].bounds.centroid);
        }
        root.numRef = prims.Length;

        // Remove degenerates.
        //把无效的boundingbox去掉，例如线和带负数的
        int firstRef = m_refStack.Count - root.numRef;
        for (int i = m_refStack.Count - 1; i >= firstRef; i--)
        {
            //if (i >= m_refStack.Count || i < 0)
            //{
            //    Debug.LogError("Remove degenerates error!");
            //}
            Vector3 size = m_refStack[i].bounds.Diagonal;
            //removes the negetive size and the line bounding
            if (m_refStack[i].bounds.MinSize() < 0.0f || (size.x + size.y + size.z) == m_refStack[i].bounds.MaxSize())
            {
                m_refStack[i] = m_refStack[m_refStack.Count - 1];
                m_refStack.RemoveAt(m_refStack.Count - 1);
            }
        }
        root.numRef = m_refStack.Count - firstRef;
        

        //m_rightBounds = new GPUBounds[Mathf.Max(root.numRef, NumSpatialBins) - 1];
        m_minOverlap = root.bounds.SurfaceArea() * m_splitAlpha;
        innerNodes = 0;
        leafNodes = 0;
        totalNodes = 0;
        
        BVHBuildNode rootNode = RecursiveBuild(root, 0, 0, 1.0f);
        
        Debug.Log("InnerNodes num = " + innerNodes);
        Debug.Log("LeafNodes num = " + leafNodes);
        Debug.Log("TotalNodes num = " + totalNodes);

        return rootNode;
    }

    BVHBuildNode RecursiveBuild(NodeSpec spec, int level, float progressStart, float progressEnd)
    {
        totalNodes++;

        if (spec.numRef < maxPrimsInNode || level >= MaxDepth)
            return CreateLeaf(spec);

        //find the split candidate
        //判断split space和split object的依据是？
        //float area = spec.bounds.SurfaceArea();
        //float leafSAH = GetTriangleCost(spec.numRef);
        //这里是因为2个子节点？
        //float nodeSAH = area * GetNodeCost(2);

        // Choose the maximum extent
        int axis = spec.centroidBounds.MaximumExtent();
        float border = spec.centroidBounds.centroid[axis];

        SplitType split_type = SplitType.kObject;
        Profiler.BeginSample("BVH Find Object Splits");
        SahSplit objectSplit = FindObjectSplit(spec);
        Profiler.EndSample();

        SahSplit spatialSplit = SahSplit.Default();
        if (level < MaxSpatialDepth && objectSplit.overlap >= m_minOverlap)
        {
            //由于object划分会产生overlap的区域，当overlap的区域＞minOverlap的时候，需要划分spatial split
            Profiler.BeginSample("BVH FindSpatialSplit");
            spatialSplit = FindSpatialSplit(spec);
            Profiler.EndSample();
        }
        

        //BVHBuildNode node = new BVHBuildNode();
        //float minSAH = Mathf.Min(objectSplit.sah, spatialSplit.sah);
        //minSAH = Mathf.Min(minSAH, leafSAH);
        //if (minSAH == leafSAH && spec.numRef <= maxPrimsInNode)
        //{
        //for (int i = 0; i < spec.numRef; i++)
        //{
        //    //tris.add(m_refStack.removeLast().triIdx);
        //    Reference last = m_refStack[m_refStack.Count - 1];
        //    m_refStack.RemoveAt(m_refStack.Count - 1);
        //    _orderedPrimitives.Add(_primitives[last.triIdx]);
        //}
        //node.InitLeaf(_orderedPrimitives.Count - spec.numRef, spec.numRef, spec.bounds);
        //return CreateLeaf(spec);
        //}

        if (objectSplit.sah < spatialSplit.sah)
        {
            axis = objectSplit.dim;
        }
        else
        {
            split_type = SplitType.kSpatial;
            axis = spatialSplit.dim;
        }

        if (split_type == SplitType.kSpatial)
        {
            int elems = spec.startIdx + spec.numRef * 2;
            if (m_refStack.Count < elems)
            {
                //primrefs.resize(elems);
                //List<Reference> extras = new List<Reference>();
                int refCount = m_refStack.Count;
                for (int i = 0; i < elems - refCount; ++i)
                    m_refStack.Add(Reference.DefaultReference());
                    //extras.Add(Reference.DefaultReference());
                    //m_refStack.AddRange(extras);
            }

            // Split prim refs and add extra refs to request
            int extra_refs = 0;
            Profiler.BeginSample("SplitPrimRefs");
            SplitPrimRefs(spatialSplit, spec, m_refStack, ref extra_refs);
            Profiler.EndSample();
            spec.numRef += extra_refs;
            border = spatialSplit.pos;
            axis = spatialSplit.dim;
        }
        else
        {
            border = !float.IsNaN(objectSplit.pos) ? objectSplit.pos : border;
            axis = !float.IsNaN(objectSplit.pos) ? objectSplit.dim : axis;
        }

        //分组，把原来ref队列进行分组
        // Start partitioning and updating extents for children at the same time
        GPUBounds leftbounds = GPUBounds.DefaultBounds();
        GPUBounds rightbounds = GPUBounds.DefaultBounds();
        GPUBounds leftcentroid_bounds = GPUBounds.DefaultBounds();
        GPUBounds rightcentroid_bounds = GPUBounds.DefaultBounds();
        int splitidx = spec.startIdx;

        bool near2far = ((spec.numRef + spec.startIdx) & 0x1) != 0;



        Cmp cmp1 = compl;//near2far ? compl : compge;
        if (!near2far)
            cmp1 = compge;
        Cmp cmp2 = compge;
        if (!near2far)
            cmp2 = compl;

        if (spec.centroidBounds.Extend[axis] > 0.0f)
        {
            int first = spec.startIdx;
            int last = spec.startIdx + spec.numRef;

            while (true)
            {
                while ((first != last) && cmp1(m_refStack[first].bounds.centroid[axis], border))
                {
                    //leftbounds = GPUBounds.Union(m_refStack[first].bounds, leftbounds);
                    //leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[first].bounds.centroid);
                    leftbounds.Union(m_refStack[first].bounds);
                    leftcentroid_bounds.Union(m_refStack[first].bounds.centroid);
                    ++first;
                }

                if (first == last--)
                    break;

                //rightbounds = GPUBounds.Union(m_refStack[first].bounds, rightbounds);
                //rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[first].bounds.centroid);
                rightbounds.Union(m_refStack[first].bounds);
                rightcentroid_bounds.Union(m_refStack[first].bounds.centroid);

                while ((first != last) && cmp2(m_refStack[last].bounds.centroid[axis], border))
                {
                    //rightbounds = GPUBounds.Union(m_refStack[last].bounds, rightbounds);
                    //rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[last].bounds.centroid);
                    rightbounds.Union(m_refStack[last].bounds);
                    rightcentroid_bounds.Union(m_refStack[last].bounds.centroid);
                    --last;
                }

                if (first == last)
                    break;

                //leftbounds = GPUBounds.Union(m_refStack[last].bounds, leftbounds);
                //leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[last].bounds.centroid);
                leftbounds.Union(m_refStack[last].bounds);
                leftcentroid_bounds.Union(m_refStack[last].bounds.centroid);

                //std::swap(primrefs[first++], primrefs[last]);
                SortSwap(m_refStack, first, last);
                first++;
            }


            splitidx = first;
        }


        if (splitidx == spec.startIdx || splitidx == spec.startIdx + spec.numRef)
        {
            splitidx = spec.startIdx + (spec.numRef >> 1);

            for (int i = spec.startIdx; i < splitidx; ++i)
            {
                //leftbounds = GPUBounds.Union(m_refStack[i].bounds, leftbounds);
                //leftcentroid_bounds = GPUBounds.Union(leftcentroid_bounds, m_refStack[i].bounds.centroid);
                leftbounds.Union(m_refStack[i].bounds);
                leftcentroid_bounds.Union(m_refStack[i].bounds.centroid);
            }

            for (int i = splitidx; i < spec.startIdx + spec.numRef; ++i)
            {
                //rightbounds = GPUBounds.Union(m_refStack[i].bounds, rightbounds);
                //rightcentroid_bounds = GPUBounds.Union(rightcentroid_bounds, m_refStack[i].bounds.centroid);
                rightbounds.Union(m_refStack[i].bounds);
                rightcentroid_bounds.Union(m_refStack[i].bounds.centroid);
            }
        }
        //分组结束

        NodeSpec left = new NodeSpec()
        {
            startIdx = spec.startIdx,
            numRef = splitidx - spec.startIdx,
            bounds = leftbounds,
            centroidBounds = leftcentroid_bounds
        };
        NodeSpec right = new NodeSpec()
        {
            startIdx = splitidx,
            numRef = spec.numRef - (splitidx - spec.startIdx),
            bounds = rightbounds,
            centroidBounds = rightcentroid_bounds
        };
        

        //if (minSAH == spatialSplit.sah)
        //    PerformSpatialSplit(left, right, spec, spatialSplit);
        //if (left.numRef == 0 || right.numRef == 0)
        //    PerformObjectSplit(left, right, spec, objectSplit);

        m_numDuplicates += left.numRef + right.numRef - spec.numRef;
        float progressMid = Mathf.Lerp(progressStart, progressEnd, (float)right.numRef / (float)(left.numRef + right.numRef));
        BVHBuildNode rightNode = RecursiveBuild(right, level + 1, progressStart, progressMid);
        BVHBuildNode leftNode = RecursiveBuild(left, level + 1, progressMid, progressEnd);
        BVHBuildNode innerNode = CreateInnerNode(spec.bounds, leftNode, rightNode);
        return innerNode;
    }

    SahSplit FindObjectSplit(NodeSpec spec)
    {
        SahSplit split = defaultSahSplit;

        Vector3 origin = spec.bounds.min;
        Vector3 binSize = (spec.bounds.max - origin) * (1.0f / (float)NumSpatialBins);
        int splitidx = -1;

        int start = spec.startIdx;
        int end = start + spec.numRef;
        float sah = float.MaxValue;
        float thisNodeSurfaceArea = spec.bounds.SurfaceArea();

        for (int axis = 0; axis < 3; axis++)
        {
            
            float centroid_rng = spec.centroidBounds.Extend[axis];

            if (centroid_rng == 0.0f) 
                continue;
            int nBuckets = buckets.Length;
            
            for (int i = 0; i < nBuckets; ++i)
            {
                buckets[i] = defaultBucket;
            }

            // Initialize _BucketInfo_ for SAH partition buckets
            for (int i = start; i < end; ++i)
            {
                //计算当前的Primitive属于哪个bucket
                int b = (int)(nBuckets *
                    spec.centroidBounds.Offset(m_refStack[i].bounds.centroid)[axis]);
                if (b == nBuckets)
                    b = nBuckets - 1;
                //CHECK_GE(b, 0);
                //CHECK_LT(b, nBuckets);
                buckets[b].count++;
                //计算bucket的bounds
                //buckets[b].bounds =
                //    GPUBounds.Union(buckets[b].bounds, m_refStack[i].bounds);
                buckets[b].bounds.Union(m_refStack[i].bounds);
            }

            //用pbrt的方法没必要sort了
            //BVHSort.Sort<Reference>(start, end, m_refStack, ReferenceCompare, SortSwap);
            // Sweep right to left and determine bounds.

            GPUBounds rightBox = defaultBound;
            for (int i = nBuckets - 1; i > 0; i--)
            {
                //rightBox = GPUBounds.Union(buckets[i].bounds, rightBox);
                rightBox.Union(buckets[i].bounds);
                rightBounds[i - 1] = rightBox;
            }

            // Sweep left to right and select lowest SAH.

            GPUBounds leftBounds = defaultBound;
            int leftcount = 0;
            int rightcount = spec.numRef;

            //分组，计算每组的cost
            //cost(A,B) = t_trav + pA∑t_isect(ai) + pB∑t_isect(ai)
            //t_trav = 0.125; t_isect = 1
            //float[] cost = new float[nBuckets - 1];

            for (int i = 0; i < nBuckets - 1; ++i)
            {
                /*
                GPUBounds bA = GPUBounds.DefaultBounds();
                //bA.SetMinMax(min, max);
                GPUBounds bB = GPUBounds.DefaultBounds();
                //bB.SetMinMax(min, max);
                int count0 = 0, count1 = 0;
                for (int j = 0; j <= i; ++j)
                {
                    bA = GPUBounds.Union(bA, buckets[j].bounds);
                    count0 += buckets[j].count;
                }
                for (int j = i + 1; j < nBuckets; ++j)
                {
                    bB = GPUBounds.Union(bB, buckets[j].bounds);
                    count1 += buckets[j].count;
                }
                //t_trav = 0.125f
                cost[i] = 0.125f +
                    (count0 * bA.SurfaceArea() +
                        count1 * bB.SurfaceArea()) /
                    spec.bounds.SurfaceArea();
                */
                //leftBounds = GPUBounds.Union(buckets[i].bounds, leftBounds);
                leftBounds.Union(buckets[i].bounds);
                leftcount += buckets[i].count;
                rightcount -= buckets[i].count;
                float sahTemp = m_traversalCost +
                    (GetTriangleCost(leftcount) * leftBounds.SurfaceArea() + GetTriangleCost(rightcount) * rightBounds[i].SurfaceArea()) /
                    thisNodeSurfaceArea;

                if (sahTemp < sah)
                {
                    sah = sahTemp;
                    split.sah = sah;
                    split.dim = axis;
                    splitidx = i;
                    //split.numLeft = i;
                    //split.leftBounds = leftBounds;
                    //split.rightBounds = m_rightBounds[i];
                    split.overlap = GPUBounds.Intersection(leftBounds, rightBounds[i]).SurfaceArea() / thisNodeSurfaceArea;
                }
            }
        }

        if (splitidx != -1)
        {
            split.pos = spec.centroidBounds.min[split.dim] + (splitidx + 1) * (spec.centroidBounds.Extend[split.dim] / NumSpatialBins);
        }

        //split.overlap = GPUBounds.Intersection(split.leftBounds, split.rightBounds).SurfaceArea() / spec.bounds.SurfaceArea();
        return split;
    }
    SahSplit FindSpatialSplit(NodeSpec spec)
    {
        // Initialize bins.
        Vector3 origin = spec.bounds.min;
        Vector3 binSize = (spec.bounds.max - origin) * (1.0f / (float)NumSpatialBins);
        Vector3 invBinSize = new Vector3(1.0f / binSize.x, 1.0f / binSize.y, 1.0f / binSize.z);

        

        for (int dim = 0; dim < 3; dim++)
        {
            for (int i = 0; i < NumSpatialBins; i++)
            {
                int index = dim * NumSpatialBins + i;
                m_bins[index] = defaultBin; //new SpatialBin();
            }
        }

        // Chop references into bins.

        for (int refIdx = spec.startIdx; refIdx < spec.startIdx + spec.numRef; refIdx++)
        {
            Reference reference = m_refStack[refIdx];
            Vector3 minMinusOrig = reference.bounds.min - origin;
            Vector3 maxMinusOrig = reference.bounds.max - origin;

            //Vector3Int firstBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(minMinusOrig.x * invBinSize.x, minMinusOrig.y * invBinSize.y, minMinusOrig.z * invBinSize.z)),
            //    0, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));
            //Vector3Int lastBin = ClampVector3Int(Vector3Int.FloorToInt(new Vector3(maxMinusOrig.x * invBinSize.x, maxMinusOrig.y * invBinSize.y, maxMinusOrig.z * invBinSize.z)), 
            //    firstBin, new Vector3Int(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));
            Vector3 firstBin = ClampVector3(new Vector3(minMinusOrig.x * invBinSize.x, minMinusOrig.y * invBinSize.y, minMinusOrig.z * invBinSize.z), 0, NumSpatialBins - 1);
            Vector3 lastBin = ClampVector3(new Vector3(maxMinusOrig.x * invBinSize.x, maxMinusOrig.y * invBinSize.y, maxMinusOrig.z * invBinSize.z), firstBin,
                 new Vector3(NumSpatialBins - 1, NumSpatialBins - 1, NumSpatialBins - 1));

            for (int dim = 0; dim< 3; dim++)
            {
                if (spec.bounds.Extend[dim] == 0.0f)
                    continue;
                Reference currRef = reference;
                for (int i = (int)firstBin[dim]; i < (int)lastBin[dim]; i++)
                {
                    Reference leftRef = defaultReference;
                    Reference rightRef = defaultReference;
                    float splitPos = origin[dim] + binSize[dim] * (float)(i + 1);
                    //SplitReference(leftRef, rightRef, currRef, dim, splitPos);
                    Profiler.BeginSample("SplitPrimRef");
                    if (SplitPrimRef(currRef, dim, splitPos, ref leftRef, ref rightRef))
                    {
                        //m_bins[dim, i].bounds = GPUBounds.Union(m_bins[dim, i].bounds, leftRef.bounds);
                        int index = dim * NumSpatialBins + i;
                        m_bins[index].bounds.Union(leftRef.bounds);
                        currRef = rightRef;
                    }
                    Profiler.EndSample();
                }

                //m_bins[dim, (int)lastBin[dim]].bounds = GPUBounds.Union(m_bins[dim, (int)lastBin[dim]].bounds, currRef.bounds);
                m_bins[dim * NumSpatialBins + (int)lastBin[dim]].bounds.Union(currRef.bounds);
                m_bins[dim * NumSpatialBins + (int)firstBin[dim]].enter++;
                m_bins[dim * NumSpatialBins + (int)lastBin[dim]].exit++;
            }
        }

        // Select best split plane.
        SahSplit split = defaultSahSplit;
        for (int dim = 0; dim < 3; dim++)
        {
            if (spec.bounds.Extend[dim] == 0.0f)
                continue;
            // Sweep right to left and determine bounds.
            //GPUBounds[] rightBounds = new GPUBounds[NumSpatialBins - 1];
            GPUBounds rightBox = defaultBound;
            for (int i = NumSpatialBins - 1; i > 0; i--)
            {
                //rightBox = GPUBounds.Union(rightBox, m_bins[dim, i].bounds);
                int index = dim * NumSpatialBins + i;
                rightBox.Union(m_bins[index].bounds);
                rightBounds[i - 1] = rightBox;
            }

            // Sweep left to right and select lowest SAH.

            GPUBounds leftBox = defaultBound;
            int leftNum = 0;
            int rightNum = spec.numRef;

            for (int i = 1; i < NumSpatialBins; i++)
            {
                //leftBounds = GPUBounds.Union(leftBounds, m_bins[dim, i - 1].bounds);
                int index = dim * NumSpatialBins + i - 1;
                leftBox.Union(m_bins[index].bounds);
                leftNum += m_bins[index].enter;
                rightNum -= m_bins[index].exit;

                float sah = m_traversalCost + (leftBox.SurfaceArea() * GetTriangleCost(leftNum) + rightBounds[i - 1].SurfaceArea() * GetTriangleCost(rightNum)) /
                    spec.bounds.SurfaceArea();
                if (sah < split.sah)
                {
                    split.sah = sah;
                    split.dim = dim;
                    split.pos = origin[dim] + binSize[dim] * (float) i;
                }
            }
        }
        return split;
    }

    //return the SAH ray triangle intersect cost
    float GetTriangleCost(int triangles)
    {
        //1.0表示一次求交的消耗
        return triangles * 1.0f;
    }

    //return the SAH ray node intersect cost
    float GetNodeCost(int nodes)
    {
        //1.0表示一次求交的消耗
        return nodes * 1.0f;
    }

    BVHBuildNode CreateInnerNode(GPUBounds bounds, BVHBuildNode left, BVHBuildNode right)
    {
        innerNodes++;
        BVHBuildNode node = new BVHBuildNode();
        node.bounds = bounds;
        node.childrenLeft = left;
        node.childrenRight = right;
        node.nPrimitives = 0;
        return node;
    }

    BVHBuildNode CreateLeaf(NodeSpec spec)
    {
        leafNodes++;
        //List<int> tris = m_bvh.getTriIndices();
        for (int i = spec.startIdx; i < spec.startIdx + spec.numRef; i++)
        {
            //Reference last = m_refStack[m_refStack.Count - 1];
            //m_refStack.RemoveAt(m_refStack.Count - 1);
            Reference primRef = m_refStack[i];
            //_orderedPrimitives.Add(_primitives[primRef.triIdx]);
            _orderedPrimitives.Add(primRef.triIdx);
        }
        BVHBuildNode leafNode = new BVHBuildNode();
        leafNode.InitLeaf(_orderedPrimitives.Count - spec.numRef, spec.numRef, spec.bounds);
        return leafNode;
        //return new BVHBuildNode(spec.bounds, tris.getSize() - spec.numRef, tris.getSize());
    }

    /*
    void SplitReference(ref Reference left, ref Reference right, Reference reference, int dim, float pos)
    {
        // Initialize references.

        left.triIdx = right.triIdx = reference.triIdx;
        left.bounds = right.bounds = GPUBounds.DefaultBounds();

        // Loop over vertices/edges.

        //const Vec3i* tris = (const Vec3i*)m_bvh.getScene()->getTriVtxIndexBuffer().getPtr();
        //const Vec3f* verts = (const Vec3f*)m_bvh.getScene()->getVtxPosBuffer().getPtr();
        //const Vec3i& inds = tris[ref.triIdx];
        //const Vec3f* v1 = &verts[inds.z];
        Primitive triangle = _primitives[reference.triIdx];
        //if (triangle.triangleOffset + 2 >= _triangles.Count)
        //{
        //    Debug.LogError("Triangle Out of range");
        //}
        //int triIndex = _triangles[triangle.triangleOffset + 2];
        Vector3 v1 = _vertices[triangle.triIndices.z].position;

        for (int i = 0; i < 3; i++)
        {
            Vector3 v0 = v1;
            //v1 = _positions[_triangles[triangle.triangleOffset + i]];
            v1 = _vertices[triangle.triIndices[i]].position;
            float v0p = v0[dim];
            float v1p = v1[dim];

            // Insert vertex to the boxes it belongs to.

            if (v0p <= pos)
                //left.bounds = GPUBounds.Union(left.bounds, v0);
                left.bounds.Union(v0);
            if (v0p >= pos)
                //right.bounds = GPUBounds.Union(right.bounds, v0);
                right.bounds.Union(v0);

            // Edge intersects the plane => insert intersection to both boxes.

            if ((v0p < pos && v1p > pos) || (v0p > pos && v1p < pos))
            {
                Vector3 t = Vector3.Lerp(v0, v1, Mathf.Clamp((pos - v0p) * (1 / (v1p - v0p)), 0.0f, 1.0f));
                //left.bounds = GPUBounds.Union(left.bounds, t);
                //right.bounds = GPUBounds.Union(right.bounds, t);
                left.bounds.Union(t);
                right.bounds.Union(t);
            }
        }

        // Intersect with original bounds.

        left.bounds.max[dim] = pos;
        right.bounds.min[dim] = pos;
        left.bounds.Intersect(reference.bounds);
        right.bounds.Intersect(reference.bounds);
    }

    void PerformObjectSplit(NodeSpec left, NodeSpec right, NodeSpec spec, SahSplit split)
    {
        m_sortDim = split.sortDim;
        int count = spec.numRef;
        int start = m_refStack.Count - spec.numRef;
        int end = start + count;
        //sort(this, m_refStack.getSize() - spec.numRef, m_refStack.getSize(), sortCompare, sortSwap);
        //m_refStack.Sort(m_refStack.Count - spec.numRef, count, new ReferenceCompair(m_sortDim));
        BVHSort.Sort(start, end, m_refStack, ReferenceCompare, SortSwap);

        left.numRef = split.numLeft;
        left.bounds = split.leftBounds;
        right.numRef = spec.numRef - split.numLeft;
        right.bounds = split.rightBounds;
    }
    */

    bool SplitPrimRef(Reference refPrim, int axis, float split, ref Reference leftref, ref Reference rightref)
    {
        // Start with left and right refs equal to original ref
        leftref.triIdx = rightref.triIdx = refPrim.triIdx;
        leftref.bounds = rightref.bounds = refPrim.bounds;

        // Only split if split value is within our bounds range
        if (split > refPrim.bounds.min[axis] && split < refPrim.bounds.max[axis])
        {
            // Trim left box on the right
            leftref.bounds.max[axis] = split;
            // Trim right box on the left
            rightref.bounds.min[axis] = split;
            return true;
        }

        return false;
    }

    void SplitPrimRefs(SahSplit split, NodeSpec req, List<Reference> refs, ref int extra_refs)
    {
        // We are going to append new primitives at the end of the array
        int appendprims = req.numRef;

        // Split refs if any of them require to be split
        for (int i = req.startIdx; i < req.startIdx + req.numRef; ++i)
        {
            Debug.Assert((req.startIdx + appendprims) < refs.Count, "array out of index [req.startIdx + appendprims] = " + (req.startIdx + appendprims) + " refs.Count = " + refs.Count);

            Reference leftref = Reference.DefaultReference(); //new Reference();
            Reference rightref = Reference.DefaultReference();//new Reference();
            if (SplitPrimRef(refs[i], split.dim, split.pos, ref leftref, ref rightref))
            {
                // Copy left ref instead of original
                refs[i] = leftref;
                // Append right one at the end
                refs[req.startIdx + appendprims++] = rightref;
            }
        }

        // Return number of primitives after this operation
        extra_refs = appendprims - req.numRef;
    }
}
