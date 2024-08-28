namespace SharpSteer2.Helpers;

public static class LocalSpaceBasisHelpers
{
    /// <summary>
    /// Transforms a direction in global space to its equivalent in local space.
    /// </summary>
    /// <param name="basis">The basis which this should operate on</param>
    /// <param name="globalDirection">The global space direction to transform.</param>
    /// <returns>The global space direction transformed to local space .</returns>
    public static Vector3 LocalizeDirection(this ILocalSpaceBasis basis, Vector3 globalDirection) =>
        // dot offset with local basis vectors to obtain local coordiantes
        new(Vector3.Dot(globalDirection, basis.Side), Vector3.Dot(globalDirection, basis.Up), Vector3.Dot(globalDirection, basis.Forward));

    /// <summary>
    /// Transforms a point in global space to its equivalent in local space.
    /// </summary>
    /// <param name="basis">The basis which this should operate on</param>
    /// <param name="globalPosition">The global space position to transform.</param>
    /// <returns>The global space position transformed to local space.</returns>
    public static Vector3 LocalizePosition(this ILocalSpaceBasis basis, Vector3 globalPosition)
    {
        // global offset from local origin
        Vector3 globalOffset = globalPosition - basis.Position;

        // dot offset with local basis vectors to obtain local coordiantes
        return LocalizeDirection(basis, globalOffset);
    }

    /// <summary>
    /// Transforms a point in local space to its equivalent in global space.
    /// </summary>
    /// <param name="basis">The basis which this should operate on</param>
    /// <param name="localPosition">The local space position to tranform.</param>
    /// <returns>The local space position transformed to global space.</returns>
    public static Vector3 GlobalizePosition(this ILocalSpaceBasis basis, Vector3 localPosition) => basis.Position + GlobalizeDirection(basis, localPosition);

    /// <summary>
    /// Transforms a direction in local space to its equivalent in global space.
    /// </summary>
    /// <param name="basis">The basis which this should operate on</param>
    /// <param name="localDirection">The local space direction to tranform.</param>
    /// <returns>The local space direction transformed to global space</returns>
    public static Vector3 GlobalizeDirection(this ILocalSpaceBasis basis, Vector3 localDirection) =>
        ((basis.Side * localDirection.X) +
         (basis.Up * localDirection.Y) +
         (basis.Forward * localDirection.Z));

    /// <summary>
    /// Rotates, in the canonical direction, a vector pointing in the
    /// "forward" (+Z) direction to the "side" (+/-X) direction as implied
    /// by IsRightHanded.
    /// </summary>
    /// <param name="basis">The basis which this should operate on</param>
    /// <param name="value">The local space vector.</param>
    /// <returns>The rotated vector.</returns>
    public static Vector3 LocalRotateForwardToSide(this ILocalSpaceBasis basis, Vector3 value) => new(-value.Z, value.Y, value.X);

    public static void ResetLocalSpace(out Vector3 forward, out Vector3 side, out Vector3 up, out Vector3 position)
    {
        forward = -Vector3.UnitZ;
        side = Vector3.UnitX;
        up = Vector3.UnitY;
        position = Vector3.Zero;
    }

    /// <summary>
    /// set "side" basis vector to normalized cross product of forward and up
    /// </summary>
    /// <param name="forward"></param>
    /// <param name="side"></param>
    /// <param name="up"></param>
    public static void SetUnitSideFromForwardAndUp(ref Vector3 forward, out Vector3 side, ref Vector3 up) =>
        // derive new unit side basis vector from forward and up
        side = Vector3.Normalize(Vector3.Cross(forward, up));

    /// <summary>
    /// regenerate the orthonormal basis vectors given a new forward
    /// (which is expected to have unit length)
    /// </summary>
    /// <param name="newUnitForward"></param>
    /// <param name="forward"></param>
    /// <param name="side"></param>
    /// <param name="up"></param>
    public static void RegenerateOrthonormalBasisUF(Vector3 newUnitForward, out Vector3 forward, out Vector3 side, ref Vector3 up)
    {
        forward = newUnitForward;

        // derive new side basis vector from NEW forward and OLD up
        SetUnitSideFromForwardAndUp(ref forward, out side, ref up);

        // derive new Up basis vector from new Side and new Forward
        //(should have unit length since Side and Forward are
        // perpendicular and unit length)
        up = Vector3.Cross(side, forward);
    }

    /// <summary>
    /// for when the new forward is NOT know to have unit length
    /// </summary>
    /// <param name="newForward"></param>
    /// <param name="forward"></param>
    /// <param name="side"></param>
    /// <param name="up"></param>
    public static void RegenerateOrthonormalBasis(Vector3 newForward, out Vector3 forward, out Vector3 side, ref Vector3 up) => RegenerateOrthonormalBasisUF(Vector3.Normalize(newForward), out forward, out side, ref up);

    /// <summary>
    /// for supplying both a new forward and and new up
    /// </summary>
    /// <param name="newForward"></param>
    /// <param name="newUp"></param>
    /// <param name="forward"></param>
    /// <param name="side"></param>
    /// <param name="up"></param>
    public static void RegenerateOrthonormalBasis(Vector3 newForward, Vector3 newUp, out Vector3 forward, out Vector3 side, out Vector3 up)
    {
        up = newUp;
        RegenerateOrthonormalBasis(Vector3.Normalize(newForward), out forward, out side, ref up);
    }

    public static Matrix4x4 ToMatrix(this ILocalSpaceBasis basis) => ToMatrix(basis.Forward, basis.Side, basis.Up, basis.Position);

    public static Matrix4x4 ToMatrix(Vector3 forward, Vector3 side, Vector3 up, Vector3 position)
    {
        Matrix4x4 m = Matrix4x4.Identity;
        m.Translation = position;
        MatrixHelpers.Right(ref m, ref side);
        MatrixHelpers.Up(ref m, ref up);
        MatrixHelpers.Right(ref m, ref forward);

        return m;
    }

    public static void FromMatrix(Matrix4x4 transformation, out Vector3 forward, out Vector3 side, out Vector3 up, out Vector3 position)
    {
        position = transformation.Translation;
        side = MatrixHelpers.Right(ref transformation);
        up = MatrixHelpers.Up(ref transformation);
        forward = MatrixHelpers.Backward(ref transformation);
    }
}