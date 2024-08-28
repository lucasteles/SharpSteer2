using System;
using System.Collections.Generic;
using System.Linq;
using Demo.PlugIns.MeshPathFollowing;
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Helpers;
using SharpSteer2.Pathway;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.GatewayPathFollowing
{
    public class GatewayPathFollowingPlugin
        :PlugIn
    {
        private GatewayPathway _path;
        private readonly List<PathWalker> _walkers = new List<PathWalker>();

        public override bool RequestInitialSelection
        {
            get
            {
                return true;
            }
        }

        public GatewayPathFollowingPlugin(IAnnotationService annotations)
            :base(annotations)
        {
        }

        public override void Open()
        {
            GeneratePath();

            _walkers.Clear();
            for (int i = 0; i < 7; i++)
            {
                _walkers.Add(new PathWalker(_path, Annotations, _walkers)
                {
                    Position = new Vector3(i * 1 * 2, 0, 0),
                    Forward = new Vector3(0, 0, 1),
                });
            }
        }

        private void GeneratePath()
        {
            var rand = new Random();

            float xOffsetDeriv = 0;
            float xOffset = 0;

            var gateways = new List<GatewayPathway.Gateway>();
            for (var i = 0; i < 200; i++)
            {
                xOffsetDeriv = Utilities.Clamp((float)rand.NextDouble() * 2 - (xOffsetDeriv * 0.0125f), -15, 15);
                xOffset += xOffsetDeriv;

                if (i % 3 == 0)
                {
                    gateways.Add(new GatewayPathway.Gateway(
                        new Vector3(xOffset + 1, 0, i) * 5,
                        new Vector3(xOffset - 1, 0, i) * 5
                    ));
                }
                else
                {
                    gateways.Add(new GatewayPathway.Gateway(
                        new Vector3(xOffset - 1, 0, i) * 5,
                        new Vector3(xOffset + 1, 0, i) * 5
                    ));
                }
            }

            _path = new GatewayPathway(gateways);
        }

        public override void Update(float currentTime, float elapsedTime)
        {
            foreach (var walker in _walkers)
                walker.Update(elapsedTime);
        }

        public override void Redraw(float currentTime, float elapsedTime)
        {
            Demo.UpdateCamera(elapsedTime, _walkers[0]);
            foreach (var walker in _walkers)
                walker.Draw();

            var tri = _path.TrianglePathway.Triangles.ToArray();
            foreach (var triangle in tri)
            {
                Drawing.Draw2dLine(triangle.A, triangle.A + triangle.Edge0, Color.Black);
                Drawing.Draw2dLine(triangle.A, triangle.A + triangle.Edge1, Color.Black);
                Drawing.Draw2dLine(triangle.A + triangle.Edge0, triangle.A + triangle.Edge1, Color.Black);
            }

            var points = _path.Centerline.Points.ToArray();
            for (int i = 0; i < points.Length - 1; i++)
            {
                Drawing.Draw2dLine(points[i], points[i + 1], Color.Gray);
            }
        }

        public override void Close()
        {
            
        }

        public override string Name
        {
            get { return "Gateway Path Following"; }
        }

        public override IEnumerable<IVehicle> Vehicles
        {
            get
            {
                return _walkers;
            }
        }
    }
}
