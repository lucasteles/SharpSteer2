using SharpSteer2.Helpers;

namespace SharpSteer2.Pathway;

/// <summary>
///     A pathway made out of triangular segments
/// </summary>
public class TrianglePathway
    : IPathway
{
    readonly Triangle[] path;

    public TrianglePathway(IEnumerable<Triangle> path, bool cyclic = false)
    {
        this.path = path.ToArray();

        //Calculate center points
        for (var i = 0; i < this.path.Length; i++)
            this.path[i].PointOnPath = ((2 * this.path[i].A) + this.path[i].Edge0) / 2;

        //Calculate tangents along path
        for (var i = 0; i < this.path.Length; i++)
        {
            var bIndex = cyclic ? (i + 1) % this.path.Length : Math.Min(i + 1, this.path.Length - 1);

            var vectorToNextTriangle = this.path[bIndex].PointOnPath - this.path[i].PointOnPath;
            var l = vectorToNextTriangle.Length();

            this.path[i].Tangent = vectorToNextTriangle / l;

            if (Math.Abs(l) < float.Epsilon)
                this.path[i].Tangent = Vector3.Zero;
        }

        Centerline = new(this.path.Select(a => a.PointOnPath).ToArray(), 0.1f, cyclic);
    }

    public IEnumerable<Triangle> Triangles => path;
    public PolylinePathway Centerline { get; }

    public Vector3 MapPointToPath(Vector3 point, out Vector3 tangent, out float outside)
    {
        int index;
        return MapPointToPath(point, out tangent, out outside, out index);
    }

    public Vector3 MapPathDistanceToPoint(float pathDistance) => Centerline.MapPathDistanceToPoint(pathDistance);

    //// clip or wrap given path distance according to cyclic flag
    //if (_cyclic)
    //    pathDistance = pathDistance % _totalPathLength;
    //else
    //{
    //    if (pathDistance < 0)
    //        return _path[0].PointOnPath;
    //    if (pathDistance >= _totalPathLength)
    //        return _path[_path.Length - 1].PointOnPath;
    //}
    //// step through segments, subtracting off segment lengths until
    //// locating the segment that contains the original pathDistance.
    //// Interpolate along that segment to find 3d point value to return.
    //for (int i = 1; i < _path.Length; i++)
    //{
    //    if (_path[i].Length < pathDistance)
    //    {
    //        pathDistance -= _path[i].Length;
    //    }
    //    else
    //    {
    //        float ratio = pathDistance / _path[i].Length;
    //        var l = Vector3.Lerp(_path[i].PointOnPath, _path[i].PointOnPath + _path[i].Tangent * _path[i].Length, ratio);
    //        return l;
    //    }
    //}
    //return Vector3.Zero;
    public float MapPointToPathDistance(Vector3 point) => Centerline.MapPointToPathDistance(point);

    Vector3 MapPointToPath(Vector3 point, out Vector3 tangent, out float outside, out int segmentIndex)
    {
        var distanceSqr = float.PositiveInfinity;
        var closestPoint = Vector3.Zero;
        var inside = false;
        segmentIndex = -1;

        for (var i = 0; i < path.Length; i++)
        {
            bool isInside;
            var p = ClosestPointOnTriangle(ref path[i], point, out isInside);

            var normal = point - p;
            var dSqr = normal.LengthSquared();

            if (dSqr < distanceSqr)
            {
                distanceSqr = dSqr;
                closestPoint = p;
                inside = isInside;
                segmentIndex = i;
            }

            if (isInside)
                break;
        }

        if (segmentIndex == -1)
            throw new InvalidOperationException("Closest Path Segment Not Found (Zero Length Path?");

        tangent = path[segmentIndex].Tangent;
        outside = (float)Math.Sqrt(distanceSqr) * (inside ? -1 : 1);
        return closestPoint;
    }

    static Vector3 ClosestPointOnTriangle(ref Triangle triangle, Vector3 sourcePosition, out bool inside)
    {
        float a, b;
        return ClosestPointOnTriangle(ref triangle, sourcePosition, out a, out b, out inside);
    }

    internal static Vector3 ClosestPointOnTriangle(ref Triangle triangle, Vector3 sourcePosition,
        out float edge0Distance, out float edge1Distance, out bool inside)
    {
        var v0 = triangle.A - sourcePosition;

        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        var a = triangle.Edge0.LengthSquared();
        var b = Vector3.Dot(triangle.Edge0, triangle.Edge1);
        // ReSharper disable once ImpureMethodCallOnReadonlyValueField
        var c = triangle.Edge1.LengthSquared();
        var d = Vector3.Dot(triangle.Edge0, v0);
        var e = Vector3.Dot(triangle.Edge1, v0);

        var det = triangle.Determinant;
        var s = (b * e) - (c * d);
        var t = (b * d) - (a * e);

        inside = false;
        if (s + t < det)
        {
            if (s < 0)
            {
                if (t < 0)
                {
                    if (d < 0)
                    {
                        s = Utilities.Clamp(-d / a, 0, 1);
                        t = 0;
                    }
                    else
                    {
                        s = 0;
                        t = Utilities.Clamp(-e / c, 0, 1);
                    }
                }
                else
                {
                    s = 0;
                    t = Utilities.Clamp(-e / c, 0, 1);
                }
            }
            else if (t < 0)
            {
                s = Utilities.Clamp(-d / a, 0, 1);
                t = 0;
            }
            else
            {
                var invDet = 1 / det;
                s *= invDet;
                t *= invDet;
                inside = true;
            }
        }
        else
        {
            if (s < 0)
            {
                var tmp0 = b + d;
                var tmp1 = c + e;
                if (tmp1 > tmp0)
                {
                    var numer = tmp1 - tmp0;
                    var denom = a - (2 * b) + c;
                    s = Utilities.Clamp(numer / denom, 0, 1);
                    t = 1 - s;
                }
                else
                {
                    t = Utilities.Clamp(-e / c, 0, 1);
                    s = 0;
                }
            }
            else if (t < 0)
            {
                if (a + d > b + e)
                {
                    var numer = c + e - b - d;
                    var denom = a - (2 * b) + c;
                    s = Utilities.Clamp(numer / denom, 0, 1);
                    t = 1 - s;
                }
                else
                {
                    s = Utilities.Clamp(-e / c, 0, 1);
                    t = 0;
                }
            }
            else
            {
                var numer = c + e - b - d;
                var denom = a - (2 * b) + c;
                s = Utilities.Clamp(numer / denom, 0, 1);
                t = 1 - s;
            }
        }

        edge0Distance = s;
        edge1Distance = t;
        return triangle.A + (s * triangle.Edge0) + (t * triangle.Edge1);
    }

    public struct Triangle
    {
        public readonly Vector3 A;
        public readonly Vector3 Edge0;
        public readonly Vector3 Edge1;

        internal Vector3 Tangent;
        internal Vector3 PointOnPath;

        internal readonly float Determinant;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            Edge0 = b - a;
            Edge1 = c - a;

            PointOnPath = Vector3.Zero;
            Tangent = Vector3.Zero;

            // ReSharper disable once ImpureMethodCallOnReadonlyValueField
            var edge0LengthSquared = Edge0.LengthSquared();

            var edge0DotEdge1 = Vector3.Dot(Edge0, Edge1);
            var edge1LengthSquared = Vector3.Dot(Edge1, Edge1);

            Determinant = (edge0LengthSquared * edge1LengthSquared) - (edge0DotEdge1 * edge0DotEdge1);
        }
    }
}
