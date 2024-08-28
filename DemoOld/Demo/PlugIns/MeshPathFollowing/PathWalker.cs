using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Helpers;
using SharpSteer2.Pathway;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.MeshPathFollowing
{
    public class PathWalker
        :SimpleVehicle
    {
        public readonly IPathway Path;
        private readonly List<PathWalker> _vehicles;

        public override float MaxForce { get { return 1; } }
        public override float MaxSpeed { get { return 10; } }

        private readonly Trail _trail = new Trail(30, 300);

        public PathWalker(IPathway path, IAnnotationService annotation, List<PathWalker> vehicles)
            :base(annotation)
        {
            Path = path;
            _vehicles = vehicles;
        }

        private float _time;
        public void Update(float dt)
        {
            const float PREDICTION = 3;

            //Avoid other vehicles, and follow the path
            var avoid = SteerToAvoidCloseNeighbors(0.25f, _vehicles.Except(new[] { this }));
            if (avoid != Vector3.Zero)
                ApplySteeringForce(avoid, dt);
            else
            {
                var f = SteerToFollowPath(true, PREDICTION, Path);
                ApplySteeringForce(f, dt);
            }

            //If the vehicle leaves the path, penalise it by applying a braking force
            if (Path.HowFarOutsidePath(Position) > 0)
                ApplyBrakingForce(0.3f, dt);

            _time += dt;
            _trail.Record(_time, Position);

            annotation.VelocityAcceleration(this);
        }

        internal void Draw()
        {
            Drawing.DrawBasic2dCircularVehicle(this, Color.Gray);

            _trail.Draw(annotation);
        }
    }
}
