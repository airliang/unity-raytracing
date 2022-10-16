
#ifndef LIGHT_HLSL
#define LIGHT_HLSL
#include "geometry.hlsl"
#include "GPUStructs.hlsl"
#include "distributions.hlsl"
#include "bvhaccel.hlsl"
#include "sampler.hlsl"

#define AreaLightType 0
#define EnvLightType 1
#define PointLightType 2


int   enviromentTextureMask;
float3 enviromentColor;
float3 enviromentColorScale;
float2  envMapDistributionSize;
float _EnvmapRotation;
float _EnvMapDistributionInt;
int   _EnvLightIndex;
bool  _UniformSampleLight;
bool  _EnvMapEnable;

//TextureCube _EnvMap;
//SamplerState _EnvMap_linear_repeat_sampler;
Texture2D _LatitudeLongitudeMap;
SamplerState _LatitudeLongitudeMap_linear_repeat_sampler;
//#ifdef _ENVMAP_ENABLE
StructuredBuffer<float2> EnvmapMarginals;
StructuredBuffer<float2> EnvmapConditions;
StructuredBuffer<float>  EnvmapConditionFuncInts;
//#endif

float3 RotateAroundYInDegrees(float3 vertex, float degrees)
{
	float alpha = degrees * PI / 180.0;
	float sina, cosa;
	sincos(alpha, sina, cosa);
	float2x2 m = float2x2(cosa, -sina, sina, cosa);
	return float3(mul(m, vertex.xz), vertex.y).xzy;
}

inline float2 DirectionToPolar(float3 direction)
{
	float3 normalizedCoords = normalize(direction);
	float latitude = acos(normalizedCoords.y);
	float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
	float2 sphereCoords = float2(longitude, latitude) * float2(INV_TWO_PI, INV_PI);
	return float2(0.5, 1.0) - sphereCoords;
}

inline float3 PolarToDirection(float2 uv)
{
	float2 sphereCoords = (float2(0.5, 1.0) - uv) * float2(TWO_PI, PI);
	float theta = sphereCoords.y;
	float phi = sphereCoords.x;
	float cosTheta = cos(theta);
	float sinTheta = sin(theta);
	float sinPhi = sin(phi);
	float cosPhi = cos(phi);
	//left hand coordinate and y is up
	float x = sinTheta * cosPhi;
	float y = cosTheta;
	float z = sinTheta * sinPhi;
	return float3(x, y, z);
}

float3 SampleEnviromentLight(float2 uv)
{
	return _LatitudeLongitudeMap.SampleLevel(_LatitudeLongitudeMap_linear_repeat_sampler, uv, 0).rgb;
}

float3 EnviromentLightLe(float3 dir)
{
	if (enviromentTextureMask == 1)
	{
		float3 vertex = RotateAroundYInDegrees(normalize(dir), _EnvmapRotation);
		float2 uv = DirectionToPolar(vertex);
		float3 col = SampleEnviromentLight(uv);
		return col.rgb;
	}
	return enviromentColor;
}

float EnvLightLiPdf(float3 wi)
{
	if (_EnvMapEnable)
	{
		if (_UniformSampleLight)
			return INV_FOUR_PI;
		else
		{
			float theta = acos(wi.y);//SphericalTheta(wi);
			float phi = atan2(wi.z, wi.x);
			float2 sphereCoords = float2(phi, theta) * float2(INV_TWO_PI, INV_PI);
			float2 uv = float2(0.5 - sphereCoords.x, 1.0 - sphereCoords.y);
			if (uv.x < 0)
				uv.x += 1.0;
			float sinTheta = sin(theta);
			if (sinTheta == 0)
				return 0;
			DistributionDiscript discript = (DistributionDiscript)0;
			discript.start = 0;
			discript.num = (int)envMapDistributionSize.y;
			discript.unum = (int)envMapDistributionSize.x;
			discript.domain = float4(0, 1, 0, 1);
			discript.funcInt = _EnvMapDistributionInt;
			return Distribution2DPdf(uv, discript, EnvmapMarginals, EnvmapConditions) /
				(2 * PI * PI * sinTheta);
		}
	}
	else
		return INV_FOUR_PI;

}

