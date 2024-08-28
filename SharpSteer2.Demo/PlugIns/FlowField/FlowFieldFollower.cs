using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SharpSteer2.Database;
using SharpSteer2.Demo.PlugIns.Ctf;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.FlowField;

public class FlowFieldFollower
    : SimpleVehicle
{
    readonly FlowFieldPlugIn plugin;
    Trail trail;

    readonly ITokenForProximityDatabase<IVehicle> proximityToken;

    public override float MaxSpeed => 2;
    public override float MaxForce => 4;

    public FlowFieldFollower(FlowFieldPlugIn plugin)
        : base(plugin.Annotations)
    {
        this.plugin = plugin;

        proximityToken = plugin.Database.AllocateToken(this);
    }

    public override void Reset()
    {
        base.Reset();

        RandomizeStartingPositionAndHeading();  // new starting position

        trail = new(7.5f, 600);
        trail.Clear();
    }

    void RandomizeStartingPositionAndHeading()
    {
        // randomize position on a ring between inner and outer radii
        // centered around the home base
        var rRadius = RandomHelpers.Random(10, 50);
        var randomOnRing = Vector3Helpers.RandomUnitVectorOnXZPlane() * rRadius;
        Position = Globals.HomeBaseCenter + randomOnRing;
        RandomizeHeadingOnXZPlane();
    }

    public void Update(float currentTime, float elapsedTime)
    {
        ApplySteeringForce(SteeringForce(), elapsedTime);

        Annotation.VelocityAcceleration(this);
        trail.Record(currentTime, Position);

        proximityToken.UpdateForNewPosition(Position);
    }

    Vector3 SteeringForce()
    {
        if (Position.X > 25 || Position.X < -25 || Position.Z > 25 || Position.Z < -25)
            return SteerForSeek(Vector3.Zero);

        var flowField = SteerToFollowFlowField(plugin.FlowField, plugin.PredictionTime);

        const float caLeadTime = 3;

        // find all neighbors within maxRadius using proximity database
        // (radius is largest distance between vehicles traveling head-on
        // where a collision is possible within caLeadTime seconds.)
        var maxRadius = caLeadTime * MaxSpeed * 2;
        var neighbours = new List<IVehicle>();
        proximityToken.FindNeighbors(Position, maxRadius, neighbours);

        if (neighbours.Count > 0)
        {
            var avoid = SteerToAvoidNeighbors(caLeadTime, neighbours) * 10;
            if (avoid != Vector3.Zero)
                return avoid;
        }

        return flowField;
    }

    protected override Vector3 AdjustRawSteeringForce(Vector3 force, float deltaTime) => base.AdjustRawSteeringForce(new(force.X, 0, force.Z), deltaTime);

    internal void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, Color.GhostWhite);
        trail.Draw(Annotation);
    }
}
