using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public enum FilterType
{
    Box,
    Gaussian,
}

public class Filter
{
    public float[,] filterValues;
    protected Distribution2D samples;

    public Distribution2D SampleDistributions()
    {
        return samples;
    }

    virtual public float Evaluate(Vector2 p)
    {
        return 0;
    }

    virtual public Vector2 GetSample(Vector2 u, out float pdf, out Vector2Int position)
    {
        pdf = 0;
        position = Vector2Int.zero;
        return Vector2.zero;
    }

    virtual public List<Vector2> GetGPUDistributions()
    {
        return null;
    }

    virtual public Vector2Int GetDistributionSize()
    {
        return Vector2Int.zero;
    }

    virtual public List<Vector2> GetGPUMarginalDistributions()
    {
        return null;
    }

    virtual public List<Vector2> GetGPUConditionalDistributions()
    {
        return null;
    }

    virtual public GPUFilterSample Sample(Vector2 u)
    {
        return new GPUFilterSample();
    }

    virtual public Bounds2D GetDomain()
    {
        return new Bounds2D();
    }
}

public class GaussianFilter : Filter
{
    public Vector2 radius;
    public float sigma;
    public float expX;
    public float expY;

    
    public GaussianFilter(Vector2 radius, float sigma = 0.5f)
    {
        this.radius = radius;
        this.sigma = sigma;

        expX = MathUtil.Gaussian(radius.x, 0, sigma);
        expY = MathUtil.Gaussian(radius.y, 0, sigma);

        int xSize = (int)(32.0f * radius.x);
        int ySize = (int)(32.0f * radius.y);

        filterValues = new float[xSize, ySize];
        float[] funcs = new float[xSize * ySize];
        //pdfUVs = new float[64, 64];
        Vector2 domainMin = -radius;
        Vector2 domainMax = radius;
        for (int v = 0; v < ySize; ++v)
        {
            for (int u = 0; u < xSize; ++u)
            {
                Vector2 t = new Vector2((float)(u + 0.5f) / xSize, (float)(v + 0.5f) / ySize);
                Vector2 point = MathUtil.Lerp(domainMin, domainMax, t);
                filterValues[u, v] = Evaluate(point);
                int index = u + v * ySize;
                funcs[index] = filterValues[u, v];
            }
        }

        Bounds2D bounds = new Bounds2D
        {
            min = domainMin,
            max = domainMax
        };

        samples = new Distribution2D(funcs, xSize, ySize, bounds);
    }

    public override float Evaluate(Vector2 p)  
    {
        return (Mathf.Max(0.0f, MathUtil.Gaussian(p.x, 0, sigma) - expX) *
                Mathf.Max(0.0f, MathUtil.Gaussian(p.y, 0, sigma) - expY));
    }

    public override Vector2 GetSample(Vector2 u, out float pdf, out Vector2Int position)
    {
        return samples.SampleContinuous(u, out pdf, out position);
    }

    public float Integral()
    {
        return ((MathUtil.GaussianIntegral(-radius.x, radius.x, 0, sigma) - 2 * radius.x * expX) *
                (MathUtil.GaussianIntegral(-radius.y, radius.y, 0, sigma) - 2 * radius.y * expY));
    }

    public override List<Vector2> GetGPUDistributions()
    {
        return samples.GetGPUDistributions();
    }

    public override Vector2Int GetDistributionSize()
    {
        return new Vector2Int((int)(32.0f * radius.x), (int)(32.0f * radius.y));
    }

    public override List<Vector2> GetGPUMarginalDistributions()
    {
        return samples.GetGPUMarginalDistributions();
    }

    public override List<Vector2> GetGPUConditionalDistributions()
    {
        return samples.GetGPUConditionalDistributions();
    }

    public override GPUFilterSample Sample(Vector2 u)
    {
        float pdf = 0;
        Vector2Int position;
        Vector2 sample = GetSample(u, out pdf, out position);
        return new GPUFilterSample()
        {
            p = sample,
            weight = filterValues[position.x, position.y] / pdf
        };
    }

    public override Bounds2D GetDomain()
    {
        return new Bounds2D()
        {
            min = -radius,
            max = radius
        };
    }
}

public class BoxFilter : Filter
{
    public Vector2 radius;

    public BoxFilter(Vector2 radius)
    {
        this.radius = radius;
    }

    public override float Evaluate(Vector2 p)
    {
        return (Mathf.Abs(p.x) <= radius.x && Mathf.Abs(p.y) <= radius.y) ? 1 : 0;
    }

    public override Vector2 GetSample(Vector2 u, out float pdf, out Vector2Int position)
    {
        pdf = 1.0f / Integral();
        position = Vector2Int.one;  //all the position is the same
        return new Vector2(Mathf.Lerp(-radius.x, radius.x, u.x), Mathf.Lerp(-radius.y, radius.y, u.y));
    }

    public float Integral()
    {
        return 2 * radius.x * 2 * radius.y;
    }

    public override List<Vector2> GetGPUDistributions()
    {
        return null;
    }

    public override Vector2Int GetDistributionSize()
    {
        return Vector2Int.zero;
    }

    public override List<Vector2> GetGPUMarginalDistributions()
    {
        return null;
    }

    public override List<Vector2> GetGPUConditionalDistributions()
    {
        return null;
    }

    public override GPUFilterSample Sample(Vector2 u)
    {
        float pdf = 0;
        Vector2Int position;
        Vector2 sample = GetSample(u, out pdf, out position);
        return new GPUFilterSample()
        {
            p = sample,
            weight = filterValues[position.x, position.y] / pdf
        };
    }

    public override Bounds2D GetDomain()
    {
        return new Bounds2D()
        {
            min = -radius,
            max = radius
        };
    }
}

