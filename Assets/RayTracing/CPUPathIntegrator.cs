using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class GeometryMath
{
    public static float AbsCosTheta(Vector3 w)
    {
        return Mathf.Abs(w.z);
    }

    public static bool SameHemisphere(Vector3 w, Vector3 wp)
    {
        return w.z * wp.z > 0;
    }

    public static Vector3 Faceforward(Vector3 normal, Vector3 v)
    {
        return (Vector3.Dot(normal, v) < 0.0f) ? -normal : normal;
    }

    public static float CosTheta(Vector3 w) { return w.z; }
    public static float Cos2Theta(Vector3 w) { return w.z * w.z; }
    public static float Sin2Theta(Vector3 w)
    {
        return Mathf.Max(0, 1.0f - Cos2Theta(w));
    }

    public static float SinTheta(Vector3 w) { return Mathf.Sqrt(Sin2Theta(w)); }

    public static float TanTheta(Vector3 w) { return SinTheta(w) / CosTheta(w); }

    public static float Tan2Theta(Vector3 w)
    {
        return Sin2Theta(w) / Cos2Theta(w);
    }

    public static float CosPhi(Vector3 w)
    {
        float sinTheta = SinTheta(w);
        return (sinTheta == 0) ? 1 : Mathf.Clamp(w.x / sinTheta, -1, 1);
    }

    public static float SinPhi(Vector3 w)
    {
        float sinTheta = SinTheta(w);
        return (sinTheta == 0) ? 0 : Mathf.Clamp(w.y / sinTheta, -1, 1);
    }

    public static float Cos2Phi(Vector3 w)
    {
        float v = CosPhi(w);
        return v * v;
    }

    public static float Sin2Phi(Vector3 w)
    {
        float v = SinPhi(w);
        return v * v;
    }

    public static Vector3 SphericalDirection(float sinTheta, float cosTheta, float phi)
    {
        return new Vector3(sinTheta * Mathf.Cos(phi), sinTheta * Mathf.Sin(phi), cosTheta);
    }

    public static float SphericalTheta(Vector3 v)
    {
        return Mathf.Acos(Mathf.Clamp(v.z, -1, 1));
    }

    public static float SphericalPhi(Vector3 v)
    {
        float p = Mathf.Atan2(v.y, v.x);
        return (p < 0) ? (p + 2 * Mathf.PI) : p;
    }

    public static float Pow5(float v)
    {
        return v * v * v * v * v;
    }

    public static Vector3 Reflect(Vector3 wo, Vector3 n)
    {
        return -wo + 2 * Vector3.Dot(wo, n) * n;
    }

    public static bool Refract(Vector3 wi, Vector3 n, float eta, out Vector3 wt)
    {
        float cosThetaI = Vector3.Dot(n, wi);
        float sin2ThetaI = Mathf.Max(0, 1.0f - cosThetaI * cosThetaI);
        float sin2ThetaT = eta * eta * sin2ThetaI;
        wt = Vector3.zero;
        // Handle total internal reflection for transmission
        if (sin2ThetaT >= 1.0f)
            return false;
        float cosThetaT = Mathf.Sqrt(1.0f - sin2ThetaT);
        wt = eta * -wi + (eta * cosThetaI - cosThetaT) * n;
        return true;
    }

    public static float SchlickWeight(float cosTheta)
    {
        float m = Mathf.Clamp(1 - cosTheta, 0, 1);
        return (m * m) * (m * m) * m;
    }

    public static float FrSchlick(float R0, float cosTheta)
    {
        return Mathf.Lerp(R0, 1, SchlickWeight(cosTheta));
    }

    public static float TWO_PI = Mathf.PI * 2;
    public static float HALF_PI = Mathf.PI * 0.5f;
    public static float INV_PI = 0.31830988618379067154f;
}

public static class Microfacet
{
    public static float RoughnessToAlpha(float roughness)
    {
        roughness = Mathf.Max(roughness, 0.001f);
        float x = Mathf.Log(roughness);
        return 1.62142f + 0.819955f * x + 0.1734f * x * x +
            0.0171201f * x * x * x + 0.000640711f * x * x * x * x;
    }

    public static float Lambda(Vector3 w, float alphax, float alphay)
    {
        float absTanTheta = Mathf.Abs(GeometryMath.TanTheta(w));
        if (float.IsInfinity(absTanTheta))
            return 0;
        // Compute _alpha_ for direction _w_
        float alpha =
            Mathf.Sqrt(GeometryMath.Cos2Phi(w) * alphax * alphax + GeometryMath.Sin2Phi(w) * alphay * alphay);
        float alpha2Tan2Theta = (alpha * absTanTheta) * (alpha * absTanTheta);
        return (-1.0f + Mathf.Sqrt(1.0f + alpha2Tan2Theta)) * 0.5f;
    }

    public static float Distribution(Vector3 wh, float alphax, float alphay)
    {
        float tan2Theta = GeometryMath.Tan2Theta(wh);
        if (float.IsInfinity(tan2Theta))
            return 0;
        float cosTheta2 = GeometryMath.Cos2Theta(wh);
        float cos4Theta = cosTheta2 * cosTheta2;
        float e =
            tan2Theta * (GeometryMath.Cos2Phi(wh) / (alphax * alphax) + GeometryMath.Sin2Phi(wh) / (alphay * alphay));
        return 1.0f / (Mathf.PI * alphax * alphay * cos4Theta * (1 + e) * (1 + e));
    }

    public static Vector3 SampleVector(Vector3 wo, Vector2 u, float alphax, float alphay)
    {
        float phi = GeometryMath.TWO_PI * u[1];
        float cosTheta = 0;
        if (alphax == alphay)
        {
            float tanTheta2 = alphax * alphax * u[0] / (1.0f - u[0]);
            cosTheta = 1.0f / Mathf.Sqrt(1.0f + tanTheta2);
        }
        else
        {
            //https://agraphicsguy.wordpress.com/2018/07/18/sampling-anisotropic-microfacet-brdf/
            phi = Mathf.Atan(alphay / alphax * Mathf.Tan(GeometryMath.TWO_PI * u[1] + GeometryMath.HALF_PI));
            if (u[1] > 0.5f)
                phi += Mathf.PI;
            float sinPhi = Mathf.Sin(phi);
            float cosPhi = Mathf.Cos(phi);
            float alphax2 = alphax * alphax, alphay2 = alphay * alphay;
            float alpha2 =
                1 / (cosPhi * cosPhi / alphax2 + sinPhi * sinPhi / alphay2);
            float tanTheta2 = alpha2 * u[0] / (1 - u[0]);
            cosTheta = 1.0f / Mathf.Sqrt(1 + tanTheta2);
        }
        float sinTheta =
            Mathf.Sqrt(Mathf.Max(0, 1.0f - cosTheta * cosTheta));
        Vector3 wh = GeometryMath.SphericalDirection(sinTheta, cosTheta, phi);
        if (!GeometryMath.SameHemisphere(wo, wh))
            wh = -wh;

        return wh;
    }

