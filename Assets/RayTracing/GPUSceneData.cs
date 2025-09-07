using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class LightInstance
{
    public enum LightType
    {
        Area,
        Envmap,
        Point,
    }

    public LightType lightType;
    public float area = 0;
    public Vector3 radiance;
    public Texture textureRadiance;
}

public class AreaLightResource
{

    //public List<Vector2> triangleDistributions = new List<Vector2>();
    //��GPUDistributionDiscript�еĵ�ַ
    public int discriptAddress = -1;
    public int meshIndex = -1;
    public Distribution1D triangleDistributions = null;
    public List<float> triangleAreas = new List<float>();
    public List<int> gpuLightIndices = new List<int>();
}

class AreaLightInstance : LightInstance
{
    public AreaLightInstance()
    {
        lightType = LightType.Area;
    }
    public AreaLightResource light;
    public int meshInstanceID = -1;

    public float intensity;
    public float pointRadius;

}

class EnviromentLight : LightInstance
{
    public EnviromentLight()
    {
        lightType = LightType.Envmap;
        area = Mathf.PI * 4.0f;
        radiance = Vector3.one;
    }
    //public Cubemap textureRadiance;
    public Vector3 colorScale = Vector3.one;

    //public Texture2D envmap;
    public Distribution2D envmapDistributions;
    public float rotation;
    //just for test
    //public  ComputeBuffer computeBuffer;

    public void CreateDistributions()
    {
        int mipmap = 1;
        Texture2D envmap = textureRadiance as Texture2D;
        if (envmap != null)
        {
            int width = envmap.width >> mipmap;
            int height = envmap.height >> mipmap;
            float[] distributions = new float[width * height];
            Color[] pixels = envmap.GetPixels(mipmap);
            for (int v = 0; v < height; ++v)
            {
                float vp = ((float)(height - 1 - v) + 0.5f) / (float) height;
                float sinTheta = Mathf.Sin(Mathf.PI * vp);
                for (int u = 0; u < width; ++u)
                {
                    float y = pixels[u + v * width].ToVector3().magnitude;
                    float distribution = y;
                    if (distribution == 0)
                        distribution = float.Epsilon;
                    distributions[u + v * width] = distribution * sinTheta;
                }
            }

            Bounds2D domain = new Bounds2D();
            domain.min = Vector2.zero;
            domain.max = Vector2.one; //new Vector2(width, height);
            envmapDistributions = new Distribution2D(distributions, width, height, domain);

            //computeBuffer = new ComputeBuffer(distributions.Length, sizeof(float), ComputeBufferType.Structured);
            //computeBuffer.SetData(distributions);
        }
        else
        {
            int width = 2;
            int height = 2;
            float[] distributions = new float[width * height];
            //Color[] pixels = envmap.GetPixels(mipmap);
            for (int v = 0; v < height; ++v)
            {
                float vp = ((float)(height - 1 - v) + 0.5f) / (float)height;
                float sinTheta = Mathf.Sin(Mathf.PI * vp);
                for (int u = 0; u < width; ++u)
                {
                    float y = radiance.magnitude;
                    float distribution = y;
                    if (distribution == 0)
                        distribution = float.Epsilon;
                    distributions[u + v * width] = distribution * sinTheta;
                }
            }

            Bounds2D domain = new Bounds2D();
            domain.min = Vector2.zero;
            domain.max = Vector2.one; //new Vector2(width, height);
            envmapDistributions = new Distribution2D(distributions, width, height, domain);
        }
    }
}

public class PathTracingParam
{
    public static int _AccelerationStructure = -1;
    public static int _Output = -1;
    public static int _InstBVHAddr = -1;
    public static int _BVHNodesNum = -1;
    public static int _InvCameraViewProj = -1;
    public static int _CameraPosWS = -1;
    public static int _CameraFarDistance = -1;
    public static int _RasterToCamera = -1;
    public static int _CameraToWorld = -1;
    public static int _WorldToRaster = -1;
    public static int _LensRadius = -1;
    public static int _FocalLength = -1;
    public static int _FrameIndex = -1;
    public static int _MaxDepth = -1;
    public static int _MinDepth = -1;
    public static int _LightsNum = -1;
    public static int _RNGs = -1;
    public static int _Lights = -1;
    public static int _TriangleLights = -1;
    public static int _LightDistributions1D = -1;
    public static int _LightDistributionDiscripts = -1;
    public static int _LightTriangles = -1;
    public static int _LightVertices = -1;
    public static int _RayConeGBuffer = -1;
    public static int _CameraConeSpreadAngle = -1;
    public static int _Spectrums = -1;
    public static int _DebugView = -1;
    public static int _Materials = -1;
    //public static int _InstanceTransforms = -1;
    public static int _EnvmapRotation = -1;
    public static int _EnvironmentColor = -1;
    public static int _LatitudeLongitudeMap = -1;
    public static int _EnvmapMarginals = -1;
    public static int _EnvmapConditions = -1;
    public static int _EnvmapConditionFuncInts = -1;
    public static int _EnvMapDistributionSize = -1;
    public static int _EnvMapDistributionInt = -1;
    public static int _FilterMarginals = -1;
    public static int _FilterConditions = -1;
    public static int _FilterConditionsFuncInts = -1;
    public static int _MarginalNum = -1;
    public static int _ConditionNum = -1;
    public static int _FilterDomain = -1;
    public static int _FilterFuncInt = -1;
    public static int _EnvironmentMapEnable = -1;
    public static int _EnvironmentLightPmf = -1;
    public static int _ScreenSize = -1;

