#ifndef MATHDEF_HLSL
#define MATHDEF_HLSL

#define PI          3.14159265358979323846
#define TWO_PI      6.28318530717958647693
#define FOUR_PI     12.5663706143591729538
#define INV_PI      0.31830988618379067154
#define INV_TWO_PI  0.15915494309189533577
#define INV_FOUR_PI 0.07957747154594766788
#define HALF_PI     1.57079632679489661923
#define INV_HALF_PI 0.63661977236758134308
#define PI_OVER_2   1.57079632679489661923
#define PI_OVER_4   0.78539816339744830961
#define LOG2_E      1.44269504088896340736
#define ShadowEpsilon 0.0001

#define MILLIMETERS_PER_METER 1000
#define METERS_PER_MILLIMETER rcp(MILLIMETERS_PER_METER)
#define CENTIMETERS_PER_METER 100
#define METERS_PER_CENTIMETER rcp(CENTIMETERS_PER_METER)

#define FLT_EPSILON     1.192092896e-07  //smallest positive number
#define FLT_INF  asfloat(0x7F800000)
#define FLT_EPS  5.960464478e-8  // 2^-24, machine epsilon: 1 + EPS = 1 (half of the ULP for 1.0f)
#define FLT_MIN  1.175494351e-38 // Minimum normalized positive floating-point number
#define FLT_MAX  3.402823466e+38 // Maximum representable floating-point number
#define HALF_MIN 6.103515625e-5  // 2^-14, the same value for 10, 11 and 16-bit: https://www.khronos.org/opengl/wiki/Small_Float_Formats
#define HALF_MAX 65504.0
#define UINT_MAX 0xFFFFFFFFu
#define ONE_MINUS_EPSILON 0.99999994


float gamma(int n) {
	return (n * FLT_EPSILON) / (1 - n * FLT_EPSILON);
}

float GammaCorrect(float value) {
	if (value <= 0.0031308f) return 12.92f * value;
	return 1.055f * pow(value, (Float)(1.f / 2.4f)) - 0.055f;
}

float InverseGammaCorrect(float value) {
	if (value <= 0.04045f) return value * 1.f / 12.92f;
	return pow((value + 0.055f) * 1.f / 1.055f, 2.4f);
}

float Pow2(float x)
{
	return x * x;
}

float3 Pow5(float x)
{
	return (x * x) * (x * x) * x;
}

float Luminance(in const float3 c) {
	return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
}

bool IsInf(float3 val)
{
	return isinf(val.x) || isinf(val.y) || isinf(val.z);
}

bool IsNan(float3 val)
{
	return isnan(val.x) || isnan(val.y) || isnan(val.z);
}

float MaxValue(float3 val)
{
	return max(max(val.x, val.y), val.z);
}

#endif