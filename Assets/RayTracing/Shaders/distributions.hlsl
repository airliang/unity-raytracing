#ifndef DISTRIBUTIONS_HLSL
#define DISTRIBUTIONS_HLSL

#include "GPUStructs.hlsl"


//binary search
int FindIntervalSmall(int start, int cdfSize, float u, StructuredBuffer<float2> funcs)
{
    if (cdfSize < 2)
        return start;
    int first = 0, len = cdfSize;
    while (len > 0)
    {
        int nHalf = len >> 1;
        int middle = first + nHalf;
        // Bisect range based on value of _pred_ at _middle_
        float2 distrubution = funcs[start + middle];
        if (distrubution.y <= u)
        {
            first = middle + 1;
            len -= nHalf + 1;
        }
        else
            len = nHalf;
    }
    //if first - 1 < 0, the clamp function is useless
    return clamp(first - 1, 0, cdfSize - 2) + start;
}


int Sample1DDiscrete(float u, DistributionDiscript discript, StructuredBuffer<float2> funcs, out float pmf)
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

float Sample1DContinuous(float u, DistributionDiscript discript, StructuredBuffer<float2> funcs, out float pdf, out int off)
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
    return lerp(discript.domain.x, discript.domain.y, (offset - discript.start + du) / discript.num);
}

float DiscretePdf(int index, DistributionDiscript discript, StructuredBuffer<float2> funcs)
{
    return funcs[discript.start + index].x * (discript.domain.y - discript.domain.x) / (discript.funcInt * discript.num);
}

float2 Sample2DContinuous(float2 u, DistributionDiscript discript, StructuredBuffer<float2> marginal, StructuredBuffer<float2> conditions, StructuredBuffer<float> conditionFuncInts, out float pdf)
{
    float pdfMarginal;
    int v;
    float d1 = Sample1DContinuous(u.y, discript, marginal, pdfMarginal, v);
    int nu;
    float pdfCondition;
    DistributionDiscript dCondition = (DistributionDiscript)0;
    dCondition.start = v * (discript.unum + 1);   //the size of structuredbuffer is func.size + 1, because the cdfs size is func.size + 1 
    dCondition.num = discript.unum;
    dCondition.funcInt = conditionFuncInts[v];
    dCondition.domain.xy = discript.domain.zw;
    float d0 = Sample1DContinuous(u.x, dCondition, conditions, pdfCondition, nu);
    //p(v|u) = p(u,v) / pv(u)
    //so 
    //p(u,v) = p(v|u) * pv(u)
    pdf = pdfCondition * pdfMarginal;
    return float2(d0, d1);
}

float Distribution2DPdf(float2 u, DistributionDiscript discript, StructuredBuffer<float2> marginal, StructuredBuffer<float2> conditions)
{
    int iu = clamp(int(u[0] * discript.unum), 0, discript.unum - 1);
    int iv = clamp(int(u[1] * discript.num), 0, discript.num - 1);
    int conditionVOffset = iv * (discript.unum + 1) + iu;
    return conditions[conditionVOffset].x / discript.funcInt;
}

#endif
