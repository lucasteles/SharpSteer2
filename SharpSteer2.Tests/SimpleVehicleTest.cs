using System.Numerics;

namespace SharpSteer2.Tests;

[TestClass]
public class SimpleVehicleTest
{
    readonly SimpleVehicle vehicle = new();

    [TestMethod]
    public void Construct()
    {
        Assert.AreEqual(Vector3.Zero, vehicle.Acceleration);
        Assert.AreEqual(-Vector3.UnitZ, vehicle.Forward);
        Assert.AreEqual(Vector3.Zero, vehicle.Velocity);
        Assert.AreEqual(0, vehicle.Speed);
        Assert.AreEqual(Vector3.Zero, vehicle.SmoothedPosition);
    }

    [TestMethod]
    public void ApplyForce()
    {
        vehicle.ApplySteeringForce(-Vector3.UnitZ, 1);

        Assert.AreEqual(-Vector3.UnitZ * vehicle.Speed, vehicle.Velocity);
    }
}
