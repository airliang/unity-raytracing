using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using System.IO;
using AOT;

public static class RadeonBVH
{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX 
    const string BVHBUILDER_DLL = "__Internal";
#else
    const string BVHBUILDER_DLL = "BvhBuilder";
#endif

    [System.Serializable]
    public class BuildParam
    {
        public float cost = 10.0f;
        public int numBins = 64;
        public int splitDepth = 16;
        public float miniOverlap = 0.05f;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct Node
    {
        public Vector3 min;
        public Vector3 max;
        public Vector3 LRLeaf;
        //public Vector3 pad;
        //public bool isLeaf => left != -1;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct MeshInstance
    {
        public int meshID;
        public int materialID;
        public int bvhStartIndex;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct BBox
    {
        public Vector3 min;
        public Vector3 max;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct BVHHandle
    {
        public GPUBounds bounds;
        public int numNodes;
        public int numIndices;
        public IntPtr sortedIndices;
        public IntPtr innerBvh;
    }

    public class BVHFlat
    {
        public Vector3 min;
        public Vector3 max;
        public Node[] nodes;
        public int[] sortedIndices;
        public LinearBVHNode[] linearBVHNodes;
        public int bvhTrianglesNum;
        public int bvhNodeNums;

        public bool isValid => nodes != null && nodes.Length != 0;
        public void Save(string path)
        {
            if (!isValid)
                return;

            using (var bw = new BinaryWriter(new MemoryStream()))
            {
                var stream = bw.BaseStream as MemoryStream;
                bw.Write(min.x);
                bw.Write(min.y);
                bw.Write(min.z);
                bw.Write(max.x);
                bw.Write(max.y);
                bw.Write(max.z);
                bw.Write((uint)nodes.Length);
                Array.ForEach(nodes, n =>
                {
                    bw.Write(n.min.x);
                    bw.Write(n.min.y);
                    bw.Write(n.min.z);
                    bw.Write(n.max.x);
                    bw.Write(n.max.y);
                    bw.Write(n.max.z);
                    bw.Write(n.LRLeaf.x);
                    bw.Write(n.LRLeaf.y);
                    bw.Write(n.LRLeaf.z);
                    //bw.Write(n.pad.x);
                    //bw.Write(n.pad.y);
                    //bw.Write(n.pad.z);
                });
                bw.Write((uint)(sortedIndices != null ? sortedIndices.Length : 0));
                Array.ForEach(sortedIndices, s => bw.Write(s));

                var mode = File.Exists(path) ? FileMode.Truncate : FileMode.CreateNew;
                using (var fs = new FileStream(path, mode, FileAccess.Write, FileShare.None, 1024, true))
                    fs.Write(stream.GetBuffer(), 0, (int)stream.Position);
            }
        }

        public void Load(string path)
        {
            if (!File.Exists(path))
                return;
            var file = File.OpenRead(path);
            if (file == null)
                return;
            var bytes = new byte[file.Length];
            file.Read(bytes, 0, bytes.Length);
            file.Close();

            using (var br = new BinaryReader(new MemoryStream(bytes)))
            {
                min.x = br.ReadSingle();
                min.y = br.ReadSingle();
                min.z = br.ReadSingle();
                max.x = br.ReadSingle();
                max.y = br.ReadSingle();
                max.z = br.ReadSingle();
                var nodeCount = br.ReadUInt32();
                nodes = new Node[nodeCount];
                for (int i = 0; i < nodeCount; i++)
                {
                    Node node;
                    node.min.x = br.ReadSingle();
                    node.min.y = br.ReadSingle();
                    node.min.z = br.ReadSingle();
                    //node.left = br.ReadInt32();
                    node.max.x = br.ReadSingle();
                    node.max.y = br.ReadSingle();
                    node.max.z = br.ReadSingle();
                    //node.right = br.ReadInt32();
                    node.LRLeaf.x = br.ReadSingle();
                    node.LRLeaf.y = br.ReadSingle();
                    node.LRLeaf.z = br.ReadSingle();
                    //node.pad.x = br.ReadSingle();
                    //node.pad.y = br.ReadSingle();
                    //node.pad.z = br.ReadSingle();
                    nodes[i] = node;
                }
                var sortedCount = br.ReadUInt32();
                if (sortedCount > 0)
                {
                    sortedIndices = new int[sortedCount];
                    for (int i = 0; i < sortedCount; i++)
                        sortedIndices[i] = br.ReadInt32();
                }
            }
        }
    }

    [DllImport(BVHBUILDER_DLL, EntryPoint = "CreateBVH", CallingConvention = CallingConvention.Cdecl)]
    private static extern BVHHandle CreateBVH([In] GPUBounds[] bounds, [In] int size, bool useSah, bool useSplit, float traversalCost, int numBins, int splitDepth, float miniOverlap);

    [DllImport(BVHBUILDER_DLL, EntryPoint = "DestroyBVH", CallingConvention = CallingConvention.Cdecl)]
    private static extern void DestroyBVH(ref BVHHandle accelerationStructure);
    [DllImport(BVHBUILDER_DLL, EntryPoint = "TransferToFlat", CallingConvention = CallingConvention.Cdecl)]
    private static extern int TransferToFlat([In, Out] Node[] nodes, ref BVHHandle accelerationStructure, bool isTLAS, int curTriIndex, int bvhNodeOffset, [In] MeshInstance[] meshInstances);
    [DllImport(BVHBUILDER_DLL, EntryPoint = "FlattenBVHTree", CallingConvention = CallingConvention.Cdecl)]
    private static extern void FlattenBVHTree(ref BVHHandle accelerationStructure, [In, Out] LinearBVHNode[] nodes);

    [DllImport(BVHBUILDER_DLL, EntryPoint = "RegisterLogCallback", CallingConvention = CallingConvention.Cdecl)]
    static extern void RegisterLogCallback(logCallback cb);

    delegate void logCallback(IntPtr request, int color, int size);
    enum Color { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(logCallback))]
    static void OnDebugCallback(IntPtr request, int color, int size)
    {
        //Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);

        //Add Specified Color
        debug_string =
            String.Format("{0}{1}{2}{3}{4}",
            "<color=",
            ((Color)color).ToString(),
            ">",
            debug_string,
            "</color>"
            );

        UnityEngine.Debug.Log(debug_string);
    }

    public static BVHFlat CreateBLAS(GPUBounds[] bounds, BuildParam param, int bvhTriangleIndex, int bvhNodeIndex)
    {
        RegisterLogCallback(OnDebugCallback);
        var handle = CreateBVH(bounds, bounds.Length, true, true, param.cost, param.numBins, param.splitDepth, param.miniOverlap);
        var flat = new BVHFlat();
        flat.min = handle.bounds.min;
        flat.max = handle.bounds.max;
        flat.nodes = new Node[handle.numNodes];
        flat.sortedIndices = new int[handle.numIndices];
        Marshal.Copy(handle.sortedIndices, flat.sortedIndices, 0, handle.numIndices);
        //TransferToFlat(flat.nodes, ref handle, false);
        flat.linearBVHNodes = new LinearBVHNode[handle.numNodes];
        FlattenBVHTree(ref handle, flat.linearBVHNodes);
        MeshInstance[] meshInstances = null;
        TransferToFlat(flat.nodes, ref handle, false, bvhTriangleIndex, bvhNodeIndex, meshInstances);
        flat.bvhTrianglesNum = handle.numIndices;
        flat.bvhNodeNums = handle.numNodes;
        DestroyBVH(ref handle);
        return flat;
    }

    public static BVHFlat CreateTLAS(GPUBounds[] bounds, BuildParam param, RadeonBVH.MeshInstance[] meshInstances, int instanceBVHOffset, int bvhNodeIndex)
    {
        RegisterLogCallback(OnDebugCallback);
        var handle = CreateBVH(bounds, bounds.Length, false, false, param.cost, param.numBins, param.splitDepth, param.miniOverlap);
        var flat = new BVHFlat();
        flat.min = handle.bounds.min;
        flat.max = handle.bounds.max;
        flat.nodes = new Node[handle.numNodes];
        flat.sortedIndices = new int[handle.numIndices];
        //TransferToFlat(flat.nodes, ref handle, true);
        Marshal.Copy(handle.sortedIndices, flat.sortedIndices, 0, handle.numIndices);
        //TransferToFlat(flat.nodes, ref handle, false);
        flat.linearBVHNodes = new LinearBVHNode[handle.numNodes];
        FlattenBVHTree(ref handle, flat.linearBVHNodes);
        TransferToFlat(flat.nodes, ref handle, true, instanceBVHOffset, bvhNodeIndex, meshInstances);
        flat.bvhTrianglesNum = handle.numIndices;
        flat.bvhNodeNums = handle.numNodes;
        DestroyBVH(ref handle);
        return flat;
    }
}
