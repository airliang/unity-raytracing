#ifndef GEOMETRY_HLSL
#define GEOMETRY_HLSL

#include "mathdef.hlsl"

struct Ray
{
    float3 orig;
    float3 direction;
	float  tmax;
	float  tmin;
};

float origin() { return 1.0f / 32.0f; }
float float_scale() { return 1.0f / 65536.0f; }
float int_scale() { return 256.0f; }

// Normal points outward for rays exiting the surface, else is flipped.
float3 offset_ray(const float3 p, const float3 n)
{
	int3 of_i = int3(int_scale() * n.x, int_scale() * n.y, int_scale() * n.z);

	float3 p_i = float3(
		asfloat(asint(p.x) + ((p.x < 0) ? -of_i.x : of_i.x)),
		asfloat(asint(p.y) + ((p.y < 0) ? -of_i.y : of_i.y)),
		asfloat(asint(p.z) + ((p.z < 0) ? -of_i.z : of_i.z)));

	return float3(abs(p.x) < origin() ? p.x + float_scale() * n.x : p_i.x,
		abs(p.y) < origin() ? p.y + float_scale() * n.y : p_i.y,
		abs(p.z) < origin() ? p.z + float_scale() * n.z : p_i.z);
}

Ray SpawnRay(float3 p, float3 direction, float3 normal, float tMax)
{
	Ray ray;
	float s = sign(dot(normal, direction));
	normal *= s;
	ray.orig = offset_ray(p, normal);
	ray.tmax = tMax;
	ray.direction = direction;
	ray.tmin = 0;
	return ray;
}

struct AreaLight
{
	float3 Lemit;  //xyz radiance
	float  Area;   //area
};

struct Triangle
{
	float3 p0;
	float3 p1;
	float3 p2;
};

struct Bounds
{
	float3 min;
	float3 max;

	float3 MinOrMax(int n)
	{
		return n == 0 ? min : max;
	}

	float3 corner(int n)
	{
		return float3(MinOrMax(n & 1).x,
			MinOrMax((n & 2) ? 1 : 0).y,
			MinOrMax((n & 4) ? 1 : 0).z);

	}

	float3 center()
	{
		return (min + max) * 0.5;
	}

	float radius()
	{
		return length(max - min) * 0.5;
	}
};

struct Primitive
{
	int vertexOffset;
	int triangleOffset;
	int transformId; //
	int faceIndex;   //
};


struct Vertex
{
	float3 position;
	float2 uv;
	float3 normal;
};


float MinComponent(float3 v) {
	return min(v.x, min(v.y, v.z));
}


float MaxComponent(const float3 v) {
	return max(v.x, max(v.y, v.z));
}

int MaxDimension(float3 v) 
{
	return (v.x > v.y) ? ((v.x > v.z) ? 0 : 2) : ((v.y > v.z) ? 1 : 2);
}

float3 Permute(float3 v, int x, int y, int z)
{
	return float3(v[x], v[y], v[z]);
}

void GetUVs(out float2 uv[3]) 
{
	uv[0] = float2(0, 0);
	uv[1] = float2(1, 0);
	uv[2] = float2(1, 1);
}

void CoordinateSystem(float3 v1, out float3 v2,
	out float3 v3)
{
	//¹¹Ôìv2£¬v2 dot v1 = 0
	if (abs(v1.x) > abs(v1.y))
		v2 = float3(-v1.z, 0, v1.x) / sqrt(v1.x * v1.x + v1.z * v1.z);
	else
		v2 = float3(0, v1.z, -v1.y) / sqrt(v1.y * v1.y + v1.z * v1.z);
	v3 = cross(v1, v2);
}

float3 WorldToLocal(float3 v, float3 n, float3 ts, float3 ss)
{
	return float3(dot(v, ss), dot(v, ts), dot(v, n));
}

float3 LocalToWorld(float3 v, float3 ns, float3 ts, float3 ss)
{
	return float3(ss.x * v.x + ts.x * v.y + ns.x * v.z,
		ss.y * v.x + ts.y * v.y + ns.y * v.z,
		ss.z * v.x + ts.z * v.y + ns.z * v.z);
}

//w is localspace(z-up) vector 
float AbsCosTheta(float3 w)
{
	return abs(w.z);
}

bool SameHemisphere(float3 w, float3 wp)
{
	return w.z * wp.z > 0;
}

Ray TransformRay(float4x4 mat, Ray ray)
{
	Ray output;
	output.orig = mul(mat, float4(ray.orig.xyz, 1)).xyz;
	output.tmax = ray.tmax;
	output.direction = mul(mat, float4(ray.direction.xyz, 0)).xyz;
	output.tmin = ray.tmin;
	return output;
}

//return the a point of triangle
//p0 p1 p2 is the world position of a mesh
float3 SamplePointOnTriangle(float3 p0, float3 p1, float3 p2, float2 u, out float3 normal, out float pdf)
{
	//caculate bery centric uv w = 1 - u - v
	float t = sqrt(u.x);
	float2 uv = float2(1.0 - t, t * u.y);
	float w = 1 - uv.x - uv.y;

	float3 position = p0 * w + p1 * uv.x + p2 * uv.y;
	float3 crossVector = cross(p1 - p0, p2 - p0);
	normal = normalize(crossVector);
	pdf = 2.0 / length(crossVector);

	return position;
}

inline float CosTheta(float3 w) { return w.z; }
inline float Cos2Theta(float3 w) { return w.z * w.z; }
inline float Sin2Theta(float3 w) {
	return max(0, 1.0 - Cos2Theta(w));
}

inline float SinTheta(float3 w) { return sqrt(Sin2Theta(w)); }

inline float TanTheta(float3 w) { return SinTheta(w) / CosTheta(w); }

inline float Tan2Theta(float3 w) {
	return Sin2Theta(w) / Cos2Theta(w);
}

inline float CosPhi(float3 w) {
	float sinTheta = SinTheta(w);
	return (sinTheta == 0) ? 1 : clamp(w.x / sinTheta, -1, 1);
}

inline float SinPhi(float3 w) {
	float sinTheta = SinTheta(w);
	return (sinTheta == 0) ? 0 : clamp(w.y / sinTheta, -1, 1);
}

inline float Cos2Phi(float3 w) 
{ 
	float v = CosPhi(w);
	return v * v;
}

inline float Sin2Phi(float3 w) 
{ 
	float v = SinPhi(w);
	return v * v;
}

inline float CosDPhi(float3 wa, float3 wb) {
	float waxy = wa.x * wa.x + wa.y * wa.y;
	float wbxy = wb.x * wb.x + wb.y * wb.y;
	if (waxy == 0 || wbxy == 0)
		return 1;
	return clamp((wa.x * wb.x + wa.y * wb.y) / sqrt(waxy * wbxy), -1, 1);
}

float3 Faceforward(float3 normal, float3 v)
{
	return (dot(normal, v) < 0.0f) ? -normal : normal;
}

float3 SphericalDirection(float sinTheta, float cosTheta, float phi)
{
	return float3(sinTheta * cos(phi), sinTheta * sin(phi), cosTheta);
}

float SphericalTheta(float3 v) {
	return acos(clamp(v.z, -1, 1));
}

float SphericalPhi(float3 v) {
	float p = atan2(v.y, v.x);
	return (p < 0) ? (p + 2 * PI) : p;
}

bool IsBlack(float3 radiance)
{
	return (radiance.x + radiance.y + radiance.z) < 0.001;
}

#endif