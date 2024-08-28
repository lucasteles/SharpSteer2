using System.Numerics;
using SharpSteer2.Obstacles;

namespace SharpSteer2.Tests;

[TestClass]
public class SphericalObstacleTests
{
    readonly SphericalObstacle obstacle = new(10, Vector3.Zero);

    readonly SimpleVehicle vehicle = new();

    [TestMethod]
    public void SteerToAvoidReturnsZeroVectorIfThereIsNoIntersection()
    {
        vehicle.Position = new(100, 100, 100);

        Assert.AreEqual(Vector3.Zero, obstacle.SteerToAvoid(vehicle, 1));
    }

    [TestMethod]
    public void SteerToAvoidReturnsNonZeroVectorForStationaryVehicleInsideObstacle()
    {
        vehicle.Position = new(0, 0, 1);

        Assert.AreNotEqual(Vector3.Zero, obstacle.SteerToAvoid(vehicle, 1));
    }

    [TestMethod]
    public void SteerToAvoidReturnsNonZeroVectorForMovingVehicleOutsideObstacle()
    {
        vehicle.Position = -vehicle.Forward * 11;
        vehicle.ApplySteeringForce(vehicle.Forward, 3);

        var f = obstacle.SteerToAvoid(vehicle, 10);
        var dot = Vector3.Dot(vehicle.Position - obstacle.Center, f);

        Assert.IsTrue(dot >= 0);
    }
}