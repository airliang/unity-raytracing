using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUDistributionTest
{
    private delegate bool MatchFunc<T>(int index);
    static int FindInterval<T>(int start, int size, MatchFunc<T> match)
    {
        int first = 0, len = size;
        while (len > 0)
        {
            int nHalf = len >> 1;
            int middle = first + nHalf;
            // Bisect range based on value of _pred_ at _middle_
            if (match(start + middle))
            {
                first = middle + 1;
                len -= nHalf + 1;
            }
            else
                len = nHalf;
        }
        return Mathf.Clamp(first - 1, 0, Mathf.Max(size - 2, 0)) + start;
    }
    public static float Sample1DContinuous(float u, GPUDistributionDiscript discript, Vector2 domain, List<Vector2> gpuDistributions, out float pdf, out int offset)
    {
        int cdfSize = discript.num + 1;
        offset = FindInterval<float>(discript.start, cdfSize, index => (gpuDistributions[index].y <= u));
        float du = u - gpuDistributions[offset].y;
        if ((gpuDistributions[offset + 1].y - gpuDistributions[offset].y) > 0)
        {
            du /= (gpuDistributions[offset + 1].y - gpuDistributions[offset].y);
        }

        // Compute PDF for sampled offset
        pdf = gpuDistributions[offset].x / discript.funcInt;


        return Mathf.Lerp(domain.x, domain.y, (float)(offset - discript.start + du) / discript.num);
    }

    public static int Sample1DDiscrete(float u, GPUDistributionDiscript discript, List<Vector2> gpuDistributions, out float pdf)
    {
        int cdfSize = discript.num + 1;
        int offset = FindInterval<float>(discript.start, cdfSize, index => (gpuDistributions[index].y <= u));
        float du = u - gpuDistributions[offset].y;
        if ((gpuDistributions[offset + 1].y - gpuDistributions[offset].y) > 0)
        {
            du /= (gpuDistributions[offset + 1].y - gpuDistributions[offset].y);
        }

        // Compute PDF for sampled offset
        pdf = gpuDistributions[offset].x / discript.funcInt;

        return offset - discript.start; // (int)(offset - discript.start + du) / discript.num;
    }

    public static Vector2 Sample2DContinuous(Vector2 u, GPUDistributionDiscript discript, Distribution2D distribution2D, out float pdf)
    {
        float pdfMarginal;
        int v;
        float d1 = Sample1DContinuous(u.y, discript, new Vector2(discript.domain.x, discript.domain.y), distribution2D.pMarginal.GetGPUDistributions(), out pdfMarginal, out v);
        int nu;
        float pdfCondition;
        GPUDistributionDiscript dCondition;
        dCondition.start = discript.num + 1 + v * (discript.unum + 1);
        dCondition.num = discript.unum;
        dCondition.unum = 0;
        dCondition.funcInt = distribution2D.pConditionalV[v].Intergal();
        dCondition.domain = Vector4.zero;
        int cOffset = 0;
        float d0 = Sample1DContinuous(u.x, dCondition, new Vector2(discript.domain.z, discript.domain.w), distribution2D.GetGPUConditionalDistributions(), out pdfCondition, out cOffset);
        //p(v|u) = p(u,v) / pv(u)
        //so 
        //p(u,v) = p(v|u) * pv(u)
        pdf = pdfCondition * pdfMarginal;
        return new Vector2(d0, d1);
    }
}
