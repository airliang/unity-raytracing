#ifndef DISNEY_HLSL
#define DISNEY_HLSL
#include "GPUStructs.hlsl"
#include "bxdf.hlsl"
//https://media.disneyanimation.com/uploads/production/publication_asset/48/asset/s2012_pbs_disney_brdf_notes_v3.pdf
//https://github.com/Twinklebear/ChameleonRT/blob/master/backends/dxr/disney_bsdf.hlsl

float schlick_weight(float cos_theta) {
	return pow(saturate(1.f - cos_theta), 5.f);
}

float3 spherical_dir(float sin_theta, float cos_theta, float phi) {
	return float3(sin_theta * cos(phi), sin_theta * sin(phi), cos_theta);
}

// Complete Fresnel Dielectric computation, for transmission at ior near 1
// they mention having issues with the Schlick approximation.
// eta_i: material on incident side's ior
// eta_t: material on transmitted side's ior
float fresnel_dielectric(float cos_theta_i, float eta_i, float eta_t) {
	float g = Pow2(eta_t) / Pow2(eta_i) - 1.f + Pow2(cos_theta_i);
	if (g < 0.f) {
		return 1.f;
	}
	return 0.5f * Pow2(g - cos_theta_i) / Pow2(g + cos_theta_i)
		* (1.f + Pow2(cos_theta_i * (g + cos_theta_i) - 1.f) / Pow2(cos_theta_i * (g - cos_theta_i) + 1.f));
}

// D_GTR1: Generalized Trowbridge-Reitz with gamma=1
// Burley notes eq. 4
float gtr_1(float cos_theta_h, float alpha) {
	if (alpha >= 1.f) {
		return INV_PI;
	}
	float alpha_sqr = alpha * alpha;
	return INV_PI * (alpha_sqr - 1.f) / (log(alpha_sqr) * (1.f + (alpha_sqr - 1.f) * cos_theta_h * cos_theta_h));
}

// D_GTR2: Generalized Trowbridge-Reitz with gamma=2
// Burley notes eq. 8
float gtr_2(float cos_theta_h, float alpha) {
	float alpha_sqr = alpha * alpha;
	return INV_PI * alpha_sqr / Pow2(1.f + (alpha_sqr - 1.f) * cos_theta_h * cos_theta_h);
}

// D_GTR2 Anisotropic: Anisotropic generalized Trowbridge-Reitz with gamma=2
// Burley notes eq. 13
float gtr_2_aniso(float h_dot_n, float h_dot_x, float h_dot_y, float2 alpha) {
	return INV_PI / (alpha.x * alpha.y
		* Pow2(Pow2(h_dot_x / alpha.x) + Pow2(h_dot_y / alpha.y) + h_dot_n * h_dot_n));
}

float smith_shadowing_ggx(float n_dot_o, float alpha_g) {
	float a = alpha_g * alpha_g;
	float b = n_dot_o * n_dot_o;
	return 1.f / (n_dot_o + sqrt(a + b - a * b));
}

float smith_shadowing_ggx_aniso(float n_dot_o, float o_dot_x, float o_dot_y, float2 alpha) {
	return 1.f / (n_dot_o + sqrt(Pow2(o_dot_x * alpha.x) + Pow2(o_dot_y * alpha.y) + Pow2(n_dot_o)));
}

// Sample a reflection direction the hemisphere oriented along n and spanned by v_x, v_y using the random samples in s
float3 sample_lambertian_dir(in const float2 s) {
	const float3 hemi_dir = normalize(CosineSampleHemisphere(s));
	return hemi_dir;
}

// Sample the microfacet normal vectors for the various microfacet distributions
float3 sample_gtr_1_h(float alpha, in const float2 s) {
	float phi_h = 2.f * PI * s.x;
	float alpha_sqr = alpha * alpha;
	float cos_theta_h_sqr = (1.f - pow(alpha_sqr, 1.f - s.y)) / (1.f - alpha_sqr);
	float cos_theta_h = sqrt(cos_theta_h_sqr);
	float sin_theta_h = 1.f - cos_theta_h_sqr;
	float3 hemi_dir = normalize(spherical_dir(sin_theta_h, cos_theta_h, phi_h));
	return hemi_dir;
}

