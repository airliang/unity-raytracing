#ifndef ENVIRONMENT_LIGHTING_HLSL
#define ENVIRONMENT_LIGHTING_HLSL

cbuffer EnvironmentBuffer
{
    float3 _EnvironmentColor;
    float _EnvmapRotation;
    float _EnvMapDistributionInt;
    float2 _EnvMapDistributionSize;
};

Texture2D _LatitudeLongitudeMap;
SamplerState _LatitudeLongitudeMap_linear_repeat_sampler;

StructuredBuffer<float2> _EnvmapMarginals;
StructuredBuffer<float2> _EnvmapConditions;
StructuredBuffer<float>  _EnvmapConditionFuncInts;

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
    float3 vertex = RotateAroundYInDegrees(normalize(dir), -_EnvmapRotation);
    float2 uv = DirectionToPolar(vertex);
    float3 col = SampleEnviromentLight(uv);
    return col.rgb;
}

float EnvLightLiPdf(float3 wi)
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
    discript.num = (int)_EnvMapDistributionSize.y;
    discript.unum = (int)_EnvMapDistributionSize.x;
    discript.domain = float4(0, 1, 0, 1);
    discript.funcInt = _EnvMapDistributionInt;
    return Distribution2DPdf(uv, discript, _EnvmapMarginals, _EnvmapConditions) /
        (2 * PI * PI * sinTheta);

}

#endif
