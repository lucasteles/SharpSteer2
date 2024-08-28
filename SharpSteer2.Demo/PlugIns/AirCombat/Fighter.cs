using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.AirCombat;

class Fighter
    : SimpleVehicle
{
    readonly Trail trail;
    readonly ITokenForProximityDatabase<IVehicle> proximityToken;

    public List<Fighter> Enemy { get; set; }
    readonly List<IVehicle> neighbours = new();

    public override float MaxForce => 7;

    public override float MaxSpeed => 15;

    public const float WorldRadius = 30;

    float lastFired = -100;
    const float RefireTime = 2f;
    readonly Action<Fighter, Fighter> fireMissile;

    public Color Color = Color.White;

    public Fighter(IProximityDatabase<IVehicle> proximity, IAnnotationService annotation,
        Action<Fighter, Fighter> fireMissile)
        : base(annotation)
    {
        trail = new(5, 50)
        {
            TrailColor = Color.WhiteSmoke,
            TickColor = Color.LightGray
        };
        proximityToken = proximity.AllocateToken(this);

        this.fireMissile = fireMissile;
    }

    public void Update(float currentTime, float elapsedTime)
    {
        trail.Record(currentTime, Position);

        neighbours.Clear();
        proximityToken.FindNeighbors(Position, 50, neighbours);

        var target = ClosestEnemy(neighbours);

        //if (Vector3.Dot(Vector3.Normalize(Enemy.Position - Position), Forward) > 0.7f)
        {
            if (currentTime - lastFired > RefireTime && target is not null)
            {
                fireMissile(this, ClosestEnemy(neighbours));
                lastFired = currentTime;
            }
        }

        var otherPlaneForce = SteerToAvoidCloseNeighbors(3, neighbours);
        if (target is not null)
            otherPlaneForce += SteerForPursuit(target);

        var boundary = HandleBoundary();

        var evasion = neighbours
            .Where(v => v is Missile)
            .Cast<Missile>()
            .Where(m => m.Target == this)
            .Select(m => SteerForEvasion(m, 1))
            .Aggregate(Vector3.Zero, (a, b) => a + b);

        ApplySteeringForce(otherPlaneForce + boundary + evasion * 0.5f + SteerForWander(elapsedTime) * 0.1f,
            elapsedTime);

        proximityToken.UpdateForNewPosition(Position);
    }

    Fighter ClosestEnemy(List<IVehicle> neighbours)
    {
        if (this.neighbours.Count == 0)
            return null;

        var enemyFighterNeighbours = this.neighbours
            .Where(v => v is Fighter)
            .Cast<Fighter>()
            .Where(f => f.Enemy != Enemy);

        if (!enemyFighterNeighbours.Any())
            return null;

        return enemyFighterNeighbours
            .Select(f => new
            {
                Distance = (Position - f.Position).LengthSquared(),
                Fighter = f
            })
            .Aggregate((a, b) => a.Distance < b.Distance ? a : b)
            .Fighter;
    }

    protected override void RegenerateLocalSpace(Vector3 newVelocity, float elapsedTime) =>
        RegenerateLocalSpaceForBanking(newVelocity, elapsedTime);

    Vector3 HandleBoundary()
    {
        // while inside the sphere do noting
        if (Position.Length() < WorldRadius)
            return Vector3.Zero;

        // steer back when outside
        var seek = SteerForSeek(Vector3.Zero);
        var lateral = Vector3Helpers.PerpendicularComponent(seek, Forward);
        return lateral;
    }

    public void Draw()
    {
        trail.Draw(Annotation);
        Drawing.DrawBasic3dSphericalVehicle(this, Color);
    }
}
