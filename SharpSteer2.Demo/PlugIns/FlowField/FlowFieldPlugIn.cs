using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Database;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.FlowField;

public class FlowFieldPlugIn : PlugIn
{
    const int FollowerCount = 15;
    readonly List<FlowFieldFollower> followers = new(FollowerCount);

    public IFlowField FlowField { get; private set; }
    public float PredictionTime { get; private set; }

    public IProximityDatabase<IVehicle> Database { get; private set; }

    public override IEnumerable<IVehicle> Vehicles => followers;

    public IAnnotationService Annotations => annotations;

    public override string Name => "Flow Field Following";

    public FlowFieldPlugIn(IAnnotationService annotations) : base(annotations)
    {
        PredictionTime = 1;
        Database = new LocalityQueryProximityDatabase<IVehicle>(Vector3.Zero, new(250, 250, 250), new(10));
    }

    public override void Open()
    {
        // create the specified number of enemies,
        // storing pointers to them in an array.
        for (var i = 0; i < FollowerCount; i++)
            followers.Add(new(this));

        // initialize camera
        GameDemo.Init2dCamera(followers.First());
        GameDemo.Camera.Mode = Camera.CameraMode.Fixed;
        GameDemo.Camera.FixedTarget = Vector3.Zero;
        GameDemo.Camera.FixedTarget.X = 15;
        GameDemo.Camera.FixedPosition.X = 80;
        GameDemo.Camera.FixedPosition.Y = 60;
        GameDemo.Camera.FixedPosition.Z = 0;

        FlowField = GenerateFlowField();
    }

    static IFlowField GenerateFlowField()
    {
        var f = new SimpleFlowField(50, 1, 50, new(25, 0.5f, 25));

        //Start random
        f.Randomize(1);

        //Swirl around center
        //Half the field is a swirl (basically just concentric circles) while the other half has a slight bias to spiral inwards towards the center
        f.Func(
            pos => Vector3.Lerp(pos / 5, Vector3.Normalize(Vector3.Cross(pos, Vector3.UnitY)),
                pos.X > 0.5f ? 0.75f : 0.9f), 0.85f);

        //Keep it flat on the plane
        f.ClampXz();

        //Clean NaN values
        f.Clean();

        return f;
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        foreach (var flowFieldFollower in followers)
        {
            flowFieldFollower.Update(currentTime, elapsedTime);
        }
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // selected vehicle (user can mouse click to select another)
        var selected = GameDemo.SelectedVehicle;

        GameDemo.UpdateCamera(elapsedTime, selected);
        GameDemo.GridUtility(Vector3.Zero);

        //Draw flow field
        const float range = 50;
        const int samples = 25;
        for (var i = 0; i < samples; i++)
        {
            for (var j = 0; j < samples; j++)
            {
                var location = new Vector3(range / samples * i - range / 2, 0, range / samples * j - range / 2);
                var flow = FlowField.Sample(location);
                Annotations.Line(location, location + flow, Color.Black.ToVector3().ToNumerics());
            }
        }

        // draw vehicles
        foreach (var vehicle in followers)
            vehicle.Draw();
    }

    public override void Close() => followers.Clear();
}
