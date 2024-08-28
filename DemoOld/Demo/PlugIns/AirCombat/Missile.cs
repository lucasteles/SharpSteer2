using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Database;

namespace Demo.PlugIns.AirCombat
{
    class Missile
        :SimpleVehicle
    {
        private readonly Trail _trail;
        private ITokenForProximityDatabase<IVehicle> _proximityToken;

        public readonly IVehicle Target;

        public bool IsDead
        {
            get { return _timer <= 0; }
        }
        private float _timer = 15;

        public override float MaxForce
        {
            get { return 400; }
        }
        public override float MaxSpeed
        {
            get { return 50; }
        }

        public Color Color = Color.Red;

        public Missile(IProximityDatabase<IVehicle> proximity, IVehicle target, IAnnotationService annotation)
            :base(annotation)
        {
            _trail = new Trail(1, 10)
            {
                TrailColor = Color.Red,
                TickColor = Color.DarkRed
            };
            _proximityToken = proximity.AllocateToken(this);
            Target = target;
        }

        public void Update(float currentTime, float elapsedTime)
        {
            _timer -= elapsedTime;
            if (!IsDead)
            {
                _trail.Record(currentTime, Position);
                ApplySteeringForce(SteerForPursuit(Target, 1) * 0.95f + SteerForWander(elapsedTime) * 0.05f, elapsedTime);
                _proximityToken.UpdateForNewPosition(Position);
            }
            else if (_proximityToken != null)
            {
                _proximityToken.Dispose();
                _proximityToken = null;
            }
        }

        public void Draw()
        {
            _trail.Draw(annotation);
            Drawing.DrawBasic3dSphericalVehicle(this, Color);
        }
    }
}