float3 sample_gtr_2_h(float alpha, in const float2 s) {
	float phi_h = 2.f * PI * s.x;
	float cos_theta_h_sqr = (1.f - s.y) / (1.f + (alpha * alpha - 1.f) * s.y);
	float cos_theta_h = sqrt(cos_theta_h_sqr);
	float sin_theta_h = 1.f - cos_theta_h_sqr;
	float3 hemi_dir = normalize(spherical_dir(sin_theta_h, cos_theta_h, phi_h));
	return hemi_dir.x;
}

float3 sample_gtr_2_aniso_h(in const float2 alpha, in const float2 s) {
	float x = 2.f * PI * s.x;
	float3 w_h = sqrt(s.y / (1.f - s.y)) * (alpha.x * cos(x) * float3(1, 0, 0) + alpha.y * sin(x) * float3(0, 1, 0)) + float3(0, 0, 1);
	return normalize(w_h);
}

float gtr_1_pdf(in const float3 w_o, in const float3 w_i, float alpha) {
	if (!SameHemisphere(w_o, w_i)) {
		return 0.f;
	}
	float3 w_h = normalize(w_i + w_o);
	float cos_theta_h = w_h.z;
	float d = gtr_1(cos_theta_h, alpha);
	return d * cos_theta_h / (4.f * dot(w_o, w_h));
}

float gtr_2_pdf(in const float3 w_o, in const float3 w_i, float alpha) {
	if (!SameHemisphere(w_o, w_i)) {
		return 0.f;
	}
	float3 w_h = normalize(w_i + w_o);
	float cos_theta_h = w_h.z;
	float d = gtr_2(cos_theta_h, alpha);
	return d * cos_theta_h / (4.f * dot(w_o, w_h));
}

float gtr_2_transmission_pdf(in const float3 w_o, in const float3 w_i,
	float alpha, float ior)
{
	if (SameHemisphere(w_o, w_i)) {
		return 0.f;
	}
	bool entering = w_o.z > 0.f;
	float eta_o = entering ? 1.f : ior;
	float eta_i = entering ? ior : 1.f;
	float3 w_h = normalize(w_o + w_i * eta_i / eta_o);
	float cos_theta_h = abs(w_h.z);
	float i_dot_h = dot(w_i, w_h);
	float o_dot_h = dot(w_o, w_h);
	float d = gtr_2(cos_theta_h, alpha);
	float dwh_dwi = o_dot_h * Pow2(eta_o) / Pow2(eta_o * o_dot_h + eta_i * i_dot_h);
	return d * cos_theta_h * abs(dwh_dwi);
}

float gtr_2_aniso_pdf(in const float3 w_o, in const float3 w_i, const float2 alpha)
{
	if (!SameHemisphere(w_o, w_i)) {
		return 0.f;
	}
	float3 w_h = normalize(w_i + w_o);
	float cos_theta_h = w_h.z;
	float d = gtr_2_aniso(cos_theta_h, abs(w_h.x), abs(w_h.y), alpha);
	return d * cos_theta_h / (4.f * dot(w_o, w_h));
}

float3 disney_diffuse(in const DisneyMaterial mat,
	in const float3 w_o, in const float3 w_i)
{
	float3 w_h = normalize(w_i + w_o);
	float n_dot_o = abs(w_o.z);
	float n_dot_i = abs(w_i.z);
	float i_dot_h = dot(w_i, w_h);
	float fd90 = 0.5f + 2.f * mat.roughness * i_dot_h * i_dot_h;
	float fi = schlick_weight(n_dot_i);
	float fo = schlick_weight(n_dot_o);
	return mat.baseColor * INV_PI * lerp(1.f, fd90, fi) * lerp(1.f, fd90, fo);
}

float3 disney_microfacet_isotropic(in const DisneyMaterial mat, 
	in const float3 w_o, in const float3 w_i)
{
	float3 w_h = normalize(w_i + w_o);
	float lum = Luminance(mat.baseColor);
	float3 tint = lum > 0.f ? mat.baseColor / lum : float3(1, 1, 1);
	float3 spec = lerp(mat.specular * 0.08 * lerp(float3(1, 1, 1), tint, mat.specularTint), mat.baseColor, mat.metallic);

	float alpha = max(0.001, mat.roughness * mat.roughness);
	float d = gtr_2(w_h.z, alpha);
	float3 f = lerp(spec, float3(1, 1, 1), schlick_weight(dot(w_i, w_h)));
	float g = smith_shadowing_ggx(w_i.z, alpha) * smith_shadowing_ggx(w_o.z, alpha);
	return d * f * g;
}

