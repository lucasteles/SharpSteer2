using System.Numerics;
using SharpSteer2.Helpers;

namespace SharpSteer2.Tests;

[TestClass]
public class Vector3HelpersTest
{
    // ReSharper disable once InconsistentNaming
    public const float PiOver2 = (float)Math.PI / 2;

    static void AssertVectorEquality(Vector3 expected, Vector3 actual, float epsilon = float.Epsilon)
    {
        Assert.AreEqual(expected.X, actual.X, epsilon, Err());
        Assert.AreEqual(expected.Y, actual.Y, epsilon, Err());
        Assert.AreEqual(expected.Z, actual.Z, epsilon, Err());
        return;

        string Err() => $"expected {expected} but got {actual}";
    }

    [TestMethod]
    public void ParallelComponentTest()
    {
        var basis = Vector3.UnitY;
        var v = Vector3.Normalize(new(1, 1, 0));

        var result = Vector3Helpers.ParallelComponent(v, basis);

        AssertVectorEquality(new(0, v.Y, 0), result);
    }

    [TestMethod]
    public void PerpendicularComponentTest()
    {
        var basis = Vector3.UnitY;
        var v = Vector3.Normalize(new(1, 1, 0));

        var result = Vector3Helpers.PerpendicularComponent(v, basis);

        AssertVectorEquality(new(v.X, 0, 0), result);
    }

    [TestMethod]
    public void TruncateVectorLengthDoesNotTruncateShortVector() =>
        AssertVectorEquality(Vector3.UnitY, Vector3.UnitY.TruncateLength(2));

    [TestMethod]
    public void TruncateVectorLengthTruncatesLongVector() =>
        AssertVectorEquality(Vector3.UnitY * 0.5f, Vector3.UnitY.TruncateLength(0.5f));

    [TestMethod]
    public void RotateVectorAboutGlobalYClockwise() =>
        AssertVectorEquality(new(1, 1, 0), new Vector3(0, 1, 1).RotateAboutGlobalY(PiOver2), 0.0000001f);

    [TestMethod]
    public void RotateVectorAboutGlobalYAntiClockwise() => AssertVectorEquality(new(1, 1, 0),
        new Vector3(0, 1, -1).RotateAboutGlobalY(-PiOver2), 0.0000001f);

    [TestMethod]
    public void RotateVectorAboutGlobalYClockwiseWithCache()
    {
        const float angle = PiOver2;
        var sin = (float)Math.Sin(angle);
        var cos = (float)Math.Cos(angle);

        float computedSin = 0;
        float computedCos = 0;
        AssertVectorEquality(new(1, 1, 0),
            new Vector3(0, 1, 1).RotateAboutGlobalY(angle, ref computedSin, ref computedCos), 0.0000001f);

        Assert.AreEqual(sin, computedSin);
        Assert.AreEqual(cos, computedCos);
    }

    [TestMethod]
    public void RotateVectorAboutGlobalYAntiClockwiseWithCache()
    {
        const float angle = -PiOver2;
        var sin = (float)Math.Sin(angle);
        var cos = (float)Math.Cos(angle);

        float computedSin = 0;
        float computedCos = 0;
        AssertVectorEquality(new(1, 1, 0),
            new Vector3(0, 1, -1).RotateAboutGlobalY(angle, ref computedSin, ref computedCos), 0.0000001f);

        Assert.AreEqual(sin, computedSin);
        Assert.AreEqual(cos, computedCos);
    }

    [TestMethod]
    public void SperhicalWraparoundDoesNotChangeVectorInsideSphere()
    {
        var pos = new Vector3(10, 11, 12);

        var center = Vector3.Zero;
        const float radius = 20;

        Assert.AreEqual(pos, pos.SphericalWrapAround(center, radius));
    }

    [TestMethod]
    public void SperhicalWraparoundWrapsAround()
    {
        var pos = new Vector3(0, 0, 30);

        var center = Vector3.Zero;
        const float radius = 20;

        Assert.AreEqual(new(0, 0, -10), pos.SphericalWrapAround(center, radius));
    }

    [TestMethod]
    public void SperhicalWraparoundWrapsAroundVeryLargeValue()
    {
        var pos = new Vector3(0, 0, 90);

        var center = Vector3.Zero;
        const float radius = 20;

        Assert.AreEqual(new(0, 0, 10), pos.SphericalWrapAround(center, radius));
    }

    static void BitsetDirections(Vector3 a, ref int bitset)
    {
        bitset |= a.X > 0 ? 1 : 2;
        bitset |= a.Y > 0 ? 4 : 8;
        bitset |= a.Z > 0 ? 16 : 32;
    }

