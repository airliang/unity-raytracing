#ifndef BXDF_HLSL
#define BXDF_HLSL
#include "sampler.hlsl"
#include "microfacet.hlsl"
#include "fresnel.hlsl"
#include "geometry.hlsl"

#define BXDF_REFLECTION 1
#define BXDF_TRANSMISSION 1 << 1
#define BXDF_DIFFUSE 1 << 2
#define BXDF_SPECULAR 1 << 3
#define BXDF_GLOSSY 1 << 4

bool IsSpecular(int bxdfFlag)
{
    return (bxdfFlag & BXDF_SPECULAR) > 0;
}

struct BSDFSample
{
    float3 reflectance;
    float3 wi;   //in local space (z up space)
    float  pdf;
    float  eta;
    int    bxdfFlag;

    bool IsSpecular()
    {
        return (bxdfFlag & BXDF_SPECULAR) > 0;
    }
};

float3 Reflect(float3 wo, float3 n) {
    return -wo + 2 * dot(wo, n) * n;
}

bool Refract(float3 wi, float3 n, float eta, out float3 wt)
{
    float cosThetaI = dot(n, wi);
    float sin2ThetaI = max(0, Float(1 - cosThetaI * cosThetaI));
    float sin2ThetaT = eta * eta * sin2ThetaI;

    // Handle total internal reflection for transmission
    if (sin2ThetaT >= 1) 
        return false;
    float cosThetaT = sqrt(1.0 - sin2ThetaT);
    wt = eta * -wi + (eta * cosThetaI - cosThetaT) * n;
    return true;
}

float3 LambertBRDF(float3 wi, float3 wo, float3 R)
{
    return wo.z == 0 ? 0 : R * INV_PI;
}

//wi and wo must in local space
float LambertPDF(float3 wi, float3 wo)
{
    return SameHemisphere(wo, wi) ? AbsCosTheta(wi) * INV_PI : 0;
}

float TrowbridgeReitzLambda(float3 w, float alphax, float alphay)
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

float TrowbridgeReitzD(float3 wh, float alphax, float alphay)
{
    float tan2Theta = Tan2Theta(wh);
    if (isinf(tan2Theta))
        return 0;
    const float cos4Theta = Cos2Theta(wh) * Cos2Theta(wh);
    float e =
        (Cos2Phi(wh) / (alphax * alphax) + Sin2Phi(wh) / (alphay * alphay)) *
        tan2Theta;
    return 1 / (PI * alphax * alphay * cos4Theta * (1 + e) * (1 + e));
}

float3 SampleTrowbridgeReitzDistributionVector(float3 wo, float2 u, float alphax, float alphay)
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

float MicrofacetG(float3 wo, float3 wi, float alphax, float alphay)
{
    return 1.0 / (1 + TrowbridgeReitzLambda(wo, alphax, alphay) + TrowbridgeReitzLambda(wi, alphax, alphay));
}

float Pdf_wh(float D, float3 wh)
{
    return D * AbsCosTheta(wh);
}

float MicrofacetReflectionPdf(float3 wo, float3 wi, float alphax, float alphay)
{
    if (!SameHemisphere(wo, wi))
        return 0;
    float3 wh = normalize(wo + wi);
    float D = TrowbridgeReitzD(wh, alphax, alphay);
    return Pdf_wh(D, wh) / (4.0 * dot(wo, wh));
}

