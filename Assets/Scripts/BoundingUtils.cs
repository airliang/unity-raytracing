using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BoundingUtils
{
    public static Bounds DefaultBounds()
    {
        Bounds bounds = new Bounds();
        bounds.min = Vector3.positiveInfinity;
        bounds.max = Vector3.negativeInfinity;
        return bounds;
    }
    public static Bounds Union(Bounds a, Vector3 p)
    {
        Bounds bounds = a;
        bounds.min = Vector3.Min(a.min, p);
        bounds.max = Vector3.Max(a.max, p);
        return bounds;
    }

    public static Bounds Intersect(Bounds a, Bounds b)
    {
        Bounds bounds = a;

        bounds.min = Vector3.Max(a.min, b.min);
        bounds.max = Vector3.Min(a.max, b.max);
        return bounds;
    }

    public static Bounds TransformBounds(ref Matrix4x4 matrix, ref Bounds bounds)
    {
        Bounds result = new Bounds();
        Vector3 column0 = matrix.GetColumn(0);
        Vector3 xa = column0 * bounds.min.x;
        Vector3 xb = column0 * bounds.max.x;

        Vector3 column1 = matrix.GetColumn(1);
        Vector3 ya = column1 * bounds.min.y;
        Vector3 yb = column1 * bounds.max.y;

        Vector3 column2 = matrix.GetColumn(2);
        Vector3 za = column2 * bounds.min.z;
        Vector3 zb = column2 * bounds.max.z;

        Vector3 column3 = matrix.GetColumn(3);
        result.min = Vector3.Min(xa, xb) + Vector3.Min(ya, yb) + Vector3.Min(za, zb) + column3;
        result.max = Vector3.Max(xa, xb) + Vector3.Max(ya, yb) + Vector3.Max(za, zb) + column3;
        return result;
    }

    public static void TransformBounds(ref Matrix4x4 matrix, ref Vector3 min, ref Vector3 max)
    {
        Vector3 column0 = matrix.GetColumn(0);
        Vector3 xa = column0 * min.x;
        Vector3 xb = column0 * max.x;

        Vector3 column1 = matrix.GetColumn(1);
        Vector3 ya = column1 * min.y;
        Vector3 yb = column1 * max.y;

        Vector3 column2 = matrix.GetColumn(2);
        Vector3 za = column2 * min.z;
        Vector3 zb = column2 * max.z;

        Vector3 column3 = matrix.GetColumn(3);
        min = Vector3.Min(xa, xb) + Vector3.Min(ya, yb) + Vector3.Min(za, zb) + column3;
        max = Vector3.Max(xa, xb) + Vector3.Max(ya, yb) + Vector3.Max(za, zb) + column3;
    }

    public static void CalculateBoundingSphereFromFrustumPoints(Vector3[] points, out Vector3 outCenter, out float outRadius)
    {
        Vector3[] spherePoints = new Vector3[4];
        spherePoints[0] = points[0];
        spherePoints[1] = points[3];
        spherePoints[2] = points[5];
        spherePoints[3] = points[7];

        // Is bounding sphere at the far or near plane?
        for (int plane = 1; plane >= 0; --plane)
        {
            Vector3 pointA = spherePoints[plane * 2];
            Vector3 pointB = spherePoints[plane * 2 + 1];
            Vector3 center = (pointA + pointB) * 0.5f;
            float radius2 = (pointA - center).sqrMagnitude;
            Vector3 pointC = spherePoints[(1 - plane) * 2];
            Vector3 pointD = spherePoints[(1 - plane) * 2 + 1];

            // Check if all points are inside sphere
            if ((pointC - center).sqrMagnitude <= radius2 &&
                (pointD - center).sqrMagnitude <= radius2)
            {
                outCenter = center;
                outRadius = Mathf.Sqrt(radius2);
                return;
            }
        }

        // Sphere touches all four frustum points
        CalculateSphereFrom4Points(spherePoints, out outCenter, out outRadius);
    }

    public static void CalculateSphereFrom4Points(Vector3[] points, out Vector3 outCenter, out float outRadius)
    {
        Matrix4x4 mat = Matrix4x4.zero;

        for (int i = 0; i < 4; ++i)
        {
            mat[i, 0] = points[i].x;
            mat[i, 1] = points[i].y;
            mat[i, 2] = points[i].z;
            mat[i, 3] = 1;
        }
        float m11 = mat.determinant;

        for (int i = 0; i < 4; ++i)
        {
            mat[i, 0] = points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z;
            mat[i, 1] = points[i].y;
            mat[i, 2] = points[i].z;
            mat[i, 3] = 1;
        }
        float m12 = mat.determinant;

        for (int i = 0; i < 4; ++i)
        {
            mat[i, 0] = points[i].x;
            mat[i, 1] = points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z;
            mat[i, 2] = points[i].z;
            mat[i, 3] = 1;
        }
        float m13 = mat.determinant;

        for (int i = 0; i < 4; ++i)
        {
            mat[i, 0] = points[i].x;
            mat[i, 1] = points[i].y;
            mat[i, 2] = points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z;
            mat[i, 3] = 1;
        }
        float m14 = mat.determinant;

        for (int i = 0; i < 4; ++i)
        {
            mat[i, 0] = points[i].x * points[i].x + points[i].y * points[i].y + points[i].z * points[i].z;
            mat[i, 1] = points[i].x;
            mat[i, 2] = points[i].y;
            mat[i, 3] = points[i].z;
        }
        float m15 = mat.determinant;

        Vector3 c;
        c.x = 0.5f * m12 / m11;
        c.y = 0.5f * m13 / m11;
        c.z = 0.5f * m14 / m11;
        outRadius = Mathf.Sqrt(c.x * c.x + c.y * c.y + c.z * c.z - m15 / m11);
        outCenter = c;
    }
}


