﻿namespace SharpSteer2.Pathway;

/// <summary>
///     A path consisting of a series of gates which must be passed through
/// </summary>
public class GatewayPathway
    : IPathway
{
    public GatewayPathway(IEnumerable<Gateway> gateways, bool cyclic = false)
    {
        var triangles = new List<TrianglePathway.Triangle>();

        var first = true;
        var previous = default(Gateway);
        var previousNormalized = Vector3.Zero;
        foreach (var gateway in gateways)
        {
            var n = Vector3.Normalize(gateway.B - gateway.A);

            if (!first)
            {
                if (Vector3.Dot(n, previousNormalized) < 0)
                {
                    triangles.Add(new(previous.A, previous.B, gateway.A));
                    triangles.Add(new(previous.A, gateway.A, gateway.B));
                }
                else
                {
                    triangles.Add(new(previous.A, previous.B, gateway.A));
                    triangles.Add(new(previous.B, gateway.A, gateway.B));
                }
            }

            first = false;

            previousNormalized = n;
            previous = gateway;
        }

        TrianglePathway = new(triangles, cyclic);
    }

    public PolylinePathway Centerline => TrianglePathway.Centerline;
    public TrianglePathway TrianglePathway { get; }

    public Vector3 MapPointToPath(Vector3 point, out Vector3 tangent, out float outside) =>
        TrianglePathway.MapPointToPath(point, out tangent, out outside);

    public Vector3 MapPathDistanceToPoint(float pathDistance) => TrianglePathway.MapPathDistanceToPoint(pathDistance);

    public float MapPointToPathDistance(Vector3 point) => TrianglePathway.MapPointToPathDistance(point);

    public struct Gateway(Vector3 a, Vector3 b)
    {
        public readonly Vector3 A = a;
        public readonly Vector3 B = b;
    }
}
