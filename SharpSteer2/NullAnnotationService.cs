namespace SharpSteer2;

class NullAnnotationService : IAnnotationService
{
    public bool IsEnabled
    {
        get => false;
        set { }
    }

    public void Line(Vector3 startPoint, Vector3 endPoint, Vector3 color, float opacity = 1) { }

    public void CircleXz(float radius, Vector3 center, Vector3 color, int segments) { }

    public void DiskXz(float radius, Vector3 center, Vector3 color, int segments) { }

    public void Circle3D(float radius, Vector3 center, Vector3 axis, Vector3 color, int segments) { }

    public void Disk3D(float radius, Vector3 center, Vector3 axis, Vector3 color, int segments) { }

    public void CircleOrDiskXz(float radius, Vector3 center, Vector3 color, int segments, bool filled) { }

    public void CircleOrDisk3D(float radius, Vector3 center, Vector3 axis, Vector3 color, int segments, bool filled) { }

    public void CircleOrDisk(float radius, Vector3 axis, Vector3 center, Vector3 color, int segments, bool filled,
        bool in3D) { }

    public void AvoidObstacle(float minDistanceToCollision) { }

    public void PathFollowing(Vector3 future, Vector3 onPath, Vector3 target, float outside) { }

    public void AvoidCloseNeighbor(IVehicle other, float additionalDistance) { }

    public void AvoidNeighbor(IVehicle threat, float steer, Vector3 ourFuture, Vector3 threatFuture) { }

    public void VelocityAcceleration(IVehicle vehicle) { }

    public void VelocityAcceleration(IVehicle vehicle, float maxLength) { }

    public void VelocityAcceleration(IVehicle vehicle, float maxLengthAcceleration, float maxLengthVelocity) { }
}
