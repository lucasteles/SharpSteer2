using Microsoft.Xna.Framework;
using SharpSteer2.Database;

namespace SharpSteer2.Demo.PlugIns.AirCombat;

class Missile
    :SimpleVehicle
{
    readonly Trail trail;
    ITokenForProximityDatabase<IVehicle> proximityToken;

    public readonly IVehicle Target;

    public bool IsDead => timer <= 0;
    float timer = 15;

    public override float MaxForce => 400;

    public override float MaxSpeed => 50;

    public Color Color = Color.Red;

    public Missile(IProximityDatabase<IVehicle> proximity, IVehicle target, IAnnotationService annotation)
        :base(annotation)
    {
        trail = new(1, 10)
        {
            TrailColor = Color.Red,
            TickColor = Color.DarkRed
        };
        proximityToken = proximity.AllocateToken(this);
        Target = target;
    }

    public void Update(float currentTime, float elapsedTime)
    {
        timer -= elapsedTime;
        if (!IsDead)
        {
            trail.Record(currentTime, Position);
            ApplySteeringForce(SteerForPursuit(Target, 1) * 0.95f + SteerForWander(elapsedTime) * 0.05f, elapsedTime);
            proximityToken.UpdateForNewPosition(Position);
        }
        else if (proximityToken is not null)
        {
            proximityToken.Dispose();
            proximityToken = null;
        }
    }

    public void Draw()
    {
        trail.Draw(Annotation);
        Drawing.DrawBasic3dSphericalVehicle(this, Color);
    }
}
