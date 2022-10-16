using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct MeshHandle
{
    //public int index;   //index in array
    public int vertexOffset;
    public int triangleOffset;
    public int vertexCount;
    public int triangleCount;
    //public int woodTriangleStartOffset;
    //public int indexStart;    //the index start offset.attually is the last mesh's total vertex count.
    public GPUBounds localBounds;

    public MeshHandle(
        //int _index,
        int _vertexOff, 
        int _triangleOff,
        int _vertexCount,
        int _triangleCount,
        //int _indexStart,
        Bounds bounds)
    {
        //index = _index;
        vertexOffset = _vertexOff;
        triangleOffset = _triangleOff;
        vertexCount = _vertexCount;
        triangleCount = _triangleCount;
        //woodTriangleStartOffset = -1;
        //indexStart = _indexStart;
        localBounds = GPUBounds.ConvertUnityBounds(bounds);
    }
}

public struct MeshInstance
{
    public Matrix4x4 localToWorld;
    public Matrix4x4 worldToLocal;
    //public int meshHandleIndex;
    public int materialIndex;
    public int lightIndex;
    public int vertexOffsetStart;
    public int triangleStartOffset;  //triangle index start in trianglebuffer
    //public int trianglesNum;
    //public int indexStart;  //the index start offset.attually is the last mesh's total vertex count.

    public MeshInstance(Matrix4x4 _local2world, Matrix4x4 _world2local, /*int _meshHandleIndex, */int _materialIndex, 
        int _lightIndex, int _vertexOffset, int _triangleOffset/*, int _trianglesNum*/)
    {
        localToWorld = _local2world;
        worldToLocal = _world2local;
        //meshHandleIndex = _meshHandleIndex;
        materialIndex = _materialIndex;
        lightIndex = _lightIndex;
        vertexOffsetStart = _vertexOffset;
        triangleStartOffset = _triangleOffset;
        //trianglesNum = _trianglesNum;
    }
}

