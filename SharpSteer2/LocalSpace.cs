// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using SharpSteer2.Helpers;

namespace SharpSteer2;

/// <summary>
///     transformation as three orthonormal unit basis vectors and the
///     origin of the local space.  These correspond to the "rows" of
///     a 3x4 transformation matrix with [0 0 0 1] as the final column
/// </summary>
public class LocalSpaceBasis
    : ILocalSpaceBasis
{
    protected Vector3 forwardField;

    protected Vector3 positionField;
    protected Vector3 sideField;

    protected Vector3 upField;

    /// <summary>
    ///     side-pointing unit basis vector
    /// </summary>
    public Vector3 Side
    {
        get => sideField;
        set => sideField = value;
    }

    /// <summary>
    ///     upward-pointing unit basis vector
    /// </summary>
    public Vector3 Up
    {
        get => upField;
        set => upField = value;
    }

    /// <summary>
    ///     forward-pointing unit basis vector
    /// </summary>
    public Vector3 Forward
    {
        get => forwardField;
        set => forwardField = value;
    }

    /// <summary>
    ///     origin of local space
    /// </summary>
    public Vector3 Position
    {
        get => positionField;
        set => positionField = value;
    }
}

/// <summary>
///     LocalSpaceMixin is a mixin layer, a class template with a paramterized base
///     class.  Allows "LocalSpace-ness" to be layered on any class.
/// </summary>
public class LocalSpace : LocalSpaceBasis
{
    public LocalSpace() => ResetLocalSpace();

    public LocalSpace(Vector3 up, Vector3 forward, Vector3 position)
    {
        Up = up;
        Forward = forward;
        Position = position;
        SetUnitSideFromForwardAndUp();
    }

    public LocalSpace(Matrix4x4 transformation) => LocalSpaceBasisHelpers.FromMatrix(transformation, out forwardField,
        out sideField, out upField, out positionField);

    // ------------------------------------------------------------------------
    // reset transform: set local space to its identity state, equivalent to a
    // 4x4 homogeneous transform like this:
    //
    //     [ X 0 0 0 ]
    //     [ 0 1 0 0 ]
    //     [ 0 0 1 0 ]
    //     [ 0 0 0 1 ]
    //
    // where X is 1 for a left-handed system and -1 for a right-handed system.
    public void ResetLocalSpace() =>
        LocalSpaceBasisHelpers.ResetLocalSpace(out forwardField, out sideField, out upField, out positionField);

    // ------------------------------------------------------------------------
    // set "side" basis vector to normalized cross product of forward and up
    public void SetUnitSideFromForwardAndUp() =>
        LocalSpaceBasisHelpers.SetUnitSideFromForwardAndUp(ref forwardField, out sideField, ref upField);

    // ------------------------------------------------------------------------
    // regenerate the orthonormal basis vectors given a new forward
    //(which is expected to have unit length)
    public void RegenerateOrthonormalBasisUF(Vector3 newUnitForward) =>
        LocalSpaceBasisHelpers.RegenerateOrthonormalBasisUF(newUnitForward, out forwardField, out sideField,
            ref upField);

    // for when the new forward is NOT know to have unit length
    public void RegenerateOrthonormalBasis(Vector3 newForward) =>
        LocalSpaceBasisHelpers.RegenerateOrthonormalBasis(newForward, out forwardField, out sideField, ref upField);

    // for supplying both a new forward and new up
    public void RegenerateOrthonormalBasis(Vector3 newForward, Vector3 newUp) =>
        LocalSpaceBasisHelpers.RegenerateOrthonormalBasis(newForward, newUp, out forwardField, out sideField,
            out upField);
}
