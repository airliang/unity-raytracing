#ifndef MATERIALS_HLSL
#define MATERIALS_HLSL
#include "disney.hlsl"
#include "bvhaccel.hlsl"
#include "bxdf.hlsl"

#define BSDF_REFLECTION  1 << 0
#define BSDF_TRANSMISSION  1 << 1
#define BSDF_DIFFUSE  1 << 2
#define BSDF_GLOSSY  1 << 3
#define BSDF_SPECULAR  1 << 4
#define BSDF_ALL  BSDF_DIFFUSE | BSDF_GLOSSY | BSDF_SPECULAR | BSDF_REFLECTION | BSDF_TRANSMISSION
#define BSDF_DISNEY 1 << 5

#define Matte 0
#define Plastic 1
#define Metal 2
#define Mirror 3
#define Glass 4
#define Substrate 5
#define Disney 6

#define TEXTURED_PARAM_MASK 0x80000000
#define IS_TEXTURED_PARAM(x) ((x) & 0x80000000)
#define GET_TEXTUREARRAY_ID(x) (((x) & 0x0000ff00) >> 8)
#define GET_TEXTUREARRAY_INDEX(x) ((x) & 0x000000ff)

#define TEXTURE_DEFAULT_SIZE 512

struct TextureSampleInfo
{
	float cosine;
	float coneWidth;
	float screenSpaceArea;
	float uvArea;
	float2 uv;
};

//Texture2DArray albedoTexArray128;
//Texture2DArray albedoTexArray256;
//Texture2DArray albedoTexArray512;
//Texture2DArray albedoTexArray1024;
//
//Texture2DArray normalTexArray128;
//Texture2DArray normalTexArray256;
//Texture2DArray normalTexArray512;
//Texture2DArray normalTexArray1024;
Texture2DArray albedoTexArray;
Texture2DArray normalTexArray;
Texture2DArray glossySpecularTexArray;
SamplerState Albedo_linear_repeat_sampler;
SamplerState Albedo_linear_clamp_sampler;
SamplerState Normal_linear_repeat_sampler;
SamplerState linearRepeatSampler;

float4 SampleAlbedoTexture(float2 uv, int texIndex, float mipmapLevel)
{
	return albedoTexArray.SampleLevel(Albedo_linear_repeat_sampler, float3(uv, texIndex), mipmapLevel);
}

float4 SampleGlossySpecularTexture(float2 uv, int texIndex, float mipmapLevel)
{
	return glossySpecularTexArray.SampleLevel(Albedo_linear_repeat_sampler, float3(uv, texIndex), mipmapLevel);
}

float GetTriangleLODConstant(float screenSpaceArea, float uvArea)
{
	float P_a = screenSpaceArea;     // Eq. 5
	float T_a = uvArea * TEXTURE_DEFAULT_SIZE * TEXTURE_DEFAULT_SIZE;  // Eq. 4
	return 0.5 * max(log2(T_a / P_a), 0); // Eq. 3
}


float ComputeTextureLOD(TextureSampleInfo texLod)
{
	float lambda = GetTriangleLODConstant(texLod.screenSpaceArea, texLod.uvArea);
	lambda += max(log2(abs(texLod.coneWidth)), 0);
	//lambda += 0.5 * log2(512 * 512);
	lambda -= max(log2(abs(texLod.cosine)), 0);
	return max(lambda, 0);
}

void UnpackShadingMaterial(Material material, inout ShadingMaterial shadingMaterial, TextureSampleInfo texLod)
{
	shadingMaterial = (ShadingMaterial)0;
	//check if using texture
	shadingMaterial.materialType = material.materialType;
	int textureArrayId = -1;
	int textureIndex = -1;
	const uint mask = asuint(material.albedoMapMask);
	shadingMaterial.reflectance = material.kd.rgb;
	shadingMaterial.fresnelType = material.fresnelType;
	shadingMaterial.transmission = material.transmission;
	if (IS_TEXTURED_PARAM(mask))
	{
		textureIndex = GET_TEXTUREARRAY_INDEX(mask);
		
		float mipmapLevel = ComputeTextureLOD(texLod);
        float2 uv = texLod.uv.xy * material.albedo_ST.xy + material.albedo_ST.zw;
		float4 albedo = SampleAlbedoTexture(uv, textureIndex, mipmapLevel);
		shadingMaterial.reflectance *= albedo.rgb;
	}
	shadingMaterial.specular = material.ks;
	shadingMaterial.roughness = material.roughness;
	shadingMaterial.roughnessV = material.anisotropy;
	shadingMaterial.k = material.k;
	shadingMaterial.eta = material.eta;
}