    public static float G(Vector3 wo, Vector3 wi, float alphax, float alphay)
    {
        return 1.0f / (1 + Lambda(wo, alphax, alphay) + Lambda(wi, alphax, alphay));
    }

    public static float Pdf_wh(Vector3 wh, float alphaX, float alphaY)
    {
        return Distribution(wh, alphaX, alphaY) * GeometryMath.AbsCosTheta(wh);
    }

    public static float Pdf_wi(Vector3 wo, Vector3 wi, float alphaX, float alphaY)
    {
        if (!GeometryMath.SameHemisphere(wo, wi))
            return 0;
        Vector3 wh = Vector3.Normalize(wo + wi);
        float D = Distribution(wh, alphaX, alphaY);
        return Pdf_wh(wh, alphaX, alphaY) / (4.0f * Vector3.Dot(wo, wh));
    }

    public static Vector3 BRDF(Vector3 wo, Vector3 wi, Vector3 fr, float cosThetaO, float cosThetaI, float alphaX, float alphaY)
    {
        Vector3 wh = Vector3.Normalize(wo + wi);
        float D = Distribution(wh, alphaX, alphaY);
        float GTerm = G(wo, wi, alphaX, alphaY);
        return D * fr * GTerm * 0.25f / Mathf.Abs(cosThetaO * cosThetaI);
    }
}

public class CPUPathIntegrator
{
    
    static float INV_TWO_PI = 0.15915494309189533577f;
    static float INV_FOUR_PI = 0.07957747154594766788f;
    static float HALF_PI = 1.57079632679489661923f;
    static float INV_HALF_PI = 0.63661977236758134308f;
    static float PI_OVER_2 = 1.57079632679489661923f;
    static float PI_OVER_4 = 0.78539816339744830961f;
    public enum BSDFMaterial
    {
        Matte,
        Plastic,
        Metal,
        Mirror,
        Glass,
        Substrate,
    }
    struct PathVertex
    {
        public Vector3 wi;
        public Vector3 bsdfVal;
        public float bsdfPdf;
        public GPUInteraction nextISect;
        public int found;
    }

    static int BXDF_REFLECTION = 1;
    static int BXDF_TRANSMISSION = 1 << 1;
    static int BXDF_DIFFUSE = 1 << 2;
    static int BXDF_SPECULAR = 1 << 3;
    static int BXDF_GLOSSY = 1 << 4;

    public struct BSDFSample
    {
        public Vector3 reflectance;
        public Vector3 wi;   //in local space (z up space)
        public float pdf;
        public float eta;
        public int bxdfFlag;

        public bool IsSpecular()
        {
            return (bxdfFlag & BXDF_SPECULAR) > 0;
        }
    };

    

    static float PowerHeuristic(int nf, float fPdf, int ng, float gPdf)
    {
        float f = nf * fPdf, g = ng * gPdf;
        return (f * f) / (f * f + g * g);
    }

    static Vector2 ConcentricSampleDisk(Vector2 u)
    {
        //mapping u to [-1,1]
        Vector2 u1 = new Vector2(u.x * 2.0f - 1, u.y * 2.0f - 1);

        if (u1.x == 0 && u1.y == 0)
            return Vector2.zero;

        //r = x
        //θ = y/x * π/4
        //最后返回x,y
        //x = rcosθ, y = rsinθ
        float theta, r;
        if (Mathf.Abs(u1.x) > Mathf.Abs(u1.y))
        {
            r = u1.x;
            theta = u1.y / u1.x * PI_OVER_4;
        }
        else
        {
            //这里要反过来看，就是把视野选择90度
            r = u1.y;
            theta = PI_OVER_2 - u1.x / u1.y * PI_OVER_4;
        }
        return r * new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
    }

    static Vector3 CosineSampleHemisphere(Vector2 u)
    {
        Vector2 rphi = ConcentricSampleDisk(u);

        float z = Mathf.Sqrt(1.0f - rphi.x * rphi.x - rphi.y * rphi.y);
        return new Vector3(rphi.x, rphi.y, z);
    }

    static float FrDielectric(float cosThetaI, float etaI, float etaT)
    {
        cosThetaI = Mathf.Clamp(cosThetaI, -1, 1);
        // Potentially swap indices of refraction
        //etaI = 0;
        //etaT = 0;
        bool entering = cosThetaI > 0;
        if (!entering)
        {
            //swap(etaI, etaT);
            float tmp = etaI;
            etaI = etaT;
            etaT = tmp;
            cosThetaI = Mathf.Abs(cosThetaI);
        }

        // Compute _cosThetaT_ using Snell's law
        float sinThetaI = Mathf.Sqrt(Mathf.Max(0, 1 - cosThetaI * cosThetaI));
        float sinThetaT = etaI / etaT * sinThetaI;

        // Handle total internal reflection
        if (sinThetaT >= 1)
            return 1;
        float cosThetaT = Mathf.Sqrt(Mathf.Max((float)0, 1 - sinThetaT * sinThetaT));
        float Rparl = ((etaT * cosThetaI) - (etaI * cosThetaT)) /
            ((etaT * cosThetaI) + (etaI * cosThetaT));
        float Rperp = ((etaI * cosThetaI) - (etaT * cosThetaT)) /
            ((etaI * cosThetaI) + (etaT * cosThetaT));
        return (Rparl * Rparl + Rperp * Rperp) / 2;
    }

    struct BxDFFresnelSpecular
    {
        public Vector3 R;
        public Vector3 T;
        public float eta;