    public static int _ReservoirSamples = -1;
    public static int _TemporalReuseSamples = -1;

    public static void InitPathTracingParam()
    {
        _AccelerationStructure = Shader.PropertyToID("_AccelerationStructure");
        _Output = Shader.PropertyToID("_Output");
        _InstBVHAddr = Shader.PropertyToID("_InstBVHAddr");
        _BVHNodesNum = Shader.PropertyToID("_BVHNodesNum");
        _InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
        _CameraPosWS = Shader.PropertyToID("_CameraPosWS");
        _CameraFarDistance = Shader.PropertyToID("_CameraFarDistance");
        _RasterToCamera = Shader.PropertyToID("_RasterToCamera");
        _CameraToWorld = Shader.PropertyToID("_CameraToWorld");
        _WorldToRaster = Shader.PropertyToID("_WorldToRaster");
        _LensRadius = Shader.PropertyToID("_LensRadius");
        _FocalLength = Shader.PropertyToID("_FocalLength");
        _FrameIndex = Shader.PropertyToID("_FrameIndex");
        _MaxDepth = Shader.PropertyToID("_MaxDepth");
        _MinDepth = Shader.PropertyToID("_MinDepth");
        _LightsNum = Shader.PropertyToID("_LightsNum");
        _RNGs = Shader.PropertyToID("_RNGs");
        _Lights = Shader.PropertyToID("_Lights");
        _TriangleLights = Shader.PropertyToID("_TriangleLights");
        _LightDistributions1D = Shader.PropertyToID("_LightDistributions1D");
        _LightDistributionDiscripts = Shader.PropertyToID("_LightDistributionDiscripts");
        _LightTriangles = Shader.PropertyToID("_LightTriangles");
        _LightVertices = Shader.PropertyToID("_LightVertices");
        _RayConeGBuffer = Shader.PropertyToID("_RayConeGBuffer");
        _CameraConeSpreadAngle = Shader.PropertyToID("_CameraConeSpreadAngle");
        _Spectrums = Shader.PropertyToID("_Spectrums");
        _DebugView = Shader.PropertyToID("_DebugView");
        _Materials = Shader.PropertyToID("_Materials");
        _EnvironmentColor = Shader.PropertyToID("_EnvironmentColor");
        _EnvmapRotation = Shader.PropertyToID("_EnvmapRotation");
        _LatitudeLongitudeMap = Shader.PropertyToID("_LatitudeLongitudeMap");
        _EnvmapMarginals = Shader.PropertyToID("_EnvmapMarginals");
        _EnvmapConditions = Shader.PropertyToID("_EnvmapConditions");
        _EnvmapConditionFuncInts = Shader.PropertyToID("_EnvmapConditionFuncInts");
        _EnvMapDistributionSize = Shader.PropertyToID("_EnvMapDistributionSize");
        _EnvMapDistributionInt = Shader.PropertyToID("_EnvMapDistributionInt");
        _FilterMarginals = Shader.PropertyToID("_FilterMarginals");
        _FilterConditions = Shader.PropertyToID("_FilterConditions");
        _FilterConditionsFuncInts = Shader.PropertyToID("_FilterConditionsFuncInts");
        _MarginalNum = Shader.PropertyToID("_MarginalNum");
        _ConditionNum = Shader.PropertyToID("_ConditionNum");
        _FilterDomain = Shader.PropertyToID("_FilterDomain");
        _FilterFuncInt = Shader.PropertyToID("_FilterFuncInt");
        _EnvironmentMapEnable = Shader.PropertyToID("_EnvironmentMapEnable");
        _EnvironmentLightPmf = Shader.PropertyToID("_EnvironmentLightPmf");
        _ScreenSize = Shader.PropertyToID("_ScreenSize");

        _ReservoirSamples = Shader.PropertyToID("_ReservoirSamples");
        _TemporalReuseSamples = Shader.PropertyToID("_TemporalReuseSamples");
    }
}

public class GPUSceneData
{
    ComputeBuffer woopTriBuffer;
    ComputeBuffer woopTriIndexBuffer;
    ComputeBuffer verticesBuffer;
    ComputeBuffer triangleBuffer;
    ComputeBuffer meshInstanceBuffer;
    ComputeBuffer BVHBuffer;
    //ComputeBuffer intersectBuffer;
    ComputeBuffer lightBuffer;
    ComputeBuffer materialBuffer;
    ComputeBuffer distribution1DBuffer;
    ComputeBuffer distributionDiscriptBuffer;
    ComputeBuffer envLightMarginalBuffer;
    ComputeBuffer envLightConditionBuffer;
    ComputeBuffer envLightConditionFuncIntsBuffer;

    public List<Primitive> primitives = new List<Primitive>();
    public List<MeshHandle> meshHandles = new List<MeshHandle>();
    public List<MeshInstance> meshInstances = new List<MeshInstance>();
    public List<int> meshInstanceHandleIndices = new List<int>();
    public List<Vector3Int> triangles = new List<Vector3Int>();
    public List<GPUVertex> gpuVertices = new List<GPUVertex>();
    public List<GPULight> gpuLights = new List<GPULight>();
    public List<GPUMaterial> gpuMaterials = new List<GPUMaterial>();
    public Dictionary<Material, int> materialIds = new Dictionary<Material, int>();

