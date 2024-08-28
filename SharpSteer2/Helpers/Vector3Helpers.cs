// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

namespace SharpSteer2.Helpers;

public static class Vector3Helpers
{
    /// <summary>
    /// return component of vector parallel to a unit basis vector
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="unitBasis">A unit length basis vector</param>
    /// <returns></returns>
    public static Vector3 ParallelComponent(Vector3 vector, Vector3 unitBasis)
    {
        float projection = Vector3.Dot(vector, unitBasis);
        return unitBasis * projection;
    }

    /// <summary>
    /// return component of vector perpendicular to a unit basis vector
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="unitBasis">A unit length basis vector</param>
    /// <returns></returns>
    public static Vector3 PerpendicularComponent(Vector3 vector, Vector3 unitBasis) => (vector - ParallelComponent(vector, unitBasis));

    /// <summary>
    /// clamps the length of a given vector to maxLength.  If the vector is
    /// shorter its value is returned unaltered, if the vector is longer
    /// the value returned has length of maxLength and is parallel to the
    /// original input.
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="maxLength"></param>
    /// <returns></returns>
    public static Vector3 TruncateLength(this Vector3 vector, float maxLength)
    {
        float maxLengthSquared = maxLength * maxLength;
        float vecLengthSquared = vector.LengthSquared();
        if (vecLengthSquared <= maxLengthSquared)
            return vector;

        return (vector * (maxLength / (float)Math.Sqrt(vecLengthSquared)));
    }

    /// <summary>
    /// rotate this vector about the global Y (up) axis by the given angle
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="radians"></param>
    /// <returns></returns>
    public static Vector3 RotateAboutGlobalY(this Vector3 vector, float radians)
    {
        float s = 0;
        float c = 0;
        return RotateAboutGlobalY(vector, radians, ref s, ref c);
    }

    /// <summary>
    /// Rotate this vector about the global Y (up) axis by the given angle
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="radians"></param>
    /// <param name="sin">Either Sin(radians) or default(float), if default(float) this value will be initialized with Sin(radians)</param>
    /// <param name="cos">Either Cos(radians) or default(float), if default(float) this value will be initialized with Cos(radians)</param>
    /// <returns></returns>
    public static Vector3 RotateAboutGlobalY(this Vector3 vector, float radians, ref float sin, ref float cos)
    {
        // if both are default, they have not been initialized yet
// ReSharper disable CompareOfFloatsByEqualityOperator
        if (sin == default(float) && cos == default(float))
// ReSharper restore CompareOfFloatsByEqualityOperator
        {
            sin = (float)Math.Sin(radians);
            cos = (float)Math.Cos(radians);
        }
        return new((vector.X * cos) + (vector.Z * sin), vector.Y, (vector.Z * cos) - (vector.X * sin));
    }

    /// <summary>
    /// Wrap a position around so it is always within 1 radius of the sphere (keeps repeating wrapping until position is within sphere)
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    /// <returns></returns>
    public static Vector3 SphericalWrapAround(this Vector3 vector, Vector3 center, float radius)
    {
        float r;
        do
        {
            Vector3 offset = vector - center;
            r = offset.Length();

            if (r > radius)
                vector = vector + ((offset / r) * radius * -2);

        } while (r > radius);

        return vector;
    }

    /// <summary>
    /// Returns a position randomly distributed on a disk of unit radius
    /// on the XZ (Y=0) plane, centered at the origin.  Orientation will be
    /// random and length will range between 0 and 1
    /// </summary>
    /// <returns></returns>
    public static Vector3 RandomVectorOnUnitRadiusXZDisk()
    {
        Vector3 v;
        do
        {
            v.X = (RandomHelpers.Random() * 2) - 1;
            v.Y = 0;
            v.Z = (RandomHelpers.Random() * 2) - 1;
        }
        while (v.Length() >= 1);

        return v;
    }

    /// <summary>
    /// Returns a position randomly distributed inside a sphere of unit radius
    /// centered at the origin.  Orientation will be random and length will range
    /// between 0 and 1
    /// </summary>
    /// <returns></returns>
    public static Vector3 RandomVectorInUnitRadiusSphere()
    {
        Vector3 v = new Vector3();
        do
        {
            v.X = (RandomHelpers.Random() * 2) - 1;
            v.Y = (RandomHelpers.Random() * 2) - 1;
            v.Z = (RandomHelpers.Random() * 2) - 1;
        }
        while (v.Length() >= 1);

        return v;
    }

    /// <summary>
    /// Returns a position randomly distributed on the surface of a sphere
    /// of unit radius centered at the origin.  Orientation will be random
    /// and length will be 1
    /// </summary>
    /// <returns></returns>
    public static Vector3 RandomUnitVector() => Vector3.Normalize(RandomVectorInUnitRadiusSphere());

    /// <summary>
    /// Returns a position randomly distributed on a circle of unit radius
    /// on the XZ (Y=0) plane, centered at the origin.  Orientation will be
    /// random and length will be 1
    /// </summary>
    /// <returns></returns>
    public static Vector3 RandomUnitVectorOnXZPlane()
    {
        Vector3 temp = RandomVectorInUnitRadiusSphere();
        temp.Y = 0;
        temp = Vector3.Normalize(temp);

        return temp;
    }

