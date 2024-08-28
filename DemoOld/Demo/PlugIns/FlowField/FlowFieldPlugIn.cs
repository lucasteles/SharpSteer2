using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Database;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.FlowField
{
    public class FlowFieldPlugIn
        : PlugIn
    {
        private const int FOLLOWER_COUNT = 15;
        private readonly List<FlowFieldFollower> _followers = new List<FlowFieldFollower>(FOLLOWER_COUNT);

        public IFlowField FlowField { get; private set; }
        public float PredictionTime { get; private set; }

        public IProximityDatabase<IVehicle> Database { get; private set; }

        public override IEnumerable<IVehicle> Vehicles
        {
            get { return _followers; }
        }

        public new IAnnotationService Annotations
        {
            get { return base.Annotations; }
        }

        public override string Name
        {
            get { return "Flow Field Following"; }
        }

        public FlowFieldPlugIn(IAnnotationService annotations) : base(annotations)
        {
            PredictionTime = 1;
            Database = new LocalityQueryProximityDatabase<IVehicle>(Vector3.Zero, new Vector3(250, 250, 250), new Vector3(10));
        }

        public override void Open()
        {
            // create the specified number of enemies, 
            // storing pointers to them in an array.
            for (int i = 0; i < FOLLOWER_COUNT; i++)
                _followers.Add(new FlowFieldFollower(this));

            // initialize camera
            Demo.Init2dCamera(_followers.First());
            Demo.Camera.Mode = Camera.CameraMode.Fixed;
            Demo.Camera.FixedTarget = Vector3.Zero;
            Demo.Camera.FixedTarget.X = 15;
            Demo.Camera.FixedPosition.X = 80;
            Demo.Camera.FixedPosition.Y = 60;
            Demo.Camera.FixedPosition.Z = 0;

            FlowField = GenerateFlowField();
        }

        private static IFlowField GenerateFlowField()
        {
            var f = new SimpleFlowField(50, 1, 50, new Vector3(25, 0.5f, 25));

            //Start random
            f.Randomize(1);

            //Swirl around center
            //Half the field is a swirl (basically just concentric circles) while the other half has a slight bias to spiral inwards towards the center
            f.Func(pos => Vector3.Lerp(pos / 5, Vector3.Normalize(Vector3.Cross(pos, Vector3.UnitY)), pos.X > 0.5f ? 0.75f : 0.9f), 0.85f);

            //Keep it flat on the plane
            f.ClampXZ();

            //Clean NaN values
            f.Clean();

            return f;
        }

        public override void Update(float currentTime, float elapsedTime)
        {
            foreach (var flowFieldFollower in _followers)
            {
                flowFieldFollower.Update(currentTime, elapsedTime);
            }
        }

        public override void Redraw(float currentTime, float elapsedTime)
        {
            // selected vehicle (user can mouse click to select another)
            IVehicle selected = Demo.SelectedVehicle;

            Demo.UpdateCamera(elapsedTime, selected);
            Demo.GridUtility(Vector3.Zero);

            //Draw flow field
            const float RANGE = 50;
            const int SAMPLES = 25;
            for (int i = 0; i < SAMPLES; i++)
            {
                for (int j = 0; j < SAMPLES; j++)
                {
                    Vector3 location = new Vector3(RANGE / SAMPLES * i - RANGE / 2, 0, RANGE / SAMPLES * j - RANGE / 2);
                    var flow = FlowField.Sample(location);
                    Annotations.Line(location, location + flow, Color.Black.ToVector3().FromXna());
                }
            }

            // draw vehicles
            foreach (var vehicle in _followers)
                vehicle.Draw();
        }

        public override void Close()
        {
            _followers.Clear();
        }
    }
}
