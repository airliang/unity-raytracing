using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public enum BxDFType
{
    BSDF_REFLECTION = 1 << 0,
    BSDF_TRANSMISSION = 1 << 1,
    BSDF_DIFFUSE = 1 << 2,
    BSDF_GLOSSY = 1 << 3,
    BSDF_SPECULAR = 1 << 4,
    BSDF_DISNEY = 1 << 5,
    BSDF_ALL = BSDF_DIFFUSE | BSDF_GLOSSY | BSDF_SPECULAR | BSDF_REFLECTION |
               BSDF_TRANSMISSION,
};

public enum BSDFMaterialType
{
    Matte,
    Plastic,
    Metal,
    Mirror,
    Glass,
    Substrate,
    Disney,
}

public class MaterialParam
{
    public int materialType;
    public Texture2D AlbedoMap;
    public Vector4 Albedo_ST;
    public Color BaseColor = Color.white;
    public Vector3 LinearBaseColor = Vector3.one;
    private bool useLinearBaseColor = false;
    public Texture2D NormalMap;

    public Color Transmission = Color.white;

    //public Texture2D SpecularGlossMap;
    public Texture2D MetallicGlossMap;
    public Color GlossySpecularColor = Color.white;
    public float fresnelType;
    public bool remappingRoughness = false;

    //disney params
    public struct DisneyParam
    {
        public float Metallic;
        public float Specular;
        public float Roughness;
        public float Anisotropy;
        public float Cutoff;
    }

    DisneyParam disneyParam;

    public float RoughnessU;
    public float RoughnessV;
    public Vector3 Eta;
    public Vector3 K;

    static float RoughnessToAlpha(float roughness)
    {
        roughness = Mathf.Max(roughness, 0.001f);
        float x = Mathf.Log(roughness);
        return 1.62142f + 0.819955f * x + 0.1734f * x * x +
            0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
    }

