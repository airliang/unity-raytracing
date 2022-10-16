using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Distribution1D
{
    public delegate bool MatchFunc<T>(int index);
    public List<float> distributions = new List<float>();
    //public List<float> pdf = new List<float>();

    public List<float> pdfs = new List<float>();
    //if Distribution1D use as the conditional pdf, cdf is the conditional pdf because:
    //p(v|u) = p(u, v) / pv(u)
    public List<float> cdf = new List<float>();
    public float distributionInt = 0;
    public Vector2 domain;
    int FindInterval<T>(int size, MatchFunc<T> match)
    {
        int first = 0, len = size;
        while (len > 0)
        {
            int nHalf = len >> 1;
            int middle = first + nHalf;
            // Bisect range based on value of _pred_ at _middle_
            //float distrubution = values[middle];
            if (match(middle))
            {
                first = middle + 1;
                len -= nHalf + 1;
            }
            else
                len = nHalf;
        }
        return Mathf.Clamp(first - 1, 0, size - 2);
    }

    public Distribution1D(float[] distribution, int start, int count, float min = 0, float max = 1)
    {
        for (int i = start; i < start + count; ++i)
        {
            distributions.Add(distribution[i]);
            pdfs.Add(0);
        }
        cdf.Add(0);

        for (int i = 1; i < count + 1; ++i)
        {
            cdf.Add(cdf[i - 1] + distributions[i - 1] * (max - min) / count);
        }
        distributionInt = cdf[count];

        if (distributionInt == 0)
        {
            for (int i = 1; i < count + 1; ++i)
            {
                cdf[i] = (float)i / count;
                pdfs[i - 1] = 0;
            }

        }
        else
        {
            for (int i = 1; i < count + 1; ++i)
            {
                cdf[i] = cdf[i] / distributionInt;
                pdfs[i - 1] = distributions[i - 1] / distributionInt;
            }
        }

        domain.x = min;
        domain.y = max;
    }

    public int Count()
    {
        return distributions.Count;
    }
    public float SampleContinuous(float u, out float pdf, out int off)
    {
        // Find surrounding CDF segments and _offset_
        int offset = FindInterval<float>((int)cdf.Count, index => (cdf[index] <= u));
        off = offset;
        // Compute offset along CDF segment
        float du = u - cdf[offset];
        if ((cdf[offset + 1] - cdf[offset]) > 0)
        {
            du /= (cdf[offset + 1] - cdf[offset]);
        }

        // Compute PDF for sampled offset
        pdf = (distributionInt > 0) ? distributions[offset] / distributionInt : 0;


        return Mathf.Lerp(domain.x, domain.y, (offset + du) / Count());
    }
    public int SampleDiscrete(float u, out float pdf, out float uRemapped)
    {
        if (distributions.Count == 0)
        {
            pdf = 0;
            uRemapped = 0;
            return 0;
        }
        // Find surrounding CDF segments and _offset_
        int offset = FindInterval<float>((int)cdf.Count, index => (cdf[index] <= u));
        pdf = (distributionInt > 0) ? distributions[offset] * (domain.y - domain.x) / (distributionInt * Count()) : 0;

        uRemapped = (u - cdf[offset]) / (cdf[offset + 1] - cdf[offset]);

        return offset;
    }
    public float DiscretePDF(int index)
    {
        return distributions[index] / (distributionInt * Count());
    }

    public float Intergal()
    {
        return distributionInt;
    }
    //public float ReverseSample(float u, out int offset)
    //{
    //    offset = FindInterval<float>((int)cdf.Count, index => (cdf[index] <= u));

    //    // Compute offset along CDF segment
    //    float du = u - cdf[offset];
    //    if ((cdf[offset + 1] - cdf[offset]) > 0)
    //    {
    //        du /= (cdf[offset + 1] - cdf[offset]);
    //    }

    //    return Mathf.Lerp(cdf[offset], cdf[offset + 1], du);
    //}

    public List<Vector2> GetGPUDistributions()
    {
        List<Vector2> gpuDistributions = new List<Vector2>();
        for (int u = 0; u < Count(); ++u)
        {
            gpuDistributions.Add(new Vector2(distributions[u], cdf[u]));
        }
        gpuDistributions.Add(new Vector2(0, cdf[Count()]));

        return gpuDistributions;
    }
}