    public Dictionary<Mesh, AreaLightResource> meshDistributions = new Dictionary<Mesh, AreaLightResource>();
    public List<LightInstance> areaLightInstances = new List<LightInstance>();
    public List<Vector2> Distributions1D = new List<Vector2>();
    //index 0 is the light source distributions
    public List<GPUDistributionDiscript> gpuDistributionDiscripts = new List<GPUDistributionDiscript>();

    Bounds worldBound;
    public BVHAccel bvhAccel = new BVHAccel();
    int instBVHNodeAddr = -1;
    EnviromentLight envLight = new EnviromentLight();
    int envLightIndex = -1;
    bool _uniformSampleLight = false;
    bool _envmapEnable = true;

    public Matrix4x4 RasterToScreen;
    public Matrix4x4 RasterToCamera;
    public Matrix4x4 WorldToRaster;
    public float cameraConeSpreadAngle = 0;

    public int InstanceBVHNodeAddr
    {
        get
        {
            return instBVHNodeAddr;
        }
    }

    public GPUSceneData(bool uniformSampleLight, bool envmapEnable, bool useBVHPlugin)
    {
        PathTracingParam.InitPathTracingParam();
        _uniformSampleLight = uniformSampleLight;
        _envmapEnable = envmapEnable;
        bvhAccel.buildByCPP = useBVHPlugin;
    }

    public int InstanceBVHAddr
    {
        get
        {
            return instBVHNodeAddr;
        }
    }

