using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public static class MathUtil
{
    public enum TestPlanesResults
    {
        /// <summary>
        /// The AABB is completely in the frustrum.
        /// </summary>
        Inside = 0,
        /// <summary>
        /// The AABB is partially in the frustrum.
        /// </summary>
        Intersect,
        /// <summary>
        /// The AABB is completely outside the frustrum.
        /// </summary>
        Outside
    }

    /// <summary>
    /// This is a faster AABB cull than brute force that also gives additional info on intersections.
    /// Calling Bounds.Min/Max is actually quite expensive so as an optimization you can precalculate these.
    /// http://www.lighthouse3d.com/tutorials/view-frustum-culling/geometric-approach-testing-boxes-ii/
    /// </summary>
    /// <param name="planes"></param>
    /// <param name="boundsMin"></param>
    /// <param name="boundsMax"></param>
    /// <returns></returns>
    public static int TestPlanesAABBFast(Plane[] planes, ref Vector3 boundsMin, ref Vector3 boundsMax, bool testIntersection = false)
    {
        Vector3 vmin, vmax;
        var testResult = (int)TestPlanesResults.Inside;

        for (int planeIndex = 0; planeIndex < planes.Length; planeIndex++)
        {
            var normal = planes[planeIndex].normal;
            var planeDistance = planes[planeIndex].distance;

            // X axis
            if (normal.x < 0)
            {
                vmin.x = boundsMin.x;
                vmax.x = boundsMax.x;
            }
            else
            {
                vmin.x = boundsMax.x;
                vmax.x = boundsMin.x;
            }

            // Y axis
            if (normal.y < 0)
            {
                vmin.y = boundsMin.y;
                vmax.y = boundsMax.y;
            }
            else
            {
                vmin.y = boundsMax.y;
                vmax.y = boundsMin.y;
            }

            // Z axis
            if (normal.z < 0)
            {
                vmin.z = boundsMin.z;
                vmax.z = boundsMax.z;
            }
            else
            {
                vmin.z = boundsMax.z;
                vmax.z = boundsMin.z;
            }

            var dot1 = normal.x * vmin.x + normal.y * vmin.y + normal.z * vmin.z;
            if (dot1 + planeDistance < 0)
                return (int)TestPlanesResults.Outside;

            if (testIntersection)
            {
                var dot2 = normal.x * vmax.x + normal.y * vmax.y + normal.z * vmax.z;
                if (dot2 + planeDistance <= 0)
                    testResult = (int)TestPlanesResults.Intersect;
            }
        }

        return testResult;
    }

    public static int FloorPowerOf2(int n)
    {
        n |= (n >> 1);
        n |= (n >> 2);
        n |= (n >> 4);
        n |= (n >> 8);
        n |= (n >> 16);
        return n - (n >> 1);
    }

    public static int CeilPowerOf2(int n)
    {
        --n;
        n |= (n >> 1);
        n |= (n >> 2);
        n |= (n >> 4);
        n |= (n >> 8);
        n |= (n >> 16);
        return ++n;
    }

    public static int CeilOf(int n, int b)
    {
        return (n / b + ((n % b) > 0 ? 1 : 0)) * b;
    }

    public static int IndexPowerOf2(int n)
    {
        int i = 1;
        while ((n = n >> 1) != 0)
            i++;
        return i;
    }

    public static float Max(this Vector3 v)
    {
        return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
    }

    public static int Max(this Vector3Int v)
    {
        return Mathf.Max(v.x, Mathf.Max(v.y, v.z));
    }

    public static Vector2 Lerp(Vector2 a, Vector2 b, Vector2 t)
    {
        return new Vector2(Mathf.Lerp(a.x, b.x, t.x), Mathf.Lerp(a.y, b.y, t.y));
    }
        
    public static Vector3 Mul(this Vector3 v, Vector3 c)
    {
        return new Vector3(v.x * c.x, v.y * c.y, v.z * c.z);
    }

    public static Vector3 Mul(this Vector3 v, float scalar)
    {
        return v * scalar;
    }

    public static Vector3 Invert(this Vector3 v)
    {
        return new Vector3(1.0f / v.x, 1.0f / v.y, 1.0f / v.z);
    }

    public static Vector3 Div(this Vector3 v, Vector3 c)
    {
        return new Vector3(v.x / c.x, v.y / c.y, v.z / c.z);
    }

    public static Vector3 Div(this Vector3 v, float c)
    {
        return new Vector3(v.x / c, v.y / c, v.z / c);
    }

    public static Vector3 Sqrt(this Vector3 v)
    {
        return new Vector3(Mathf.Sqrt(v.x), Mathf.Sqrt(v.y), Mathf.Sqrt(v.z));
    }

    public static Vector3 Add (this Vector3 v, float c)
    {
        return new Vector3(v.x + c, v.y + c, v.z + c);
    }

    public static Vector3 Sub(this Vector3 v, float c)
    {
        return new Vector3(v.x - c, v.y - c, v.z - c);
    }


    public static Vector3 Invert(this Vector3Int v)
    {
        return new Vector3(1.0f / v.x, 1.0f / v.y, 1.0f / v.z);
    }

    public static Vector3 ToVetor3(this Vector4 v)
    {
        return new Vector3(v.x, v.y, v.z);
    }

    public static Vector4 Div(this Vector4 v, float c)
    {
        return new Vector4(v.x / c, v.y / c, v.z / c, v.w / c);
    }

    public static float MaxComponent(this Vector3 v)
    {
        return Mathf.Max(Mathf.Max(v.x, v.y), v.z);
    }

    public static float MinComponent(this Vector3 v)
    {
        return Mathf.Min(Mathf.Min(v.x, v.y), v.z);
    }

    public static Color ToColorGamma(this Vector3 v)
    {
        return new Color(v.x, v.y, v.z).gamma;
    }

    public static string ToDetailString(this Vector3 v)
    {
        string s = "(" + v.x + "," + v.y + "," + v.z + ")";
        return s;
    }
        
    public static Vector4 ToVector4(this Vector4 v, float w)
    {
        return new Vector4(v.x, v.y, v.z, w);
    }

    public static Vector4 ToVector4(this Vector3 v, float w)
    {
        return new Vector4(v.x, v.y, v.z, w);
    }

    public static Vector3 ToVector3(this Color c)
    {
        return new Vector3(c.r, c.g, c.b);
    }

    public static Vector3 LinearToVector3(this Color c)
    {
        return new Vector3(c.linear.r, c.linear.g, c.linear.b);
    }

    public static Vector4 LinearToVector4(this Color c)
    {
        return new Vector4(c.linear.r, c.linear.g, c.linear.b, c.linear.a);
    }

    private static UnityEngine.Vector3 WeightedAvg(
            UnityEngine.Vector3 a,
            UnityEngine.Vector3 b,
            float aWeight,
            float bWeight)
    {
        return (a * aWeight) + (b * bWeight);
    }

    public static bool DecomposeMatrix(this Matrix4x4 matrix, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
    {
        translation = new UnityEngine.Vector3();
        rotation = new UnityEngine.Quaternion();
        scale = new UnityEngine.Vector3();

        if (matrix[3, 3] == 0.0f)
        {
            return false;
        }

        // Normalize the matrix.
        for (int i = 0; i < 4; ++i)
        {
            for (int j = 0; j < 4; ++j)
            {
                matrix[i, j] /= matrix[3, 3];
            }
        }

        // perspectiveMatrix is used to solve for perspective, but it also provides
        // an easy way to test for singularity of the upper 3x3 component.
        UnityEngine.Matrix4x4 persp = matrix;

        for (int i = 0; i < 3; i++)
        {
            persp[3, i] = 0;
        }
        persp[3, 3] = 1;

        if (persp.determinant == 0.0f)
        {
            return false;
        }

        // Next take care of translation (easy).
        translation = new UnityEngine.Vector3(matrix[0, 3], matrix[1, 3], matrix[2, 3]);
        matrix[3, 0] = 0;
        matrix[3, 1] = 0;
        matrix[3, 2] = 0;

        UnityEngine.Vector3[] rows = new UnityEngine.Vector3[3];
        UnityEngine.Vector3 Pdum3;

        // Now get scale and shear.
        for (int i = 0; i < 3; ++i)
        {
            rows[i].x = matrix[0, i];
            rows[i].y = matrix[1, i];
            rows[i].z = matrix[2, i];
        }

        // Compute X scale factor and normalize first row.
        scale.x = rows[0].magnitude;
        rows[0] = rows[0].normalized;

        // Compute XY shear factor and make 2nd row orthogonal to 1st.
        UnityEngine.Vector3 Skew;
        Skew.z = UnityEngine.Vector3.Dot(rows[0], rows[1]);
        rows[1] = WeightedAvg(rows[1], rows[0], 1, -Skew.z);

        // Now, compute Y scale and normalize 2nd row.
        scale.y = rows[1].magnitude;
        rows[1] = rows[1].normalized;

        // Compute XZ and YZ shears, orthogonalize 3rd row.
        Skew.y = UnityEngine.Vector3.Dot(rows[0], rows[2]);
        rows[2] = WeightedAvg(rows[2], rows[0], 1, -Skew.y);

        Skew.x = UnityEngine.Vector3.Dot(rows[1], rows[2]);
        rows[2] = WeightedAvg(rows[2], rows[1], 1, -Skew.x);

        // Next, get Z scale and normalize 3rd row.
        scale.z = rows[2].magnitude;
        rows[2] = rows[2].normalized;

        // At this point, the matrix (in rows[]) is orthonormal.
        // Check for a coordinate system flip.  If the determinant
        // is -1, then negate the matrix and the scaling factors.
        Pdum3 = UnityEngine.Vector3.Cross(rows[1], rows[2]);
        if (UnityEngine.Vector3.Dot(rows[0], Pdum3) < 0)
        {
            for (int i = 0; i < 3; i++)
            {
                scale[i] *= -1;
                rows[i] *= -1;
            }
        }

        // Now, get the rotations out, as described in the gem.

#if false
            // Euler Angles.
            rotation.y = UnityEngine.Mathf.Asin(-rows[0][2]);
            if (Mathf.Cos(rotation.y) != 0)
            {
                rotation.x = UnityEngine.Mathf.Atan2(rows[1][2], rows[2][2]);
                rotation.z = UnityEngine.Mathf.Atan2(rows[0][1], rows[0][0]);
            }
            else
            {
                rotation.x = UnityEngine.Mathf.Atan2(-rows[2][0], rows[1][1]);
                rotation.z = 0;
            }
#else
        // Quaternions.
        {
            int i, j, k = 0;
            float root, trace = rows[0].x + rows[1].y + rows[2].z;
            if (trace > 0)
            {
                root = UnityEngine.Mathf.Sqrt(trace + 1.0f);
                rotation.w = 0.5f * root;
                root = 0.5f / root;
                rotation.x = root * (rows[1].z - rows[2].y);
                rotation.y = root * (rows[2].x - rows[0].z);
                rotation.z = root * (rows[0].y - rows[1].x);
            } // End if > 0
            else
            {
                int[] Next = new int[] { 1, 2, 0 };
                i = 0;
                if (rows[1].y > rows[0].x) i = 1;
                if (rows[2].z > rows[i][i]) i = 2;
                j = Next[i];
                k = Next[j];

                root = UnityEngine.Mathf.Sqrt(rows[i][i] - rows[j][j] - rows[k][k] + 1.0f);

                rotation[i] = 0.5f * root;
                root = 0.5f / root;
                rotation[j] = root * (rows[i][j] + rows[j][i]);
                rotation[k] = root * (rows[i][k] + rows[k][i]);
                rotation.w = root * (rows[j][k] - rows[k][j]);
            } // End if <= 0
        }
#endif
        return true;
    }

    public static float Y(this Color c)
    {
        Vector3 YWeight = new Vector3(0.212671f, 0.715160f, 0.072169f);
        return YWeight[0] * c[0] + YWeight[1] * c[1] + YWeight[2] * c[2];
    }

    public static float Pow2(float d)
    {
        return d * d;
    }

        public static float Pow3(float d)
        {
            return d * d * d;
        }

        public static bool Equal(this float f, in float c, float e)
        {
            return Mathf.Abs(f - c) < e;
        }

        public static bool Equal(this Vector3 v, in Vector3 c, float e)
        {
            var tmp = v - c;
            return Mathf.Abs(tmp.x) < e && Mathf.Abs(tmp.y) < e && Mathf.Abs(tmp.z) < e;
        }

        public static bool Equal(this Vector4 v, in Vector4 c, float e)
        {
            var tmp = v - c;
            return Mathf.Abs(tmp.x) < e && Mathf.Abs(tmp.y) < e && Mathf.Abs(tmp.z) < e && Mathf.Abs(tmp.w) < e;
        }

        public static Vector3Int CeilToInt(this Vector3 v)
        {
            return new Vector3Int(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y), Mathf.CeilToInt(v.z));
        }
        
        public static Vector3Int FloorToInt(this Vector3 v)
        {
            return new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
        }

        public static int RoundBy(this int v, int d)
        {
            return (v + d - 1) / d;
        }

        public static long RoundBy(this long v, long d)
        {
            return (v + d - 1) / d;
        }

        public static Vector3Int DivideAndRoundUp(this Vector3Int v, int d)
        {
            return new Vector3Int(v.x.RoundBy(d), v.y.RoundBy(d), v.z.RoundBy(d));
        }
        
        public static void HashCombine(ref long seed, int v)
        {
            //http://www.boost.org/doc/libs/1_35_0/boost/functional/hash/hash.hpp            
            seed ^= (v + 0x9e3779b9 + (seed << 6) + (seed >> 2));
        }

        public static long HashCombine(int x, int y)
        {
            //http://www.boost.org/doc/libs/1_35_0/libs/functional/hash/examples/point.cpp
            long seed = 0;
            HashCombine(ref seed, x);
            HashCombine(ref seed, y);
            return seed;
        }

        public static uint PackFloat11(float v)
        {
            uint IValue;
            unsafe { IValue = *(uint*)&v; }
            
            uint Result;
            uint Sign = IValue & 0x80000000;
            uint I = IValue & 0x7FFFFFFF;

            if((I & 0x7F800000) == 0x7F800000)
            {
                // INF or NAN
                Result = 0x7C0;
                if((I & 0x7FFFFF) != 0)
                {
                    Result = 0x7C0 | (((I>>17)|(I>>11)|(I>>6)|(I))&0x3F);
                }
                else if(Sign != 0)
                {
                    // -INF is clamped to 0 since 3PK is positive only
                    Result = 0;
                }
            }
            else if(Sign != 0)
            {
                // 3PK is positive only, so clamp to zero
                Result = 0;
            }
            else if(I > 0x477E0000U)
            {
                // The number is too large to be represented as a float11, set to max
                Result = 0x7BF;
            }
            else
            {
                if(I < 0x38800000U)
                {
                    // The number is too small to be represented as a normalized float11
                    // Convert it to a denormalized value.
                    uint Shift = 113U - (I >> 23);
                    I = (0x800000U | (I & 0x7FFFFFU)) >> (int)Shift;
                }
                else
                {
                    // Rebias the exponent to represent the value as a normalized float11
                    I += 0xC8000000U;
                }
        
                Result = ((I + 0xFFFFU + ((I >> 17) & 1U)) >> 17) & 0x7ffU;
            }
            return Result;
        }

        public static float UnpackFloat11(uint v)
        {
            uint Mantissa = v & 0x3F;
            uint te = (v >> 6) & 0x1F;
            uint Result;
            uint Exponent;

            if(te == 0x1F) // INF or NAN
            {
                Result = 0x7F800000 | (Mantissa << 17);
            }
            else
            {
                if(te != 0) // The value is normalized
                {
                    Exponent = te;
                }
                else if(Mantissa != 0) // The value is denormalized
                {
                    // Normalize the value in the resulting float
                    Exponent = 1;
            
                    do
                    {
                        Exponent--;
                        Mantissa <<= 1;
                    } while ((Mantissa & 0x40) == 0);
            
                    Mantissa &= 0x3F;
                }
                else // The value is zero
                {
                    unchecked { Exponent = (uint)-112; }
                }
            
                Result = ((Exponent + 112) << 23) | (Mantissa << 17);
            }
            float fv = 0.0f;
            unsafe { fv = *(float*)&Result; }
            return fv;
        }

        public static uint PackFloat10(float v)
        {
            uint IValue;
            unsafe { IValue = *(uint*)&v; }

            uint Result;
            uint Sign = IValue & 0x80000000;
            uint I = IValue & 0x7FFFFFFF;

            if((I & 0x7F800000) == 0x7F800000)
            {
                // INF or NAN
                Result = 0x3e0;
                if((I & 0x7FFFFF) != 0)
                {
                    Result = 0x3e0 | (((I>>18)|(I>>13)|(I>>3)|(I))&0x1f);
                }
                else if(Sign != 0)
                {
                    // -INF is clamped to 0 since 3PK is positive only
                    Result = 0;
                }
            }
            else if(Sign != 0)
            {
                // 3PK is positive only, so clamp to zero
                Result = 0;
            }
            else if(I > 0x477C0000U)
            {
                // The number is too large to be represented as a float10, set to max
                Result = 0x3df;
            }
            else
            {
                if(I < 0x38800000U)
                {
                    // The number is too small to be represented as a normalized float10
                    // Convert it to a denormalized value.
                    uint Shift = 113U - (I >> 23);
                    I = (0x800000U | (I & 0x7FFFFFU)) >> (int)Shift;
                }
                else
                {
                    // Rebias the exponent to represent the value as a normalized float10
                    I += 0xC8000000U;
                }
            
                Result = ((I + 0x1FFFFU + ((I >> 18) & 1U)) >> 18) & 0x3ffU;
            }
            return Result;
        }

        public static float UnpackFloat10(uint v)
        {
            uint Mantissa = v & 0x1F;
            uint te = (v >> 5) & 0x1F;
            uint Exponent;
            uint Result;

            if(te == 0x1f ) // INF or NAN
            {
                Result = 0x7f800000 | (Mantissa << 17);
            }
            else
            {
                if(te != 0 ) // The value is normalized
                {
                    Exponent = te;
                }
                else if(Mantissa != 0) // The value is denormalized
                {
                    // Normalize the value in the resulting float
                    Exponent = 1;
            
                    do
                    {
                        Exponent--;
                        Mantissa <<= 1;
                    } while ((Mantissa & 0x20) == 0);
            
                    Mantissa &= 0x1F;
                }
                else // The value is zero
                {
                    unchecked { Exponent = (uint)-112; }
                }

                Result = ((Exponent + 112) << 23) | (Mantissa << 18);
            }
            float fv = 0.0f;
            unsafe{ fv = *(float*)&Result; }
            return fv;
        }

        public static uint EncodeR11G11B10(Color rgb)
        {
            // Pack the data to little-endian
            uint r = MathUtil.PackFloat11(rgb.r);
            uint g = MathUtil.PackFloat11(rgb.g);
            uint b = MathUtil.PackFloat10(rgb.b);

            var packed = (r & 0x7FF) | ((g & 0x7FF) << 11)| ((b & 0x3FF) << 22);
            return packed;
        }

        public static Color DecodeR11G11B10(uint packed)
        {
            uint r = packed & 0x7FF;
            uint g = (packed >> 11) & 0x7FF;
            uint b = (packed >> 22) & 0x3FF;
            var clr = new Color();
            clr.r = MathUtil.UnpackFloat11(r);
            clr.g = MathUtil.UnpackFloat11(g);
            clr.b = MathUtil.UnpackFloat10(b);
            clr.a = 1.0f;
            return clr;
        }

        public static uint EncodeRGBA8UNorm(Color rgb)
        {
            // Pack the data to little-endian
            int r = Mathf.Clamp(Mathf.RoundToInt(rgb.r * 255.0f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(rgb.g * 255.0f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(rgb.b * 255.0f), 0, 255);
            int a = Mathf.Clamp(Mathf.RoundToInt(rgb.a * 255.0f), 0, 255);
            var packed = (uint)((r & 0xFF) | ((g & 0xFF) << 8) | ((b & 0xFF) << 16) | ((a & 0xFF) << 24));
            return packed;
        }

        public static Color DecodeRGBA8UNorm(uint packed)
        {
            uint r = packed & 0xFF;
            uint g = (packed >> 8) & 0xFF;
            uint b = (packed >> 16) & 0xFF;
            uint a = (packed >> 24) & 0xFF;
            float scale = 1.0f / 255.0f;
            return new Color(r * scale, g * scale, b * scale, a * scale);
        }

        const float MaxRGBMRange = 8.0f;
        public static Color EncodeRGBM(Color rgb)
        {
            float kOneOverRGBMMaxRange = 1.0f / MaxRGBMRange;
            const float kMinMultiplier = 2.0f * 1.0e-2f;

            rgb = rgb * kOneOverRGBMMaxRange;
            float alpha = Mathf.Max(Mathf.Max(rgb.r, rgb.g), Mathf.Max(rgb.b, kMinMultiplier));
            alpha = Mathf.Ceil(alpha * 255.0f) / 255.0f;

            alpha = Mathf.Max(alpha, kMinMultiplier);

            rgb /= alpha;
            return new Color(rgb.r, rgb.g, rgb.b, alpha);
        }

        public static Color DecodeRGBM(Color rgbm)
        {
            var multiplier = rgbm.a * MaxRGBMRange;
            return new Color(rgbm.r * multiplier, rgbm.g * multiplier, rgbm.b * multiplier);
        }

    public static unsafe int SingleToInt32Bits(float value)
    {
        return *(int*)(&value);
    }

    public static unsafe uint SingleToUint32Bits(float value)
    {
        return *(uint*)(&value);
    }

    public static unsafe Vector4Int SingleToInt32Bits(Vector4 value)
    {
        Vector4Int result = new Vector4Int();
        result.x = SingleToInt32Bits(value.x);
        result.y = SingleToInt32Bits(value.y);
        result.z = SingleToInt32Bits(value.z);
        result.w = SingleToInt32Bits(value.w);
        return result;
    }
    public static unsafe float Int32BitsToSingle(int value)
    {
        return *(float*)(&value);
    }

    public static unsafe float UInt32BitsToSingle(uint value)
    {
        return *(float*)(&value);
    }

    public static int min_min(int a, int b, int c)
    {
        return Mathf.Min(Mathf.Min(a, b), c);
    }
    public static int min_max(int a, int b, int c)
    {
        return Mathf.Max(Mathf.Min(a, b), c);

    }
    public static int max_min(int a, int b, int c)
    {
        return Mathf.Min(Mathf.Max(a, b), c);
    }

    public static int max_max(int a, int b, int c)
    {
        return Mathf.Max(Mathf.Max(a, b), c);
    }

    public static float fmin_fmin(float a, float b, float c) { return Int32BitsToSingle(min_min(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }
    public static float fmin_fmax(float a, float b, float c) { return Int32BitsToSingle(min_max(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }
    public static float fmax_fmin(float a, float b, float c) { return Int32BitsToSingle(max_min(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }
    public static float fmax_fmax(float a, float b, float c) { return Int32BitsToSingle(max_max(SingleToInt32Bits(a), SingleToInt32Bits(b), SingleToInt32Bits(c))); }

    public static double Erf(double x)
    {
        // constants
        double a1 = 0.254829592;
        double a2 = -0.284496736;
        double a3 = 1.421413741;
        double a4 = -1.453152027;
        double a5 = 1.061405429;
        double p = 0.3275911;

        // Save the sign of x
        int sign = 1;
        if (x < 0)
            sign = -1;
        x = Math.Abs(x);

        // A&S formula 7.1.26
        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }
    public static float Gaussian(float x, float mu = 0, float sigma = 1)
    {
        return 1.0f / Mathf.Sqrt(2.0f * Mathf.PI * sigma * sigma) *
               Mathf.Exp(-Pow2(x - mu) / (2 * sigma * sigma));
    }

    public static float GaussianIntegral(float x0, float x1, float mu = 0,
                                           float sigma = 1)
    {
        float sigmaRoot2 = sigma * 1.414213562373095f;
        
        return 0.5f * ((float)Erf((mu - x0) / sigmaRoot2) - (float)Erf((mu - x1) / sigmaRoot2));
    }

    public static Vector2 GetRandom01()
    {
        return new Vector2(UnityEngine.Random.Range(0.0f, 1.0f), UnityEngine.Random.Range(0.0f, 1.0f));
    }

    private static uint random_seed = 0;

    public static float UniformFloat()
    {
        if (random_seed == 0)
        {
            uint s0 = 0;
            uint v0 = 1;
            uint v1 = 1;
            for (int n = 0; n < 4; ++n)
            {
                s0 += 0x9e3779b9u;
                v0 += ((v1 << 4) + 0xa341316c) ^ (v1 + s0) ^ ((v1 >> 5) + 0xc8013ea4);
                v1 += ((v0 << 4) + 0xad90777d) ^ (v0 + s0) ^ ((v0 >> 5) + 0x7e95761e);
            }
        }
        uint lcg_a = 1664525u;
        uint lcg_c = 1013904223u;
        random_seed = lcg_a * random_seed + lcg_c;
        return (random_seed & 0x00ffffffu) * (1.0f / (0x01000000u));
    }

    public static void CoordinateSystem(Vector3 v1, ref Vector3 v2, ref Vector3 v3)
    {
        //¹¹Ôìv2£¬v2 dot v1 = 0
        if (Mathf.Abs(v1.x) > Mathf.Abs(v1.y))
            v2 = new Vector3(-v1.z, 0, v1.x) / Mathf.Sqrt(v1.x * v1.x + v1.z * v1.z);
        else
            v2 = new Vector3(0, v1.z, -v1.y) / Mathf.Sqrt(v1.y * v1.y + v1.z * v1.z);
        v3 = Vector3.Cross(v1, v2);
    }
}