float3 UniformSampleEnviromentLight(float2 u, out float pdf, out float3 wi)
{
	float mapPdf = 1.0 / (4.0 * PI);
	//float theta = (1.0 - u[1]) * PI;
	//float phi = u[0] * 2 * PI;
	//float cosTheta = cos(theta);
	//float sinTheta = sin(theta);
	//float sinPhi = sin(phi);
	//float cosPhi = cos(phi);
	wi = PolarToDirection(u);
	//float2 uv = DirectionToPolar(wi);
	pdf = mapPdf;
	return SampleEnviromentLight(u);
}


float3 ImportanceSampleEnviromentLight(float2 u, out float pdf, out float3 wi)
{
	if (_EnvMapEnable)
	{
		DistributionDiscript discript = (DistributionDiscript)0;
		discript.start = 0;
		discript.num = (int)envMapDistributionSize.y;
		discript.unum = (int)envMapDistributionSize.x;
		discript.domain = float4(0, 1, 0, 1);
		discript.funcInt = _EnvMapDistributionInt;
		float mapPdf = 0;
		pdf = 0;
		wi = 0;
		float2 uv = Sample2DContinuous(u, discript, EnvmapMarginals, EnvmapConditions, EnvmapConditionFuncInts, mapPdf);
		if (mapPdf == 0)
			return float3(0, 0, 0);
		// Convert infinite light sample point to direction
		//uv = float2(0.8, 0.5);
		float theta = (1.0 - uv.y) * PI;
		float phi = (0.5 - uv.x) * 2 * PI;
		float cosTheta = cos(theta);
		float sinTheta = sin(theta);
		float sinPhi = sin(phi);
		float cosPhi = cos(phi);
		//left hand coordinate and y is up
		float x = sinTheta * cosPhi;
		float y = cosTheta;
		float z = sinTheta * sinPhi;
		wi = float3(x, y, z);

		// Compute PDF for sampled infinite light direction
		pdf = mapPdf / (2 * PI * PI * sinTheta);
		if (sinTheta == 0)
		{
			pdf = 0;
			return 0;
		}

		return SampleEnviromentLight(uv);
	}
	else
		return UniformSampleEnviromentLight(u, pdf, wi);
}

float3 SampleTriangleLight(float3 p0, float3 p1, float3 p2, float2 u, float3 litPoint, Light light, out float3 wi, out float3 position, out float pdf)
{
	float3 Li = 0;
	float3 lightPointNormal;
	float triPdf = 0;
	position = SamplePointOnTriangle(p0, p1, p2, u, lightPointNormal, triPdf);
	pdf = triPdf;
	wi = position - litPoint;
	float wiLength = length(wi);
	wi = normalize(wi);
	float cos = dot(lightPointNormal, -wi);
	float absCos = abs(cos);
	pdf *= wiLength * wiLength / absCos;
	if (isinf(pdf) || wiLength == 0)
	{
		pdf = 0;
		return 0;
	}
	
	return cos > 0 ? light.radiance : 0;
}

int SampleTriangleIndexOfLightPoint(float u, DistributionDiscript discript, StructuredBuffer<float2> distributions, out float pdf)
{
	//get light mesh triangle index
	int index = Sample1DDiscrete(u, discript, distributions, pdf);
	return index;
}

