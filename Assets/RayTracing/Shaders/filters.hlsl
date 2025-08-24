#ifndef FILTER_HLSL
#define FILTER_HLSL
#include "distributions.hlsl"

StructuredBuffer<float2> _FilterMarginals;
StructuredBuffer<float2> _FilterConditions;
StructuredBuffer<float>  _FilterConditionsFuncInts;
//cbuffer FilterImportanceBuffer
//{
    int _MarginalNum;
    int _ConditionNum;
    float4 _FilterDomain;
    float _FilterFuncInt;
//};

float2 ImportanceFilterSample(float2 u)
{
	DistributionDiscript discript = (DistributionDiscript)0;
	discript.start = 0;
	discript.num = _MarginalNum;
	discript.unum = _ConditionNum;
	discript.domain = _FilterDomain;
	discript.funcInt = _FilterFuncInt;
	float pdf = 0;
	return Sample2DContinuous(u, discript, _FilterMarginals, _FilterConditions, _FilterConditionsFuncInts, pdf);
}

float2 BoxFilterSample(float2 u)
{
	return float2(lerp(-_FilterDomain.x, _FilterDomain.x, u.x), lerp(-_FilterDomain.y, _FilterDomain.y, u.y));
}


#endif