void UnpackDisneyMaterial(Material material, inout DisneyMaterial materialDisney, float2 uv)
{
	materialDisney.baseColor = material.kd.xyz;
	materialDisney.metallic = 0;
	materialDisney.specular = 0.0;
	materialDisney.roughness = 0;
	materialDisney.specularTint = 0;
	materialDisney.anisotropy = 0.0;
	materialDisney.sheen = 0;
	materialDisney.sheenTint = 0;
	materialDisney.clearcoat = 0;
	materialDisney.clearcoatGloss = 0;
	materialDisney.ior = 1;
	materialDisney.specularTransmission = 0;
}

int GetMaterialBxDFNum(int bsdfType)
{
	int num = 0;
	num += bsdfType | BSDF_REFLECTION;
	num += bsdfType | BSDF_TRANSMISSION;
	num += bsdfType | BSDF_DIFFUSE;
	num += bsdfType | BSDF_GLOSSY;
	return num;
}

void UnpackFresnel(ShadingMaterial shadingMaterial, out FresnelData fresnel)
{
	fresnel.fresnelType = shadingMaterial.fresnelType;
	fresnel.etaI = 1;
	fresnel.etaT = shadingMaterial.eta;
	fresnel.K = shadingMaterial.k;
	fresnel.R = shadingMaterial.reflectance;
}

void ComputeBxDFLambertReflection(ShadingMaterial shadingMaterial, out BxDFLambertReflection bxdf)
{
	bxdf.R = shadingMaterial.reflectance;
}

void ComputeBxDFPlastic(ShadingMaterial shadingMaterial, out BxDFPlastic bxdf)
{
	bxdf = (BxDFPlastic)0;
	bxdf.alphax = shadingMaterial.roughness; // RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = shadingMaterial.roughnessV;//RoughnessToAlpha(shadingMaterial.roughnessV);
	bxdf.R = shadingMaterial.specular;
	//bxdf.fresnel.fresnelType = FresnelDielectric;
	bxdf.eta = 1.5;
	//bxdf.fresnel.k = 0;

}

void ComputeBxDFMetal(ShadingMaterial shadingMaterial, out BxDFMetal bxdf)
{
	bxdf = (BxDFMetal)0;
	bxdf.alphax = shadingMaterial.roughness;//RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = shadingMaterial.roughnessV;//RoughnessToAlpha(shadingMaterial.roughnessV);
	bxdf.R = 1;
	bxdf.etaI = float3(1, 1, 1);
	bxdf.etaT = shadingMaterial.eta;
	bxdf.K = shadingMaterial.k;
}

void ComputeBxDFSpecularReflection(ShadingMaterial shadingMaterial, out BxDFSpecularReflection bxdf)
{
	bxdf = (BxDFSpecularReflection)0;
	UnpackFresnel(shadingMaterial, bxdf.fresnel);
	bxdf.R = shadingMaterial.reflectance;
}

void ComputeBxDFMicrofacetTransmission(ShadingMaterial shadingMaterial, out BxDFMicrofacetTransmission bxdf)
{
	bxdf = (BxDFMicrofacetTransmission)0;
	UnpackFresnel(shadingMaterial, bxdf.fresnel);
	bxdf.T = shadingMaterial.transmission;
	bxdf.etaA = 1.0;
	bxdf.etaB = shadingMaterial.eta.x;
	bxdf.alphax = shadingMaterial.roughness; //RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = shadingMaterial.roughnessV; //RoughnessToAlpha(shadingMaterial.roughnessV);

}

void ComputeBxDFSpecularTransmission(ShadingMaterial shadingMaterial, out BxDFSpecularTransmission bxdf)
{
	bxdf = (BxDFSpecularTransmission)0;
	UnpackFresnel(shadingMaterial, bxdf.fresnel);
	bxdf.T = shadingMaterial.transmission;
	bxdf.eta = shadingMaterial.eta.x;
}