struct BxDFPlastic
{
    float alphax;
    float alphay;
    float eta;
    float3 R;
    

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleTrowbridgeReitzDistributionVector(wo, u, alphax, alphay);
    }

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_REFLECTION;
        if (wo.z == 0)
            return bsdfSample;

        float3 wh = Sample_wh(u, wo);
        wh = normalize(wh);
        if (dot(wo, wh) < 0)
            return bsdfSample;

        float3 wi = reflect(-wo, wh);
        bsdfSample.wi = wi;
        if (!SameHemisphere(wo, wi))
            return bsdfSample;

        float D = TrowbridgeReitzD(wh, alphax, alphay);

        bsdfSample.pdf = Pdf_wh(D, wh) / (4 * dot(wo, wh));
        float cosThetaI = AbsCosTheta(wi);
        float cosThetaO = AbsCosTheta(wo);
        float3 F = FrDielectric(abs(dot(wo, wh)), eta);
        bsdfSample.reflectance = R * D * MicrofacetG(wo, wi, alphax, alphay) * F * 0.25 / (cosThetaI * cosThetaO);
        return bsdfSample;
    }

    float Pdf(float3 wo, float3 wi)
    {
        return MicrofacetReflectionPdf(wo, wi, alphax, alphay);
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        float cosThetaO = AbsCosTheta(wo);
        float cosThetaI = AbsCosTheta(wi);
        float3 wh = wi + wo;
        pdf = 0;
        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return float3(0, 0, 0);
        if (wh.x == 0 && wh.y == 0 && wh.z == 0)
            return float3(0, 0, 0);
        wh = normalize(wh);
        // For the Fresnel call, make sure that wh is in the same hemisphere
        // as the surface normal, so that TIR is handled correctly.
        float3 F = FrDielectric(abs(dot(wo, Faceforward(wh, float3(0, 0, 1)))), eta);
        
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        pdf = Pdf_wh(D, wh) * 0.25 / (dot(wo, wh));

        return R * D * MicrofacetG(wo, wi, alphax, alphay) * F * 0.25 /
            (cosThetaI * cosThetaO);
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        float3 wh = normalize(wo + wi);
        //float cosThetaI = AbsCosTheta(wi);
        return FrDielectric(abs(dot(wo, Faceforward(wh, float3(0, 0, 1)))), eta);
    }
};

struct BxDFMetal
{
    float alphax;
    float alphay;
    float3 R;
    float3 K;
    float3 etaI;
    float3 etaT;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleTrowbridgeReitzDistributionVector(wo, u, alphax, alphay);
    }

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_REFLECTION;
        if (wo.z == 0)
            return bsdfSample;

        float3 wh = Sample_wh(u, wo);
        wh = normalize(wh);
        if (dot(wo, wh) < 0)
            return bsdfSample;

        float3 wi = normalize(reflect(-wo, wh));
        bsdfSample.wi = wi;
        if (!SameHemisphere(wo, wi))
            return bsdfSample;

        float D = TrowbridgeReitzD(wh, alphax, alphay);

        bsdfSample.pdf = Pdf_wh(D, wh) * 0.25 / (dot(wo, wh));
        float cosThetaI = AbsCosTheta(wi);
        float cosThetaO = AbsCosTheta(wo);
        //etaI = float3(1, 1, 1);
        //etaT = float3(0, 0, 0);
        //K = 0; // float3(3.9747, 2.38, 1.5998);
        float3 Fresnel = FrConductor(abs(dot(wo, Faceforward(wh, float3(0, 0, 1)))), etaI, etaT, K);
        bsdfSample.reflectance = R * D * MicrofacetG(wo, wi, alphax, alphay) * Fresnel / (4 * cosThetaI * cosThetaO);
        return bsdfSample;
    }

    float Pdf(float3 wo, float3 wi)
    {
        return MicrofacetReflectionPdf(wo, wi, alphax, alphay);
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        float cosThetaO = AbsCosTheta(wo);
        float cosThetaI = AbsCosTheta(wi);
        float3 wh = wi + wo;
        pdf = 0;
        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return float3(0, 0, 0);
        if (wh.x == 0 && wh.y == 0 && wh.z == 0)
            return float3(0, 0, 0);
        wh = normalize(wh);
        // For the Fresnel call, make sure that wh is in the same hemisphere
        // as the surface normal, so that TIR is handled correctly.
        //K = 0; // float3(3.9747, 2.38, 1.5998);
        //etaI = float3(1, 1, 1);
        //etaT = float3(0, 0, 0);
        float3 Fresnel = FrConductor(abs(dot(wo, Faceforward(wh, float3(0, 0, 1)))), etaI, etaT, K);
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        pdf = Pdf_wh(D, wh) * 0.25 / (dot(wo, wh));
        return R * D * MicrofacetG(wo, wi, alphax, alphay) * Fresnel /
            (4 * cosThetaI * cosThetaO);
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        float3 wh = wi + wo;
        wh = normalize(wh);
        if (!SameHemisphere(wo, wi))
            return float3(0, 0, 0);
        return FrConductor(abs(dot(wo, Faceforward(wh, float3(0, 0, 1)))), 1, etaT, K);
    }
};

