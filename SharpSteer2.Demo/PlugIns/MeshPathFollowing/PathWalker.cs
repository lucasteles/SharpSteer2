using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Helpers;
using SharpSteer2.Pathway;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.MeshPathFollowing;

public class PathWalker
    :SimpleVehicle
{
    public readonly IPathway Path;
    readonly List<PathWalker> vehicles;

    public override float MaxForce => 1;
    public override float MaxSpeed => 10;

    readonly Trail trail = new(30, 300);

    public PathWalker(IPathway path, IAnnotationService annotation, List<PathWalker> vehicles)
        :base(annotation)
    {
        Path = path;
        this.vehicles = vehicles;
    }

    float time;
    public void Update(float dt)
    {
        const float prediction = 3;

        //Avoid other vehicles, and follow the path
        var avoid = SteerToAvoidCloseNeighbors(0.25f, vehicles.Except(new[] { this }));
        if (avoid != Vector3.Zero)
            ApplySteeringForce(avoid, dt);
        else
        {
            var f = SteerToFollowPath(true, prediction, Path);
            ApplySteeringForce(f, dt);
        }

        //If the vehicle leaves the path, penalise it by applying a braking force
        if (Path.HowFarOutsidePath(Position) > 0)
            ApplyBrakingForce(0.3f, dt);

        time += dt;
        trail.Record(time, Position);

        Annotation.VelocityAcceleration(this);
    }

    internal void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, Color.Gray);

        trail.Draw(Annotation);
    }
}