void ComputeBxDFMicrofacetReflection(ShadingMaterial shadingMaterial, out BxDFMicrofacetReflection bxdf)
{
	bxdf = (BxDFMicrofacetReflection)0;
	UnpackFresnel(shadingMaterial, bxdf.fresnel);
	bxdf.R = shadingMaterial.reflectance;
	bxdf.alphax = shadingMaterial.roughness; // RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = shadingMaterial.roughnessV; // RoughnessToAlpha(shadingMaterial.roughnessV);
}

void ComputeBxDFFresnelSpecular(ShadingMaterial shadingMaterial, out BxDFFresnelSpecular bxdf)
{
	bxdf = (BxDFFresnelSpecular)0;
	//UnpackFresnel(shadingMaterial, bxdf.fresnel);
	bxdf.T = shadingMaterial.transmission;
	bxdf.R = shadingMaterial.reflectance;
	bxdf.eta = shadingMaterial.eta.x;
}

void ComputeBxDFFresnelBlend(ShadingMaterial shadingMaterial, out BxDFFresnelBlend bxdf)
{
	bxdf = (BxDFFresnelBlend)0;
	//UnpackFresnel(shadingMaterial, bxdf.fresnel);
	bxdf.R = shadingMaterial.reflectance;
	bxdf.S = shadingMaterial.specular;
	bxdf.alphax = shadingMaterial.roughness; // RoughnessToAlpha(shadingMaterial.roughness);
	bxdf.alphay = shadingMaterial.roughnessV; // RoughnessToAlpha(shadingMaterial.roughnessV);
	bxdf.eta = shadingMaterial.eta;
}

float3 MaterialBRDF(Material material, Interaction isect, float3 wo, float3 wi, out float pdf)
{
	ShadingMaterial shadingMaterial = (ShadingMaterial)0;
	float3 f = 0;
	pdf = 0;
	if (shadingMaterial.materialType == Disney)
	{

	}
	else
	{
		TextureSampleInfo texLod = (TextureSampleInfo)0;
		texLod.cosine = dot(isect.wo, isect.normal);
		texLod.coneWidth = isect.coneWidth;
		texLod.screenSpaceArea = isect.screenSpaceArea;
		texLod.uvArea = isect.uvArea;
		texLod.uv = isect.uv.xy;
		UnpackShadingMaterial(material, shadingMaterial, texLod);
		int nComponent = 0;
		if (shadingMaterial.materialType == Plastic)
		{
			nComponent = 2;
			f += LambertBRDF(wi, wo, shadingMaterial.reflectance);
			pdf += LambertPDF(wi, wo);
			//BxDFPlastic bxdfPlastic;
			//ComputeBxDFPlastic(shadingMaterial, bxdfPlastic);
			BxDFMicrofacetReflection bxdfReflection;
			ComputeBxDFMicrofacetReflection(shadingMaterial, bxdfReflection);
			bxdfReflection.fresnel.etaI = 1.5;
			bxdfReflection.fresnel.etaT = 1.0;
			float pdfMicroReflection = 0;
			f += bxdfReflection.F(wo, wi, pdfMicroReflection);//MicrofacetReflectionF(wo, wi, bxdf, pdfMicroReflection);
			pdf += pdfMicroReflection;//MicrofacetReflectionPdf(wo, wi, bxdf.alphax, bxdf.alphay);
		}
		else if (shadingMaterial.materialType == Metal)
		{
			nComponent = 1;
			BxDFMetal bxdfMetal;
			ComputeBxDFMetal(shadingMaterial, bxdfMetal);
			f = bxdfMetal.F(wo, wi, pdf);  //MicrofacetReflectionF(wo, wi, bxdf, pdf);
		}
		else if (shadingMaterial.materialType == Glass)
		{
			/*                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                     
			nComponent = 2;
			BxDFSpecularReflection bxdfSpecularReflection;
			ComputeBxDFSpecularReflection(shadingMaterial, bxdfSpecularReflection);
			float pdfReflection = 0;
			f += bxdfSpecularReflection.F(wo, wi, pdfReflection);//MicrofacetReflectionF(wo, wi, bxdf, pdfMicroReflection);
			pdf += pdfReflection;
			BxDFSpecularTransmission bxdfSpecularTransmission;
			ComputeBxDFSpecularTransmission(shadingMaterial, bxdfSpecularTransmission);
			float pdfTransmission = 0;
			f += bxdfSpecularTransmission.F(wo, wi, pdfTransmission);
			pdf += pdfTransmission;
			*/
			if (shadingMaterial.roughness > 0.001)
			{
				nComponent = 2;
				if (SameHemisphere(wo, wi))
				{
					BxDFMicrofacetReflection bxdfReflection;
					ComputeBxDFMicrofacetReflection(shadingMaterial, bxdfReflection);
					float pdfMicroReflection = 0;
					f = bxdfReflection.F(wo, wi, pdf);
				}
				else
				{
					BxDFMicrofacetTransmission bxdfTranssimion;
					ComputeBxDFMicrofacetTransmission(shadingMaterial, bxdfTranssimion);
					f = bxdfTranssimion.F(wo, wi, pdf);
				}
			}
			else
			{
				nComponent = 1;
				BxDFFresnelSpecular bxdfFresnelSpecular;
				ComputeBxDFFresnelSpecular(shadingMaterial, bxdfFresnelSpecular);
				f = bxdfFresnelSpecular.F(wo, wi, pdf);
			}
			
		}
		else if (shadingMaterial.materialType == Mirror)
		{
			nComponent = 1;
			BxDFSpecularReflection bxdfSpecularReflection;
			ComputeBxDFSpecularReflection(shadingMaterial, bxdfSpecularReflection);
			//float pdfReflection = 0;
			f = bxdfSpecularReflection.F(wo, wi, pdf);
		}
		else if (shadingMaterial.materialType == Substrate)
		{
			nComponent = 1;
			BxDFFresnelBlend bxdfFresnelBlend;
			ComputeBxDFFresnelBlend(shadingMaterial, bxdfFresnelBlend);
			f = bxdfFresnelBlend.F(wo, wi, pdf);
		}
		else
		{
			nComponent = 1;
			f = LambertBRDF(wi, wo, shadingMaterial.reflectance);
			pdf = LambertPDF(wi, wo);
		}
		if (nComponent > 1)
		{
			pdf /= (float)nComponent;
		}
	}
	
	return f;
}