        public BSDFSample Sample_F(Vector2 u, Vector3 wo)
        {
            BSDFSample bsdfSample = new BSDFSample();

            float F = FrDielectric(wo.z, 1.0f, eta);
            float pdf = 0;
            if (u[0] < F)
            {
                bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_SPECULAR;
                // Compute specular reflection for _FresnelSpecular_

                // Compute perfect specular reflection direction
                Vector3 wi = new Vector3(-wo.x, -wo.y, wo.z);
                //if (sampledType)
                //    *sampledType = BxDFType(BSDF_SPECULAR | BSDF_REFLECTION);
                bsdfSample.pdf = F;
                bsdfSample.reflectance = F * R / Mathf.Abs(wi.z);
                bsdfSample.wi = wi;
                return bsdfSample;
            }
            else
            {
                // Compute specular transmission for _FresnelSpecular_
                bsdfSample.bxdfFlag = BXDF_TRANSMISSION | BXDF_SPECULAR;
                // Figure out which $\eta$ is incident and which is transmitted
                bool entering = wo.z > 0;
                float etaI = entering ? 1 : eta;
                float etaT = entering ? eta : 1;

                // Compute ray direction for specular transmission
                Vector3 wi = Vector3.zero;
                bool bValid = Refract(wo, GeometryMath.Faceforward(new Vector3(0, 0, 1), wo), etaI / etaT, ref wi);
                bsdfSample.wi = wi;
                if (!bValid)
                {
                    bsdfSample.reflectance = Vector3.one;
                    bsdfSample.pdf = 1;
                    return bsdfSample;
                }
                //T = 1;
                F = 0;
                Vector3 ft = T.Mul(1.0f - F);

                // Account for non-symmetry with transmission to different medium
                //if (mode == TransportMode::Radiance)
                ft *= (etaI * etaI) / (etaT * etaT);
                //if (sampledType)
                //    *sampledType = BxDFType(BSDF_SPECULAR | BSDF_TRANSMISSION);
                bsdfSample.pdf = 1 - F;
                bsdfSample.reflectance = ft / Mathf.Abs(wi.z);
                return bsdfSample;
            }
        }

        float Pdf(Vector3 wo, Vector3 wi)
        {
            return 0;
        }

        public Vector3 F(Vector3 wo, Vector3 wi, ref float pdf)
        {
            pdf = 0;
            return Vector3.zero;
        }
    };

    public struct BxDFFresnelBlend
    {
        public Vector3 R;
        public Vector3 S;
        public float alphax;
        public float alphay;
        public Vector3 eta;

        public Vector3 Sample_wh(Vector2 u, Vector3 wo)
        {
            return Microfacet.SampleVector(wo, u, alphax, alphay);
        }

        public Vector3 SchlickFresnel(float cosTheta)
        {
            return Vector3.Lerp(S, Vector3.one, GeometryMath.Pow5(1 - cosTheta));//S + GeometryMath.Pow5(1 - cosTheta) * (Vector3.one - S);
        }

        public BSDFSample Sample_F(float uOrig, Vector2 u, Vector3 wo)
        {
            BSDFSample bsdfSample = new BSDFSample();
            float uc = uOrig;
            Vector3 wi;
            float fr = FrDielectric(GeometryMath.CosTheta(wo), 1, eta.x);
            float pdf = 0;
            if (uc > fr)
            {
                bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_DIFFUSE;
                //u[0] = min(2 * (u[0] - fr), ONE_MINUS_EPSILON);
                // Cosine-sample the hemisphere, flipping the direction if necessary
                wi = CosineSampleHemisphere(u);
                if (wo.z < 0)
                    wi.z *= -1;
                pdf = GeometryMath.AbsCosTheta(wi) * GeometryMath.INV_PI * (1.0f - fr);
                Vector3 diffuse = R.Mul(28.0f / (23.0f * Mathf.PI)).Mul((Vector3.one - S).Mul(
                    (1.0f - GeometryMath.Pow5(1.0f - 0.5f * GeometryMath.AbsCosTheta(wi))) *
                    (1.0f - GeometryMath.Pow5(1.0f - 0.5f * GeometryMath.AbsCosTheta(wo)))));
                bsdfSample.reflectance = diffuse;
            }
            else
            {
                bsdfSample.bxdfFlag = BXDF_REFLECTION | BXDF_GLOSSY;
                //u[0] = min(2 * (1.0 - fr) * u[0], ONE_MINUS_EPSILON);
                // Sample microfacet orientation $\wh$ and reflected direction $\wi$
                Vector3 wh = Sample_wh(u, wo);
                wi = Vector3.Normalize(GeometryMath.Reflect(wo, wh));
                if (!GeometryMath.SameHemisphere(wo, wi))
                    return bsdfSample;
                float D = Microfacet.Distribution(wh, alphax, alphay);
                Vector3 specular = SchlickFresnel(Vector3.Dot(wi, wh)) * D / (4.0f * Mathf.Abs(Vector3.Dot(wi, wh)) * Mathf.Max(GeometryMath.AbsCosTheta(wi), GeometryMath.AbsCosTheta(wo)));
                bsdfSample.reflectance = specular;
                float pdf_wh = Microfacet.Pdf_wh(wh, alphax, alphay);
                pdf = fr * pdf_wh / (4.0f * Vector3.Dot(wo, wh));
            }

            //bsdfSample.reflectance = F(wo, wi, pdf);
            bsdfSample.pdf = pdf;
            bsdfSample.wi = wi;
            bsdfSample.eta = eta.x;
            return bsdfSample;
        }

        public float Pdf(Vector3 wo, Vector3 wi)
        {
            if (!GeometryMath.SameHemisphere(wo, wi))
                return 0;
            Vector3 wh = Vector3.Normalize(wo + wi);
            //float D = TrowbridgeReitzD(wh, alphax, alphay);
            float pdf_wh = Microfacet.Pdf_wh(wh, alphax, alphay);
            return 0.5f * (GeometryMath.AbsCosTheta(wi) * GeometryMath.INV_PI + pdf_wh / (4.0f * Vector3.Dot(wo, wh)));
        }

        public Vector3 F(Vector3 wo, Vector3 wi, ref float pdf)
        {
            Vector3 diffuse = R.Mul((28.0f / (23.0f * Mathf.PI))).Mul((Vector3.one - S).Mul(
                (1.0f - GeometryMath.Pow5(1.0f - 0.5f * GeometryMath.AbsCosTheta(wi))) *
                (1.0f - GeometryMath.Pow5(1.0f - 0.5f * GeometryMath.AbsCosTheta(wo)))));
            Vector3 wh = wi + wo;
            pdf = 0;
            if (wh.x == 0 && wh.y == 0 && wh.z == 0)
                return Vector3.zero;
            wh = Vector3.Normalize(wh);
            float D = Microfacet.Distribution(wh, alphax, alphay);
            float fr = GeometryMath.FrSchlick(S.x, Vector3.Dot(wi, wh)) * D / (4.0f * Mathf.Abs(Vector3.Dot(wi, wh)) * Mathf.Max(GeometryMath.AbsCosTheta(wi), GeometryMath.AbsCosTheta(wo)));
            Vector3 specular = new Vector3(fr, fr, fr);
            float pdf_wh = Microfacet.Pdf_wh(wh, alphax, alphay);
            pdf = 0.5f * (GeometryMath.AbsCosTheta(wi) * GeometryMath.INV_PI + pdf_wh / (4.0f * Vector3.Dot(wo, wh)));
            return diffuse + specular;
        }
    }


