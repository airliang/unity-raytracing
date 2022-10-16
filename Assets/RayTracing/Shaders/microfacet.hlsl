#ifndef MICROFACET_HLSL
#define MICROFACET_HLSL

#include "geometry.hlsl"

#define GGX 0
#define BECKMAN 1

//just implement the GGX functions

float RoughnessToAlpha(float roughness) {
    roughness = max(roughness, 0.001);
    float x = log(roughness);
    return 1.62142f + 0.819955f * x + 0.1734f * x * x +
        0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
}

float Lambda(float3 w, float alphax, float alphay)
{
    float absTanTheta = abs(TanTheta(w));
    if (isinf(absTanTheta))
        return 0;
    // Compute _alpha_ for direction _w_
    float alpha =
        sqrt(Cos2Phi(w) * alphax * alphax + Sin2Phi(w) * alphay * alphay);
    float alpha2Tan2Theta = (alpha * absTanTheta) * (alpha * absTanTheta);
    return (-1.0 + sqrt(1.0 + alpha2Tan2Theta)) * 0.5;
}

float Distribution(float3 wh, float alphax, float alphay)
{
    float tan2Theta = Tan2Theta(wh);
    if (isinf(tan2Theta))
        return 0;
    float cosTheta2 = Cos2Theta(wh);
    const float cos4Theta = cosTheta2 * cosTheta2;
    float e =
        tan2Theta * (Cos2Phi(wh) / (alphax * alphax) + Sin2Phi(wh) / (alphay * alphay));
    return 1.0 / (PI * alphax * alphay * cos4Theta * (1 + e) * (1 + e));
}

float3 SampleVector(float3 wo, float2 u, float alphax, float alphay)
{
    float phi = TWO_PI * u[1];
    float cosTheta = 0;
    if (alphax == alphay)
    {
        float tanTheta2 = alphax * alphax * u[0] / (1.0 - u[0]);
        cosTheta = 1.0 / sqrt(1.0 + tanTheta2);
    }
    else
    {
        //https://agraphicsguy.wordpress.com/2018/07/18/sampling-anisotropic-microfacet-brdf/
        phi = atan(alphay / alphax * tan(TWO_PI * u[1] + HALF_PI));
        if (u[1] > 0.5f) 
            phi += PI;
        float sinPhi = sin(phi);
        float cosPhi = cos(phi);
        float alphax2 = alphax * alphax, alphay2 = alphay * alphay;
        float alpha2 =
            1 / (cosPhi * cosPhi / alphax2 + sinPhi * sinPhi / alphay2);
        float tanTheta2 = alpha2 * u[0] / (1 - u[0]);
        cosTheta = 1 / sqrt(1 + tanTheta2);
    }
    float sinTheta =
        sqrt(max(0, 1.0 - cosTheta * cosTheta));
    float3 wh = SphericalDirection(sinTheta, cosTheta, phi);
    if (!SameHemisphere(wo, wh))
        wh = -wh;

    return wh;
}

float G(float3 wo, float3 wi, float alphax, float alphay)
{
    return 1.0 / (1 + Lambda(wo, alphax, alphay) + Lambda(wi, alphax, alphay));
}

float Pdf_wh(float3 wh, float alphaX, float alphaY)
{
    return Distribution(wh, alphaX, alphaY) * AbsCosTheta(wh);
}

float Pdf_wi(float3 wo, float3 wi, float alphaX, float alphaY)
{
    if (!SameHemisphere(wo, wi))
        return 0;
    float3 wh = normalize(wo + wi);
    float D = Distribution(wh, alphaX, alphaY);
    return Pdf_wh(wh, alphaX, alphaY) / (4.0 * dot(wo, wh));
}

float3 BRDF(float3 wo, float3 wi, float3 fr, float cosThetaO, float cosThetaI, float alphaX, float alphaY)
{
    float3 wh = normalize(wo + wi);
    float D = Distribution(wh, alphaX, alphaY);
    float GTerm = G(wo, wi, alphaX, alphaY);
    return D * fr * GTerm * 0.25 / abs(cosThetaO * cosThetaI);
}


#endif
