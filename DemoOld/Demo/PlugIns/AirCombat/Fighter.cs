using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.AirCombat
{
    class Fighter
        :SimpleVehicle
    {
        private readonly Trail _trail;
        private readonly ITokenForProximityDatabase<IVehicle> _proximityToken;

        public List<Fighter> Enemy { get; set; }
        private readonly List<IVehicle> _neighbours = new List<IVehicle>();

        public override float MaxForce
        {
            get { return 7; }
        }
        public override float MaxSpeed
        {
            get { return 15; }
        }

        public const float WORLD_RADIUS = 30;

        private float _lastFired = -100;
        private const float REFIRE_TIME = 2f;
        private readonly Action<Fighter, Fighter> _fireMissile;

        public Color Color = Color.White;

        public Fighter(IProximityDatabase<IVehicle> proximity, IAnnotationService annotation, Action<Fighter, Fighter> fireMissile)
            :base(annotation)
        {
            _trail = new Trail(5, 50)
            {
                TrailColor = Color.WhiteSmoke,
                TickColor = Color.LightGray
            };
            _proximityToken = proximity.AllocateToken(this);

            _fireMissile = fireMissile;
        }

        public void Update(float currentTime, float elapsedTime)
        {
            _trail.Record(currentTime, Position);

            _neighbours.Clear();
            _proximityToken.FindNeighbors(Position, 50, _neighbours);

            var target = ClosestEnemy(_neighbours);

            //if (Vector3.Dot(Vector3.Normalize(Enemy.Position - Position), Forward) > 0.7f)
            {
                if (currentTime - _lastFired > REFIRE_TIME && target != null)
                {
                    _fireMissile(this, ClosestEnemy(_neighbours));
                    _lastFired = currentTime;
                }
            }

            Vector3 otherPlaneForce = SteerToAvoidCloseNeighbors(3, _neighbours);
            if (target != null)
                otherPlaneForce += SteerForPursuit(target);

            var boundary = HandleBoundary();

            var evasion = _neighbours
                .Where(v => v is Missile)
                .Cast<Missile>()
                .Where(m => m.Target == this)
                .Select(m => SteerForEvasion(m, 1))
                .Aggregate(Vector3.Zero, (a, b) => a + b);

            ApplySteeringForce(otherPlaneForce + boundary + evasion * 0.5f + SteerForWander(elapsedTime) * 0.1f, elapsedTime);

            _proximityToken.UpdateForNewPosition(Position);
        }

        private Fighter ClosestEnemy(List<IVehicle> neighbours)
        {
            if (_neighbours.Count == 0)
                return null;

            var enemyFighterNeighbours = _neighbours
                .Where(v => v is Fighter)
                .Cast<Fighter>()
                .Where(f => f.Enemy != Enemy);

            if (!enemyFighterNeighbours.Any())
                return null;

            return enemyFighterNeighbours
                .Select(f => new { Distance = (Position - f.Position).LengthSquared(), Fighter = f })
                .Aggregate((a, b) => a.Distance < b.Distance ? a : b)
                .Fighter;
        }

        protected override void RegenerateLocalSpace(Vector3 newVelocity, float elapsedTime)
        {
            RegenerateLocalSpaceForBanking(newVelocity, elapsedTime);
        }

        private Vector3 HandleBoundary()
        {
            // while inside the sphere do noting
            if (Position.Length() < WORLD_RADIUS)
                return Vector3.Zero;

            // steer back when outside
            Vector3 seek = SteerForSeek(Vector3.Zero);
            Vector3 lateral = Vector3Helpers.PerpendicularComponent(seek, Forward);
            return lateral;

        }

        public void Draw()
        {
            _trail.Draw(annotation);
            Drawing.DrawBasic3dSphericalVehicle(this, Color);
        }
    }
}