float3 disney_microfacet_transmission_isotropic(in const DisneyMaterial mat, 
	in const float3 w_o, in const float3 w_i)
{
	float o_dot_n = w_o.z;
	float i_dot_n = w_i.z;
	if (o_dot_n == 0.f || i_dot_n == 0.f) {
		return 0.f;
	}
	bool entering = o_dot_n > 0.f;
	float eta_o = entering ? 1.f : mat.ior;
	float eta_i = entering ? mat.ior : 1.f;
	float3 w_h = normalize(w_o + w_i * eta_i / eta_o);

	float alpha = max(0.001, mat.roughness * mat.roughness);
	float d = gtr_2(abs(w_h.z), alpha);

	float f = fresnel_dielectric(abs(w_i.z), eta_o, eta_i);
	float g = smith_shadowing_ggx(abs(w_i.z), alpha) * smith_shadowing_ggx(abs(w_o.z), alpha);

	float i_dot_h = dot(w_i, w_h);
	float o_dot_h = dot(w_o, w_h);

	float c = abs(o_dot_h) / abs(w_o.z) * abs(i_dot_h) / abs(w_i.z)
		* Pow2(eta_o) / Pow2(eta_o * o_dot_h + eta_i * i_dot_h);

	return mat.baseColor * c * (1.f - f) * g * d;
}

float3 disney_microfacet_anisotropic(in const DisneyMaterial mat, 
	in const float3 w_o, in const float3 w_i)
{
	float3 w_h = normalize(w_i + w_o);
	float lum = Luminance(mat.baseColor);
	float3 tint = lum > 0.f ? mat.baseColor / lum : float3(1, 1, 1);
	float3 spec = lerp(mat.specular * 0.08 * lerp(float3(1, 1, 1), tint, mat.specularTint), mat.baseColor, mat.metallic);

	float aspect = sqrt(1.f - mat.anisotropy * 0.9f);
	float a = mat.roughness * mat.roughness;
	float2 alpha = float2(max(0.001, a / aspect), max(0.001, a * aspect));
	float d = gtr_2_aniso(w_h.z, abs(w_h.x), abs(w_h.y), alpha);
	float3 f = lerp(spec, float3(1, 1, 1), schlick_weight(dot(w_i, w_h)));
	float g = smith_shadowing_ggx_aniso(w_i.z, abs(w_i.x), abs(w_i.y), alpha)
		* smith_shadowing_ggx_aniso(w_o.z, abs(w_o.x), abs(w_o.y), alpha);
	return d * f * g;
}

float disney_clear_coat(in const DisneyMaterial mat,
	in const float3 w_o, in const float3 w_i)
{
	float3 w_h = normalize(w_i + w_o);
	float alpha = lerp(0.1f, 0.001f, mat.clearcoatGloss);
	float d = gtr_1(w_h.z, alpha);
	float f = lerp(0.04f, 1.f, schlick_weight(w_i.z));
	float g = smith_shadowing_ggx(w_i.z, 0.25f) * smith_shadowing_ggx(w_o.z, 0.25f);
	return 0.25 * mat.clearcoat * d * f * g;
}

float3 disney_sheen(in const DisneyMaterial mat, 
	in const float3 w_o, in const float3 w_i)
{
	float3 w_h = normalize(w_i + w_o);
	float lum = Luminance(mat.baseColor);
	float3 tint = lum > 0.f ? mat.baseColor / lum : float3(1, 1, 1);
	float3 sheen_color = lerp(float3(1, 1, 1), tint, mat.sheenTint);
	float f = schlick_weight(w_i.z);
	return f * mat.sheen * sheen_color;
}