struct BxDFSpecularReflection
{
    FresnelData fresnel;
    float3 R;

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_SPECULAR;
        float3 wi = normalize(float3(-wo.x, -wo.y, wo.z));
        bsdfSample.wi = wi;
        bsdfSample.pdf = 1;
        float3 fr = fresnel.Evaluate(wi.z);
        bsdfSample.reflectance = fr * R / AbsCosTheta(wi);
        return bsdfSample;
    }

    float Pdf(float3 wo, float3 wi)
    {
        return 0;
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        pdf = 0;
        return 0;
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        return fresnel.Evaluate(wi.z);
    }
};

struct BxDFSpecularTransmission
{
    FresnelData fresnel;
    float eta;
    float3 T;

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_TRANSMISSION | BXDF_SPECULAR;
        bool entering = CosTheta(wo) > 0;
        float etaI = entering ? 1 : eta;
        float etaT = entering ? eta : 1;

        // Compute ray direction for specular transmission
        float3 wi;
        bool bValid = Refract(wo, Faceforward(float3(0, 0, 1), wo), etaI / etaT, wi);
        bsdfSample.wi = wi;
        if (!bValid)
            return bsdfSample;
        bsdfSample.pdf = 1;
        float3 ft = T * (1 - fresnel.Evaluate(CosTheta(wi)));
        // Account for non-symmetry with transmission to different medium
        //if (mode == TransportMode::Radiance) ft *= (etaI * etaI) / (etaT * etaT);
        bsdfSample.reflectance = ft / AbsCosTheta(wi);
        return bsdfSample;
    }

    float Pdf(float3 wo, float3 wi)
    {
        return 0;
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        pdf = 0;
        return 0;
    }
};

struct BxDFMicrofacetReflection
{
    FresnelData fresnel;
    float alphax;
    float alphay;
    float3 R;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleTrowbridgeReitzDistributionVector(wo, u, alphax, alphay);
    }

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_REFLECTION;
        float3 wh = Sample_wh(u, wo);
        float3 wi = normalize(reflect(-wo, wh));
        bsdfSample.wi = wi;
        bsdfSample.reflectance = F(wo, wi, bsdfSample.pdf);
        return bsdfSample;
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        float cosThetaO = AbsCosTheta(wo);
        float cosThetaI = AbsCosTheta(wi);
        float3 wh = wi + wo;
        pdf = 0;
        // Handle degenerate cases for microfacet reflection
        if (cosThetaI == 0 || cosThetaO == 0)
            return float3(0, 0, 0);
        if (wh.x == 0 && wh.y == 0 && wh.z == 0)
            return float3(0, 0, 0);
        wh = normalize(wh);
        wh = Faceforward(wh, float3(0, 0, 1));
        // For the Fresnel call, make sure that wh is in the same hemisphere
        // as the surface normal, so that TIR is handled correctly.
        float3 F = FrDielectric(abs(dot(wi, wh)), 1.0/1.5); //fresnel.Evaluate(dot(wi, Faceforward(wh, float3(0, 0, 1))));

        float D = TrowbridgeReitzD(wh, alphax, alphay);
        pdf = Pdf_wh(D, wh) * 0.25 / (dot(wo, wh));

        return R * D * MicrofacetG(wo, wi, alphax, alphay) * F * 0.25 /
            (cosThetaI * cosThetaO);
    }

    float Pdf(float3 wo, float3 wi)
    {
        return MicrofacetReflectionPdf(wo, wi, alphax, alphay);
    }
};

