#ifndef FRESNEL_HLSL
#define FRESNEL_HLSL


#define FresnelDielectric 0
#define FresnelConductor 1
#define FresnelSchlick 2
#define FresnelConst 3

inline float SchlickWeight(float cosTheta) {
    float m = clamp(1 - cosTheta, 0, 1);
    return (m * m) * (m * m) * m;
}

//inline float FrSchlick(float R0, float cosTheta) {
//    return lerp(R0, 1, SchlickWeight(cosTheta));
//}

inline float3 FrSchlick(float3 R0, float cosTheta) {
    return lerp(R0, float3(1, 1, 1), SchlickWeight(cosTheta));
}

float FrDielectric(float cosThetaI, float eta) {
    cosThetaI = clamp(cosThetaI, -1, 1);
    float cosThetaT = 0;
    if (cosThetaI < 0.0f) {
        eta = 1.0f / eta;
        cosThetaI = -cosThetaI;
    }
    float sinThetaTSq = eta * eta * (1.0f - cosThetaI * cosThetaI);
    if (sinThetaTSq > 1.0f) {
        cosThetaT = 0.0f;
        return 1.0f;
    }
    cosThetaT = sqrt(max(1.0f - sinThetaTSq, 0.0f));

    float Rs = (eta * cosThetaI - cosThetaT) / (eta * cosThetaI + cosThetaT);
    float Rp = (eta * cosThetaT - cosThetaI) / (eta * cosThetaT + cosThetaI);

    return (Rs * Rs + Rp * Rp) * 0.5f;
}

// https://seblagarde.wordpress.com/2013/04/29/memo-on-fresnel-equations/
float3 FrConductor(float cosThetaI, float3 etai, float3 etat, float3 k) {
    cosThetaI = clamp(cosThetaI, -1, 1);
    float3 eta = etat / etai;
    float3 etak = k / etai;
    
    float cosThetaI2 = cosThetaI * cosThetaI;
    float sinThetaI2 = 1. - cosThetaI2;
    float3 eta2 = eta * eta;
    float3 etak2 = etak * etak;

    float3 t0 = eta2 - etak2 - sinThetaI2;
    float3 a2plusb2 = sqrt(t0 * t0 + 4 * eta2 * etak2);
    float3 t1 = a2plusb2 + cosThetaI2;
    float3 a = sqrt((a2plusb2 + t0) * 0.5);
    float3 t2 = 2.0 * cosThetaI * a;
    float3 Rs = (t1 - t2) / (t1 + t2);

    float3 t3 = cosThetaI2 * a2plusb2 + sinThetaI2 * sinThetaI2;
    float3 t4 = t2 * sinThetaI2;
    float3 Rp = Rs * (t3 - t4) / (t3 + t4);

    return (Rp + Rs) * 0.5;
}

struct FresnelData
{
    int fresnelType;
    float3 etaI;
    float3 etaT;
    float3 K;    //just for conductor
    float3 R;

    float3 Evaluate(float cosThetaI)
    {
        if (fresnelType == FresnelSchlick)
        {
            return FrSchlick(R, cosThetaI);
        }
        else if (fresnelType == FresnelDielectric)
        {
            return FrDielectric(cosThetaI, etaI/etaT);
        }
        else if (fresnelType == FresnelConductor)
        {
            return FrConductor(cosThetaI, etaI, etaT, K);
        }
        else
        {
            return 1;
        }
    }
};


#endif