    public static MaterialParam ConvertUnityMaterial(Material material)
    {
        MaterialParam materialParam = new MaterialParam();
        if (material.shader.name == "Standard")
        {

        }
        else if (material.shader.name == "RayTracing/Disney")
        {
            materialParam.materialType = (int)BSDFMaterialType.Matte;
            Texture mainTex = material.GetTexture("_MainTex");
            if (mainTex != null)
                materialParam.AlbedoMap = mainTex as Texture2D;
            materialParam.BaseColor = material.GetColor("_BaseColor");
            Texture normalTex = material.GetTexture("_NormalTex");
            if (normalTex != null)
                materialParam.NormalMap = normalTex as Texture2D;
            Texture metallicTex = material.GetTexture("_MetallicGlossMap");
            if (metallicTex != null)
                materialParam.MetallicGlossMap = metallicTex as Texture2D;
            materialParam.disneyParam.Metallic = material.GetFloat("_metallic");
            materialParam.disneyParam.Roughness = material.GetFloat("_roughness");
            materialParam.disneyParam.Specular = material.GetFloat("_specular");
            materialParam.disneyParam.Anisotropy = material.GetFloat("_anisotropy");
            materialParam.disneyParam.Cutoff = material.GetFloat("_Cutoff");
        }
        else if (material.shader.name == "RayTracing/Uber")
        {
            materialParam.materialType = material.GetInt("_MaterialType");
            Texture mainTex = material.GetTexture("_MainTex");
            if (mainTex != null)
                materialParam.AlbedoMap = mainTex as Texture2D;
            materialParam.BaseColor = material.GetColor("_BaseColor");
            materialParam.useLinearBaseColor = material.GetFloat("_UseLinearBaseColor") == 1.0f;
            materialParam.LinearBaseColor = material.GetVector("_BaseColorLinear");
            Texture normalTex = material.GetTexture("_NormalTex");
            if (normalTex != null)
                materialParam.NormalMap = normalTex as Texture2D;
            materialParam.Transmission = material.GetColor("_t");

            if (materialParam.materialType == (int)BSDFMaterialType.Plastic)
            {
                Texture metallicTex = material.GetTexture("_GlossySpecularTex");
                if (metallicTex != null)
                    materialParam.MetallicGlossMap = metallicTex as Texture2D;
                materialParam.GlossySpecularColor = material.GetColor("_GlossySpecularColor");
                materialParam.RoughnessU = material.GetFloat("_roughnessU");
                materialParam.RoughnessV = material.GetFloat("_roughnessV");
            }
            else if (materialParam.materialType == (int)BSDFMaterialType.Metal)
            {
                materialParam.RoughnessU = material.GetFloat("_roughnessU");
                materialParam.RoughnessV = material.GetFloat("_roughnessV");
                materialParam.Eta = material.GetVector("_eta");
                materialParam.K = material.GetVector("_k");
            }
            else if (materialParam.materialType == (int)BSDFMaterialType.Glass)
            {
                materialParam.RoughnessU = material.GetFloat("_roughnessU");
                materialParam.RoughnessV = material.GetFloat("_roughnessV");
                materialParam.Eta = material.GetVector("_eta");
            }
            else if (materialParam.materialType == (int)BSDFMaterialType.Substrate)
            {
                materialParam.RoughnessU = material.GetFloat("_roughnessU");
                materialParam.RoughnessV = material.GetFloat("_roughnessV");
                materialParam.Eta = material.GetVector("_eta");
                materialParam.GlossySpecularColor = material.GetColor("_GlossySpecularColor");
            }
            materialParam.fresnelType = material.GetFloat("_FresnelType");
            materialParam.Albedo_ST = material.GetVector("_MainTex_ST");
        }
        else if (material.shader.name == "RayTracing/AreaLight")
        {
            materialParam.BaseColor = Color.black;
            materialParam.Transmission = Color.black;
        }
        else
        {
            //return null;
            Color color = Color.black;
            if (material.HasProperty("_Color"))
            {
                color = material.GetColor("_Color");
            }
            else if (material.HasProperty("_MainColor"))
            {
                color = material.GetColor("_MainColor");
            }
            else if (material.HasProperty("_BaseColor"))
            {
                color = material.GetColor("_BaseColor");
            }

            if (color.a < 1.0f)
                return null;
        }

        if (materialParam.remappingRoughness)
        {
            materialParam.RoughnessU = RoughnessToAlpha(materialParam.RoughnessU);
            materialParam.RoughnessV = RoughnessToAlpha(materialParam.RoughnessV);
        }
        return materialParam;
    }

    public GPUMaterial ConvertToGPUMaterial()
    {
        GPUMaterial gpuMaterial = new GPUMaterial();
        if (AlbedoMap != null)
        {
            gpuMaterial.albedoMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddAlbedoTexture(AlbedoMap));
            uint mask = MathUtil.SingleToUint32Bits(gpuMaterial.albedoMapMask) & 0x80000000;
            mask = mask >> 31;
            if (((MathUtil.SingleToUint32Bits(gpuMaterial.albedoMapMask) & 0x80000000) >> 31) > 0)
                Debug.Log("mask = " + mask);
        }
        else
            gpuMaterial.albedoMapMask = MathUtil.UInt32BitsToSingle(0);
        gpuMaterial.baseColor = useLinearBaseColor ? LinearBaseColor : BaseColor.LinearToVector3();
        gpuMaterial.transmission = Transmission.LinearToVector3();
        gpuMaterial.fresnelType = fresnelType;
        gpuMaterial.specularColor = GlossySpecularColor.ToVector3();
        gpuMaterial.albedo_ST = Albedo_ST;