    private void PrepareSceneData(MeshRenderer[] meshRenderers)
    {
        Profiler.BeginSample("Scene Mesh Data Process");

        List<Mesh> sharedMeshes = new List<Mesh>();
        Dictionary<Mesh, List<int>> meshHandlesDict = new Dictionary<Mesh, List<int>>();
        int lightObjectsNum = 0;

        //������MeshHandle
        int meshHandleIndex = 0;

        for (int i = 0; i < meshRenderers.Length; ++i)
        {
            MeshRenderer meshRenderer = meshRenderers[i];
            //worldMatrices[i] = shapes[i].transform.localToWorldMatrix;

            BSDFMaterial bsdfMaterial = meshRenderers[i].GetComponent<BSDFMaterial>();
            //if ((meshRenderers[i].shapeType == Shape.ShapeType.triangleMesh || meshRenderers[i].shapeType == Shape.ShapeType.rectangle) && bsdfMaterial != null)

            //MeshRenderer meshRenderer = meshRenderers[i];//shapes[i].GetComponent<MeshFilter>();
            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;
            if (!mesh.isReadable)
            {
                continue;
            }

            Light lightComponent = meshRenderer.GetComponent<Light>();
            if (lightComponent != null && lightComponent.type == LightType.Rectangle)
            {
                lightObjectsNum++;
            }


            if (sharedMeshes.Contains(mesh))
            {
                continue;
            }
            //if (mesh.normals == null || mesh.normals.Length == 0)
            //    mesh.RecalculateNormals();
            sharedMeshes.Add(mesh);
            List<int> meshHandleIndices = new List<int>();
            meshHandlesDict.Add(mesh, meshHandleIndices);


            //int meshId = i;
            Profiler.BeginSample("Getting mesh orig datas");
            int vertexOffset = gpuVertices.Count;
            List<Vector2> meshUVs = new List<Vector2>();
            List<Vector3> meshVertices = new List<Vector3>();
            List<Vector3> meshNormals = new List<Vector3>();

            mesh.GetUVs(0, meshUVs);

            mesh.GetVertices(meshVertices);
            mesh.GetNormals(meshNormals);
            if (meshNormals.Count == 0)
            {
                mesh.RecalculateNormals();
                mesh.GetNormals(meshNormals);
            }
            Profiler.EndSample();
            GPUVertex vertexTmp = new GPUVertex();

            Profiler.BeginSample("Geometry Vertex data fetching");
            for (int sm = 0; sm < mesh.subMeshCount; ++sm)
            {
                int subMeshVertexOffset = gpuVertices.Count;
                int triangleOffset = triangles.Count;

                List<int> meshTriangles = new List<int>();
                mesh.GetTriangles(meshTriangles, sm);

                SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(sm);

                MeshHandle meshHandle = new MeshHandle(subMeshVertexOffset, triangleOffset, subMeshDescriptor.vertexCount,
                    subMeshDescriptor.indexCount, subMeshDescriptor.bounds);
                meshHandleIndices.Add(meshHandleIndex);
                meshHandleIndex++;
                meshHandles.Add(meshHandle);

                for (int j = 0; j < subMeshDescriptor.vertexCount; ++j)
                {
                    vertexTmp.position = meshVertices[subMeshDescriptor.firstVertex + j];//mesh.vertices[j];
                    vertexTmp.uv = meshUVs.Count == 0 ? Vector2.zero : meshUVs[subMeshDescriptor.firstVertex + j];
                    vertexTmp.normal = meshNormals[subMeshDescriptor.firstVertex + j];
                    gpuVertices.Add(vertexTmp);
                }
                for (int j = 0; j < subMeshDescriptor.indexCount / 3; ++j)
                {
                    Vector3Int triangleIndex = new Vector3Int(meshTriangles[j * 3 + subMeshDescriptor.indexStart] + vertexOffset, 
                        meshTriangles[j * 3 + subMeshDescriptor.indexStart + 1] + vertexOffset, meshTriangles[j * 3 + subMeshDescriptor.indexStart + 2] + vertexOffset);
                    triangles.Add(triangleIndex);
                }
            }
            Profiler.EndSample();
        }

        List<RuntimeEntityDebug> runtimeEntityDebugs = new List<RuntimeEntityDebug>();
        //����meshinstance�Ͷ�Ӧ��material
        meshHandleIndex = 0;
        for (int i = 0; i < meshRenderers.Length; ++i)
        {
            MeshRenderer meshRenderer = meshRenderers[i];
            if (i == 0)
            {
                worldBound = meshRenderer.bounds;
            }
            else
            {
                worldBound.Encapsulate(meshRenderer.bounds);
            }
            //BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
            Transform transform = meshRenderer.transform;
            int lightIndex = -1;
            //if ((shapes[i].shapeType == Shape.ShapeType.triangleMesh || shapes[i].shapeType == Shape.ShapeType.rectangle) && bsdfMaterial != null)

            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;

            Light lightComponent = meshRenderer.GetComponent<Light>();

            RuntimeEntityDebug entityDebug = meshRenderer.gameObject.AddComponent<RuntimeEntityDebug>();
            runtimeEntityDebugs.Add(entityDebug);
            if (lightComponent != null && lightComponent.type == LightType.Rectangle)
            {
                Profiler.BeginSample("Lights data fetching");
                AreaLightResource areaLight = null;
                List<Vector3> lightMeshVertices = new List<Vector3>();
                if (!meshDistributions.TryGetValue(mesh, out areaLight))
                {
                    areaLight = new AreaLightResource();

                    mesh.GetVertices(lightMeshVertices);

                    for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                    {
                        List<int> meshTriangles = new List<int>();
                        mesh.GetTriangles(meshTriangles, sm);
                        for (int t = 0; t < meshTriangles.Count; t += 3)
                        {
                            Vector3 p0 = lightMeshVertices[meshTriangles[t]];       //transform.TransformPoint(lightMeshVertices[meshTriangles[t]]);
                            Vector3 p1 = lightMeshVertices[meshTriangles[t + 1]];   //transform.TransformPoint(lightMeshVertices[meshTriangles[t + 1]]);
                            Vector3 p2 = lightMeshVertices[meshTriangles[t + 2]];   //transform.TransformPoint(lightMeshVertices[meshTriangles[t + 2]]);
                            float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                            areaLight.triangleAreas.Add(triangleArea);
                        }
                    }

                    areaLight.triangleDistributions = new Distribution1D(areaLight.triangleAreas.ToArray(), 0, areaLight.triangleAreas.Count, 0, areaLight.triangleAreas.Count);
                    meshDistributions.Add(mesh, areaLight);
                    //lightTriangleDistributions.AddRange(areaLight.triangleDistributions);
                }

                float lightArea = 0;

                lightMeshVertices.Clear();
                mesh.GetVertices(lightMeshVertices);

                for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                {
                    List<int> meshTriangles = new List<int>();
                    mesh.GetTriangles(meshTriangles, sm);
                    for (int t = 0; t < meshTriangles.Count; t += 3)
                    {
                        Vector3 p0 = transform.TransformPoint(lightMeshVertices[meshTriangles[t]]);
                        Vector3 p1 = transform.TransformPoint(lightMeshVertices[meshTriangles[t + 1]]);
                        Vector3 p2 = transform.TransformPoint(lightMeshVertices[meshTriangles[t + 2]]);
                        float triangleArea = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                        lightArea += triangleArea;
                    }
                }

                AreaLightInstance areaLightInstance = new AreaLightInstance();
                areaLightInstance.light = areaLight;
                areaLightInstance.meshInstanceID = meshInstances.Count;
                areaLightInstance.area = lightArea;

                Material lightMaterial = meshRenderer.sharedMaterial;
                if (lightMaterial != null && lightMaterial.shader.name == "RayTracing/AreaLight")
                {
                    Color emssionColor = lightMaterial.GetColor("_Emission");
                    Vector3 lightIntensity = lightMaterial.GetVector("_Intensity");
                    areaLightInstance.radiance = emssionColor.LinearToVector3().Mul(lightIntensity);
                }
                areaLightInstance.pointRadius = 0;
                areaLightInstance.intensity = lightComponent.intensity; // shapes[i].lightIntensity;
                areaLightInstances.Add(areaLightInstance);

                lightIndex = gpuLights.Count;
                GPULight gpuLight = new GPULight();
                gpuLight.type = (int)LightInstance.LightType.Area;
                gpuLight.radiance = areaLightInstance.radiance;
                //gpuLight.intensity = areaLightInstance.intensity;
                gpuLight.trianglesNum = mesh.triangles.Length / 3;
                //gpuLight.pointRadius = 0;
                //why add 1? because the first discript is the light object distributions.
                gpuLight.distributionDiscriptIndex = gpuLights.Count + 1;
                gpuLight.meshInstanceID = meshInstances.Count;
                gpuLight.area = areaLightInstance.area;
                gpuLights.Add(gpuLight);
                areaLight.gpuLightIndices.Add(lightIndex);
                Profiler.EndSample();

                entityDebug.lightIndex = lightIndex;
            }

            List<int> meshHandleIndices = null;
            meshHandlesDict.TryGetValue(mesh, out meshHandleIndices);
            if (meshHandleIndices == null)
            {
                Debug.LogError("meshHandleIndices == null, mesh.name = " + mesh.name);
            }
            Profiler.BeginSample("SetupMaterials");
            for (int sm = 0; sm < mesh.subMeshCount; ++sm)
            {
                meshHandleIndex = meshHandleIndices[sm];
                int materialIndex = SetupMaterials(meshRenderer, sm);
                MeshHandle meshHandle = meshHandles[meshHandleIndex];
                MeshInstance meshInstance = new MeshInstance(transform.localToWorldMatrix, transform.worldToLocalMatrix,
                    materialIndex, lightIndex, meshHandle.vertexOffset, meshHandle.triangleOffset);
                entityDebug.meshInstanceIDs.Add(meshInstances.Count);
                meshInstances.Add(meshInstance);
                meshInstanceHandleIndices.Add(meshHandleIndex);
            }
            Profiler.EndSample();
        }
    }