struct BxDFMicrofacetTransmission
{
    FresnelData fresnel;
    float alphax;
    float alphay;
    float etaA;
    float etaB;
    float3 T;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleTrowbridgeReitzDistributionVector(wo, u, alphax, alphay);
    }

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_TRANSMISSION;
        float3 wh = Sample_wh(u, wo);
        if (dot(wo, wh) < 0) 
            return bsdfSample;  // Should be rare

        Float eta = CosTheta(wo) > 0 ? (etaA / etaB) : (etaB / etaA);
        float3 wi;
        bool valid = Refract(wo, wh, eta, wi);
        bsdfSample.wi = wi;
        if (!valid)
            return bsdfSample;
        
        bsdfSample.reflectance = F(wo, wi, bsdfSample.pdf);
        return bsdfSample;
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        //https://www.cs.cornell.edu/~srm/publications/EGSR07-btdf.pdf
        //formular (21)
        pdf = 0;

        if (SameHemisphere(wo, wi)) 
            return 0;  

        float cosThetaO = CosTheta(wo);
        float cosThetaI = CosTheta(wi);
        if (cosThetaI == 0 || cosThetaO == 0) 
            return 0;

        float eta = CosTheta(wo) > 0 ? (etaB / etaA) : (etaA / etaB);
        //by snell's law
        float3 wh = normalize(wo + wi * eta);
        wh = wh.z < 0 ? -wh : wh;

        // Same side?
        if (dot(wo, wh) * dot(wi, wh) > 0) 
            return 0;

        float3 F = fresnel.Evaluate(dot(wo, wh));
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        float G = MicrofacetG(wo, wi, alphax, alphay);
        float sqrtDenom = dot(wo, wh) + eta * dot(wi, wh);
        float factor = 1.0 / eta;

        float dwh_dwi = abs((eta * eta * dot(wi, wh)) / (sqrtDenom * sqrtDenom));
        pdf = Pdf_wh(D, wh) * dwh_dwi;

        return (1.0f - F) * T * abs(D * G * eta * eta *
                abs(dot(wi, wh)) * abs(dot(wo, wh)) * factor * factor /
                (cosThetaI * cosThetaO * sqrtDenom * sqrtDenom));
    }

    float Pdf(float3 wo, float3 wi)
    {
        if (SameHemisphere(wo, wi))
            return 0;
        float eta = CosTheta(wo) > 0 ? (etaB / etaA) : (etaA / etaB);
        //by snell's law
        float3 wh = normalize(wo + wi * eta);
        if (dot(wo, wh) * dot(wi, wh) > 0)
            return 0;
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        float sqrtDenom = dot(wo, wh) + eta * dot(wi, wh);
        float dwh_dwi = abs((eta * eta * dot(wi, wh)) / (sqrtDenom * sqrtDenom));
        return Pdf_wh(D, wh) * dwh_dwi;
    }
};

struct BxDFLambertReflection
{
    float3 R;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return CosineSampleHemisphere(u);
    }

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        bsdfSample.bxdfFlag = BXDF_DIFFUSE | BXDF_REFLECTION;
        float3 wi = CosineSampleHemisphere(u);
        if (wo.z < 0)
            wi.z *= -1;
        bsdfSample.wi = wi;
        bsdfSample.pdf = LambertPDF(wi, wo);
        bsdfSample.reflectance = LambertBRDF(wi, wo, R);
        return bsdfSample;
    }

    float Pdf(float3 wo, float3 wi)
    {
        return LambertPDF(wi, wo);
    }

    float3 Fresnel(float3 wo, float3 wi)
    {
        return 1;
    }
};

struct BxDFFresnelSpecular
{
    float3 R;
    float3 T;
    float eta;

    BSDFSample Sample_F(float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        
        float F = FrDielectric(CosTheta(wo), 1.0/eta);
        float pdf = 0;
        if (u[0] < F) 
        {
            bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_SPECULAR;
            // Compute specular reflection for _FresnelSpecular_

            // Compute perfect specular reflection direction
            float3 wi = float3(-wo.x, -wo.y, wo.z);
            //if (sampledType)
            //    *sampledType = BxDFType(BSDF_SPECULAR | BSDF_REFLECTION);
            bsdfSample.pdf = F;
            bsdfSample.reflectance = F * R / AbsCosTheta(wi);
            bsdfSample.wi = wi;
            return bsdfSample;
        }
        else 
        {
            // Compute specular transmission for _FresnelSpecular_
            bsdfSample.bxdfFlag = BXDF_TRANSMISSION | BXDF_SPECULAR;
            // Figure out which $\eta$ is incident and which is transmitted
            bool entering = CosTheta(wo) > 0;
            float etaI = entering ? 1 : eta;
            float etaT = entering ? eta : 1;

            // Compute ray direction for specular transmission
            float3 wi;
            bool bValid = Refract(wo, Faceforward(float3(0, 0, 1), wo), etaI / etaT, wi);
            bsdfSample.wi = wi;
            if (!bValid)
            {
                bsdfSample.reflectance = 1;
                bsdfSample.pdf = 1;
                return bsdfSample;
            }
            //T = 1;
            F = 0;
            float3 ft = T * (1 - F);

            // Account for non-symmetry with transmission to different medium
            //if (mode == TransportMode::Radiance)
            ft *= (etaI * etaI) / (etaT * etaT);
            //if (sampledType)
            //    *sampledType = BxDFType(BSDF_SPECULAR | BSDF_TRANSMISSION);
            bsdfSample.pdf = 1 - F;
            bsdfSample.reflectance = ft / AbsCosTheta(wi);
            return bsdfSample;
        }
    }

