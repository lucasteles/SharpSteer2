using System.Numerics;
using SharpSteer2.Helpers;

namespace SharpSteer2.Tests;

[TestClass]
public class NullAnnotationTest
{
    [TestMethod]
    public void NullAnnotationsDoNotThrow()
    {
        var a = new NullAnnotationService();

        a.AvoidCloseNeighbor(null, 0);
        a.AvoidNeighbor(null, 0, Vector3.Zero, Vector3.Zero);
        a.AvoidObstacle(0);
        a.Circle3D(0, Vector3.Zero, Vector3.Zero, Colors.White, 0);
        a.CircleOrDisk(0, Vector3.Zero, Vector3.Zero, Colors.White, 0, true, true);
        a.CircleOrDisk3D(0, Vector3.Zero, Vector3.Zero, Colors.White, 0, true);
        a.CircleOrDiskXz(0, Vector3.Zero, Colors.White, 0, true);
        a.CircleXz(0, Vector3.Zero, Colors.White, 0);
        a.Disk3D(0, Vector3.Zero, Vector3.Zero, Colors.White, 0);
        a.DiskXz(0, Vector3.Zero, Colors.White, 0);
        a.Line(Vector3.Zero, Vector3.Zero, Colors.White);
        a.PathFollowing(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0);
        a.VelocityAcceleration(null);
        a.VelocityAcceleration(null, 0);
        a.VelocityAcceleration(null, 0, 0);
        a.IsEnabled = !a.IsEnabled;
    }
}