BSDFSample SampleLambert(ShadingMaterial material, float3 wo, inout RNG rng)
{
	BSDFSample bsdfSample = (BSDFSample)0;
	bsdfSample.bxdfFlag = BXDF_DIFFUSE;
	float2 u = Get2D(rng);
	float3 wi = CosineSampleHemisphere(u);
	if (wo.z < 0)
		wi.z *= -1;
	bsdfSample.wi = wi;
	bsdfSample.pdf = LambertPDF(wi, wo);
	bsdfSample.reflectance = LambertBRDF(wi, wo, material.reflectance);
	return bsdfSample;
}

BSDFSample SamplePlastic(ShadingMaterial material, float3 wo, inout RNG rng)
{
	//BxDFLambertReflection lambert;
	//ComputeBxDFLambertReflection(material, lambert);
	BxDFMicrofacetReflection bxdfReflection;
	ComputeBxDFMicrofacetReflection(material, bxdfReflection);
	bxdfReflection.fresnel.etaI = 1.5;
	bxdfReflection.fresnel.etaT = 1.0;
	//BxDFPlastic bxdfPlastic;
	//ComputeBxDFPlastic(material, bxdfPlastic);
	float matchingComponent = 2;
	float2 u = Get2D(rng);
	int compIndex = min(floor(u[0] * matchingComponent), matchingComponent - 1);

	float2 uRemapped = float2(min(u[0] * matchingComponent - compIndex, ONE_MINUS_EPSILON), u[1]);
	BSDFSample bsdfSample;
	//choose one of the bxdf to sample the wi vector
	if (compIndex == 0)
		bsdfSample = SampleLambert(material, wo, rng);
	else
		bsdfSample = bxdfReflection.Sample_F(uRemapped, wo);//SampleMicrofacetReflectionF(bxdf, uRemapped, wo, wi, pdf);
	//choosing pdf caculate
	bsdfSample.pdf /= 2;

	return bsdfSample;
}

BSDFSample SampleMetal(ShadingMaterial material, float3 wo, inout RNG rng)
{
	BxDFMetal bxdf;
	ComputeBxDFMetal(material, bxdf);
	float2 u = Get2D(rng);
	return bxdf.Sample_F(u, wo);
}

BSDFSample SampleMirror(ShadingMaterial material, float3 wo, inout RNG rng)
{
	BxDFSpecularReflection bxdf;
	ComputeBxDFSpecularReflection(material, bxdf);
	float2 u = Get2D(rng);
	return bxdf.Sample_F(u, wo);
}