    float Pdf(float3 wo, float3 wi)
    {
        return 0;
    }

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        pdf = 0;
        return 0;
    }
};

struct BxDFFresnelBlend
{
    float3 R;
    float3 S;
    float alphax;
    float alphay;
    float3 eta;

    float3 Sample_wh(float2 u, float3 wo)
    {
        return SampleVector(wo, u, alphax, alphay);
    }

    float3 SchlickFresnel(float cosTheta) 
    {
        return FrSchlick(S, cosTheta);//S + Pow5(1 - cosTheta) * (1 - S);
    }

    BSDFSample Sample_F(float uOrig, float2 u, float3 wo)
    {
        BSDFSample bsdfSample = (BSDFSample)0;
        float uc = uOrig;
        float3 wi;
        float fr = FrDielectric(CosTheta(wo), 1.0/eta.x);
        float pdf = 0;
        if (uc > fr) 
        {
            bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_DIFFUSE;
            //u[0] = min(2 * (u[0] - fr), ONE_MINUS_EPSILON);
            // Cosine-sample the hemisphere, flipping the direction if necessary
            wi = CosineSampleHemisphere(u);
            if (wo.z < 0) 
                wi.z *= -1;
            pdf = AbsCosTheta(wi) * INV_PI * (1.0 - fr);
            float3 diffuse = (28.0f / (23.0f * PI)) * R * (1.0 - S) *
                (1.0 - Pow5(1.0 - 0.5f * AbsCosTheta(wi))) *
                (1.0 - Pow5(1.0 - 0.5f * AbsCosTheta(wo)));
            bsdfSample.reflectance = diffuse;
        }
        else 
        {
            bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_GLOSSY;
            //u[0] = min(2 * (1.0 - fr) * u[0], ONE_MINUS_EPSILON);
            // Sample microfacet orientation $\wh$ and reflected direction $\wi$
            float3 wh = Sample_wh(u, wo);
            wi = normalize(reflect(-wo, wh));
            if (!SameHemisphere(wo, wi))
                return bsdfSample;
            float D = TrowbridgeReitzD(wh, alphax, alphay);
            float3 specular = SchlickFresnel(abs(dot(wi, wh))) * D / (4.0 * abs(dot(wi, wh)) * max(AbsCosTheta(wi), AbsCosTheta(wo)));
            bsdfSample.reflectance = specular;
            float pdf_wh = Pdf_wh(D, wh);
            pdf = fr * pdf_wh / (4.0 * dot(wo, wh));
        }
        //pdf = (1.0 - fr) * pdf_diffuse + fr * pdf_specular;
        bsdfSample.pdf = pdf;
        bsdfSample.wi = wi;
        bsdfSample.eta = eta;
        return bsdfSample;
    }

    //float Pdf(float3 wo, float3 wi)
    //{
    //    if (!SameHemisphere(wo, wi)) 
    //        return 0;
    //    float3 wh = normalize(wo + wi);
    //    //float D = TrowbridgeReitzD(wh, alphax, alphay);
    //    float pdf_wh = Pdf_wh(wh, alphax, alphay);
    //    return 0.5f * (AbsCosTheta(wi) * INV_PI + pdf_wh / (4 * dot(wo, wh)));
    //}

    float3 F(float3 wo, float3 wi, out float pdf)
    {
        float3 diffuse = (28.0f / (23.0f * PI)) * R * (1.0 - S) *
            (1.0 - Pow5(1.0 - 0.5f * AbsCosTheta(wi))) *
            (1.0 - Pow5(1.0 - 0.5f * AbsCosTheta(wo)));
        float3 wh = wi + wo;
        if (wh.x == 0 && wh.y == 0 && wh.z == 0) 
            return 0;
        wh = normalize(wh);
        float fr = FrDielectric(CosTheta(wo), 1.0 / eta.x);
        float D = TrowbridgeReitzD(wh, alphax, alphay);
        float3 specular = SchlickFresnel(abs(dot(wi, wh))) * D / (4.0 * abs(dot(wi, wh)) * max(AbsCosTheta(wi), AbsCosTheta(wo)));
        float pdf_wh = Pdf_wh(D, wh);
        pdf = (1.0 - fr) * AbsCosTheta(wi) * INV_PI + fr * pdf_wh / (4.0 * dot(wo, wh));
        return diffuse + specular;
    }
};


#endif
