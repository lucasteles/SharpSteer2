using System.Numerics;
using SharpSteer2.Helpers;

namespace SharpSteer2.Tests;

/// <summary>
///This is a test class for UtilitiesTest and is intended
///to contain all UtilitiesTest Unit Tests
///</summary>
[TestClass]
public class UtilitiesTest
{
    [TestMethod]
    public void Random()
    {
    }

    [TestMethod]
    public void ScalarRandomWalk()
    {
        var rands = Enumerable.Range(1, 1000).Select(a => Utilities.ScalarRandomWalk(0, 10, -5, 5));

        foreach (var rand in rands)
        {
            Assert.IsTrue(rand >= -5);
            Assert.IsTrue(rand <= 5);
        }
    }

    [TestMethod]
    public void BoundedRandom()
    {
        const int lower = -17;
        const int upper = 24;

        var rand = RandomHelpers.Random(lower, upper);
        Assert.IsTrue(lower <= rand);
        Assert.IsTrue(upper >= rand);
    }

    [TestMethod]
    public void BoundedRandomInt()
    {
        const int lower = -17;
        const int upper = 24;

        var rand = RandomHelpers.RandomInt(lower, upper);
        Assert.IsTrue(lower <= rand);
        Assert.IsTrue(upper >= rand);
    }

    [TestMethod]
    public void RemapIntervalChangeUpperBound()
    {
        const int a = 0;
        const int b = 10;

        const int c = 0;
        const int d = 20;

        const int x = 5;

        // 5 is halfway between 0->10, so result is halfway between 0->20 (i.e. 10)
        Assert.AreEqual(10, Utilities.RemapInterval(x, a, b, c, d));
    }

    [TestMethod]
    public void RemapIntervalChangeLowerBound()
    {
        const int a = 0;
        const int b = 10;

        const int c = -10;
        const int d = 10;

        const int x = 5;

        // 5 is halfway between 0->10, so result is halfway between -10->10 (i.e. 0)
        Assert.AreEqual(0, Utilities.RemapInterval(x, a, b, c, d));
    }

    [TestMethod]
    public void RemapIntervalChangeBothBounds()
    {
        const int a = 0;
        const int b = 10;

        const int c = -20;
        const int d = 40;

        const int x = 5;

        // 5 is halfway between 0->10, so result is halfway between -20->40 (i.e. 10)
        Assert.AreEqual(10, Utilities.RemapInterval(x, a, b, c, d));
    }

    [TestMethod]
    public void RemapIntervalBeyondBound()
    {
        const int a = 0;
        const int b = 10;

        const int c = 0;
        const int d = 20;

        const int x = 20;

        // 20 is the entire range width (10) above max (10), so result is entire range width (20) above max (20) (i.e. 40)
        Assert.AreEqual(40, Utilities.RemapInterval(x, a, b, c, d));
    }

    [TestMethod]
    public void RemapIntervalClip()
    {
        const int a = 0;
        const int b = 10;

        const int c = 0;
        const int d = 20;

        const int x = 20;

        Assert.AreEqual(20, Utilities.RemapIntervalClip(x, a, b, c, d));
    }

    [TestMethod]
    public void IntervalComparison()
    {
        Assert.AreEqual(-1, Utilities.IntervalComparison(0, 1, 2));
        Assert.AreEqual(0, Utilities.IntervalComparison(1.5f, 1, 2));
        Assert.AreEqual(+1, Utilities.IntervalComparison(3, 1, 2));
    }

    [TestMethod]
    public void FloatBlendIntoAccumulator()
    {
        float smoothedValue = 1;
        Utilities.BlendIntoAccumulator(0.5f, 2, ref smoothedValue);

        Assert.AreEqual(Utilities.Lerp(1, 2, 0.5f), smoothedValue);
    }

    [TestMethod]
    public void Vector3BlendIntoAccumulator()
    {
        Vector3 smoothedValue = Vector3.One;
        Utilities.BlendIntoAccumulator(0.5f, new(2, 2, 2), ref smoothedValue);

        Assert.AreEqual(Vector3.Lerp(Vector3.One, new(2), 0.5f), smoothedValue);
    }
}