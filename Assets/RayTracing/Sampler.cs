using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public struct RNG
{
    public uint state;
}

public class IndepententSampler
{
    public static uint InitState(uint x, uint y)
    {
        uint s0 = 0;
        uint v0 = x;
        uint v1 = y;
        //https://blogs.cs.umbc.edu/2010/07/01/gpu-random-numbers/
        for (int n = 0; n < 4; ++n)
        {
            s0 += 0x9e3779b9u;
            v0 += ((v1 << 4) + 0xa341316c) ^ (v1 + s0) ^ ((v1 >> 5) + 0xc8013ea4);
            v1 += ((v0 << 4) + 0xad90777d) ^ (v0 + s0) ^ ((v0 >> 5) + 0x7e95761e);
        }

        return v0;
    }
    public void Init(uint x, uint y)
    {
        rng.state = InitState(x, y);
    }
    public float UniformFloat()
    {
        uint lcg_a = 1664525u;
        uint lcg_c = 1013904223u;
        rng.state = lcg_a * rng.state + lcg_c;
        return (rng.state & 0x00ffffffu) * (1.0f / (0x01000000u));
    }

    private RNG rng = new RNG();
}
