using SharpSteer2.Helpers;
using SharpSteer2.Pathway;

namespace SharpSteer2.Tests;

[TestClass]
public class MeshPathwayTest
{
    #region MeshPath.TriangleData

    TrianglePathway.Triangle triangle = new(
        new(0, 0, 0),
        new(0, 0, 1),
        new(1, 0, 0)
    );

    [TestMethod]
    public void ClosestPointOnTriangleEdge0()
    {
        float s;
        float t;
        bool inside;
        var closest = TrianglePathway.ClosestPointOnTriangle(ref triangle, new(-1, 0, 0.5f), out s, out t, out inside);

        Assert.IsFalse(inside);
        Assert.AreEqual(0.0f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.5f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.5f, s, float.Epsilon);
        Assert.AreEqual(0.0f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointOnTriangleEdge1()
    {
        float s;
        float t;
        bool inside;
        var closest =
            TrianglePathway.ClosestPointOnTriangle(ref triangle, new(0.6f, 0, 0.6f), out s, out t, out inside);

        Assert.IsFalse(inside);
        Assert.AreEqual(0.5f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.5f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.5f, s, float.Epsilon);
        Assert.AreEqual(0.5f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointOnTriangleEdge2()
    {
        float s;
        float t;
        bool inside;
        var closest = TrianglePathway.ClosestPointOnTriangle(ref triangle, new(0.5f, 0, -1f), out s, out t, out inside);

        Assert.IsFalse(inside);
        Assert.AreEqual(0.5f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.0f, s, float.Epsilon);
        Assert.AreEqual(0.5f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointOnTriangleCorner0()
    {
        float s;
        float t;
        bool inside;
        var closest = TrianglePathway.ClosestPointOnTriangle(ref triangle, new(-1f, 0, -1f), out s, out t, out inside);

        Assert.IsFalse(inside);
        Assert.AreEqual(0.0f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.0f, s, float.Epsilon);
        Assert.AreEqual(0.0f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointOnTriangleCorner1()
    {
        float s;
        float t;
        bool inside;
        var closest = TrianglePathway.ClosestPointOnTriangle(ref triangle, new(0f, 0, 2f), out s, out t, out inside);

        Assert.IsFalse(inside);
        Assert.AreEqual(0.0f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(1.0f, closest.Z, float.Epsilon);
        Assert.AreEqual(1.0f, s, float.Epsilon);
        Assert.AreEqual(0.0f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointOnTriangleCorner2()
    {
        float s;
        float t;
        bool inside;
        var closest = TrianglePathway.ClosestPointOnTriangle(ref triangle, new(2f, 0, 0f), out s, out t, out inside);

        Assert.IsFalse(inside);
        Assert.AreEqual(1.0f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.0f, s, float.Epsilon);
        Assert.AreEqual(1.0f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointInTriangleEdge0()
    {
        float s;
        float t;
        bool inside;
        var closest =
            TrianglePathway.ClosestPointOnTriangle(ref triangle, new(0.1f, 0, 0.5f), out s, out t, out inside);

        Assert.IsTrue(inside);
        Assert.AreEqual(0.1f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.5f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.5f, s, float.Epsilon);
        Assert.AreEqual(0.1f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointInTriangleEdge1()
    {
        float s;
        float t;
        bool inside;
        var closest =
            TrianglePathway.ClosestPointOnTriangle(ref triangle, new(0.4f, 0, 0.4f), out s, out t, out inside);

        Assert.IsTrue(inside);
        Assert.AreEqual(0.4f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.4f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.4f, s, float.Epsilon);
        Assert.AreEqual(0.4f, t, float.Epsilon);
    }

    [TestMethod]
    public void ClosestPointInTriangleEdge2()
    {
        float s;
        float t;
        bool inside;
        var closest =
            TrianglePathway.ClosestPointOnTriangle(ref triangle, new(0.5f, 0, 0.1f), out s, out t, out inside);

        Assert.IsTrue(inside);
        Assert.AreEqual(0.5f, closest.X, float.Epsilon);
        Assert.AreEqual(0.0f, closest.Y, float.Epsilon);
        Assert.AreEqual(0.1f, closest.Z, float.Epsilon);
        Assert.AreEqual(0.1f, s, float.Epsilon);
        Assert.AreEqual(0.5f, t, float.Epsilon);
    }

    #endregion

    #region MeshPath

    readonly TrianglePathway path = new(new[]
    {
        new TrianglePathway.Triangle(new(0, 0, 0), new(0, 0, 1), new(1, 0, 0)),
        new TrianglePathway.Triangle(new(0, 0, 1), new(1, 0, 1), new(1, 0, 0)),
        new TrianglePathway.Triangle(new(1, 0, 1), new(2, 0, 1), new(1, 0, 0)),
    });

    [TestMethod]
    public void HowFarOutsidePath()
    {
        var distance = path.HowFarOutsidePath(new(-1, 0, 0));
        Assert.AreEqual(1, distance, float.Epsilon);

        var distance1 = path.HowFarOutsidePath(new(3, 0, 1));
        Assert.AreEqual(1, distance1, float.Epsilon);
    }

    #endregion
}