    [TestMethod]
    public void RandomVectorOnUnitRadiusXZDiskIsAlwaysWithinOneUnitOfOrigin()
    {
        var set = 0;
        for (var i = 0; i < 1000; i++)
        {
            var v = Vector3Helpers.RandomVectorOnUnitRadiusXZDisk();
            Assert.IsTrue(v.Length() <= 1);
            BitsetDirections(v, ref set);
        }

        // Y is always zero, so we expect to find every direction except positive Y
        Assert.AreEqual(59, set);
    }

    [TestMethod]
    public void RandomVectorInUnitRadiusSphereIsAlwaysWithinOneUnitOfOrigin()
    {
        var set = 0;
        for (var i = 0; i < 1000; i++)
        {
            var v = Vector3Helpers.RandomVectorInUnitRadiusSphere();
            Assert.IsTrue(v.Length() <= 1);
            BitsetDirections(v, ref set);
        }

        // We expect to find every direction
        Assert.AreEqual(63, set);
    }

    [TestMethod]
    public void RandomUnitVectorIsAlwaysLengthOne()
    {
        var set = 0;
        for (var i = 0; i < 1000; i++)
        {
            var v = Vector3Helpers.RandomUnitVector();
            Assert.IsTrue(Math.Abs(v.Length() - 1) < 0.000001f);
            BitsetDirections(v, ref set);
        }

        // We expect to find every direction
        Assert.AreEqual(63, set);
    }

    [TestMethod]
    public void RandomUnitVectorOnXzPlaneIsAlwaysLengthOne()
    {
        var set = 0;
        for (var i = 0; i < 1000; i++)
        {
            var v = Vector3Helpers.RandomUnitVectorOnXZPlane();
            Assert.IsTrue(Math.Abs(v.Length() - 1) < 0.000001f);
            BitsetDirections(v, ref set);
        }

        // Y is always zero, so we expect to find every direction except positive Y
        Assert.AreEqual(59, set);
    }

    [TestMethod]
    public void DistanceFromLineTest()
    {
        var point = new Vector3(0, 100, 0);

        var origin = Vector3.Zero;
        var direction = new Vector3(1, 0, 0);

        Assert.AreEqual(100, point.DistanceFromLine(origin, direction));
    }

    [TestMethod]
    public void FindPerpendicularIn3dIsAlwaysPerpendicular()
    {
        var set = 0;

        for (var i = 0; i < 1000; i++)
        {
            var v = Vector3Helpers.RandomUnitVector();
            var perp = v.FindPerpendicularIn3d();

            BitsetDirections(perp, ref set);

            Assert.AreEqual(0, Vector3.Dot(v, perp));
        }

        Assert.AreEqual(63, set);
    }

    [TestMethod]
    public void ClipWithinConeIsAlwaysWithinCone()
    {
        for (var i = 0; i < 5000; i++)
        {
            var vector = Vector3Helpers.RandomUnitVector();

            var basis = Vector3Helpers.RandomUnitVector();
            var angle = RandomHelpers.Random(0.1f, PiOver2);
            var cosAngle = (float)Math.Cos(angle);

            var result = vector.LimitMaxDeviationAngle(cosAngle, basis);
            var measuredAngle = (float)Math.Acos(Vector3.Dot(result, basis));
            Assert.IsTrue(measuredAngle <= angle + 0.0001f);
        }
    }

    [TestMethod]
    public void ClipWithoutConeIsAlwaysWithoutCone()
    {
        for (var i = 0; i < 5000; i++)
        {
            var vector = Vector3Helpers.RandomUnitVector();

            var basis = Vector3Helpers.RandomUnitVector();
            var angle = RandomHelpers.Random(0.1f, PiOver2);
            var cosAngle = (float)Math.Cos(angle);

            var result = vector.LimitMinDeviationAngle(cosAngle, basis);
            var measuredAngle = (float)Math.Acos(Vector3.Dot(result, basis));
            Assert.IsTrue(measuredAngle >= angle - 0.0001f);
        }
    }

    [TestMethod]
    public void ClipWithinConeReturnsZeroLengthVectors() =>
        Assert.AreEqual(Vector3.Zero, Vector3.Zero.LimitMaxDeviationAngle(0.2f, Vector3.UnitY));

    [TestMethod]
    public void ClipBackwardsVectorIsZero() =>
        Assert.AreEqual(Vector3.Zero, Vector3.UnitZ.LimitMaxDeviationAngle(0.2f, -Vector3.UnitZ));

    [TestMethod]
    public void ClipWithoutConeReturnsZeroLengthVectors() =>
        Assert.AreEqual(Vector3.Zero, Vector3.Zero.LimitMinDeviationAngle(0.2f, Vector3.UnitY));
}
