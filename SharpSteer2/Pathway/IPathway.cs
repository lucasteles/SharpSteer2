namespace SharpSteer2.Pathway;

public interface IPathway
{
    /// <summary>
    ///     Given an arbitrary point ("A"), returns the nearest point ("P") on
    ///     this path.  Also returns, via output arguments, the path tangent at
    ///     P and a measure of how far A is outside the Pathway's "tube".  Note
    ///     that a negative distance indicates A is inside the Pathway.
    /// </summary>
    /// <param name="point"></param>
    /// <param name="tangent"></param>
    /// <param name="outside"></param>
    /// <returns></returns>
    Vector3 MapPointToPath(Vector3 point, out Vector3 tangent, out float outside);

    /// <summary>
    ///     given a distance along the path, convert it to a point on the path
    /// </summary>
    /// <param name="pathDistance"></param>
    /// <returns></returns>
    Vector3 MapPathDistanceToPoint(float pathDistance);

    /// <summary>
    ///     Given an arbitrary point, convert it to a distance along the path.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    float MapPointToPathDistance(Vector3 point);
}
