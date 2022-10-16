using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace liangairan.raytracer
{
    public static class MeshUtil
    {
        public enum MeshType
        {
            Quad,
            Plane,
            Disk,
            Cube,
            Sphere
        }

        private static Dictionary<MeshType, Mesh> m_meshes = new Dictionary<MeshType, Mesh>();

        public static Mesh GetMesh(MeshType meshType)
        {
            Mesh mesh = null;
            if (!m_meshes.TryGetValue(meshType, out mesh))
            {
                mesh = CreateMesh(meshType);
                m_meshes.Add(meshType, mesh);
            }
            if (mesh == null)
            {
                m_meshes.Remove(meshType);
                mesh = CreateMesh(meshType);
                m_meshes.Add(meshType, mesh);
            }

            return mesh;
        }

        private static Mesh CreateMesh(MeshType meshType)
        {
            if (meshType == MeshType.Plane || meshType == MeshType.Quad)
            {
                Vector3[] positions = new Vector3[4]
                {
                    new Vector3(0.5f, 0, 0.5f),
                    new Vector3(0.5f, 0, -0.5f),
                    new Vector3(-0.5f, 0, -0.5f),
                    new Vector3(-0.5f, 0, 0.5f),
                };

                Vector2[] uvs = new Vector2[4]
                {
                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),
                };

                Vector3[] normals = new Vector3[4];
                for (int i = 0; i < 4; ++i)
                {
                    normals[i] = Vector3.up;
                }

                int[] triangles = new int[6]
                {
                    0,1,2,
                    0,2,3,
                };
                Mesh mesh = new Mesh();
                mesh.SetVertices(positions);
                mesh.SetUVs(0, uvs);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.name = "plane";
                return mesh;
            }
            else if (meshType == MeshType.Disk)
            {
                List<Vector3> verticiesList = new List<Vector3> { };
                float x;
                float z;
                float radius = 1.0f;
                for (int i = 0; i < 12; i++)
                {
                    x = radius * Mathf.Sin((2 * Mathf.PI * i) / 12);
                    z = radius * Mathf.Cos((2 * Mathf.PI * i) / 12);
                    verticiesList.Add(new Vector3(x, 0f, z));
                }
                Vector3[] verticies = verticiesList.ToArray();

                //triangles
                List<int> trianglesList = new List<int> { };
                for (int i = 0; i < (12 - 2); i++)
                {
                    trianglesList.Add(0);
                    trianglesList.Add(i + 1);
                    trianglesList.Add(i + 2);
                }
                int[] triangles = trianglesList.ToArray();

                //normals
                List<Vector3> normalsList = new List<Vector3> { };
                for (int i = 0; i < verticies.Length; i++)
                {
                    normalsList.Add(Vector3.up);
                }
                Vector3[] normals = normalsList.ToArray();
                Mesh mesh = new Mesh();
                //initialise
                mesh.vertices = verticies;
                mesh.triangles = triangles;
                mesh.normals = normals;
                mesh.name = "disk";
                return mesh;
            }
            else if (meshType == MeshType.Cube)
            {
                Vector3[] positions = new Vector3[24]
                {
                    // Front face
                    new Vector3(-1.0f, -1.0f,  1.0f),
                    new Vector3(1.0f, -1.0f,  1.0f),
                    new Vector3(1.0f,  1.0f,  1.0f),
                    new Vector3(-1.0f,  1.0f,  1.0f),

                    // Back face
                    new Vector3(-1.0f, -1.0f, -1.0f),
                    new Vector3(-1.0f,  1.0f, -1.0f),
                    new Vector3(1.0f,  1.0f, -1.0f),
                    new Vector3(1.0f, -1.0f, -1.0f),

                    // Top face
                    new Vector3(-1.0f,  1.0f, -1.0f),
                    new Vector3(-1.0f,  1.0f,  1.0f),
                    new Vector3( 1.0f,  1.0f,  1.0f),
                    new Vector3( 1.0f,  1.0f, -1.0f),

                    // Bottom face
                    new Vector3(-1.0f, -1.0f, -1.0f),
                    new Vector3( 1.0f, -1.0f, -1.0f),
                    new Vector3( 1.0f, -1.0f,  1.0f),
                    new Vector3(-1.0f, -1.0f,  1.0f),

                    // Right face
                    new Vector3(1.0f, -1.0f, -1.0f),
                    new Vector3(1.0f,  1.0f, -1.0f),
                    new Vector3(1.0f,  1.0f,  1.0f),
                    new Vector3(1.0f, -1.0f,  1.0f),

                    // Left face
                    new Vector3(-1.0f, -1.0f, -1.0f),
                    new Vector3(-1.0f, -1.0f,  1.0f),
                    new Vector3(-1.0f,  1.0f,  1.0f),
                    new Vector3(-1.0f,  1.0f, -1.0f),                
                };

                Vector2[] uvs = new Vector2[24]
                {
                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),

                    new Vector2(0.0f, 0.0f),
                    new Vector2(0.0f, 1.0f),
                    new Vector2(1.0f, 1.0f),
                    new Vector2(1.0f, 0.0f),
                };

                int[] triangles = new int[36]
                {
                    0,  1,  2,      0,  2,  3,    // front
                    4,  5,  6,      4,  6,  7,    // back
                    8,  9,  10,     8,  10, 11,   // top
                    12, 13, 14,     12, 14, 15,   // bottom
                    16, 17, 18,     16, 18, 19,   // right
                    20, 21, 22,     20, 22, 23,   // left
                };

                Mesh mesh = new Mesh();
                mesh.SetVertices(positions);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.name = "cube";
                return mesh;
            }
            else if (meshType == MeshType.Sphere)
            {
                List<Vector3> positions = new List<Vector3>();
                List<Vector2> uvs = new List<Vector2>();
                List<int> triangles = new List<int>();

                int uvNumberOfLongitudes = 12;
                int uvNumberOfLatitudes = 12;
                List<int> lastRowIdx = new List<int>();

                // Calculate vertices
                for (int i = 0; i < uvNumberOfLongitudes; i++)
                {
                    List<int> thisRowIdx = new List<int>();

                    float ratio = (float)(i + 1) / (float)(uvNumberOfLongitudes + 1);
                    float axy = Mathf.PI * ratio;
                    axy -= Mathf.PI / 2f;

                    float rlo = Mathf.Cos(axy);
                    float y = Mathf.Sin(axy);

                    for (int j = 0; j < uvNumberOfLatitudes; j++)
                    {
                        float axz = j == 0 ? 0f : Mathf.PI * 2 * ((float)j / (float)uvNumberOfLatitudes);

                        float x = Mathf.Cos(axz) * rlo;
                        float z = Mathf.Sin(axz) * rlo;

                        thisRowIdx.Add(positions.Count);
                        positions.Add(new Vector3(x, y, z));
                        float u = 1.0f - (float)j / uvNumberOfLatitudes;
                        float v = 1.0f - (float)i / uvNumberOfLongitudes;
                        uvs.Add(new Vector2(u, v));
                    }

                    if (lastRowIdx.Count == 0)
                    {
                        for (int j = 0; j < thisRowIdx.Count; j++)
                        {
                            int jp1 = (j + 1) % thisRowIdx.Count;
                            triangles.Add(0);
                            triangles.Add(j + 2);
                            triangles.Add(jp1 + 2);
                        }
                    }
                    else
                    {
                        for (int j = 0; j < thisRowIdx.Count; j++)
                        {
                            int jp1 = (j + 1) % thisRowIdx.Count;
                            triangles.Add(lastRowIdx[j]);
                            triangles.Add(thisRowIdx[j]);
                            triangles.Add(thisRowIdx[jp1]);

                            triangles.Add(lastRowIdx[j]);
                            triangles.Add(thisRowIdx[jp1]);
                            triangles.Add(lastRowIdx[jp1]);
                        }
                    }

                    lastRowIdx = thisRowIdx;
                }

                for (int j = 0; j < lastRowIdx.Count; j++)
                {
                    int jp1 = (j + 1) % lastRowIdx.Count;
                    triangles.Add(1);
                    triangles.Add(lastRowIdx[jp1]);
                    triangles.Add(lastRowIdx[j]);
                }

                Mesh mesh = new Mesh();
                mesh.SetVertices(positions);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateNormals();
                mesh.name = "sphere";
                return mesh;
            }
            return null;
        }
    }
}