    static bool Refract(Vector3 wi, Vector3 n, float eta, ref Vector3 wt)
    {
        float cosThetaI = Vector3.Dot(n, wi);
        float sin2ThetaI = Mathf.Max(0, (1.0f - cosThetaI * cosThetaI));
        float sin2ThetaT = eta * eta * sin2ThetaI;

        // Handle total internal reflection for transmission
        if (sin2ThetaT >= 1)
            return false;
        float cosThetaT = Mathf.Sqrt(1.0f - sin2ThetaT);
        wt = eta * -wi + (eta * cosThetaI - cosThetaT) * n;
        return true;
    }

    static Vector3 LambertBRDF(Vector3 wi, Vector3 wo, Vector3 R)
    {
        return wo.z == 0 ? Vector3.zero : R.Div(Mathf.PI);
    }

    //wi and wo must in local space
    static float LambertPDF(Vector3 wi, Vector3 wo)
    {
        return GeometryMath.SameHemisphere(wo, wi) ? Mathf.Abs(wi.z) / Mathf.PI : 0;
    }

    static Vector3 MaterialBRDF(GPUMaterial material, GPUInteraction isect, Vector3 wo, Vector3 wi, ref float pdf)
    {
        void ComputeBxDFFresnelSpecular(GPUMaterial shadingMaterial, ref BxDFFresnelSpecular bxdf)
        {
            //UnpackFresnel(shadingMaterial, bxdf.fresnel);
            bxdf.T = shadingMaterial.transmission;
            bxdf.R = shadingMaterial.baseColor;
            bxdf.eta = shadingMaterial.eta.x;
        }

        //ShadingMaterial shadingMaterial = (ShadingMaterial)0;
        Vector3 f = Vector3.zero;
        pdf = 0;
        if (material.materialType == -1)
        {

        }
        else
        {
            //UnpackShadingMaterial(material, shadingMaterial, isect);
            int nComponent = 0;
            if (material.materialType == (int)BSDFMaterial.Glass)
            {

                nComponent = 1;
                BxDFFresnelSpecular bxdfFresnelSpecular = new BxDFFresnelSpecular();
                ComputeBxDFFresnelSpecular(material, ref bxdfFresnelSpecular);
                float pdfReflection = 0;
                f += bxdfFresnelSpecular.F(wo, wi, ref pdfReflection);
                pdf += pdfReflection;
            }
            else if (material.materialType == (int)BSDFMaterial.Substrate)
            {
                nComponent = 1;
                BxDFFresnelBlend bxdfFresnelBlend = new BxDFFresnelBlend();
                ComputeBxDFFresnelBlend(material, ref bxdfFresnelBlend);
                float pdfReflection = 0;
                f += bxdfFresnelBlend.F(wo, wi, ref pdfReflection);
                pdf += pdfReflection;
            }
            else
            {
                nComponent = 1;
                f += LambertBRDF(wi, wo, material.baseColor);
                pdf += LambertPDF(wi, wo);
            }
            if (nComponent > 1)
            {
                pdf /= (float)nComponent;
                f /= (float)nComponent;
            }
        }

        return f;
    }

    static void ComputeBxDFFresnelSpecular(GPUMaterial shadingMaterial, ref BxDFFresnelSpecular bxdf)
    {
        //UnpackFresnel(shadingMaterial, bxdf.fresnel);
        bxdf.T = shadingMaterial.transmission;
        bxdf.R = shadingMaterial.baseColor;
        bxdf.eta = shadingMaterial.eta.x;
    }

    static void ComputeBxDFFresnelBlend(GPUMaterial shadingMaterial, ref BxDFFresnelBlend bxdf)
    {
        bxdf.R = shadingMaterial.baseColor;
        bxdf.S = shadingMaterial.specularColor;
        bxdf.alphax = shadingMaterial.roughness; // RoughnessToAlpha(shadingMaterial.roughness);
        bxdf.alphay = shadingMaterial.anisotropy; // RoughnessToAlpha(shadingMaterial.roughnessV);
        bxdf.eta = shadingMaterial.eta;
    }

    static BSDFSample SampleGlass(GPUMaterial material, Vector3 wo)
    {

        BxDFFresnelSpecular bxdf = new BxDFFresnelSpecular();
        ComputeBxDFFresnelSpecular(material, ref bxdf);
        Vector2 u = Get2D();
        return bxdf.Sample_F(u, wo);
    }

    static BSDFSample SampleLambert(GPUMaterial material, Vector3 wo)
    {
        BSDFSample bsdfSample = new BSDFSample();
        Vector2 u = Get2D();
        Vector3 wi = CosineSampleHemisphere(u);
        if (wo.z < 0)
            wi.z *= -1;
        bsdfSample.wi = wi;
        bsdfSample.pdf = LambertPDF(wi, wo);
        bsdfSample.reflectance = LambertBRDF(wi, wo, material.baseColor);
        return bsdfSample;
    }

    static BSDFSample SampleSubstrate(GPUMaterial material, Vector3 wo)
    {
        BxDFFresnelBlend bxdf = new BxDFFresnelBlend();
        ComputeBxDFFresnelBlend(material, ref bxdf);
        float uc = Get1D();
        Vector2 u = Get2D();
        return bxdf.Sample_F(uc, u, wo);
    }

    //wi wo is a vector which in local space of the interfaction surface
    static BSDFSample SampleMaterialBRDF(GPUMaterial material, GPUInteraction isect, Vector3 wo)
    {
        switch (material.materialType)
        {
            //case Disney:
            //	return 0;
            case (int)BSDFMaterial.Matte:
                return SampleLambert(material, wo);
            case (int)BSDFMaterial.Plastic:
                //return SamplePlastic(material, wo);
            case (int)BSDFMaterial.Metal:
                //return SampleMetal(material, wo);
            case (int)BSDFMaterial.Mirror:
                //return SampleMirror(material, wo);
            case (int)BSDFMaterial.Glass:
                return SampleGlass(material, wo);
            case (int)BSDFMaterial.Substrate:
                return SampleSubstrate(material, wo);
            default:
                return SampleLambert(material, wo);
        }
        //return SampleGlass(material, wo);
    }

