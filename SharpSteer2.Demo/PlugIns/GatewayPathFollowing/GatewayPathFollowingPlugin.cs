using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Demo.PlugIns.MeshPathFollowing;
using SharpSteer2.Helpers;
using SharpSteer2.Pathway;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.GatewayPathFollowing;

public class GatewayPathFollowingPlugin
    :PlugIn
{
    GatewayPathway path;
    readonly List<PathWalker> walkers = new();

    public override bool RequestInitialSelection => true;

    public GatewayPathFollowingPlugin(IAnnotationService annotations)
        :base(annotations)
    {
    }

    public override void Open()
    {
        GeneratePath();

        walkers.Clear();
        for (var i = 0; i < 7; i++)
        {
            walkers.Add(new(path, annotations, walkers)
            {
                Position = new(i * 1 * 2, 0, 0),
                Forward = new(0, 0, 1),
            });
        }
    }

    void GeneratePath()
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
                gateways.Add(new(
                    new Vector3(xOffset + 1, 0, i) * 5,
                    new Vector3(xOffset - 1, 0, i) * 5
                ));
            }
            else
            {
                gateways.Add(new(
                    new Vector3(xOffset - 1, 0, i) * 5,
                    new Vector3(xOffset + 1, 0, i) * 5
                ));
            }
        }

        path = new(gateways);
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        foreach (var walker in walkers)
            walker.Update(elapsedTime);
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        GameDemo.UpdateCamera(elapsedTime, walkers[0]);
        foreach (var walker in walkers)
            walker.Draw();

        var tri = path.TrianglePathway.Triangles.ToArray();
        foreach (var triangle in tri)
        {
            Drawing.Draw2dLine(triangle.A, triangle.A + triangle.Edge0, Color.Black);
            Drawing.Draw2dLine(triangle.A, triangle.A + triangle.Edge1, Color.Black);
            Drawing.Draw2dLine(triangle.A + triangle.Edge0, triangle.A + triangle.Edge1, Color.Black);
        }

        var points = path.Centerline.Points.ToArray();
        for (var i = 0; i < points.Length - 1; i++)
        {
            Drawing.Draw2dLine(points[i], points[i + 1], Color.Gray);
        }
    }

    public override void Close()
    {
            
    }

    public override string Name => "Gateway Path Following";

    public override IEnumerable<IVehicle> Vehicles => walkers;
}