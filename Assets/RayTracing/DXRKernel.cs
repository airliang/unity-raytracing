using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public class DXRKernel : TracingKernel
{
    RaytracingData _rayTracingData;
    private const int k_MaxNumSubMeshes = 32;
    public RayTracingAccelerationStructure rtas = null;
    private RayTracingShader pathTracing = null;
    private RayTracingShader generateReservoirSamples = null;
    private ComputeShader spatialReuse = null;
    private MeshRenderer[] renderers = null;
    RayTracingSubMeshFlags[] m_SubMeshFlagArray = new RayTracingSubMeshFlags[k_MaxNumSubMeshes];
    Material gBufferMaterial = null;
    CommandBuffer renderGBufferCmd = null;
    private CommandBuffer cmdDXR = null;

    public Matrix4x4 _RasterToScreen;
    public Matrix4x4 _RasterToCamera;
    public Matrix4x4 _WorldToRaster;
    public float cameraConeSpreadAngle = 0;
    GPUFilterData gpuFilterData;

    ComputeBuffer filterMarginalBuffer;
    ComputeBuffer filterConditionBuffer;
    ComputeBuffer filterConditionsFuncIntsBuffer;

    private bool supportedRTX = false;

    ComputeBuffer RNGBuffer;
    ComputeShader InitRandom;
    int framesNum = 0;

    public List<Vector3Int> lightTriangles = new List<Vector3Int>();
    public List<GPUVertex> gpuLightVertices = new List<GPUVertex>();
    public List<GPUAreaLight> gpuAreaLights = new List<GPUAreaLight>();
    private List<GPUTriangleLight> GPUTriangleLights = new List<GPUTriangleLight>();
    private List<GPUDXRMaterial> gpuMaterials = new List<GPUDXRMaterial>();
    Dictionary<Material, int> materialIds = new Dictionary<Material, int>();

    ComputeBuffer lightBuffer;
    ComputeBuffer triangleLightsBuffer;
    ComputeBuffer lightDistributionBuffer;
    ComputeBuffer lightDistributionDiscriptBuffer;

    ComputeBuffer lightTrianglesBuffer;
    ComputeBuffer lightVerticesBuffer;

    ComputeBuffer materialBuffer;

    EnviromentLight envLight = null;
    ComputeBuffer envLightMarginalBuffer;
    ComputeBuffer envLightConditionBuffer;
    ComputeBuffer envLightConditionFuncIntsBuffer;

    ComputeBuffer reservoirSamplesBuffer;
    ComputeBuffer temporalReuseSamplesBuffer;

    const uint RESERVOIR_SAMPLE_SIZE = 32; //32 bytes for each reservoir sample

    public struct GPUAreaLight
    {
        public int distributionDiscriptIndex;
        public int instanceID;
        public int triangleLightOffset;   //start offset in triangleLightsBuffer
        public int triangleLightsNum;
        public Vector3 radiance;
        public float area;
        public Matrix4x4 localToWorld;
    }

    public struct GPUTriangleLight
    {
        public int triangleIndex;   //triangle index in lightTrianglesBuffer
        public int lightIndex;      //light index in lightBuffer
        public float area;
        public float padding;
    }

    public struct GPUDXRMaterial
    {
        //public BSDFMaterial.BSDFType materialType;
        //public Vector4 materialParams;  //x-materialtype, y-sigma, z-roughness
        public int materialType;
        public Vector3 baseColor;       //a is texture mask
        public Vector3 specularColor;
        public Vector3 transmission;
        public float metallic;
        public float specular;      //when material is plastic, stand for ks
        public float roughness;     //roughnessU
        public float anisotropy;    //roughnessV
        public Vector3 eta;
        public Vector3 k;             //metal material absorption
        public float albedoMapMask;
        public float normalMapMask;
        public int   lightIndex;
        public float fresnelType;
        public Vector4 albedo_ST;
    }

    public struct GPUReservoirSample
    {
        public Vector3 position; //the position of the sample in world space
        public float targetFunction;
        public float weightSum;
        public float weight;
    }

    public struct GPUInstanceTransform
    {
        public Matrix4x4 localToWorld;
        public Matrix4x4 worldToLocal;
    }

    class DXRPathTracingParam
    {
        public static int _AccelerationStructure = -1;
        public static int _Output = -1;
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

        public static int _ReservoirSamples = -1;
        public static int _TemporalReuseSamples = -1;
    }

    public DXRKernel(DXRPTResource resourceData)
    {
        supportedRTX = SystemInfo.supportsRayTracing;
        if (supportedRTX)
        {
            InitDXRPathTracingParam();
            pathTracing = resourceData.pathTracing;
            InitRandom = resourceData.InitRandom;
        }
        
    }

    private void InitDXRPathTracingParam()
    {
        DXRPathTracingParam._AccelerationStructure = Shader.PropertyToID("_AccelerationStructure");
        DXRPathTracingParam._Output = Shader.PropertyToID("_Output");
        DXRPathTracingParam._InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
        DXRPathTracingParam._CameraPosWS = Shader.PropertyToID("_CameraPosWS");
        DXRPathTracingParam._CameraFarDistance = Shader.PropertyToID("_CameraFarDistance");
        DXRPathTracingParam._RasterToCamera = Shader.PropertyToID("_RasterToCamera");
        DXRPathTracingParam._CameraToWorld = Shader.PropertyToID("_CameraToWorld");
        DXRPathTracingParam._WorldToRaster = Shader.PropertyToID("_WorldToRaster");
        DXRPathTracingParam._LensRadius = Shader.PropertyToID("_LensRadius");
        DXRPathTracingParam._FocalLength = Shader.PropertyToID("_FocalLength");
        DXRPathTracingParam._FrameIndex = Shader.PropertyToID("_FrameIndex");
        DXRPathTracingParam._MaxDepth = Shader.PropertyToID("_MaxDepth");
        DXRPathTracingParam._MinDepth = Shader.PropertyToID("_MinDepth");
        DXRPathTracingParam._LightsNum = Shader.PropertyToID("_LightsNum");
        DXRPathTracingParam._RNGs = Shader.PropertyToID("_RNGs");
        DXRPathTracingParam._Lights = Shader.PropertyToID("_Lights");
        DXRPathTracingParam._TriangleLights = Shader.PropertyToID("_TriangleLights");
        DXRPathTracingParam._LightDistributions1D = Shader.PropertyToID("_LightDistributions1D");
        DXRPathTracingParam._LightDistributionDiscripts = Shader.PropertyToID("_LightDistributionDiscripts");
        DXRPathTracingParam._LightTriangles = Shader.PropertyToID("_LightTriangles");
        DXRPathTracingParam._LightVertices = Shader.PropertyToID("_LightVertices");
        DXRPathTracingParam._RayConeGBuffer = Shader.PropertyToID("_RayConeGBuffer");
        DXRPathTracingParam._CameraConeSpreadAngle = Shader.PropertyToID("_CameraConeSpreadAngle");
        DXRPathTracingParam._Spectrums = Shader.PropertyToID("_Spectrums");
        DXRPathTracingParam._DebugView = Shader.PropertyToID("_DebugView");
        DXRPathTracingParam._Materials = Shader.PropertyToID("_Materials");
        //DXRPathTracingParam._InstanceTransforms = Shader.PropertyToID("_InstanceTransforms");
        DXRPathTracingParam._EnvironmentColor = Shader.PropertyToID("_EnvironmentColor");
        DXRPathTracingParam._EnvmapRotation = Shader.PropertyToID("_EnvmapRotation");
        DXRPathTracingParam._LatitudeLongitudeMap = Shader.PropertyToID("_LatitudeLongitudeMap");
        DXRPathTracingParam._EnvmapMarginals = Shader.PropertyToID("_EnvmapMarginals");
        DXRPathTracingParam._EnvmapConditions = Shader.PropertyToID("_EnvmapConditions");
        DXRPathTracingParam._EnvmapConditionFuncInts = Shader.PropertyToID("_EnvmapConditionFuncInts");
        DXRPathTracingParam._EnvMapDistributionSize = Shader.PropertyToID("_EnvMapDistributionSize");
        DXRPathTracingParam._EnvMapDistributionInt = Shader.PropertyToID("_EnvMapDistributionInt");
        DXRPathTracingParam._FilterMarginals = Shader.PropertyToID("_FilterMarginals");
        DXRPathTracingParam._FilterConditions = Shader.PropertyToID("_FilterConditions");
        DXRPathTracingParam._FilterConditionsFuncInts = Shader.PropertyToID("_FilterConditionsFuncInts");
        DXRPathTracingParam._MarginalNum = Shader.PropertyToID("_MarginalNum");
        DXRPathTracingParam._ConditionNum = Shader.PropertyToID("_ConditionNum");
        DXRPathTracingParam._FilterDomain = Shader.PropertyToID("_FilterDomain");
        DXRPathTracingParam._FilterFuncInt = Shader.PropertyToID("_FilterFuncInt");
        DXRPathTracingParam._EnvironmentMapEnable = Shader.PropertyToID("_EnvironmentMapEnable");
        DXRPathTracingParam._EnvironmentLightPmf = Shader.PropertyToID("_EnvironmentLightPmf");

        DXRPathTracingParam._ReservoirSamples = Shader.PropertyToID("_ReservoirSamples");
        DXRPathTracingParam._TemporalReuseSamples = Shader.PropertyToID("_TemporalReuseSamples");
    }

    public int GetCurrentSPPCount()
    {
        return framesNum;
    }

    public GPUFilterData GetGPUFilterData()
    {
        return null;
    }

    public GPUSceneData GetGPUSceneData()
    {
        return null;
    }

    private void InitRestir()
    {
        if (reservoirSamplesBuffer == null)
        {
            reservoirSamplesBuffer = new ComputeBuffer(_rayTracingData.OutputTexture.width * _rayTracingData.OutputTexture.height,
            Marshal.SizeOf(typeof(GPUReservoirSample)), ComputeBufferType.Structured);
        }

        if (temporalReuseSamplesBuffer == null)
        {
            temporalReuseSamplesBuffer = new ComputeBuffer(_rayTracingData.OutputTexture.width * _rayTracingData.OutputTexture.height,
            Marshal.SizeOf(typeof(GPUReservoirSample)), ComputeBufferType.Structured);

            GPUReservoirSample[] temporalReuses = new GPUReservoirSample[_rayTracingData.OutputTexture.width * _rayTracingData.OutputTexture.height];
            for (int i = 0; i < temporalReuses.Length; i++)
            {
                temporalReuses[i].position = Vector3.zero;
                temporalReuses[i].weightSum = 0;
                temporalReuses[i].weight = 0;
                temporalReuses[i].targetFunction = 0;
            }
            temporalReuseSamplesBuffer.SetData(temporalReuses);
        }
    }

    public void Release()
    {
        if (rtas != null)
        {
            rtas.Release();
        }

        filterMarginalBuffer?.Release();
        filterConditionBuffer?.Release();
        filterConditionsFuncIntsBuffer?.Release();

        RNGBuffer?.Release();

        lightBuffer?.Release();
        triangleLightsBuffer?.Release();
        lightDistributionBuffer?.Release();
        lightDistributionDiscriptBuffer?.Release();
        lightTrianglesBuffer?.Release();
        lightVerticesBuffer?.Release();
        materialBuffer?.Release();

        envLightMarginalBuffer?.Release();
        envLightConditionBuffer?.Release();
        envLightConditionFuncIntsBuffer?.Release();

        reservoirSamplesBuffer?.Release();
        temporalReuseSamplesBuffer?.Release();
    }

    class AreaLightInstance
    {
        public MeshRenderer lightMeshRenderer;
        public Mesh lightMesh;
        public AreaLightMeshData areaLightMeshData;
        //public GPUAreaLight gpuAreaLight;
    }

    struct AreaLightMeshData
    {
        public int vertexOffset;
        public int triangleOffset;
    }

    void SetupLightsData(MeshRenderer[] meshRenderers, Dictionary<MeshRenderer, uint> renderInstanceIDs)
    {
        List<AreaLightInstance> areaLightInstances = new List<AreaLightInstance>();
        int totalTrianglesCount = 0;
        for (int i = 0; i < meshRenderers.Length; ++i)
        {
            MeshRenderer meshRenderer = meshRenderers[i];

            //BSDFMaterial bsdfMaterial = shapes[i].GetComponent<BSDFMaterial>();
            Transform transform = meshRenderer.transform;
            int lightIndex = -1;

            MeshFilter meshFilter = meshRenderer.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;

            Light lightComponent = meshRenderer.GetComponent<Light>();

            if (lightComponent != null && lightComponent.type == LightType.Area)
            {
                Material lightMaterial = meshRenderer.sharedMaterial;
                Vector3 lightRadiance = Vector3.zero;
                if (lightMaterial != null && lightMaterial.shader.name == "RayTracing/AreaLight")
                {
                    Color emssionColor = lightMaterial.GetColor("_Emission");
                    Vector3 lightIntensity = lightMaterial.GetVector("_Intensity");
                    lightRadiance = emssionColor.LinearToVector3().Mul(lightIntensity);
                }
                gpuAreaLights.Add(new GPUAreaLight() { triangleLightOffset = totalTrianglesCount, instanceID = lightIndex++,
                    radiance = lightRadiance, localToWorld = meshRenderer.transform.localToWorldMatrix });
                totalTrianglesCount += mesh.triangles.Length;
                
                AreaLightInstance areaLightInstance = new AreaLightInstance();
                areaLightInstance.lightMeshRenderer = meshRenderer;
                areaLightInstance.lightMesh = mesh;
                areaLightInstances.Add(areaLightInstance);
                renderInstanceIDs.Add(meshRenderer, (uint)lightIndex);
            }
        }

        List<Mesh> lightMeshList = new List<Mesh>();
        Dictionary<Mesh, AreaLightMeshData> lightMeshDatas = new Dictionary<Mesh, AreaLightMeshData>();
        int vertexOffset = 0;
        int triangleOffset = 0;
        for (int i = 0; i < areaLightInstances.Count; ++i)
        {
            //MeshRenderer meshRenderer = lightRenderers[i];
            AreaLightInstance areaLightInstance = areaLightInstances[i];
            GPUAreaLight gpuAreaLight = gpuAreaLights[i];
            MeshFilter meshFilter = areaLightInstance.lightMeshRenderer.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter.sharedMesh;
            float lightArea = 0;

            List<Vector3> lightMeshVertices = new List<Vector3>();
            List<Vector2> meshUVs = new List<Vector2>();
            List<Vector3> meshVertices = new List<Vector3>();
            List<Vector3> meshNormals = new List<Vector3>();

            if (!lightMeshList.Contains(mesh))
            {
                AreaLightMeshData areaLightMeshData = new AreaLightMeshData() { vertexOffset = vertexOffset, triangleOffset = triangleOffset };
                
                lightMeshDatas.Add(mesh, areaLightMeshData );
                lightMeshList.Add(mesh);

                
                mesh.GetVertices(lightMeshVertices);
                mesh.GetUVs(0, meshUVs);
                mesh.GetVertices(meshVertices);
                mesh.GetNormals(meshNormals);

                for (int v = 0; v < lightMeshVertices.Count; ++v)
                {
                    GPUVertex vertexTmp = new GPUVertex();
                    vertexTmp.position = lightMeshVertices[v];
                    vertexTmp.uv = meshUVs.Count == 0 ? Vector2.zero : meshUVs[v];
                    vertexTmp.normal = meshNormals[v];
                    gpuLightVertices.Add(vertexTmp);
                }

                for (int sm = 0; sm < mesh.subMeshCount; ++sm)
                {
                    List<int> meshTriangles = new List<int>();
                    mesh.GetTriangles(meshTriangles, sm);

                    SubMeshDescriptor subMeshDescriptor = mesh.GetSubMesh(sm);

                    for (int j = 0; j < subMeshDescriptor.indexCount / 3; ++j)
                    {
                        Vector3Int triangleIndex = new Vector3Int(meshTriangles[j * 3 + subMeshDescriptor.indexStart] + vertexOffset,
                            meshTriangles[j * 3 + subMeshDescriptor.indexStart + 1] + vertexOffset, meshTriangles[j * 3 + subMeshDescriptor.indexStart + 2] + vertexOffset);
                        lightTriangles.Add(triangleIndex);
                    }
                }

                vertexOffset += lightMeshVertices.Count;
                gpuAreaLight.triangleLightOffset = triangleOffset;
                triangleOffset += lightTriangles.Count;
            }

            AreaLightMeshData relativeLightMeshData;
            lightMeshDatas.TryGetValue(mesh, out relativeLightMeshData);

            Transform transform = areaLightInstance.lightMeshRenderer.transform;
            int trianglesNum = 0;
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
                    GPUTriangleLight gpuTriangleLight = new GPUTriangleLight();
                    gpuTriangleLight.area = triangleArea;
                    gpuTriangleLight.lightIndex = i;
                    gpuTriangleLight.triangleIndex = relativeLightMeshData.triangleOffset + t / 3;
                    GPUTriangleLights.Add(gpuTriangleLight);
                    trianglesNum++;
                }
            }

            gpuAreaLight.triangleLightsNum = trianglesNum;
            gpuAreaLight.area = lightArea;
            gpuAreaLights[i] = gpuAreaLight;
        }


        if (lightVerticesBuffer == null)
        {
            int vertexSize = Marshal.SizeOf(typeof(GPUVertex));
            lightVerticesBuffer = new ComputeBuffer(gpuLightVertices.Count, vertexSize, ComputeBufferType.Structured);
        }
        lightVerticesBuffer.SetData(gpuLightVertices);

        if (lightTrianglesBuffer == null)
        {
            lightTrianglesBuffer = new ComputeBuffer(lightTriangles.Count, 12, ComputeBufferType.Default);
        }
        lightTrianglesBuffer.SetData(lightTriangles);

        if (lightBuffer == null)
        {
            if (gpuAreaLights.Count > 0)
            {
                lightBuffer = new ComputeBuffer(gpuAreaLights.Count, Marshal.SizeOf(typeof(GPUAreaLight)), ComputeBufferType.Structured);
                lightBuffer.SetData(gpuAreaLights);
            }
            else
            {
                lightBuffer = new ComputeBuffer(1, Marshal.SizeOf(typeof(GPUAreaLight)), ComputeBufferType.Structured);
            }
        }

        List<float> lightObjectDistribution = new List<float>();
        for (int i = 0; i < gpuAreaLights.Count; ++i)
        {
            lightObjectDistribution.Add(gpuAreaLights[i].radiance.magnitude);
        }

        Distribution1D lightObjDistribution = new Distribution1D(lightObjectDistribution.ToArray(), 0, lightObjectDistribution.Count, 0, lightObjectDistribution.Count);

        List<Vector2> LightDistributions1D = new List<Vector2>();
        LightDistributions1D.AddRange(lightObjDistribution.GetGPUDistributions());
        List<GPUDistributionDiscript> gpuDistributionDiscripts = new List<GPUDistributionDiscript>();
        GPUDistributionDiscript discript = new GPUDistributionDiscript
        {
            start = 0,
            num = lightObjectDistribution.Count,
            unum = 0,
            funcInt = lightObjDistribution.Intergal(),
            domain = new Vector4(lightObjDistribution.domain.x, lightObjDistribution.domain.y, 0, 0)
        };
        gpuDistributionDiscripts.Add(discript);

        if (triangleLightsBuffer == null && GPUTriangleLights.Count > 0)
        {
            triangleLightsBuffer = new ComputeBuffer(GPUTriangleLights.Count, Marshal.SizeOf(typeof(GPUTriangleLight)), ComputeBufferType.Default);
            triangleLightsBuffer.SetData(GPUTriangleLights.ToArray());
        }
        //light distributions setting
        if (lightDistributionBuffer == null && LightDistributions1D.Count > 0)
        {
            lightDistributionBuffer = new ComputeBuffer(LightDistributions1D.Count, Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Default);
            lightDistributionBuffer.SetData(LightDistributions1D.ToArray());
        }

        if (lightDistributionDiscriptBuffer == null)
        {
            lightDistributionDiscriptBuffer = new ComputeBuffer(gpuDistributionDiscripts.Count, Marshal.SizeOf(typeof(GPUDistributionDiscript)), ComputeBufferType.Structured);
            lightDistributionDiscriptBuffer.SetData(gpuDistributionDiscripts.ToArray());
        }
    }

    void SetupEnviromentMap()
    {
        Material skyBoxMaterial = RenderSettings.skybox;
        if (skyBoxMaterial != null)
        {
            envLight = new EnviromentLight();
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
                envLight.CreateDistributions();
            }
            else
            {
            }
        }
        else
        {
            envLight.CreateDistributions();
        }
    }

    void SetupMaterialData()
    {
        if (materialBuffer == null)
        {
            materialBuffer = new ComputeBuffer(_rayTracingData.OutputTexture.width * _rayTracingData.OutputTexture.height, 
                Marshal.SizeOf(typeof(GPUMaterial)), ComputeBufferType.Structured);
        }
    }

    GPUDXRMaterial ConvertUnityMaterial(Material material)
    {
        MaterialParam materialParam = MaterialParam.ConvertUnityMaterial(material);

        GPUDXRMaterial gpuMaterial = new GPUDXRMaterial();

        if (materialParam.AlbedoMap != null)
        {
            gpuMaterial.albedoMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddAlbedoTexture(materialParam.AlbedoMap));
            uint mask = MathUtil.SingleToUint32Bits(gpuMaterial.albedoMapMask) & 0x80000000;
            mask = mask >> 31;
            if (((MathUtil.SingleToUint32Bits(gpuMaterial.albedoMapMask) & 0x80000000) >> 31) > 0)
                Debug.Log("mask = " + mask);
        }
        else
            gpuMaterial.albedoMapMask = MathUtil.UInt32BitsToSingle(0);
        gpuMaterial.baseColor = materialParam.useLinearBaseColor ? materialParam.LinearBaseColor : materialParam.BaseColor.LinearToVector3();
        gpuMaterial.transmission = materialParam.Transmission.LinearToVector3();
        gpuMaterial.fresnelType = materialParam.fresnelType;
        gpuMaterial.specularColor = materialParam.GlossySpecularColor.ToVector3();
        gpuMaterial.albedo_ST = materialParam.Albedo_ST;

        if (materialParam.NormalMap != null)
        {
            gpuMaterial.normalMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddNormalTexture(materialParam.NormalMap));
        }
        else
            gpuMaterial.normalMapMask = MathUtil.UInt32BitsToSingle(0);

        if (materialParam.MetallicGlossMap != null)
        {
            //gpuMaterial.metallicMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddMetallicTexture(MetallicGlossMap));
            //set the channel mask;
        }
        //else
        //    gpuMaterial.metallicMapMask = MathUtil.UInt32BitsToSingle(0);
        gpuMaterial.materialType = materialParam.materialType;
        if (materialParam.materialType == (int)BSDFMaterialType.Disney)
        {
            gpuMaterial.metallic = materialParam.disneyParam.Metallic;
            gpuMaterial.roughness = materialParam.disneyParam.Roughness;
            gpuMaterial.specular = materialParam.disneyParam.Specular;
            gpuMaterial.anisotropy = materialParam.disneyParam.Anisotropy;
        }
        else if (materialParam.materialType == (int)BSDFMaterialType.Plastic)
        {
            gpuMaterial.roughness = materialParam.RoughnessU;
            gpuMaterial.anisotropy = materialParam.RoughnessV;
            gpuMaterial.specularColor = materialParam.GlossySpecularColor.LinearToVector3();
        }
        else if (materialParam.materialType == (int)BSDFMaterialType.Metal)
        {
            gpuMaterial.roughness = materialParam.RoughnessU;
            gpuMaterial.anisotropy = materialParam.RoughnessV;
            gpuMaterial.eta = materialParam.Eta;
            gpuMaterial.k = materialParam.K;
        }
        else if (materialParam.materialType == (int)BSDFMaterialType.Glass)
        {
            gpuMaterial.roughness = materialParam.RoughnessU;
            gpuMaterial.anisotropy = materialParam.RoughnessV;
            gpuMaterial.eta = materialParam.Eta;
        }
        else if (materialParam.materialType == (int)BSDFMaterialType.Substrate)
        {
            gpuMaterial.roughness = materialParam.RoughnessU;
            gpuMaterial.anisotropy = materialParam.RoughnessV;
            gpuMaterial.eta = materialParam.Eta;
        }

        return gpuMaterial;
    }

    int SetupMaterials(MeshRenderer renderer, int subMeshIndex, int lightIndex)
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
        GPUDXRMaterial gpuMtl = ConvertUnityMaterial(renderer.sharedMaterials[subMeshIndex]);
        gpuMtl.lightIndex = lightIndex;
        //gpuMtl.baseColor = bsdfMaterial.matte.kd.spectrum.LinearToVector4(); //_Color.linear;

        id = gpuMaterials.Count;
        materialIds.Add(renderer.sharedMaterials[subMeshIndex], id);
        gpuMaterials.Add(gpuMtl);
        return id;
    }

    void InitRNGs()
    {
        if (RNGBuffer == null)
        {
            RNGBuffer = new ComputeBuffer(_rayTracingData.OutputTexture.width * _rayTracingData.OutputTexture.height, sizeof(uint), ComputeBufferType.Structured);

            float rasterWidth = Screen.width;
            float rasterHeight = Screen.height;
            int kInitRandom = InitRandom.FindKernel("CSInitSampler");
            InitRandom.SetBuffer(kInitRandom, "RNGs", RNGBuffer);
            InitRandom.SetVector("rasterSize", new Vector4(rasterWidth, rasterHeight, 0, 0));
            InitRandom.Dispatch(kInitRandom, Mathf.CeilToInt(rasterWidth / 8), Mathf.CeilToInt(rasterHeight / 8), 1);
        }
    }

    private void SetupSceneCamera(Camera camera)
    {
        float rasterWidth = Screen.width;
        float rasterHeight = Screen.height;
        //init the camera parameters

        Matrix4x4 screenToRaster = Matrix4x4.Scale(new Vector3(rasterWidth, rasterHeight, 1)) *
            Matrix4x4.Scale(new Vector3(0.5f, 0.5f, 0.5f)) *
            Matrix4x4.Translate(new Vector3(1, 1, 1));

        _RasterToScreen = screenToRaster.inverse;

        float aspect = rasterWidth / rasterHeight;

        Matrix4x4 proj = camera.projectionMatrix * Matrix4x4.Scale(new Vector3(1, 1, -1));

        Matrix4x4 cameraToScreen = camera.orthographic ? Matrix4x4.Ortho(-camera.orthographicSize * aspect, camera.orthographicSize * aspect,
            -camera.orthographicSize, camera.orthographicSize, camera.nearClipPlane, camera.farClipPlane)
            : proj;//Matrix4x4.Perspective(camera.fieldOfView, aspect, camera.nearClipPlane, camera.farClipPlane);

        cameraConeSpreadAngle = Mathf.Atan(2.0f * Mathf.Tan(Mathf.Deg2Rad * camera.fieldOfView * 0.5f) / Screen.height);

        _RasterToCamera = cameraToScreen.inverse * _RasterToScreen;
        _WorldToRaster = screenToRaster * cameraToScreen * camera.transform.localToWorldMatrix;

        //for test
        Vector3 cameraPoint = new Vector3(300.5f, 120.5f, 0);
        Vector3 nearplanePoint = _RasterToCamera.MultiplyPoint(cameraPoint);//mul(_RasterToCamera, float4(pFilm, 1));
        Vector3 direction = Vector3.Normalize(nearplanePoint);
        Vector3 directionWS = camera.transform.localToWorldMatrix.MultiplyVector(direction);
    }

    public IEnumerator Setup(Camera camera, RaytracingData data)
    {
        if (!supportedRTX)
        {
            yield break;
        }
        while (RaytracingStates.states != RaytracingStates.States.Rendering)
        {
            _rayTracingData = data;
            

            if (rtas == null)
            {
                renderers = GameObject.FindObjectsOfType<MeshRenderer>();
                Dictionary<MeshRenderer, uint> meshRenderIDs = new Dictionary<MeshRenderer, uint>();
                SetupLightsData(renderers, meshRenderIDs);
                SetupEnviromentMap();
                rtas = new RayTracingAccelerationStructure();
                uint instanceIndex = 0;
                uint lightIndex = 0;
                foreach (Renderer renderer in renderers)
                {
                    for (var i = 0; i < k_MaxNumSubMeshes; ++i)
                    {
                        m_SubMeshFlagArray[i] = RayTracingSubMeshFlags.Disabled;
                    }

                    if (renderer.gameObject.activeSelf)
                    {
                        uint instanceID = uint.MaxValue;
                        if (meshRenderIDs.ContainsKey(renderer as MeshRenderer))
                            meshRenderIDs.TryGetValue(renderer as MeshRenderer, out instanceID);

                        int subMeshesNum = 1;
                        renderer.TryGetComponent(out MeshFilter meshFilter);
                        if (meshFilter == null || meshFilter.sharedMesh == null)
                        {
                            continue;
                        }
                        subMeshesNum = meshFilter.sharedMesh.subMeshCount;
                        for (int meshIdx = 0; meshIdx < subMeshesNum; ++meshIdx)
                        {
                            SetupMaterials(renderer as MeshRenderer, meshIdx, (int)instanceID);
                            m_SubMeshFlagArray[meshIdx] = RayTracingSubMeshFlags.Enabled;
                        }

                        bool isLight = renderer.GetComponent<Light>() != null;
                        if (isLight)
                        {
                            instanceID = (lightIndex << 16) | instanceIndex;
                            lightIndex++;
                        }
                        else
                        {
                            instanceID = instanceIndex;
                        }

                        rtas.AddInstance(renderer, m_SubMeshFlagArray, true, false, 0xFF, instanceID);
                        instanceIndex++;
                    }
                }
                rtas.Build();

                
            }

            yield return null;

            gpuFilterData = new GPUFilterData();
            Filter filter = null;
            if (data.filterType == FilterType.Gaussian)
            {
                filter = new GaussianFilter(data.fiterRadius, data.gaussianSigma);
            }
            gpuFilterData.Setup(filter);

            SetupMaterialData();

            if (_rayTracingData.RestirEnable)
            {
                InitRestir();
            }

            RaytracingStates.states = RaytracingStates.States.Rendering;
            yield return null;
        }
    }

    void SetFilterGPUData(CommandBuffer cmd)
    {
        Filter filter = gpuFilterData.filter;

        Vector2Int filterSize = filter.GetDistributionSize();
        Distribution2D filterDistribution = filter.SampleDistributions();

        if (filterMarginalBuffer == null)
        {
            List<Vector2> marginal = filter.GetGPUMarginalDistributions();
            filterMarginalBuffer = new ComputeBuffer(marginal.Count, Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);

            filterMarginalBuffer.SetData(marginal.ToArray());
        }

        if (filterConditionBuffer == null)
        {
            List<Vector2> conditional = filter.GetGPUConditionalDistributions();
            filterConditionBuffer = new ComputeBuffer(filterSize.x * (filterSize.y + 1), Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
            filterConditionBuffer.SetData(conditional.ToArray());
        }

        if (filterConditionsFuncIntsBuffer == null)
        {
            List<float> conditionFuncInts = filterDistribution.GetGPUConditionFuncInts();
            filterConditionsFuncIntsBuffer =
                new ComputeBuffer(conditionFuncInts.Count, sizeof(float), ComputeBufferType.Structured);
            filterConditionsFuncIntsBuffer.SetData(conditionFuncInts);
        }

        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._FilterMarginals, filterMarginalBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._FilterConditions, filterConditionBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._FilterConditionsFuncInts, filterConditionsFuncIntsBuffer);

        cmd.SetRayTracingIntParam(pathTracing, DXRPathTracingParam._MarginalNum, filterSize.y);
        cmd.SetRayTracingIntParam(pathTracing, DXRPathTracingParam._ConditionNum, filterSize.x);
        Bounds2D domain = filter.GetDomain();
        cmd.SetRayTracingVectorParam(pathTracing, DXRPathTracingParam._FilterDomain, new Vector4(domain.min[0], domain.max[0], domain.min[1], domain.max[1]));
        cmd.SetRayTracingFloatParam(pathTracing, DXRPathTracingParam._FilterFuncInt, filterDistribution.Intergal());
    }

    void SetLightGPUData(CommandBuffer cmd)
    {
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._Lights, lightBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._LightDistributions1D, lightDistributionBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._LightDistributionDiscripts, lightDistributionDiscriptBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._LightTriangles, lightTrianglesBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._LightVertices, lightVerticesBuffer);
        cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._TriangleLights, triangleLightsBuffer);

        if (envLight != null && envLight.envmapDistributions != null)
        {
            if (envLight.textureRadiance != null)
            {
                cmd.SetRayTracingTextureParam(pathTracing, DXRPathTracingParam._LatitudeLongitudeMap, envLight.textureRadiance);
            }
            else
            {
                cmd.SetRayTracingTextureParam(pathTracing, DXRPathTracingParam._LatitudeLongitudeMap, Texture2D.blackTexture);
            }

            cmd.SetRayTracingFloatParam(pathTracing, DXRPathTracingParam._EnvMapDistributionInt, envLight.envmapDistributions.Intergal());

            if (envLightMarginalBuffer == null)
            {
                List<Vector2> marginals = envLight.envmapDistributions.GetGPUMarginalDistributions();
                envLightMarginalBuffer = new ComputeBuffer(marginals.Count, Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
                envLightMarginalBuffer.SetData(marginals);
            }

            if (envLightConditionBuffer == null)
            {
                List<Vector2> conditions = envLight.envmapDistributions.GetGPUConditionalDistributions();
                envLightConditionBuffer = new ComputeBuffer(conditions.Count, Marshal.SizeOf(typeof(Vector2)), ComputeBufferType.Structured);
                envLightConditionBuffer.SetData(conditions);
            }

            if (envLightConditionFuncIntsBuffer == null)
            {
                List<float> conditionalFuncInts = envLight.envmapDistributions.GetGPUConditionFuncInts();
                envLightConditionFuncIntsBuffer =
                    new ComputeBuffer(conditionalFuncInts.Count, sizeof(float), ComputeBufferType.Structured);
                envLightConditionFuncIntsBuffer.SetData(conditionalFuncInts);
            }

            //cmd.EnableShaderKeyword("_ENVIRONMENT_MAP_ENABLE");
            cmd.SetRayTracingIntParam(pathTracing, DXRPathTracingParam._EnvironmentMapEnable, 1);
            cmd.SetRayTracingVectorParam(pathTracing, DXRPathTracingParam._EnvironmentColor, envLight.radiance);
            cmd.SetRayTracingFloatParam(pathTracing, DXRPathTracingParam._EnvmapRotation, envLight.rotation);
            cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._EnvmapMarginals, envLightMarginalBuffer);
            cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._EnvmapConditions, envLightConditionBuffer);
            cmd.SetRayTracingBufferParam(pathTracing, DXRPathTracingParam._EnvmapConditionFuncInts, envLightConditionFuncIntsBuffer);
            cmd.SetRayTracingVectorParam(pathTracing, DXRPathTracingParam._EnvMapDistributionSize, new Vector2(envLight.envmapDistributions.size.x, envLight.envmapDistributions.size.y));
            cmd.SetRayTracingFloatParam(pathTracing, DXRPathTracingParam._EnvironmentLightPmf, _rayTracingData._EnvironmentLightPmf);
        }
        else
        {
            //cmd.DisableShaderKeyword("_ENVIRONMENT_MAP_ENABLE");
            cmd.SetRayTracingIntParam(pathTracing, DXRPathTracingParam._EnvironmentMapEnable, 0);
            cmd.SetRayTracingFloatParam(pathTracing, DXRPathTracingParam._EnvironmentLightPmf, 0.0f);
        }
    }

    public bool Update(Camera camera)
    {
        if (!supportedRTX)
        {
            return false;
        }
        if (framesNum++ >= _rayTracingData.SamplesPerPixel)
        {
            return true;
        }
        SetupSceneCamera(camera);

        InitRNGs();

        if (cmdDXR == null)
        {
            cmdDXR = new CommandBuffer();
            cmdDXR.name = "DXR Pathtracing";
        }


        cmdDXR.BeginSample("DXR Pathtracing");
        {

            cmdDXR.SetRayTracingShaderPass(pathTracing, "RayTracing");
            cmdDXR.SetRayTracingAccelerationStructure(pathTracing, DXRPathTracingParam._AccelerationStructure, rtas);
            cmdDXR.SetRayTracingTextureParam(pathTracing, DXRPathTracingParam._Output, _rayTracingData.OutputTexture);
            var projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, false);
            var viewMatrix = camera.worldToCameraMatrix;
            var viewProjMatrix = projMatrix * viewMatrix;
            var invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
            cmdDXR.SetGlobalMatrix(DXRPathTracingParam._InvCameraViewProj, invViewProjMatrix);
            cmdDXR.SetGlobalVector(DXRPathTracingParam._CameraPosWS, camera.transform.position);
            cmdDXR.SetGlobalFloat(DXRPathTracingParam._CameraFarDistance, camera.farClipPlane);
            cmdDXR.SetGlobalMatrix(DXRPathTracingParam._CameraToWorld, camera.transform.localToWorldMatrix);
            cmdDXR.SetGlobalMatrix(DXRPathTracingParam._RasterToCamera, _RasterToCamera);
            cmdDXR.SetGlobalMatrix(DXRPathTracingParam._WorldToRaster, _WorldToRaster);
            cmdDXR.SetGlobalFloat(DXRPathTracingParam._FocalLength, _rayTracingData._FocalLength);
            cmdDXR.SetGlobalFloat(DXRPathTracingParam._LensRadius, _rayTracingData._LensRadius);
            cmdDXR.SetGlobalInt(DXRPathTracingParam._FrameIndex, framesNum);
            cmdDXR.SetGlobalInt(DXRPathTracingParam._MinDepth, _rayTracingData.MinDepth);
            cmdDXR.SetGlobalInt(DXRPathTracingParam._MaxDepth, _rayTracingData.MaxDepth);
            cmdDXR.SetGlobalInt(DXRPathTracingParam._LightsNum, gpuAreaLights.Count + (envLight != null ? 1 : 0));
            cmdDXR.SetGlobalBuffer(DXRPathTracingParam._RNGs, RNGBuffer);
            cmdDXR.SetGlobalTexture(DXRPathTracingParam._RayConeGBuffer, _rayTracingData.RayConeGBuffer);
            cmdDXR.SetGlobalFloat(DXRPathTracingParam._CameraConeSpreadAngle, cameraConeSpreadAngle);
            cmdDXR.SetGlobalTexture(DXRPathTracingParam._Spectrums, _rayTracingData.SpectrumBuffer);
            cmdDXR.SetGlobalInt(DXRPathTracingParam._DebugView, (int)_rayTracingData.viewMode);

            //filter importance sampling
            SetFilterGPUData(cmdDXR);

            SetLightGPUData(cmdDXR);

            cmdDXR.SetGlobalBuffer(DXRPathTracingParam._Materials, materialBuffer);

            if (_rayTracingData.RestirEnable)
            {
                cmdDXR.SetGlobalBuffer(DXRPathTracingParam._ReservoirSamples, reservoirSamplesBuffer);
                cmdDXR.SetGlobalBuffer(DXRPathTracingParam._TemporalReuseSamples, temporalReuseSamplesBuffer);
                cmdDXR.DispatchRays(generateReservoirSamples, "GenerateSamples", (uint)_rayTracingData.OutputTexture.width, (uint)_rayTracingData.OutputTexture.height, 1, camera);
            }
            else
                cmdDXR.DispatchRays(pathTracing, "MyRaygenShader", (uint)_rayTracingData.OutputTexture.width, (uint)_rayTracingData.OutputTexture.height, 1, camera);
        }

            
        cmdDXR.EndSample("DXR Pathtracing");
        Graphics.ExecuteCommandBuffer(cmdDXR);
        cmdDXR.Clear();
        return true;
    }
}
