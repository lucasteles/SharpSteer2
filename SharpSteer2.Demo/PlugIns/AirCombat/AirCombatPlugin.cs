using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.AirCombat;

class AirCombatPlugin
    :PlugIn
{
    readonly List<Fighter> team1 = new();
    readonly List<Fighter> team2 = new();

    readonly List<Missile> missiles = new();

    IProximityDatabase<IVehicle> pd;

    public AirCombatPlugin(IAnnotationService annotations)
        :base(annotations)
    {
    }

    public override void Open()
    {
        CreateDatabase();
        missiles.Clear();

        team1.Add(new(pd, annotations, FireMissile)
        {
            Position = new(20, 0, 0),
            Forward = Vector3Helpers.RandomUnitVector(),
            Color = Color.Green,
            Enemy = team2
        });
        team1.Add(new(pd, annotations, FireMissile)
        {
            Position = new(15, 0, 5),
            Forward = Vector3Helpers.RandomUnitVector(),
            Color = Color.Green,
            Enemy = team2
        });
        team1.Add(new(pd, annotations, FireMissile)
        {
            Position = new(15, 0, -5),
            Forward = Vector3Helpers.RandomUnitVector(),
            Color = Color.Green,
            Enemy = team2
        });

        team2.Add(new(pd, annotations, FireMissile)
        {
            Position = new(-20, 0, 0),
            Forward = Vector3Helpers.RandomUnitVector(),
            Color = Color.Blue,
            Enemy = team1
        });
        team2.Add(new(pd, annotations, FireMissile)
        {
            Position = new(-15, 0, 5),
            Forward = Vector3Helpers.RandomUnitVector(),
            Color = Color.Blue,
            Enemy = team1
        });
        team2.Add(new(pd, annotations, FireMissile)
        {
            Position = new(-15, 0, -5),
            Forward = Vector3Helpers.RandomUnitVector(),
            Color = Color.Blue,
            Enemy = team1
        });
    }

    void CreateDatabase()
    {
        var center = Vector3.Zero;
        const float div = 10.0f;
        var divisions = new Vector3(div, div, div);
        const float diameter = Fighter.WorldRadius * 2;
        var dimensions = new Vector3(diameter, diameter, diameter);
        pd = new LocalityQueryProximityDatabase<IVehicle>(center, dimensions, divisions);
    }

    void FireMissile(Fighter launcher, Fighter target)
    {
        if (missiles.Count(m => m.Target == target) < 3)
        {
            missiles.Add(new(pd, target, annotations)
            {
                Position = launcher.Position,
                Forward = Vector3.Normalize(launcher.Forward * 0.9f + Vector3Helpers.RandomUnitVector() * 0.1f),
                Speed = launcher.Speed,
                Color = team1.Contains(launcher) ? Color.Black : Color.White
            });
        }
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        foreach (var fighter in team1)
            fighter.Update(currentTime, elapsedTime);
        foreach (var fighter in team2)
            fighter.Update(currentTime, elapsedTime);

        foreach (var missile in missiles)
            missile.Update(currentTime, elapsedTime);
        missiles.RemoveAll(m => m.IsDead);
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        GameDemo.UpdateCamera(elapsedTime, team1[0]);

        foreach (var fighter in team1)
            fighter.Draw();
        foreach (var fighter in team2)
            fighter.Draw();

        foreach (var missile in missiles)
            missile.Draw();
    }

    public override void Close()
    {
        team1.Clear();
        team2.Clear();
        missiles.Clear();
        pd = null;
    }

    public override string Name => "Air Combat";

    public override IEnumerable<IVehicle> Vehicles
    {
        get
        {
            foreach (var fighter in team1)
                yield return fighter;
            foreach (var fighter in team2)
                yield return fighter;
            foreach (var missile in missiles)
                yield return missile;
        }
    }
}