    public static GPURay SpawnRay(Vector3 p, Vector3 direction, Vector3 normal, float tMax)
    {
        float origin() { return 1.0f / 32.0f; }
        float float_scale() { return 1.0f / 65536.0f; }
        float int_scale() { return 256.0f; }

        Vector3 offset_ray(Vector3 p, Vector3 n)
        {
            Vector3Int of_i = new Vector3Int((int)(int_scale() * n.x), (int)(int_scale() * n.y), (int)(int_scale() * n.z));

            Vector3 p_i = new Vector3(
                MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.x) + ((p.x < 0) ? -of_i.x : of_i.x)),
                MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.y) + ((p.y < 0) ? -of_i.y : of_i.y)),
                MathUtil.Int32BitsToSingle(MathUtil.SingleToInt32Bits(p.z) + ((p.z < 0) ? -of_i.z : of_i.z)));

            return new Vector3(Mathf.Abs(p.x) < origin() ? p.x + float_scale() * n.x : p_i.x,
                Mathf.Abs(p.y) < origin() ? p.y + float_scale() * n.y : p_i.y,
                Mathf.Abs(p.z) < origin() ? p.z + float_scale() * n.z : p_i.z);
        }

        GPURay ray;
        float s = Mathf.Sign(Vector3.Dot(normal, direction));
        normal *= s;
        ray.orig = offset_ray(p, normal);
        ray.tmax = tMax;
        ray.direction = direction;
        ray.tmin = 0;
        return ray;
    }

    static bool ClosestHit(GPURay ray, GPUSceneData gpuSceneData, ref HitInfo hitInfo)
    {
        //bool hitted = gpuSceneData.BVH.IntersectInstTest(ray, gpuSceneData.meshInstances, gpuSceneData.InstanceBVHAddr, ref hitInfo);
        bool hitted = BVHAccel.NVMethod ? gpuSceneData.BVH.BVHHit(ray, ref hitInfo, false, gpuSceneData.meshInstances, gpuSceneData.InstanceBVHAddr)
            : gpuSceneData.BVH.BVHHit3(ray, ref hitInfo, false, gpuSceneData, gpuSceneData.InstanceBVHAddr);

        return hitted;
    }

    static bool AnyHit(GPURay ray, GPUSceneData gpuSceneData, ref HitInfo hitInfo)
    {
        bool hitted = BVHAccel.NVMethod ? gpuSceneData.BVH.BVHHit(ray, ref hitInfo, true, gpuSceneData.meshInstances, gpuSceneData.InstanceBVHAddr)
            : gpuSceneData.BVH.BVHHit3(ray, ref hitInfo, true, gpuSceneData, gpuSceneData.InstanceBVHAddr);

        return hitted;
    }

    static bool ShadowRayVisibilityTest(Vector3 p0, Vector3 p1, Vector3 normal, GPUSceneData gpuSceneData)
    {
        GPURay ray = SpawnRay(p0, p1 - p0, normal, 1.0f - 0.001f);
        HitInfo hitInfo = new HitInfo();
        return AnyHit(ray, gpuSceneData, ref hitInfo);

        //!IntersectP(ray, hitT, meshInstanceIndex);
    }

    static int FindIntervalSmall(int start, int cdfSize, float u, List<Vector2> funcs)
    {
        if (cdfSize < 2)
            return start;
        int first = 0, len = cdfSize;
        while (len > 0)
        {
            int nHalf = len >> 1;
            int middle = first + nHalf;
            // Bisect range based on value of _pred_ at _middle_
            Vector2 distrubution = funcs[start + middle];
            if (distrubution.y <= u)
            {
                first = middle + 1;
                len -= nHalf + 1;
            }
            else
                len = nHalf;
        }
        //if first - 1 < 0, the clamp function is useless
        return Mathf.Clamp(first - 1, 0, cdfSize - 2) + start;
    }


    static int Sample1DDiscrete(float u, GPUDistributionDiscript discript, List<Vector2> funcs, ref float pmf)
    {
        int cdfSize = discript.num + 1;
        int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
        float cdfOffset = funcs[offset].y;
        float cdfOffset1 = funcs[offset + 1].y;
        float du = u - cdfOffset;
        if ((cdfOffset1 - cdfOffset) > 0)
        {
            du /= (cdfOffset1 - cdfOffset);
        }

        // Compute PMF for sampled offset
        // pmf is the probability, so is the sample's area / total area
        pmf = discript.funcInt > 0 ? funcs[offset].x * (discript.domain.y - discript.domain.x) / (discript.funcInt * discript.num) : 0;


        return offset - discript.start; //(int)(offset - discript.start + du) / discript.num;
    }

    static float Sample1DContinuous(float u, GPUDistributionDiscript discript, List<Vector2> funcs, ref float pdf, ref int off)
    {
        // Find surrounding CDF segments and _offset_
        int cdfSize = discript.num + 1;
        int offset = FindIntervalSmall(discript.start, cdfSize, u, funcs);
        off = offset;
        // Compute offset along CDF segment
        float cdfOffset = funcs[offset].y;
        float cdfOffset1 = funcs[offset + 1].y;
        float du = u - cdfOffset;
        if ((cdfOffset1 - cdfOffset) > 0)
        {
            du /= (cdfOffset1 - cdfOffset);
        }

        // Compute PDF for sampled offset
        pdf = (discript.funcInt > 0) ? funcs[offset].x / discript.funcInt : 0;

        // Return $x\in{}[0,1)$ corresponding to sample
        return Mathf.Lerp(discript.domain.x, discript.domain.y, (offset - discript.start + du) / discript.num);
    }

    static float DiscretePdf(int index, GPUDistributionDiscript discript, List<Vector2> funcs)
    {
        return funcs[discript.start + index].x * (discript.domain.y - discript.domain.x) / (discript.funcInt * discript.num);
    }

    static Vector2 Sample2DContinuous(Vector2 u, GPUDistributionDiscript discript, List<Vector2> marginal, List<Vector2> conditions, List<float> conditionFuncInts, ref float pdf)
    {
        float pdfMarginal = 0;
        int v = 0;
        float d1 = Sample1DContinuous(u.y, discript, marginal, ref pdfMarginal, ref v);
        int nu = 0;
        float pdfCondition = 0;
        GPUDistributionDiscript dCondition = new GPUDistributionDiscript();
        dCondition.start = v * (discript.unum + 1);   //the size of structuredbuffer is func.size + 1, because the cdfs size is func.size + 1 
        dCondition.num = discript.unum;
        dCondition.funcInt = conditionFuncInts[v];
        dCondition.domain.x = discript.domain.z;
        dCondition.domain.y = discript.domain.w;
        float d0 = Sample1DContinuous(u.x, dCondition, conditions, ref pdfCondition, ref nu);
        //p(v|u) = p(u,v) / pv(u)
        //so 
        //p(u,v) = p(v|u) * pv(u)
        pdf = pdfCondition * pdfMarginal;
        return new Vector2(d0, d1);
    }

    static float Distribution2DPdf(Vector2 u, GPUDistributionDiscript discript, List<Vector2> marginal, List<Vector2> conditions)
    {
        int iu = (int)Mathf.Clamp((u[0] * discript.unum), 0, discript.unum - 1);
        int iv = (int)Mathf.Clamp((u[1] * discript.num), 0, discript.num - 1);
        int conditionVOffset = iv * (discript.unum + 1) + iu;
        return conditions[conditionVOffset].x / discript.funcInt;
    }

    static Vector3 SamplePointOnTriangle(Vector3 p0, Vector3 p1, Vector3 p2, Vector2 u, ref Vector3 normal, ref float pdf)
    {
        //caculate bery centric uv w = 1 - u - v
        float t = Mathf.Sqrt(u.x);
        Vector2 uv = new Vector2(1.0f - t, t * u.y);
        float w = 1 - uv.x - uv.y;

        Vector3 position = p0 * w + p1 * uv.x + p2 * uv.y;
        Vector3 crossVector = Vector3.Cross(p1 - p0, p2 - p0);
        normal = Vector3.Normalize(crossVector);
        pdf = 2.0f / crossVector.magnitude;

        return position;
    }


    static Vector3 SampleTriangleLight(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 u, GPUInteraction isect, GPULight light, ref Vector3 wi, ref Vector3 position, ref float pdf)
    {
        Vector3 Li = Vector3.zero;
        Vector3 lightPointNormal = Vector3.zero;
        float triPdf = 0;
        position = SamplePointOnTriangle(p0, p1, p2, u, ref lightPointNormal, ref triPdf);
        pdf = triPdf;
        wi = position - isect.p;
        float wiLength = wi.magnitude;
        wi = Vector3.Normalize(wi);
        float cos = Vector3.Dot(lightPointNormal, -wi);
        float absCos = Mathf.Abs(cos);
        pdf *= wiLength * wiLength / absCos;
        if (float.IsNaN(pdf) || wiLength == 0)
        {
            pdf = 0;
            return Vector3.zero;
        }

        return cos > 0 ? light.radiance : Vector3.zero;
    }

    static int SampleTriangleIndexOfLightPoint(float u, GPUDistributionDiscript discript, List<Vector2> distributions, ref float pdf)
    {
        int index = Sample1DDiscrete(u, discript, distributions, ref pdf);
        return index;
    }

    static Vector3 SampleLightRadiance(GPULight light, GPUInteraction isect, 
    ref Vector3 wi, ref float lightPdf, ref Vector3 lightPoint, GPUSceneData gpuSceneData)
    {
        if (light.type == (int)LightInstance.LightType.Area)
        {
            int discriptIndex = light.distributionDiscriptIndex;
            GPUDistributionDiscript lightDistributionDiscript = gpuSceneData.gpuDistributionDiscripts[discriptIndex];
            float u = Get1D();
            float triPdf = 0;
            lightPdf = 0;
            MeshInstance meshInstance = gpuSceneData.meshInstances[light.meshInstanceID];
            int triangleIndex = SampleTriangleIndexOfLightPoint(u, lightDistributionDiscript, gpuSceneData.Distributions1D, ref lightPdf) + meshInstance.triangleStartOffset;

            int vertexStart = triangleIndex;
            Vector3Int face = gpuSceneData.triangles[triangleIndex];
            int vIndex0 = face.x;//gpuSceneData.triangles[vertexStart];
            int vIndex1 = face.y;//gpuSceneData.triangles[vertexStart + 1];
            int vIndex2 = face.z;//gpuSceneData.triangles[vertexStart + 2];
            Vector3 p0 = gpuSceneData.gpuVertices[vIndex0].position;
            Vector3 p1 = gpuSceneData.gpuVertices[vIndex1].position;
            Vector3 p2 = gpuSceneData.gpuVertices[vIndex2].position;
            //convert to worldpos

            p0 = meshInstance.localToWorld.MultiplyPoint(p0); 
            p1 = meshInstance.localToWorld.MultiplyPoint(p1); 
            p2 = meshInstance.localToWorld.MultiplyPoint(p2);

            Vector3 Li = SampleTriangleLight(p0, p1, p2, Get2D(), isect, light, ref wi, ref lightPoint, ref triPdf);
            lightPdf *= triPdf;
            return Li;
        }
        else if (light.type == (int)LightInstance.LightType.Envmap)
        {
            Vector2 u = Get2D();
            //Vector3 Li = UniformSampleEnviromentLight(u, lightPdf, wi); 
            Vector3 Li = Vector3.zero;
            //if (isUniform)
            //    Li = UniformSampleEnviromentLight(u, lightPdf, wi);
            //else
            //    Li = ImportanceSampleEnviromentLight(u, lightPdf, wi);
            //Li = isUniform ? Vector3(0.5, 0, 0) : Li;
            lightPoint = isect.p + wi * 10000.0f;
            return Li;
        }

        wi = Vector3.zero;
        lightPdf = 0;
        lightPoint = Vector3.zero;
        return Vector3.zero;
    }

    static Vector3 Light_Le(Vector3 wi, GPULight light)
    {
        if (light.type == 0)
        {
            return light.radiance;
        }
        //else if (light.type == 1)
        //{
        //    return EnviromentLightLe(wi);
        //}
        return Vector3.zero;
    }

    static float AreaLightPdf(GPULight light, GPUInteraction isect, GPUSceneData gpuSceneData)
    {
        float lightPdf = 0;
        if (light.type == 0)
        {
            return 1.0f / light.area;
        }
        //else if (light.type == EnvLightType)
        //{
        //	lightPdf = EnvLightLiPdf(wi, isUniform); //INV_FOUR_PI;
        //}

        return lightPdf;
    }

    public static float Get1D()
    {
        return UnityEngine.Random.Range(0.0f, 1.0f);
    }

    public static Vector2 Get2D()
    {
        return new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
    }

    static Vector3 MIS_ShadowRay(GPULight light, GPUInteraction isect, GPUMaterial material, float lightSourcePdf, GPUSceneData gpuSceneData)
    {
        Vector3 wi = Vector3.up;
        float lightPdf = 0;
        Vector3 samplePointOnLight = Vector3.zero;
        Vector3 ld = Vector3.zero;
        Vector3 Li = SampleLightRadiance(light, isect, ref wi, ref lightPdf, ref samplePointOnLight, gpuSceneData);
        lightPdf *= lightSourcePdf;
        //lightPdf = AreaLightPdf(light, isect, wi, _UniformSampleLight) * lightSourcePdf;
        if (Li != Vector3.zero)
        {
            GPUShadowRay shadowRay = new GPUShadowRay();
            

            Vector3 wiLocal = isect.WorldToLocal(wi);
            Vector3 woLocal = isect.WorldToLocal(isect.wo);
            float scatteringPdf = 0;

            Vector3 f = MaterialBRDF(material, isect, woLocal, wiLocal, ref scatteringPdf);
            if (f != Vector3.zero && scatteringPdf > 0)
            {
                Vector3 p0 = isect.p;
                Vector3 p1 = samplePointOnLight;

                bool shadowRayVisible = ShadowRayVisibilityTest(p0, p1, isect.normal, gpuSceneData);

                if (shadowRayVisible)
                {
                    f *= Mathf.Abs(Vector3.Dot(wi, isect.normal));
                    //sample psdf and compute the mis weight
                    float weight =
                        PowerHeuristic(1, lightPdf, 1, scatteringPdf);
                    ld = f.Mul(Li) * weight / lightPdf;
                    //ld = Li / lightPdf;
                }

            }
        }

        return ld;
    }

    static Vector3 MIS_BSDF(GPUInteraction isect, GPUMaterial material, GPULight light, int lightIndex, float lightSourcePdf, ref PathVertex pathVertex, GPUSceneData gpuSceneData)
    {
        Vector3 ld = Vector3.zero;
        Vector3 woLocal = isect.WorldToLocal(isect.wo);
        //Vector3 wi;
        //float scatteringPdf = 0;
        //pathVertex = (PathVertex)0;
        Vector3 wiLocal = Vector3.zero;
        Vector2 u = Get2D();

        BSDFSample bsdfSample = SampleMaterialBRDF(material, isect, woLocal);
        Vector3 wi = isect.LocalToWorld(bsdfSample.wi);
        float scatteringPdf = bsdfSample.pdf;
        Vector3 f = bsdfSample.reflectance * Mathf.Abs(Vector3.Dot(wi, isect.normal));
        HitInfo hitInfo = new HitInfo();

        if (f != Vector3.zero && scatteringPdf > 0)
        {
            GPURay ray = SpawnRay(isect.p, wi, isect.normal, float.MaxValue);
            //Interaction lightISect = (Interaction)0;
            bool found = ClosestHit(ray, gpuSceneData, ref hitInfo);
            //pathVertex.nextISect = lightISect; 
            //pathVertex.found = found ? 1 : 0;  //can not use this expression or it will be something error. I don't know why.

            Vector3 li = Vector3.zero;
            float lightPdf = 0;

            if (found)
            {
                ComputeSurfaceIntersection(hitInfo, wi, gpuSceneData, out pathVertex.nextISect);
                pathVertex.found = 1;

                uint meshInstanceIndex = pathVertex.nextISect.meshInstanceID;
                MeshInstance meshInstance = gpuSceneData.meshInstances[(int)meshInstanceIndex];
                if (meshInstance.lightIndex == lightIndex)
                {
                    lightPdf = AreaLightPdf(light, pathVertex.nextISect, gpuSceneData) * lightSourcePdf;

                    if (lightPdf > 0)
                    {
                        li = Light_Le(wi, light);
                    }
                }
            }
            //else if (_EnvLightIndex >= 0)//(light.type == EnvLightType)
            //{
            //    Light envLight = lights[_EnvLightIndex];
            //    li = Light_Le(wi, envLight);
            //    if (light.type != EnvLightType)
            //    {
            //        lightSourcePdf = LightSourcePmf(_EnvLightIndex, _UniformSampleLight);
            //        lightPdf = EnvLightLiPdf(wi, _UniformSampleLight) * lightSourcePdf;
            //    }
            //}

            float weight = 1;
            if (!bsdfSample.IsSpecular())
                weight = PowerHeuristic(1, scatteringPdf, 1, lightPdf);
            ld = f.Mul(li) * weight / scatteringPdf;
        }

        pathVertex.wi = wi;
        pathVertex.bsdfVal = f;
        pathVertex.bsdfPdf = scatteringPdf;

        return ld;
    }

    static int ImportanceSampleLightSource(float u, GPUDistributionDiscript discript, List<Vector2> discributions, ref float pmf)
    {
        return Sample1DDiscrete(u, discript, discributions, ref pmf);
    }

    static int SampleGPULightSource(float u, GPUDistributionDiscript discript, List<Vector2> discributions, ref float pmf)
    {
        //DistributionDiscript discript = (DistributionDiscript)0;
        //discript.start = 0;
        ////the length of cdfs is N+1
        //discript.num = lightCount;
        //discript.funcInt = 
        int index  = ImportanceSampleLightSource(u, discript, discributions, ref pmf); //SampleDistribution1DDiscrete(rs.Get1D(threadId), 0, lightCount, pdf);
        
        //int index = UniformSampleLightSource(u, discript, pmf);
        return index;
    }

    static GPULight SampleLightSource(ref float lightSourcePdf, ref int lightIndex, GPUSceneData gpuSceneData)
    {

        //some error happen in SampleLightSource
        lightSourcePdf = 0;
        float u = Get1D();
        GPUDistributionDiscript discript = gpuSceneData.gpuDistributionDiscripts[0];
        lightIndex = SampleGPULightSource(u, discript, gpuSceneData.Distributions1D, ref lightSourcePdf);
        //lightIndex = 0;
        //lightSourcePdf = 0.5;
        GPULight light = gpuSceneData.gpuLights[lightIndex];
        return light;
    }

    static Vector3 EstimateDirectLighting(GPUInteraction isect, ref PathVertex pathVertex, bool breakPath, GPUSceneData gpuSceneData)
    {
        breakPath = false;
        //PathRadiance pathRadiance = (PathRadiance)0;
        //pathRadiance.beta = Vector3(1, 1, 1);
        float lightSourcePdf = 0;
        GPUMaterial material = gpuSceneData.gpuMaterials[(int)isect.materialID];
        int lightIndex = 0;
        GPULight light = SampleLightSource(ref lightSourcePdf, ref lightIndex, gpuSceneData);

        //pathVertex = (PathVertex)0;
        Vector3 ld = MIS_ShadowRay(light, isect, material, lightSourcePdf, gpuSceneData);
        ld += MIS_BSDF(isect, material, light, lightIndex, lightSourcePdf, ref pathVertex, gpuSceneData);

        if (pathVertex.bsdfPdf == 0)
        {
            breakPath = true;
        }

        return ld;
    }

    public static Vector3 PathLi(GPURay ray, GPUSceneData gpuSceneData)
    {
        Vector3 li = Vector3.zero;
        Vector3 beta = Vector3.one;
        GPUInteraction isectLast;
        PathVertex pathVertex = new PathVertex();
        GPUInteraction isect = new GPUInteraction();
        HitInfo hitInfo = new HitInfo();
        for (int bounces = 0; bounces < 5; bounces++)
        {
            bool foundIntersect = false;
            if (bounces == 0)
            {
                foundIntersect = ClosestHit(ray, gpuSceneData, ref hitInfo);
                if (foundIntersect)
                {
                    ComputeSurfaceIntersection(hitInfo, -ray.direction, gpuSceneData, out isect);
                    int meshInstanceIndex = (int)isect.meshInstanceID;
                    MeshInstance meshInstance = gpuSceneData.meshInstances[meshInstanceIndex];

                    int tri0 = hitInfo.triAddr.x;//WoopTriangleData.m_woopTriangleIndices[triAddrDebug];
                    int tri1 = hitInfo.triAddr.y;//WoopTriangleData.m_woopTriangleIndices[triAddrDebug + 1];
                    int tri2 = hitInfo.triAddr.z;//WoopTriangleData.m_woopTriangleIndices[triAddrDebug + 2];
                    if (tri0 >= gpuSceneData.gpuVertices.Count || tri1 >= gpuSceneData.gpuVertices.Count || tri2 >= gpuSceneData.gpuVertices.Count)
                    {
                        Debug.LogError("Triangle Index overflow!");
                    }
                    RenderDebug.DrawTriangle(meshInstance.localToWorld.MultiplyPoint(gpuSceneData.gpuVertices[tri0].position),
                         meshInstance.localToWorld.MultiplyPoint(gpuSceneData.gpuVertices[tri1].position),
                         meshInstance.localToWorld.MultiplyPoint(gpuSceneData.gpuVertices[tri2].position), Color.green);
                }
                
            }
            else
            {
                foundIntersect = pathVertex.found == 1;
            }

            //PathRadiance pathRadiance = pathRadiances[workIndex];
            if (foundIntersect)
            {
                int meshInstanceIndex = (int)isect.meshInstanceID;
                MeshInstance meshInstance = gpuSceneData.meshInstances[meshInstanceIndex];
                int lightIndex = meshInstance.lightIndex;



                //isect.p.w = 1;
                if (lightIndex >= 0 && bounces == 0)
                {
                    GPULight light = gpuSceneData.gpuLights[lightIndex];
                    li += light.radiance.Mul(beta);
                    //color = light.radiance;
                    //isect.p.w = 0;
                }

                bool breakPath = false;
                Vector3 ld = EstimateDirectLighting(isect, ref pathVertex, breakPath, gpuSceneData);
                //li += beta * SampleLight(isect, wi, rng, pathBeta, ray);
                li += ld.Mul(beta);
                //return li;
                if (breakPath)
                    break;

                Vector3 throughput = pathVertex.bsdfVal / pathVertex.bsdfPdf;
                beta = beta.Mul(throughput);

                //Russian roulette
                if (bounces > 3)
                {
                    float q = Mathf.Max(0.05f, 1 - beta.MaxComponent());
                    if (Get1D() < q)
                    {
                        break;
                    }
                    else
                        beta /= 1 - q;
                }

                isectLast = isect;
            }
            else
            {
                //sample enviroment map
                if (bounces == 0)
                {
                    li += Vector3.zero;
                }
                break;
            }

            //ray = SpawnRay(isect.p.xyz, pathVertex.wi, isect.normal, FLT_MAX);
            isect = pathVertex.nextISect;
            //if (pathVertex.found == 1 && pathVertex.nextISect.hitT == 0)
            //{
            //    //some error happen!
            //    return 0;
            //}

        }
        return li;
    }

    public static void ComputeSurfaceIntersection(HitInfo hitInfo, Vector3 wo, GPUSceneData gpuSceneData, out GPUInteraction interaction)
    {
        interaction = new GPUInteraction();
        Vector2 uv = hitInfo.baryCoord;
        MeshInstance meshInst = gpuSceneData.meshInstances[hitInfo.meshInstanceId];
        int vertexIndex0 = hitInfo.triAddr.x;
        int vertexIndex1 = hitInfo.triAddr.y;
        int vertexIndex2 = hitInfo.triAddr.z;
        GPUVertex vertex0 = gpuSceneData.gpuVertices[vertexIndex0];
        GPUVertex vertex1 = gpuSceneData.gpuVertices[vertexIndex1];
        GPUVertex vertex2 = gpuSceneData.gpuVertices[vertexIndex2];
        Vector3 v0 = vertex0.position;
        Vector3 v1 = vertex1.position;
        Vector3 v2 = vertex2.position;
        Vector3 hitPos = v0 * uv.x + v1 * uv.y + v2 * (1.0f - uv.x - uv.y);
        Matrix4x4 objectToWorld = meshInst.localToWorld;
        hitPos = objectToWorld.MultiplyPoint(hitPos);

        Vector3 p0 = objectToWorld.MultiplyPoint(v0);
        Vector3 p1 = objectToWorld.MultiplyPoint(v1);
        Vector3 p2 = objectToWorld.MultiplyPoint(v2);
        float triAreaInWorld = Vector3.Cross(p0 - p1, p0 - p2).magnitude * 0.5f;

        Vector3 normal0 = vertex0.normal;
        Vector3 normal1 = vertex1.normal;
        Vector3 normal2 = vertex2.normal;

        Vector3 normal = Vector3.Normalize(normal0 * uv.x + normal1 * uv.y + normal2 * (1.0f - uv.x - uv.y));

        Vector3 worldNormal = Vector3.Normalize(meshInst.worldToLocal.transpose.inverse.MultiplyPoint(normal)/*mul(normal, (float3x3)meshInst.worldToLocal)*/); ; ;

        Vector2 uv0 = vertex0.uv;
        Vector2 uv1 = vertex1.uv;
        Vector2 uv2 = vertex2.uv;

        interaction.normal = worldNormal;
        interaction.p = hitPos;
        interaction.hitT = hitInfo.hitT;
        interaction.uv = uv0 * uv.x + uv1 * uv.y + uv2 * (1.0f - uv.x - uv.y);

        Vector3 dpdu = new Vector3(1, 0, 0);
        Vector3 dpdv = new Vector3(0, 1, 0);
        MathUtil.CoordinateSystem(worldNormal, ref dpdu, ref dpdv);
        interaction.tangent = Vector3.Normalize(dpdu);
        interaction.bitangent = Vector3.Normalize(Vector3.Cross(interaction.tangent, worldNormal));
        interaction.primArea = triAreaInWorld;
        //interaction.triangleIndex = (uint)(vertexIndex0 - meshInst.triangleStartOffset) / 3;//hitInfo.triangleIndexInMesh;
        interaction.uvArea = Vector3.Cross(new Vector3(uv2.x, uv2.y, 1) - new Vector3(uv0.x, uv0.y, 1), new Vector3(uv1.x, uv1.y, 1) - new Vector3(uv0.x, uv0.y, 1)).magnitude;

        //float4 v0Screen = mul(WorldToRaster, float4(p0, 1));
        //float4 v1Screen = mul(WorldToRaster, float4(p1, 1));
        //float4 v2Screen = mul(WorldToRaster, float4(p2, 1));
        //v0Screen /= v0Screen.w;
        //v1Screen /= v1Screen.w;
        //v2Screen /= v2Screen.w;
        interaction.screenSpaceArea = 0;// length(cross(v2Screen.xyz - v0Screen.xyz, v1Screen.xyz - v0Screen.xyz));
        interaction.wo = wo;
        interaction.materialID = meshInst.materialIndex;
        interaction.meshInstanceID = (uint)hitInfo.meshInstanceId;
    }
}
