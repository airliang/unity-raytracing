/**********************************************************************
Copyright (c) 2016 Advanced Micro Devices, Inc. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
********************************************************************/
#pragma once

#include <limits>

#include "float3.h"
#include "float2.h"
#include "int2.h"

namespace RadeonRays
{
    struct ray
    {
        ray(float3 const& oo = float3(0,0,0), 
            float3 const& dd = float3(0,0,0))
            : o(oo)
            , d(dd)
        {
            SetMask(0xFFFFFFFF);
            SetActive(true);
        }

        float3 operator ()(float t) const
        {
            return o + t * d;
        }

        void SetMask(int mask)
        {
            extra.x = mask;
        }

        int GetMask() const
        {
            return extra.x;
        }

        void SetActive(bool active)
        {
            extra.y = active ? 1 : 0;
        }

        bool IsActive() const
        {
            return extra.y > 0;
        }

        float3 o;
        float3 d;
        int2 extra;
        int2 padding;
    };
}