public struct Bounds2D
{
    public Vector2 min;
    public Vector2 max;
}

public class Distribution2D
{
    //float[] distributions is the 2D distributions value
    public Distribution2D(float[] distributions, int nu, int nv, Bounds2D domain)
    {
        for (int v = 0; v < nv; ++v)
        {
            //conditionalV store the all distributions func(u,v) of the 2d distributions
            pConditionalV.Add(new Distribution1D(distributions, v * nu, nu, domain.min[0], domain.max[0]));
        }

        //caculate the marginal pdf
        List<float> marginals = new List<float>();
        for (int v = 0; v < nv; ++v)
        {
            //pv(u) = ¡Æ[v=1,nv]p(u,v)
            //distributionInt = ¡Æ[v=1,nv]p(u,v)
            marginals.Add(pConditionalV[v].distributionInt);
        }
        pMarginal = new Distribution1D(marginals.ToArray(), 0, nv, domain.min[1], domain.max[1]);
        size.x = nu;
        size.y = nv;
    }
    public List<Distribution1D> pConditionalV = new List<Distribution1D>();
    public Distribution1D pMarginal;
    public Vector2Int size = Vector2Int.zero;

    public Vector2 SampleContinuous(Vector2 u, out float pdf, out Vector2Int position)
    {
        float[] pdfs = new float[2];
        int v;
        float d1 = pMarginal.SampleContinuous(u[1], out pdfs[1], out v);
        int nu;
        float d0 = pConditionalV[v].SampleContinuous(u[0], out pdfs[0], out nu);
        //p(v|u) = p(u,v) / pv(u)
        //so 
        //p(u,v) = p(v|u) * pv(u)
        pdf = pdfs[0] * pdfs[1];
        position = new Vector2Int(nu, v);
        return new Vector2(d0, d1);
    }
    public float Pdf(Vector2 p)
    {
        int iu = (int)Mathf.Clamp(p[0] * pConditionalV[0].Count(), 0,
                       pConditionalV[0].Count() - 1);

        int iv = (int)Mathf.Clamp(p[1] * pMarginal.Count(), 0, pMarginal.Count() - 1);
        return pConditionalV[iv].distributions[iu] / pMarginal.distributionInt;
    }

    //public Vector2 ReverseSample(Vector2 u)
    //{
    //    int nu = 0;
    //    float r1 = pMarginal.ReverseSample(u.x, out nu);
    //    int nv = 0;
    //    return new Vector2(r1, pConditionalV[nu].ReverseSample(u.y, out nv));
    //}
    public List<Vector2> GetGPUMarginalDistributions()
    {
        return pMarginal.GetGPUDistributions();
    }

    public List<Vector2> GetGPUConditionalDistributions()
    {
        List<Vector2> gpuDistributions = new List<Vector2>();

        for (int v = 0; v < pConditionalV.Count; ++v)
        {
            gpuDistributions.AddRange(pConditionalV[v].GetGPUDistributions());
            //for (int vu = 0; vu < pConditionalV[v].Count(); ++vu)
            //{
            //    //gpuDistributions.Add(new Vector2(pConditionalV[v].pdfs[vu], pConditionalV[v].cdf[vu]));
            //    gpuDistributions.AddRange(pConditionalV[v].GetGPUDistributions());
            //}
        }

        return gpuDistributions;
    }
    public List<Vector2> GetGPUDistributions()
    {
        List<Vector2> gpuDistributions = pMarginal.GetGPUDistributions();

        for (int v = 0; v < pConditionalV.Count; ++v)
        {
            for (int vu = 0; vu < pConditionalV[v].Count(); ++vu)
            {
                //gpuDistributions.Add(new Vector2(pConditionalV[v].pdfs[vu], pConditionalV[v].cdf[vu]));
                gpuDistributions.AddRange(pConditionalV[v].GetGPUDistributions());
            }
        }

        return gpuDistributions;
    }

    public List<float> GetGPUConditionFuncInts()
    {
        List<float> funcInts = new List<float>();
        for (int i = 0; i < pConditionalV.Count; i++)
        {
            funcInts.Add(pConditionalV[i].Intergal());
        }

        return funcInts;
    }

    public float Intergal()
    {
        return pMarginal.Intergal();
    }
}
