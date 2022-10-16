#ifndef COLORCONVERT_HLSL
#define COLORCONVERT_HLSL

float3 XYZToRGB(float3 xyz) {
    return float3(3.240479f * xyz.x - 1.537150f * xyz.y - 0.498535f * xyz.z,
        -0.969256f * xyz.x + 1.875991f * xyz.y + 0.041556f * xyz.z,
        0.055648f * xyz.x - 0.204043f * xyz.y + 1.057311f * xyz.z);
}

/*
inline half3 GammaToLinearSpace(half3 sRGB)
{
    // Approximate version from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return sRGB * (sRGB * (sRGB * 0.305306011h + 0.682171111h) + 0.012522878h);

    // Precise version, useful for debugging.
    //return half3(GammaToLinearSpaceExact(sRGB.r), GammaToLinearSpaceExact(sRGB.g), GammaToLinearSpaceExact(sRGB.b));
}


inline half3 LinearToGammaSpace(half3 linRGB)
{
    linRGB = max(linRGB, half3(0.h, 0.h, 0.h));
    // An almost-perfect approximation from http://chilliant.blogspot.com.au/2012/08/srgb-approximations-for-hlsl.html?m=1
    return max(1.055h * pow(linRGB, 0.416666667h) - 0.055h, 0.h);

    // Exact version, useful for debugging.
    //return half3(LinearToGammaSpaceExact(linRGB.r), LinearToGammaSpaceExact(linRGB.g), LinearToGammaSpaceExact(linRGB.b));
}
*/

half3 NeutralCurve(half3 x, half a, half b, half c, half d, half e, half f)
{
    return ((x * (a * x + c * b) + d * e) / (x * (a * x + b) + d * f)) - e / f;
}

half3 NeutralTonemap(half3 x, float exposure)
{
    // Tonemap
    const half a = 0.2;
    const half b = 0.29;
    const half c = 0.24;
    const half d = 0.272;
    const half e = 0.02;
    const half f = 0.3;
    const half whiteLevel = 5.3;
    const half whiteClip = 1.0;

    half3 whiteScale = (exposure).xxx / NeutralCurve(whiteLevel, a, b, c, d, e, f);
    x = NeutralCurve(x * whiteScale, a, b, c, d, e, f);
    x *= whiteScale;

    // Post-curve white point adjustment
    x /= whiteClip.xxx;

    return x;
}

half3 ACESToneMapping(float3 color, float exposure)
{
    const half A = 2.51f;
    const half B = 0.03f;
    const half C = 2.43f;
    const half D = 0.59f;
    const half E = 0.14f;

    color *= exposure;
    return (color * (A * color + B)) / (color * (C * color + D) + E);
}

half3 Filmic(float3 c) 
{
    float3 x = max(float(0.0f), c - 0.004f);
    return (x * (6.2f * x + 0.5f)) / (x * (6.2f * x + 1.7f) + 0.06f);
}

#endif