float3 SampleLightRadiance(Light light, Interaction isect, inout RNG rng, 
	out float3 wi, out float lightPdf, out float3 lightPoint)
{
	if (light.type == AreaLightType)
	{
		//int discriptIndex = light.distributionDiscriptIndex;
		//DistributionDiscript lightDistributionDiscript = DistributionDiscripts[discriptIndex];
		float u = Get1D(rng);
		float triPdf = 0;
		lightPdf = 0;
		MeshInstance meshInstance = MeshInstances[light.meshInstanceID];
		//int triangleIndex = SampleTriangleIndexOfLightPoint(u, lightDistributionDiscript, lightDistribution, lightPdf) + meshInstance.triangleStartOffset;
		int triangleIndex = min((int)(u * light.trianglesNum), light.trianglesNum - 1) + meshInstance.triangleStartOffset;
		int vertexStart = triangleIndex;
		int3 triangles = TriangleIndices[triangleIndex];
		int vIndex0 = triangles.x;//TriangleIndices[vertexStart];
		int vIndex1 = triangles.y;//TriangleIndices[vertexStart + 1];
		int vIndex2 = triangles.z;//TriangleIndices[vertexStart + 2];
		float3 p0 = Vertices[vIndex0].position.xyz;
		float3 p1 = Vertices[vIndex1].position.xyz;
		float3 p2 = Vertices[vIndex2].position.xyz;
		//convert to worldpos

		p0 = mul(meshInstance.localToWorld, float4(p0, 1)).xyz;
		p1 = mul(meshInstance.localToWorld, float4(p1, 1)).xyz;
		p2 = mul(meshInstance.localToWorld, float4(p2, 1)).xyz;

		float triangleArea = 0.5 * length(cross(p1 - p0, p2 - p0));
		lightPdf = triangleArea / light.area;

		float3 Li = SampleTriangleLight(p0, p1, p2, Get2D(rng), isect.p.xyz, light, wi, lightPoint, triPdf);
		lightPdf *= triPdf;
		return Li;
	}
	else if (light.type == EnvLightType)
	{
		float2 u = Get2D(rng);
		//float3 Li = UniformSampleEnviromentLight(u, lightPdf, wi); 
		float3 Li = 0;
		if (_UniformSampleLight)
			Li = UniformSampleEnviromentLight(u, lightPdf, wi);
		else
			Li = ImportanceSampleEnviromentLight(u, lightPdf, wi);

		//Li = isUniform ? float3(0.5, 0, 0) : Li;
		lightPoint = isect.p + wi * 10000.0f;
		return Li;
	}

	wi = float3(0, 0, 0);
	lightPdf = 0;
	lightPoint = float3(0, 0, 0);
	return float3(0, 0, 0);
}

int ImportanceSampleLightSource(float u, DistributionDiscript discript, StructuredBuffer<float2> discributions, out float pmf)
{
	return Sample1DDiscrete(u, discript, discributions, pmf);
}

int UniformSampleLightSource(float u, DistributionDiscript discript, out float pmf)
{
	int nLights = discript.num;
	int lightIndex = min((int)(u * nLights), nLights - 1);
	pmf = 1.0 / nLights;
	return lightIndex;
}

int SampleLightSource(float u, DistributionDiscript discript, StructuredBuffer<float2> discributions, out float pmf)
{
	//int index = 0;
	//if (_UniformSampleLight)
	//	index = UniformSampleLightSource(u, discript, pmf);
	//else
	//	index = ImportanceSampleLightSource(u, discript, discributions, pmf); //SampleDistribution1DDiscrete(rs.Get1D(threadId), 0, lightCount, pdf);

	int index = UniformSampleLightSource(u, discript, pmf);
	return index;
}

float UniformLightSourcePmf(int lightsNum)
{
	return 1.0 / lightsNum;
}

float ImportanceLightSourcePmf(int lightIndex, DistributionDiscript discript)
{
	return DiscretePdf(lightIndex, discript, Distributions1D);
}

float LightSourcePmf(int lightIndex)
{
	DistributionDiscript discript = DistributionDiscripts[0];
	return UniformLightSourcePmf(discript.num);
	//DistributionDiscript discript = DistributionDiscripts[0];
	//if (_UniformSampleLight)
	//	return UniformLightSourcePmf(discript.num);
	//else
	//	return ImportanceLightSourcePmf(lightIndex, discript);
}

float3 Light_Le(float3 wi, Light light)
{
	if (light.type == AreaLightType)
	{
		return light.radiance;
	}
	else if (light.type == EnvLightType)
	{
		return EnviromentLightLe(wi);
	}
	return 0;
}

float AreaLightPdf(Light light)
{
	float lightPdf = 1.0 / light.area;
	//if (light.type == AreaLightType)
	//{
		//DistributionDiscript discript = DistributionDiscripts[light.distributionDiscriptIndex];
		//int distributionIndex = triangleIndex;
		//float pmf = DiscretePdf(distributionIndex, discript, Distributions1D);
		//lightPdf = pmf * 1.0 / triangleArea;
	//}

	return lightPdf;
}

#endif