    struct GPURadeonNode
    {
        public Vector3 min;
        public Vector3 max;
        public Vector3 LRLeaf;
        //public Vector3 pad;
    }

    public IEnumerator PrepareBVH()
    {
        //building bvh
        //float timeBegin = Time.realtimeSinceStartup;
        //Profiler.BeginSample("Build BVH");
        //List<Vector3Int> sortedTriangles = new List<Vector3Int>();
        while (bvhAccel.BuildingProgress < 1)
        {
            if (bvhAccel.BuildingProgress == 0)
            {
                bvhAccel.Prepare(meshHandles.Count);
                
                yield return null;
            }
            else if (bvhAccel.BuildingProgress < 1)
                yield return bvhAccel.Build(meshInstances, meshHandles, meshInstanceHandleIndices, gpuVertices, triangles);
        }
        

        //Profiler.EndSample();
        //float timeInterval = Time.realtimeSinceStartup - timeBegin;
        //Debug.Log("building bvh cost time:" + timeInterval);
        //Profiler.EndSample();
        //if (bvhAccel.BuildingProgress == 1)
        {
            triangles = bvhAccel.sortedTriangles;
            instBVHNodeAddr = bvhAccel.instBVHNodeAddr;
            SetupGPUBVHData();

            RaytracingStates.states = RaytracingStates.States.PrepareRendering;
            yield return null;
        }
    }

    void SetupGPUBVHData()
    {
        if (BVHAccel.NVMethod)
        {
            int BVHNodeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUBVHNode));
            if (BVHBuffer == null)
            {
                BVHBuffer = new ComputeBuffer(bvhAccel.m_nodes.Count, BVHNodeSize, ComputeBufferType.Structured);
                BVHBuffer.SetData(bvhAccel.m_nodes);
            }

            if (woopTriBuffer == null)
            {
                woopTriBuffer = new ComputeBuffer(WoopTriangleData.m_woopTriangleVertices.Count, 16, ComputeBufferType.Structured);
            }
            woopTriBuffer.SetData(WoopTriangleData.m_woopTriangleVertices);