        if (NormalMap != null)
        {
            gpuMaterial.normalMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddNormalTexture(NormalMap));
        }
        else
            gpuMaterial.normalMapMask = MathUtil.UInt32BitsToSingle(0);

        if (MetallicGlossMap != null)
        {
            gpuMaterial.metallicMapMask = MathUtil.UInt32BitsToSingle(RayTracingTextures.Instance.AddMetallicTexture(MetallicGlossMap));
            //set the channel mask;
        }
        else
            gpuMaterial.metallicMapMask = MathUtil.UInt32BitsToSingle(0);
        gpuMaterial.materialType = materialType;
        if (materialType == (int)BSDFMaterialType.Disney)
        {
            gpuMaterial.metallic = disneyParam.Metallic;
            gpuMaterial.roughness = disneyParam.Roughness;
            gpuMaterial.specular = disneyParam.Specular;
            gpuMaterial.anisotropy = disneyParam.Anisotropy;
        }
        else if (materialType == (int)BSDFMaterialType.Plastic)
        {
            gpuMaterial.roughness = RoughnessU;
            gpuMaterial.anisotropy = RoughnessV;
            gpuMaterial.specularColor = GlossySpecularColor.LinearToVector3();
        }
        else if (materialType == (int)BSDFMaterialType.Metal)
        {
            gpuMaterial.roughness = RoughnessU;
            gpuMaterial.anisotropy = RoughnessV;
            gpuMaterial.eta = Eta;
            gpuMaterial.k = K;
        }
        else if (materialType == (int)BSDFMaterialType.Glass)
        {
            gpuMaterial.roughness = RoughnessU;
            gpuMaterial.anisotropy = RoughnessV;
            gpuMaterial.eta = Eta;
        }
        else if (materialType == (int)BSDFMaterialType.Substrate)
        {
            gpuMaterial.roughness = RoughnessU;
            gpuMaterial.anisotropy = RoughnessV;
            gpuMaterial.eta = Eta;
        }

        return gpuMaterial;
    }
}

public class RayTracingTextures : Singleton<RayTracingTextures>
{
    //public Texture2DArray m_albedos128;
    //public Texture2DArray m_albedos256;
    //public Texture2DArray m_albedos512;
    //public Texture2DArray m_albedos1024;
    ////public RenderTexture m_albedos1024;
    //public Texture2DArray m_albedos2048;

    //public Texture2DArray m_normals128;
    //public Texture2DArray m_normals256;
    //public Texture2DArray m_normals512;
    //public Texture2DArray m_normals1024;
    //public Texture2DArray m_normals2048;

    //public Texture2DArray m_metallics128;
    //public Texture2DArray m_metallics256;
    //public Texture2DArray m_metallics512;
    //public Texture2DArray m_metallics1024;
    //public Texture2DArray m_metallics2048;

    public RenderTexture m_albedos;
    public RenderTexture m_normals;
    public RenderTexture m_metallics;

    private Dictionary<Texture, uint> albedoMapMasks = new Dictionary<Texture, uint>();
    private Dictionary<Texture, uint> normalMapMasks = new Dictionary<Texture, uint>();
    private Dictionary<Texture, uint> metallicMapMasks = new Dictionary<Texture, uint>();
    private int m_albedoArrayCounters = 0;
    private int m_normalArrayCounters = 0;
    private int m_metallicArrayCounters = 0;

    private int m_maxAlbedoSlice = 16;
    private int m_maxNormalSlice = 16;
    private int m_maxMetallicSlice = 16;

    private static RenderTexture CreateRT(int width, int height, int depth, TextureDimension _dimension, int _volumeDepth, string _name)
    {
        return new RenderTexture(width, height, depth, GraphicsFormat.R8G8B8A8_UNorm)
        {
            name = _name,
            dimension = _dimension,
            volumeDepth = _volumeDepth,
            useMipMap = true,
        };
    }

    public Texture GetAlbedo2DArray(int size)
    {
        
        if (m_albedos == null)
        {
            m_albedos = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxAlbedoSlice, "albedoArray512");
        }