BSDFSample SampleGlass(ShadingMaterial material, float3 wo, inout RNG rng)
{
	/*
	BxDFSpecularReflection bxdfSR;
	ComputeBxDFSpecularReflection(material, bxdfSR);
	BxDFSpecularTransmission bxdfST;
	ComputeBxDFSpecularTransmission(material, bxdfST);
	float matchingComponent = 2;
	float2 u = Get2D(rng);
	int compIndex = min(floor(u[0] * matchingComponent), matchingComponent - 1);
	//float3 f = 0;

	float2 uRemapped = float2(min(u[0] * matchingComponent - compIndex, ONE_MINUS_EPSILON), u[1]);
	//choose one of the bxdf to sample the wi vector
	BSDFSample bsdfSample;
	if (compIndex == 0)
		bsdfSample = bxdfSR.Sample_F(uRemapped, wo);
	else
		bsdfSample = bxdfST.Sample_F(uRemapped, wo);//SampleMicrofacetReflectionF(bxdf, uRemapped, wo, wi, pdf);
	//choosing pdf caculate
	bsdfSample.pdf /= 2;
	return bsdfSample;
	*/
	if (material.roughness > 0.0001)
	{
		float2 u = Get2D(rng);
		BxDFMicrofacetReflection bxdfReflection;
		ComputeBxDFMicrofacetReflection(material, bxdfReflection);
		//return bxdfReflection.Sample_F(u, wo);
		BxDFMicrofacetTransmission bxdfTranssimion;
		ComputeBxDFMicrofacetTransmission(material, bxdfTranssimion);
		
		float matchingComponent = 2;
		int compIndex = min(floor(u[0] * matchingComponent), matchingComponent - 1);

		float2 uRemapped = float2(min(u[0] * matchingComponent - compIndex, ONE_MINUS_EPSILON), u[1]);
		BSDFSample bsdfSample;
		//choose one of the bxdf to sample the wi vector
		if (compIndex == 0)
			bsdfSample = bxdfReflection.Sample_F(uRemapped, wo);
		else
			bsdfSample = bxdfTranssimion.Sample_F(uRemapped, wo);//SampleMicrofacetReflectionF(bxdf, uRemapped, wo, wi, pdf);
		//choosing pdf caculate
		bsdfSample.pdf /= 2;

		return bsdfSample;
	}
	else
	{
		BxDFFresnelSpecular bxdf;
		ComputeBxDFFresnelSpecular(material, bxdf);
		float2 u = Get2D(rng);
		return bxdf.Sample_F(u, wo);
	}
	
}

BSDFSample SampleSubstrate(ShadingMaterial material, float3 wo, inout RNG rng)
{
	BxDFFresnelBlend bxdf;
	ComputeBxDFFresnelBlend(material, bxdf);
	float uc = Get1D(rng);
	float2 u = Get2D(rng);
	return bxdf.Sample_F(uc, u, wo);
}

//wi wo is a vector which in local space of the interfaction surface
BSDFSample SampleMaterialBRDF(Material material, Interaction isect, inout RNG rng)
{
	ShadingMaterial shadingMaterial = (ShadingMaterial)0;
	TextureSampleInfo texLod = (TextureSampleInfo)0;
	texLod.cosine = dot(isect.wo, isect.normal);
	texLod.coneWidth = isect.coneWidth;
	texLod.screenSpaceArea = isect.screenSpaceArea;
	texLod.uvArea = isect.uvArea;
	texLod.uv = isect.uv.xy;
	UnpackShadingMaterial(material, shadingMaterial, texLod);
	float3 wo = isect.WorldToLocal(isect.wo);
		
	switch (shadingMaterial.materialType)
	{
	//case Disney:
	//	return 0;
	case Matte:
	{
		return SampleLambert(shadingMaterial, wo, rng);
	}
	case Plastic:
		return SamplePlastic(shadingMaterial, wo, rng);
	case Metal:
		return SampleMetal(shadingMaterial, wo, rng);
	case Mirror:
		return SampleMirror(shadingMaterial, wo, rng);
	case Glass:
		return SampleGlass(shadingMaterial, wo, rng);
	case Substrate:
		return SampleSubstrate(shadingMaterial, wo, rng);
	default:
	{
		return SampleLambert(shadingMaterial, wo, rng);
	}
	}
}


#endif
