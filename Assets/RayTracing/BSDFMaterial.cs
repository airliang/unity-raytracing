using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BSDFTextureType
{
    Constant,
    Bilerp,
    Image,
}

public enum BSDFTextureUVMapping
{
    UVMapping2D,
    SphericalMapping2D,
}

[System.Serializable]
public class UVMapping2D
{
    public float su = 1;
    public float sv = 1;
    public float du = 0;
    public float dv = 0;
}

[System.Serializable]
public struct BSDFFloatTexture
{
    public BSDFTextureType type;
    public float constantValue;
    public Texture2D image;
    public string imageFile;
    public bool gamma;
    public TextureWrapMode wrap;
    public BSDFTextureUVMapping mappingType;
    public UVMapping2D uvMapping2D;
}

[System.Serializable]
public struct BSDFSpectrumTexture
{
    public BSDFTextureType type;
    public Color spectrum;
    public Texture2D image;
    public string imageFile;
    public bool gamma;
    public TextureWrapMode wrap;
    public BSDFTextureUVMapping uvmapping;
    public BSDFTextureUVMapping mappingType;
    public UVMapping2D uvMapping2D;
}

[System.Serializable]
public struct Plastic
{
    public BSDFSpectrumTexture kd;
    public BSDFSpectrumTexture ks;

    public BSDFFloatTexture roughnessTexture;
}

[System.Serializable]
public struct Mirror
{
    public BSDFSpectrumTexture kr;
}

[System.Serializable]
public struct Matte
{
    public BSDFSpectrumTexture kd;
    public BSDFFloatTexture sigma;
}

[System.Serializable]
public struct Metal
{
    public BSDFSpectrumTexture kd;
    public BSDFFloatTexture sigma;
}

[System.Serializable]
public struct Glass
{
    public BSDFSpectrumTexture kr;  //reflection
    public BSDFSpectrumTexture ks;  //transmission
    public BSDFFloatTexture uRougness;
    public BSDFFloatTexture vRougness;
    public BSDFFloatTexture index;
}

public class BSDFMaterial : MonoBehaviour
{
    public enum BSDFType
    {
        Matte,
        Plastic,
        Mirror,
        Metal,
        Glass,
    }

    public BSDFType materialType;

    [SerializeField]
    public Plastic plastic;
    [SerializeField]
    public Matte matte;
    [SerializeField]
    public Mirror mirror;
    [SerializeField]
    public Glass glass;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