        return m_albedos;
    }

    public Texture GetNormal2DArray(int size)
    {
        if (m_normals == null)
        {
            m_normals = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxNormalSlice, "normalArray512");
        }

        return m_normals;
    }

    public uint AddAlbedoTexture(Texture2D texture)
    {
        uint mask = 0x80000000;
        if (albedoMapMasks.TryGetValue(texture, out mask))
        {
            return mask;
        }
 
        if (m_albedos == null)
        {
            m_albedos = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxAlbedoSlice, "albedoArray512");
        }

        if (m_albedoArrayCounters >= m_maxAlbedoSlice)
        {
            m_albedos.Release();
            m_maxAlbedoSlice += 16;
            m_albedos = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxAlbedoSlice, "albedoArray512");
            var enumerator = albedoMapMasks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Texture albedo = enumerator.Current.Key;
                uint textMask = enumerator.Current.Value;
                int slice = (int)(textMask & 0x000000ff);
                Graphics.Blit(texture, m_albedos, 0, slice);
            }
        }

        Graphics.Blit(texture, m_albedos, 0, m_albedoArrayCounters);

        //Graphics.CopyTexture(texture, 0, dstTextureArray, m_textureArrayCounters[textureArrayId]);
        int textureArrayId = 0;
        mask = 0x80000000;
        mask |= (uint)((m_albedoArrayCounters & 0x000000ff) | ((textureArrayId & 0xff) << 8));

        m_albedoArrayCounters++;
        albedoMapMasks.Add(texture, mask);

        return mask;
    }

    public uint AddNormalTexture(Texture2D texture)
    {
        uint mask = 0x80000000;
        if (normalMapMasks.TryGetValue(texture, out mask))
        {
            return mask;
        }

        if (m_normals == null)
        {
            m_normals = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxNormalSlice, "normalArray512");
        }

        if (m_normalArrayCounters >= m_maxNormalSlice)
        {
            m_normals.Release();
            m_maxNormalSlice += 16;
            m_normals = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxNormalSlice, "albedoArray512");
            var enumerator = normalMapMasks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Texture normal = enumerator.Current.Key;
                uint textMask = enumerator.Current.Value;
                int slice = (int)(textMask & 0x000000ff);
                Graphics.Blit(texture, m_normals, 0, slice);
            }
        }

        Graphics.Blit(texture, m_normals, 0, m_normalArrayCounters);

        int textureArrayId = 0;
        mask = 0x80000000;
        mask |= (uint)((m_normalArrayCounters & 0x000000ff) | (textureArrayId & 0xff << 8));
        m_normalArrayCounters++;
        normalMapMasks.Add(texture, mask);

        return mask;
    }

    public uint AddMetallicTexture(Texture2D texture)
    {
        uint mask = 0x80000000;
        if (metallicMapMasks.TryGetValue(texture, out mask))
        {
            return mask;
        }

        if (m_metallics == null)
        {
            m_metallics = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxMetallicSlice, "metallicArray512");
        }

        if (m_metallicArrayCounters >= m_maxMetallicSlice)
        {
            m_metallics.Release();
            m_maxMetallicSlice += 16;
            m_metallics = CreateRT(512, 512, 0, TextureDimension.Tex2DArray, m_maxMetallicSlice, "metallicArray512");
            var enumerator = metallicMapMasks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Texture metallic = enumerator.Current.Key;
                uint textMask = enumerator.Current.Value;
                int slice = (int)(textMask & 0x000000ff);
                Graphics.Blit(metallic, m_metallics, 0, slice);
            }
        }

        Graphics.Blit(texture, m_normals, 0, m_normalArrayCounters);

        mask = 0x80000000;
        int textureArrayId = 0;
        mask |= (uint)((m_metallicArrayCounters & 0x000000ff) | (textureArrayId & 0xff << 8));
        m_metallicArrayCounters++;
        metallicMapMasks.Add(texture, mask);

        return mask;
    }

    public void Release()
    {
        void ReleaseTextureArray(Texture texture2DArray)
        {
            if (texture2DArray != null)
            {
                Object.Destroy(texture2DArray);
            }
            texture2DArray = null;
        }

        ReleaseTextureArray(m_albedos);
        ReleaseTextureArray(m_normals);
        ReleaseTextureArray(m_metallics);

        albedoMapMasks.Clear();
        normalMapMasks.Clear();
        metallicMapMasks.Clear();


        m_albedoArrayCounters = 0;
        m_normalArrayCounters = 0;
        m_metallicArrayCounters = 0;

    }
}