            if (woopTriIndexBuffer == null)
            {
                woopTriIndexBuffer = new ComputeBuffer(WoopTriangleData.m_woopTriangleIndices.Count, sizeof(int), ComputeBufferType.Structured);
            }
            woopTriIndexBuffer.SetData(WoopTriangleData.m_woopTriangleIndices);
        }
        else
        {
            int BVHNodeSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(RadeonBVH.Node));
            
            if (BVHBuffer == null)
            {
                BVHBuffer = new ComputeBuffer(bvhAccel.m_flattenNodes.Count, BVHNodeSize, ComputeBufferType.Structured);
                GPURadeonNode[] gpuRadeonNodes = new GPURadeonNode[bvhAccel.m_flattenNodes.Count];
                for (int i = 0; i < bvhAccel.m_flattenNodes.Count; i++)
                {
                    gpuRadeonNodes[i] = new GPURadeonNode();
                    gpuRadeonNodes[i].min = bvhAccel.m_flattenNodes[i].min;
                    gpuRadeonNodes[i].max = bvhAccel.m_flattenNodes[i].max;
                    gpuRadeonNodes[i].LRLeaf = bvhAccel.m_flattenNodes[i].LRLeaf;
                    //gpuRadeonNodes[i].pad = bvhAccel.m_flattenNodes[i].pad;
                }
                BVHBuffer.SetData(gpuRadeonNodes);
            }
        }
        
    }

    public void SetupGPUSceneData()
    {
        if (meshInstanceBuffer == null)
        {
            meshInstanceBuffer = new ComputeBuffer(meshInstances.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(MeshInstance)), ComputeBufferType.Structured);
            meshInstanceBuffer.SetData(meshInstances);
        }

        if (verticesBuffer == null)
        {
            int vertexSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUVertex));
            verticesBuffer = new ComputeBuffer(gpuVertices.Count, vertexSize, ComputeBufferType.Structured);
        }
        verticesBuffer.SetData(gpuVertices);

        if (triangleBuffer == null)
        {
            triangleBuffer = new ComputeBuffer(triangles.Count, 12, ComputeBufferType.Default);
        }
        triangleBuffer.SetData(triangles);

        if (materialBuffer == null)
        {
            materialBuffer = new ComputeBuffer(gpuMaterials.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUMaterial)), ComputeBufferType.Structured);
        }
        materialBuffer.SetData(gpuMaterials);
        Profiler.EndSample();

        //environment light process

        Material skyBoxMaterial = RenderSettings.skybox;
        envLight.area = worldBound.extents.magnitude * 4.0f * Mathf.PI;
        GPULight gpuEnvLight = new GPULight();
        gpuEnvLight.type = (int)envLight.lightType;
        gpuEnvLight.meshInstanceID = -1;
        gpuEnvLight.distributionDiscriptIndex = gpuLights.Count + 1;

        bool envmapEnable = false;
        if (skyBoxMaterial != null)
        {
            if (skyBoxMaterial.shader.name == "Skybox/Cubemap")
            {
                envLight.textureRadiance = skyBoxMaterial.GetTexture("_Tex") as Cubemap;
            }
            else if (skyBoxMaterial.shader.name == "Skybox/Panoramic" || skyBoxMaterial.shader.name == "RayTracing/SkyboxHDR")
            {
                envLight.textureRadiance = skyBoxMaterial.GetTexture("_MainTex");
                envLight.rotation = skyBoxMaterial.GetFloat("_Rotation");
                envLight.CreateDistributions();
            }
            if (envLight.textureRadiance == null)
            {
                envLight.radiance = RenderSettings.ambientSkyColor.LinearToVector3();
                gpuEnvLight.radiance = envLight.radiance;
                envLight.CreateDistributions();
            }
            else
            {
                uint mask = 1;
                //gpuEnvLight.textureMask = MathUtil.UInt32BitsToSingle(mask);
            }

            envmapEnable = true;
        }
        else
        {
            envLight.CreateDistributions();
            envmapEnable = false;
        }

        if (envmapEnable)
            envmapEnable = _envmapEnable;

        if (envmapEnable)
        {
            areaLightInstances.Add(envLight);
            envLightIndex = gpuLights.Count;
            gpuLights.Add(gpuEnvLight);
        }
    }

    private void SetupSceneCamera(Camera camera)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //init the camera parameters

        Matrix4x4 screenToRaster = new Matrix4x4();

        screenToRaster = Matrix4x4.Scale(new Vector3(rasterWidth, rasterHeight, 1)) *
            Matrix4x4.Scale(new Vector3(0.5f, 0.5f, 0.5f)) *
            Matrix4x4.Translate(new Vector3(1, 1, 1));

        RasterToScreen = screenToRaster.inverse;

        float aspect = rasterWidth / rasterHeight;

        Matrix4x4 cameraToScreen = camera.orthographic ? Matrix4x4.Ortho(-camera.orthographicSize * aspect, camera.orthographicSize * aspect,
            -camera.orthographicSize, camera.orthographicSize, camera.nearClipPlane, camera.farClipPlane)
            : Matrix4x4.Perspective(camera.fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);

        cameraConeSpreadAngle = Mathf.Atan(2.0f * Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f) / Screen.height);

        RasterToCamera = cameraToScreen.inverse * RasterToScreen;
        WorldToRaster = screenToRaster * cameraToScreen * camera.worldToCameraMatrix;
    }

    public IEnumerator Setup(MeshRenderer[] meshRenderers, Camera camera)
    {
        //MeshRenderer[] meshRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
        //worldMatrices = new Matrix4x4[shapes.Length];
        if (meshRenderers.Length == 0)
            yield break;

        while (RaytracingStates.states != RaytracingStates.States.PrepareRendering)
        {
            if (RaytracingStates.states == RaytracingStates.States.SceneLoading)
            {
                PrepareSceneData(meshRenderers);
                RaytracingStates.states = RaytracingStates.States.BuildingBVH;
                yield return null;
            }
            else if (RaytracingStates.states == RaytracingStates.States.BuildingBVH)
            {
                yield return PrepareBVH();
            }
        }
        
        //else if (RaytracingStates.states == RaytracingStates.States.PrepareRendering)
        {
            SetupGPUSceneData();
            SetupSceneCamera(camera);
            SetupDistributions();
        }

        yield return null;
    }

    int SetupMaterials(MeshRenderer renderer, int subMeshIndex)
    {
        //Renderer renderer = shape.GetComponent<MeshRenderer>();
        //if (renderer.sharedMaterial.HasProperty("_BaseColor"))
        //{
        //    Color _Color = renderer.sharedMaterials[subMeshIndex].GetColor("_BaseColor");
        //}
        int id = -1;
        if (materialIds.TryGetValue(renderer.sharedMaterials[subMeshIndex], out id))
        {
            return id;
        }
        MaterialParam materialParam = MaterialParam.ConvertUnityMaterial(renderer.sharedMaterials[subMeshIndex]);
        if (materialParam == null)
            return -1;
        GPUMaterial gpuMtl = materialParam.ConvertToGPUMaterial();
        //gpuMtl.baseColor = bsdfMaterial.matte.kd.spectrum.LinearToVector4(); //_Color.linear;

        id = gpuMaterials.Count;
        materialIds.Add(renderer.sharedMaterials[subMeshIndex], id);
        gpuMaterials.Add(gpuMtl);
        return id;
    }

    void SetupDistributions()
    {
        Distributions1D.Clear();
        //first, 
        List<float> lightObjectDistribution = new List<float>();
        for (int i = 0; i < areaLightInstances.Count; ++i)
        {
            lightObjectDistribution.Add(areaLightInstances[i].area * areaLightInstances[i].radiance.magnitude);
        }
        Distribution1D lightObjDistribution = new Distribution1D(lightObjectDistribution.ToArray(), 0, lightObjectDistribution.Count, 0, lightObjectDistribution.Count);

        Distributions1D.AddRange(lightObjDistribution.GetGPUDistributions());
        GPUDistributionDiscript discript = new GPUDistributionDiscript
        {
            start = 0,
            num = lightObjectDistribution.Count,
            unum = 0,
            funcInt = lightObjDistribution.Intergal(),
            domain = new Vector4(lightObjDistribution.domain.x, lightObjDistribution.domain.y, 0, 0)
        };
        gpuDistributionDiscripts.Add(discript);
        //test samplelightsource
        float pdf = 0;
        float uremmap = 0;
        int lightIndex = lightObjDistribution.SampleDiscrete(UnityEngine.Random.Range(0.0f, 1.0f), out pdf, out uremmap);
        Debug.Log("lightIndex = " + lightIndex);
        //test end

        var areaLightEnumerator = meshDistributions.GetEnumerator();
        while (areaLightEnumerator.MoveNext())
        {
            AreaLightResource areaLightResource = areaLightEnumerator.Current.Value;
            discript = new GPUDistributionDiscript
            {
                start = Distributions1D.Count,
                num = areaLightResource.triangleAreas.Count,
                unum = 0,
                funcInt = areaLightResource.triangleDistributions.Intergal(),
                domain = new Vector4(areaLightResource.triangleDistributions.domain.x, areaLightResource.triangleDistributions.domain.y, 0, 0)
            };
            areaLightResource.discriptAddress = gpuDistributionDiscripts.Count;
            for (int i = 0; i < areaLightResource.gpuLightIndices.Count; ++i)
            {
                GPULight gpuLight = gpuLights[areaLightResource.gpuLightIndices[i]];
                gpuLight.distributionDiscriptIndex = areaLightResource.discriptAddress;
                gpuLights[areaLightResource.gpuLightIndices[i]] = gpuLight;
            }
            gpuDistributionDiscripts.Add(discript);
            Distributions1D.AddRange(areaLightResource.triangleDistributions.GetGPUDistributions());
        }

        
    }

    public void SetComputeShaderGPUData(ComputeShader cs, int kernel)
    {
        if (lightBuffer == null)
        {
            if (gpuLights.Count > 0)
            {
                lightBuffer = new ComputeBuffer(gpuLights.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPULight)), ComputeBufferType.Structured);
                lightBuffer.SetData(gpuLights);
            }
            else
            {
                lightBuffer = new ComputeBuffer(1, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPULight)), ComputeBufferType.Structured);
            }
        }

        if (BVHAccel.NVMethod)
        {
            cs.SetBuffer(kernel, "BVHTree", BVHBuffer);
            cs.SetBuffer(kernel, "WoopTriangles", woopTriBuffer);
            cs.SetBuffer(kernel, "WoopTriangleIndices", woopTriIndexBuffer);
        }
        else
            cs.SetBuffer(kernel, "BVHTree2", BVHBuffer);

        cs.SetBuffer(kernel, "Vertices", verticesBuffer);
        cs.SetBuffer(kernel, "TriangleIndices", triangleBuffer);
        
        //cs.SetBuffer(kernel, "Intersections", intersectBuffer);
        cs.SetBuffer(kernel, "MeshInstances", meshInstanceBuffer);
        
        cs.SetBuffer(kernel, "lights", lightBuffer);
        cs.SetBuffer(kernel, "materials", materialBuffer);
        cs.SetInt(PathTracingParam._InstBVHAddr, instBVHNodeAddr);
        cs.SetInt(PathTracingParam._BVHNodesNum, bvhAccel.m_nodes.Count);
        //if (_envmapEnable)
        //    cs.EnableKeyword("_ENVMAP_ENABLE");
        //else
        //    cs.DisableKeyword("_ENVMAP_ENABLE");
        cs.SetBool("_EnvMapEnable", _envmapEnable);
        cs.SetBool("_UniformSampleLight", _uniformSampleLight);
        //if (_uniformSampleLight)
        //    cs.EnableKeyword("_UNIFORM_SAMPLE_LIGHT");
        //else
        //    cs.DisableKeyword("_UNIFORM_SAMPLE_LIGHT"); 

        //light distributions setting
        if (distribution1DBuffer == null && Distributions1D.Count > 0)
        {
            distribution1DBuffer = new ComputeBuffer(Distributions1D.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Default);
            distribution1DBuffer.SetData(Distributions1D.ToArray());
        }

        if (distributionDiscriptBuffer == null)
        {
            distributionDiscriptBuffer = new ComputeBuffer(gpuDistributionDiscripts.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(GPUDistributionDiscript)), ComputeBufferType.Structured);
            distributionDiscriptBuffer.SetData(gpuDistributionDiscripts.ToArray());
        }

        cs.SetBuffer(kernel, "Distributions1D", distribution1DBuffer);
        cs.SetBuffer(kernel, "DistributionDiscripts", distributionDiscriptBuffer);

        //enviroment map setting
        if (envLight.textureRadiance != null)
        {
            cs.SetTexture(kernel, PathTracingParam._LatitudeLongitudeMap, envLight.textureRadiance);
            cs.SetInt("enviromentTextureMask", 1);
            cs.SetFloat(PathTracingParam._EnvmapRotation, envLight.rotation);
        }
        else
        {
            cs.SetInt("enviromentTextureMask", 0);
            cs.SetTexture(kernel, PathTracingParam._LatitudeLongitudeMap, Texture2D.blackTexture);
            cs.SetVector("enviromentColor", envLight.radiance);
        }

        if (envLight.envmapDistributions != null)
        {
            cs.SetFloat(PathTracingParam._EnvMapDistributionInt, envLight.envmapDistributions.Intergal());

            if (envLightMarginalBuffer == null)
            {
                List<Vector2> marginals = envLight.envmapDistributions.GetGPUMarginalDistributions();
                envLightMarginalBuffer = new ComputeBuffer(marginals.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
                envLightMarginalBuffer.SetData(marginals);
            }

            if (envLightConditionBuffer == null)
            {
                List<Vector2> conditions = envLight.envmapDistributions.GetGPUConditionalDistributions();
                envLightConditionBuffer = new ComputeBuffer(conditions.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
                envLightConditionBuffer.SetData(conditions);
            }

            if (envLightConditionFuncIntsBuffer == null)
            {
                List<float> conditionalFuncInts = envLight.envmapDistributions.GetGPUConditionFuncInts();
                envLightConditionFuncIntsBuffer =
                    new ComputeBuffer(conditionalFuncInts.Count, sizeof(float), ComputeBufferType.Structured);
                envLightConditionFuncIntsBuffer.SetData(conditionalFuncInts);
            }

            cs.SetBuffer(kernel, PathTracingParam._EnvmapMarginals, envLightMarginalBuffer);
            cs.SetBuffer(kernel, PathTracingParam._EnvmapConditions, envLightConditionBuffer);
            cs.SetBuffer(kernel, PathTracingParam._EnvmapConditionFuncInts, envLightConditionFuncIntsBuffer);
            cs.SetVector(PathTracingParam._EnvMapDistributionSize, new Vector2(envLight.envmapDistributions.size.x, envLight.envmapDistributions.size.y));
            
            //just for test
            //cs.SetBuffer(kernel, "EnvmapDistributions", envLight.computeBuffer);
        }
        cs.SetInt("_EnvLightIndex", envLightIndex);
        //cs.SetBool("_UniformSampleLight", _uniformSampleLight);

        cs.SetMatrix(PathTracingParam._WorldToRaster, WorldToRaster);
        cs.SetMatrix(PathTracingParam._RasterToCamera, RasterToCamera);
        cs.SetFloat(PathTracingParam._CameraConeSpreadAngle, cameraConeSpreadAngle);
    }

    public void Release()
    {
        void ReleaseComputeBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        ReleaseComputeBuffer(woopTriBuffer);
        ReleaseComputeBuffer(woopTriIndexBuffer);
        ReleaseComputeBuffer(verticesBuffer);
        ReleaseComputeBuffer(triangleBuffer);
        ReleaseComputeBuffer(meshInstanceBuffer);
        ReleaseComputeBuffer(BVHBuffer);
        //ReleaseComputeBuffer(intersectBuffer);
        ReleaseComputeBuffer(materialBuffer);
        ReleaseComputeBuffer(lightBuffer);
        ReleaseComputeBuffer(distribution1DBuffer);
        ReleaseComputeBuffer(distributionDiscriptBuffer);
        ReleaseComputeBuffer(envLightMarginalBuffer);
        ReleaseComputeBuffer(envLightConditionBuffer);
        ReleaseComputeBuffer(envLightConditionFuncIntsBuffer);
    }

    public BVHAccel BVH
    {
        get
        {
            return bvhAccel;
        }
    }

    public bool IsRunalbe()
    {
        return areaLightInstances.Count > 0 || _envmapEnable;
    }
}