    /// <summary>
    /// Clip a vector to be within the given cone
    /// </summary>
    /// <param name="source">A vector to clip</param>
    /// <param name="cosineOfConeAngle">The cosine of the cone angle</param>
    /// <param name="basis">The vector along the middle of the cone</param>
    /// <returns></returns>
    public static Vector3 LimitMaxDeviationAngle(this Vector3 source, float cosineOfConeAngle, Vector3 basis) =>
        LimitDeviationAngleUtility(true, // force source INSIDE cone
            source, cosineOfConeAngle, basis);

    /// <summary>
    /// Clip a vector to be outside the given cone
    /// </summary>
    /// <param name="source">A vector to clip</param>
    /// <param name="cosineOfConeAngle">The cosine of the cone angle</param>
    /// <param name="basis">The vector along the middle of the cone</param>
    /// <returns></returns>
    public static Vector3 LimitMinDeviationAngle(this Vector3 source, float cosineOfConeAngle, Vector3 basis) =>
        LimitDeviationAngleUtility(false, // force source OUTSIDE cone
            source, cosineOfConeAngle, basis);

    /// <summary>
    /// used by limitMaxDeviationAngle / limitMinDeviationAngle
    /// </summary>
    /// <param name="insideOrOutside"></param>
    /// <param name="source"></param>
    /// <param name="cosineOfConeAngle"></param>
    /// <param name="basis"></param>
    /// <returns></returns>
    static Vector3 LimitDeviationAngleUtility(bool insideOrOutside, Vector3 source, float cosineOfConeAngle, Vector3 basis)
    {
        // immediately return zero length input vectors
        float sourceLength = source.Length();
        if (sourceLength < float.Epsilon)
            return source;

        // measure the angular diviation of "source" from "basis"
        Vector3 direction = source / sourceLength;

        float cosineOfSourceAngle = Vector3.Dot(direction, basis);

        // Simply return "source" if it already meets the angle criteria.
        // (note: we hope this top "if" gets compiled out since the flag
        // is a constant when the function is inlined into its caller)
        if (insideOrOutside)
        {
            // source vector is already inside the cone, just return it
            if (cosineOfSourceAngle >= cosineOfConeAngle)
                return source;
        }
        else if (cosineOfSourceAngle <= cosineOfConeAngle)
            return source;

        // find the portion of "source" that is perpendicular to "basis"
        Vector3 perp = PerpendicularComponent(source, basis);
        if (perp == Vector3.Zero)
            return Vector3.Zero;

        // normalize that perpendicular
        Vector3 unitPerp = Vector3.Normalize(perp);

        // construct a new vector whose length equals the source vector,
        // and lies on the intersection of a plane (formed the source and
        // basis vectors) and a cone (whose axis is "basis" and whose
        // angle corresponds to cosineOfConeAngle)
        float perpDist = (float)Math.Sqrt(1 - (cosineOfConeAngle * cosineOfConeAngle));
        Vector3 c0 = basis * cosineOfConeAngle;
        Vector3 c1 = unitPerp * perpDist;
        return (c0 + c1) * sourceLength;
    }

    /// <summary>
    /// Returns the distance between a point and a line.
    /// </summary>
    /// <param name="point">The point to measure distance to</param>
    /// <param name="lineOrigin">A point on the line</param>
    /// <param name="lineUnitTangent">A UNIT vector parallel to the line</param>
    /// <returns></returns>
    public static float DistanceFromLine(this Vector3 point, Vector3 lineOrigin, Vector3 lineUnitTangent)
    {
        Vector3 offset = point - lineOrigin;
        Vector3 perp = PerpendicularComponent(offset, lineUnitTangent);
        return perp.Length();
    }

    /// <summary>
    /// Find any arbitrary vector which is perpendicular to the given vector
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static Vector3 FindPerpendicularIn3d(this Vector3 direction)
    {
        // to be filled in:
        Vector3 quasiPerp;  // a direction which is "almost perpendicular"
        Vector3 result;     // the computed perpendicular to be returned

        // three mutually perpendicular basis vectors
        Vector3 i = Vector3.UnitX;
        Vector3 j = Vector3.UnitY;
        Vector3 k = Vector3.UnitZ;

        // measure the projection of "direction" onto each of the axes
        float id = Vector3.Dot(i, direction);
        float jd = Vector3.Dot(j, direction);
        float kd = Vector3.Dot(k, direction);

        // set quasiPerp to the basis which is least parallel to "direction"
        if ((id <= jd) && (id <= kd))
            quasiPerp = i;           // projection onto i was the smallest
        else if ((jd <= id) && (jd <= kd))
            quasiPerp = j;           // projection onto j was the smallest
        else
            quasiPerp = k;           // projection onto k was the smallest

        // return the cross product (direction x quasiPerp)
        // which is guaranteed to be perpendicular to both of them
        result = Vector3.Cross(direction, quasiPerp);

        return result;
    }
}