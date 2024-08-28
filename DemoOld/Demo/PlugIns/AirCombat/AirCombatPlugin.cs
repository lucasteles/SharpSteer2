using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.AirCombat
{
    class AirCombatPlugin
        :PlugIn
    {
        private readonly List<Fighter> _team1 = new List<Fighter>();
        private readonly List<Fighter> _team2 = new List<Fighter>();

        private readonly List<Missile> _missiles = new List<Missile>();

        private IProximityDatabase<IVehicle> _pd;

        public AirCombatPlugin(IAnnotationService annotations)
            :base(annotations)
        {
        }

        public override void Open()
        {
            CreateDatabase();
            _missiles.Clear();

            _team1.Add(new Fighter(_pd, Annotations, FireMissile)
            {
                Position = new Vector3(20, 0, 0),
                Forward = Vector3Helpers.RandomUnitVector(),
                Color = Color.Green,
                Enemy = _team2
            });
            _team1.Add(new Fighter(_pd, Annotations, FireMissile)
            {
                Position = new Vector3(15, 0, 5),
                Forward = Vector3Helpers.RandomUnitVector(),
                Color = Color.Green,
                Enemy = _team2
            });
            _team1.Add(new Fighter(_pd, Annotations, FireMissile)
            {
                Position = new Vector3(15, 0, -5),
                Forward = Vector3Helpers.RandomUnitVector(),
                Color = Color.Green,
                Enemy = _team2
            });

            _team2.Add(new Fighter(_pd, Annotations, FireMissile)
            {
                Position = new Vector3(-20, 0, 0),
                Forward = Vector3Helpers.RandomUnitVector(),
                Color = Color.Blue,
                Enemy = _team1
            });
            _team2.Add(new Fighter(_pd, Annotations, FireMissile)
            {
                Position = new Vector3(-15, 0, 5),
                Forward = Vector3Helpers.RandomUnitVector(),
                Color = Color.Blue,
                Enemy = _team1
            });
            _team2.Add(new Fighter(_pd, Annotations, FireMissile)
            {
                Position = new Vector3(-15, 0, -5),
                Forward = Vector3Helpers.RandomUnitVector(),
                Color = Color.Blue,
                Enemy = _team1
            });
        }

        private void CreateDatabase()
        {
            Vector3 center = Vector3.Zero;
            const float DIV = 10.0f;
            Vector3 divisions = new Vector3(DIV, DIV, DIV);
            const float DIAMETER = Fighter.WORLD_RADIUS * 2;
            Vector3 dimensions = new Vector3(DIAMETER, DIAMETER, DIAMETER);
            _pd = new LocalityQueryProximityDatabase<IVehicle>(center, dimensions, divisions);
        }

        private void FireMissile(Fighter launcher, Fighter target)
        {
            if (_missiles.Count(m => m.Target == target) < 3)
            {
                _missiles.Add(new Missile(_pd, target, Annotations)
                {
                    Position = launcher.Position,
                    Forward = Vector3.Normalize(launcher.Forward * 0.9f + Vector3Helpers.RandomUnitVector() * 0.1f),
                    Speed = launcher.Speed,
                    Color = _team1.Contains(launcher) ? Color.Black : Color.White
                });
            }
        }

        public override void Update(float currentTime, float elapsedTime)
        {
            foreach (var fighter in _team1)
                fighter.Update(currentTime, elapsedTime);
            foreach (var fighter in _team2)
                fighter.Update(currentTime, elapsedTime);

            foreach (var missile in _missiles)
                missile.Update(currentTime, elapsedTime);
            _missiles.RemoveAll(m => m.IsDead);
        }

        public override void Redraw(float currentTime, float elapsedTime)
        {
            Demo.UpdateCamera(elapsedTime, _team1[0]);

            foreach (var fighter in _team1)
                fighter.Draw();
            foreach (var fighter in _team2)
                fighter.Draw();

            foreach (var missile in _missiles)
                missile.Draw();
        }

        public override void Close()
        {
            _team1.Clear();
            _team2.Clear();
            _missiles.Clear();
            _pd = null;
        }

        public override string Name
        {
            get { return "Air Combat"; }
        }

        public override IEnumerable<IVehicle> Vehicles
        {
            get
            {
                foreach (var fighter in _team1)
                    yield return fighter;
                foreach (var fighter in _team2)
                    yield return fighter;
                foreach (var missile in _missiles)
                    yield return missile;
            }
        }
    }
}