public class GPUFilterData
{
    private Filter _filter;

    ComputeBuffer filterMarginalBuffer;
    ComputeBuffer filterConditionBuffer;
    ComputeBuffer filterConditionsFuncIntsBuffer;

    public void Setup(Filter filter)
    {
        _filter = filter;

    }

    public Filter filter
    {
        get
        {
            return _filter;
        }
    }

    public void SetComputeShaderGPUData(ComputeShader cs, int kernel)
    {
        Vector2Int filterSize = _filter.GetDistributionSize();
        Distribution2D filterDistribution = _filter.SampleDistributions();

        if (filterMarginalBuffer == null)
        {
            List<Vector2> marginal = _filter.GetGPUMarginalDistributions();
            filterMarginalBuffer = new ComputeBuffer(marginal.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);

            filterMarginalBuffer.SetData(marginal.ToArray());
        }

        if (filterConditionBuffer == null)
        {
            List<Vector2> conditional = _filter.GetGPUConditionalDistributions();
            filterConditionBuffer = new ComputeBuffer(filterSize.x * (filterSize.y + 1), System.Runtime.InteropServices.Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
            filterConditionBuffer.SetData(conditional.ToArray());
        }

        if (filterConditionsFuncIntsBuffer == null)
        {
            List<float> conditionFuncInts = filterDistribution.GetGPUConditionFuncInts();
            filterConditionsFuncIntsBuffer =
                new ComputeBuffer(conditionFuncInts.Count, sizeof(float), ComputeBufferType.Structured);
            filterConditionsFuncIntsBuffer.SetData(conditionFuncInts);
        }

        cs.SetBuffer(kernel, PathTracingParam._FilterMarginals, filterMarginalBuffer);
        cs.SetBuffer(kernel, PathTracingParam._FilterConditions, filterConditionBuffer);
        cs.SetBuffer(kernel, PathTracingParam._FilterConditionsFuncInts, filterConditionsFuncIntsBuffer);

        cs.SetInt(PathTracingParam._MarginalNum, filterSize.y);
        cs.SetInt(PathTracingParam._ConditionNum, filterSize.x);
        Bounds2D domain = _filter.GetDomain();
        cs.SetVector(PathTracingParam._FilterDomain, new Vector4(domain.min[0], domain.max[0], domain.min[1], domain.max[1]));
        cs.SetFloat(PathTracingParam._FilterFuncInt, filterDistribution.Intergal());
    }

    public void Release()
    {
        void ReleaseComputeBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        ReleaseComputeBuffer(filterMarginalBuffer);
        ReleaseComputeBuffer(filterConditionBuffer);
        ReleaseComputeBuffer(filterConditionsFuncIntsBuffer);
    }
}
