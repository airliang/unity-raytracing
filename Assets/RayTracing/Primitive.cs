using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//we can use as a triangle
public struct Primitive
{
    //public int vertexOffset;      //mesh vertex offset in the whole scene vertexbuffer
    //public int triangleOffset;    //triangle offset in the whole scene trianglebuffer
    public Vector3Int triIndices;
    //public int transformId; //the primitive belong to the transform
    //public int faceIndex;   //mesh triangle indice start
    public GPUBounds worldBound;
    public int meshInstIndex;
    public int meshIndex;

    GPUBounds BuildBounds(Vector3 p0, Vector3 p1, Vector3 p2)
    {
        GPUBounds bounds = new GPUBounds();
        //bounds.min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        //bounds.max = new Vector3(float.MinValue, float.MinValue, float.MinValue); ;
        Vector3 min = new Vector3(Mathf.Min(p0.x, p1.x), Mathf.Min(p0.y, p1.y), Mathf.Min(p0.z, p1.z));
        Vector3 max = new Vector3(Mathf.Max(p0.x, p1.x), Mathf.Max(p0.y, p1.y), Mathf.Max(p0.z, p1.z));
        bounds.min = Vector3.Min(min, p2);
        bounds.max = Vector3.Max(max, p2);

        return bounds;
    }
    public Primitive(int tri0, int tri1, int tri2, Vector3 p0, Vector3 p1, Vector3 p2, int meshInstIdx, int meshIdx)
    {
        triIndices = Vector3Int.zero;
        triIndices.x = tri0;
        triIndices.y = tri1;
        triIndices.z = tri2;

        meshInstIndex = meshInstIdx;
        meshIndex = meshIdx;
        worldBound = GPUBounds.DefaultBounds();
        worldBound = BuildBounds(p0, p1, p2);
       
    }

    //专门给meshinstance的bvh使用
    public Primitive(GPUBounds _worldBound, int meshInstIdx, int meshIdx)
    {
        worldBound = _worldBound;
        meshInstIndex = meshInstIdx;
        meshIndex = meshIdx;
        triIndices = Vector3Int.zero;
    }
}