float3 disney_brdf(in const DisneyMaterial mat, in const float3 w_o, in const float3 w_i)
{
	if (!SameHemisphere(w_o, w_i)) {
		if (mat.specularTransmission > 0.f) {
			float3 spec_trans = disney_microfacet_transmission_isotropic(mat, w_o, w_i);
			return spec_trans * (1.f - mat.metallic) * mat.specularTransmission;
		}
		return 0.f;
	}

	float coat = disney_clear_coat(mat, w_o, w_i);
	float3 sheen = disney_sheen(mat, w_o, w_i);
	float3 diffuse = disney_diffuse(mat, w_o, w_i);
	float3 gloss;
	if (mat.anisotropy == 0.f) {
		gloss = disney_microfacet_isotropic(mat, w_o, w_i);
	}
	else {
		gloss = disney_microfacet_anisotropic(mat, w_o, w_i);
	}
	return (diffuse + sheen) * (1.f - mat.metallic) * (1.f - mat.specularTransmission) + gloss + coat;
}

float disney_pdf(in const DisneyMaterial mat,
	in const float3 w_o, in const float3 w_i)
{
	float alpha = max(0.001, mat.roughness * mat.roughness);
	float aspect = sqrt(1.f - mat.anisotropy * 0.9f);
	float2 alpha_aniso = float2(max(0.001, alpha / aspect), max(0.001, alpha * aspect));

	float clearcoat_alpha = lerp(0.1f, 0.001f, mat.clearcoatGloss);

	float diffuse = LambertPDF(w_i, w_o);
	float clear_coat = gtr_1_pdf(w_o, w_i, clearcoat_alpha);

	float n_comp = 3.f;
	float microfacet;
	float microfacet_transmission = 0.f;
	if (mat.anisotropy == 0.f) {
		microfacet = gtr_2_pdf(w_o, w_i, alpha);
	}
	else {
		microfacet = gtr_2_aniso_pdf(w_o, w_i, alpha_aniso);
	}
	if (mat.specularTransmission > 0.f) {
		n_comp = 4.f;
		microfacet_transmission = gtr_2_transmission_pdf(w_o, w_i, alpha, mat.ior);
	}
	return (diffuse + microfacet + microfacet_transmission + clear_coat) / n_comp;
}


float3 SampleDisneyBRDF(out float3 wi, float3 wo,  DisneyMaterial material, out float pdf, inout RNG rng)
{
	int component = 0;
	if (material.specularTransmission == 0.f) {
		component = Get1D(rng) * 3.f;
		component = clamp(component, 0, 2);
	}
	else 
	{
		component = Get1D(rng) * 4.f;
		component = clamp(component, 0, 3);
	}

	float2 samples = Get2D(rng);//float2(lcg_randomf(rng), lcg_randomf(rng));
	if (component == 0) {
		// Sample diffuse component
		wi = sample_lambertian_dir(samples);
	}
	else if (component == 1) {
		float3 wh;
		float alpha = max(0.001, material.roughness * material.roughness);
		if (material.anisotropy == 0.f) {
			wh = sample_gtr_2_h(alpha, samples);
		}
		else {
			float aspect = sqrt(1.f - material.anisotropy * 0.9f);
			float2 alpha_aniso = float2(max(0.001, alpha / aspect), max(0.001, alpha * aspect));
			wh = sample_gtr_2_aniso_h(alpha_aniso, samples);
		}
		wi = reflect(-wo, wh);

		// Invalid reflection, terminate ray
		if (!SameHemisphere(wo, wi)) {
			pdf = 0.f;
			wi = 0.f;
			return 0.f;
		}
	}
	else if (component == 2) {
		// Sample clear coat component
		float alpha = lerp(0.1f, 0.001f, material.clearcoatGloss);
		float3 wh = sample_gtr_1_h(alpha, samples);
		wi = reflect(-wo, wh);

		// Invalid reflection, terminate ray
		if (!SameHemisphere(wo, wi)) {
			pdf = 0.f;
			wi = 0.f;
			return 0.f;
		}
	}
	else {
		// Sample microfacet transmission component
		float alpha = max(0.001, material.roughness * material.roughness);
		float3 wh = sample_gtr_2_h(alpha, samples);
		if (dot(wo, wh) < 0.f) {
			wh = -wh;
		}
		bool entering = wo.z > 0.f;
		wi = refract(-wo, wh, entering ? 1.f / material.ior : material.ior);

		// Invalid refraction, terminate ray
		if (all(wi == 0.f)) {
			pdf = 0.f;
			return 0.f;
		}
	}
	pdf = disney_pdf(material, wo, wi);
	return disney_brdf(material, wo, wi);
}

#endif
