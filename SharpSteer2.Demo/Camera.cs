// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System;
using System.Numerics;
using SharpSteer2.Helpers;

namespace SharpSteer2.Demo;

public class Camera : LocalSpace
{
    // camera mode selection
    public enum CameraMode
    {
        // marks beginning of list
        StartMode,

        // fixed global position and aimpoint
        Fixed,

        // camera position is directly above (in global Up/Y) target
        // camera up direction is target's forward direction
        StraightDown,

        // look at subject vehicle, adjusting camera position to be a
        // constant distance from the subject
        FixedDistanceOffset,

        // camera looks at subject vehicle from a fixed offset in the
        // local space of the vehicle (as if attached to the vehicle)
        FixedLocalOffset,

        // camera looks in the vehicle's forward direction, camera
        // position has a fixed local offset from the vehicle.
        OffsetPov,

        // marks the end of the list for cycling (to cmStartMode+1)
        EndMode
    }

    // xxx since currently (10-21-02) the camera's Forward and Side basis
    // xxx vectors are not being set, construct a temporary local space for
    // xxx the camera view -- so as not to make the camera behave
    // xxx differently (which is to say, correctly) during mouse adjustment.
    LocalSpace ls;
    public LocalSpace Xxxls()
    {
        ls.RegenerateOrthonormalBasis(Target - Position, Up);
        return ls;
    }

    // "look at" point, center of view
    public Vector3 Target;

    // vehicle being tracked
    public IVehicle VehicleToTrack;

    // aim at predicted position of vehicleToTrack, this far into thefuture
    public float AimLeadTime;
    bool smoothNextMove;
    float smoothMoveSpeed;

    // current mode for this camera instance
    public CameraMode Mode;

    // "static" camera mode parameters
    public Vector3 FixedPosition;
    public Vector3 FixedTarget;
    public Vector3 FixedUp;

    // "constant distance from vehicle" camera mode parameters
    public float FixedDistanceDistance;        // desired distance from it
    public float FixedDistanceVerticalOffset;  // fixed vertical offset from it

    // "look straight down at vehicle" camera mode parameters
    public float LookDownDistance;             // fixed vertical offset from it

    // "fixed local offset" camera mode parameters
    Vector3 fixedLocalOffset;

    // "offset POV" camera mode parameters
    public Vector3 PovOffset;

    // constructor
    public Camera() => Reset();

    // reset all camera state to default values
    public void Reset()
    {
        // reset camera's position and orientation
        ResetLocalSpace();

        ls = new();

        // "look at" point, center of view
        Target = Vector3.Zero;

        // vehicle being tracked
        VehicleToTrack = null;

        // aim at predicted position of vehicleToTrack, this far into thefuture
        AimLeadTime = 1;

        // make first update abrupt
        smoothNextMove = false;

        // relative rate at which camera transitions proceed
        smoothMoveSpeed = 1.5f;

        // select camera aiming mode
        Mode = CameraMode.Fixed;

        // "constant distance from vehicle" camera mode parameters
        FixedDistanceDistance = 1;
        FixedDistanceVerticalOffset = 0;

        // "look straight down at vehicle" camera mode parameters
        LookDownDistance = 30;

        // "static" camera mode parameters
        FixedPosition = new(75, 75, 75);
        FixedTarget = Vector3.Zero;
        FixedUp = Vector3.UnitY;

        // "fixed local offset" camera mode parameters
        fixedLocalOffset = new(5, 5, -5);

        // "offset POV" camera mode parameters
        PovOffset = new(0, 1, -3);
    }

    // per frame simulation update
    public void Update(float elapsedTime, bool simulationPaused = false)
    {
        // vehicle being tracked (just a reference with a more concise name)
        var v = VehicleToTrack;
        var noVehicle = VehicleToTrack == null;

        // new position/target/up, set in switch below, defaults to current
        var newPosition = Position;
        var newTarget = Target;
        var newUp = Up;

        // prediction time to compensate for lag caused by smoothing moves
        var antiLagTime = simulationPaused ? 0 : 1 / smoothMoveSpeed;

        // aim at a predicted future position of the target vehicle
        var predictionTime = AimLeadTime + antiLagTime;

        // set new position/target/up according to camera aim mode
        switch (Mode)
        {
            case CameraMode.Fixed:
                newPosition = FixedPosition;
                newTarget = FixedTarget;
                newUp = FixedUp;
                break;

            case CameraMode.FixedDistanceOffset:
                if (noVehicle) break;
                newUp = Vector3.UnitY; // xxx maybe this should be v.up ?
                newTarget = v.PredictFuturePosition(predictionTime);
                newPosition = ConstantDistanceHelper();
                break;

            case CameraMode.StraightDown:
                if (noVehicle) break;
                newUp = v.Forward;
                newTarget = v.PredictFuturePosition(predictionTime);
                newPosition = newTarget;
                newPosition.Y += LookDownDistance;
                break;

            case CameraMode.FixedLocalOffset:
                if (noVehicle) break;
                newUp = v.Up;
                newTarget = v.PredictFuturePosition(predictionTime);
                newPosition = v.GlobalizePosition(fixedLocalOffset);
                break;

            case CameraMode.OffsetPov:
            {
                if (noVehicle) break;
                newUp = v.Up;
                var futurePosition = v.PredictFuturePosition(antiLagTime);
                var globalOffset = v.GlobalizeDirection(PovOffset);
                newPosition = futurePosition + globalOffset;
                // XXX hack to improve smoothing between modes (no effect on aim)
                const float l = 10;
                newTarget = newPosition + (v.Forward * l);
                break;
            }
        }

        // blend from current position/target/up towards new values
        SmoothCameraMove(newPosition, newTarget, newUp, elapsedTime);

        // set camera in draw module
        //FIXME: drawCameraLookAt(position(), target, up());
    }

    // helper function for "drag behind" mode
    Vector3 ConstantDistanceHelper()
    {
        // is the "global up"/"vertical" offset constraint enabled?  (it forces
        // the camera's global-up (Y) cordinate to be a above/below the target
        // vehicle by a given offset.)
// ReSharper disable CompareOfFloatsByEqualityOperator
        var constrainUp = FixedDistanceVerticalOffset != 0;
// ReSharper restore CompareOfFloatsByEqualityOperator

        // vector offset from target to current camera position
        var adjustedPosition = new Vector3(Position.X, constrainUp ? Target.Y : Position.Y, Position.Z);
        var offset = adjustedPosition - Target;

        // current distance between them
        var distance = offset.Length();

        // move camera only when geometry is well-defined (avoid degenerate case)
// ReSharper disable CompareOfFloatsByEqualityOperator
        if (distance == 0)
// ReSharper restore CompareOfFloatsByEqualityOperator
        {
            return Position;
        }

        // unit vector along original offset
        var unitOffset = offset / distance;

        // new offset of length XXX
        var xxxDistance = (float)Math.Sqrt(FixedDistanceDistance * FixedDistanceDistance - FixedDistanceVerticalOffset * FixedDistanceVerticalOffset);
        var newOffset = unitOffset * xxxDistance;

        // return new camera position: adjust distance to target
        return Target + newOffset + new Vector3(0, FixedDistanceVerticalOffset, 0);
    }

    // Smoothly move camera ...
    void SmoothCameraMove(Vector3 newPosition, Vector3 newTarget, Vector3 newUp, float elapsedTime)
    {
        if (smoothNextMove)
        {
            var smoothRate = elapsedTime * smoothMoveSpeed;

            var tempPosition = Position;
            var tempUp = Up;
            Utilities.BlendIntoAccumulator(smoothRate, newPosition, ref tempPosition);
            Utilities.BlendIntoAccumulator(smoothRate, newTarget, ref Target);
            Utilities.BlendIntoAccumulator(smoothRate, newUp, ref tempUp);
            Position = tempPosition;
            Up = tempUp;

            // xxx not sure if these are needed, seems like a good idea
            // xxx (also if either up or oldUP are zero, use the other?)
            // xxx (even better: force up to be perp to target-position axis))
            Up = Up == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(Up);
        }
        else
        {
            smoothNextMove = true;

            Position = newPosition;
            Target = newTarget;
            Up = newUp;
        }
    }

    public void DoNotSmoothNextMove() => smoothNextMove = false;

    // adjust the offset vector of the current camera mode based on a
    // "mouse adjustment vector" from OpenSteerDemo (xxx experiment 10-17-02)
    public void MouseAdjustOffset(Vector3 adjustment)
    {
        // vehicle being tracked (just a reference with a more concise name)
        var v = VehicleToTrack;

        switch (Mode)
        {
            case CameraMode.Fixed:
            {
                var offset = FixedPosition - FixedTarget;
                var adjusted = MouseAdjustPolar(adjustment, offset);
                FixedPosition = FixedTarget + adjusted;
                break;
            }
            case CameraMode.FixedDistanceOffset:
            {
                // XXX this is the oddball case, adjusting "position" instead
                // XXX of mode parameters, hence no smoothing during adjustment
                // XXX Plus the fixedDistVOffset feature complicates things
                var offset = Position - Target;
                var adjusted = MouseAdjustPolar(adjustment, offset);
                // XXX --------------------------------------------------
                //position = target + adjusted;
                //fixedDistDistance = adjusted.length();
                //fixedDistVOffset = position.y - target.y;
                // XXX --------------------------------------------------
                //const float s = smoothMoveSpeed * (1.0f/40f);
                //const Vector3 newPosition = target + adjusted;
                //position = interpolate (s, position, newPosition);
                //fixedDistDistance = interpolate (s, fixedDistDistance, adjusted.length());
                //fixedDistVOffset = interpolate (s, fixedDistVOffset, position.y - target.y);
                // XXX --------------------------------------------------
                //position = target + adjusted;
                Position = Target + adjusted;
                FixedDistanceDistance = adjusted.Length();
                //fixedDistVOffset = position.y - target.y;
                FixedDistanceVerticalOffset = Position.Y - Target.Y;
                // XXX --------------------------------------------------
                break;
            }
            case CameraMode.StraightDown:
            {
                var offset = new Vector3(0, 0, LookDownDistance);
                var adjusted = MouseAdjustPolar(adjustment, offset);
                LookDownDistance = adjusted.Z;
                break;
            }
            case CameraMode.FixedLocalOffset:
            {
                var offset = v.GlobalizeDirection(fixedLocalOffset);
                var adjusted = MouseAdjustPolar(adjustment, offset);
                fixedLocalOffset = v.LocalizeDirection(adjusted);
                break;
            }
            case CameraMode.OffsetPov:
            {
                // XXX this might work better as a translation control, it is
                // XXX non-obvious using a polar adjustment when the view
                // XXX center is not at the camera aim target
                var offset = v.GlobalizeDirection(PovOffset);
                var adjusted = MouseAdjustOrtho(adjustment, offset);
                PovOffset = v.LocalizeDirection(adjusted);
                break;
            }
        }
    }

    Vector3 MouseAdjust2(bool polar, Vector3 adjustment, Vector3 offsetToAdjust)
    {
        // value to be returned
        var result = offsetToAdjust;

        // using the camera's side/up axes (essentially: screen space) move the
        // offset vector sideways according to adjustment.x and vertically
        // according to adjustment.y, constrain the offset vector's length to
        // stay the same, hence the offset's "tip" stays on the surface of a
        // sphere.
        var oldLength = result.Length();
        var rate = polar ? oldLength : 1;
        result += Xxxls().Side * (adjustment.X * rate);
        result += Xxxls().Up * (adjustment.Y * rate);
        if (polar)
        {
            var newLength = result.Length();
            result *= oldLength / newLength;
        }

        // change the length of the offset vector according to adjustment.z
        if (polar)
            result *= 1 + adjustment.Z;
        else
            result += Xxxls().Forward * adjustment.Z;

        return result;
    }

    Vector3 MouseAdjustPolar(Vector3 adjustment, Vector3 offsetToAdjust) => MouseAdjust2(true, adjustment, offsetToAdjust);

    Vector3 MouseAdjustOrtho(Vector3 adjustment, Vector3 offsetToAdjust) => MouseAdjust2(false, adjustment, offsetToAdjust);

    // string naming current camera mode, used by OpenSteerDemo
    public string ModeName
    {
        get
        {
            switch (Mode)
            {
                case CameraMode.Fixed:
                    return "static";
                case CameraMode.FixedDistanceOffset:
                    return "fixed distance offset";
                case CameraMode.FixedLocalOffset:
                    return "fixed local offset";
                case CameraMode.OffsetPov:
                    return "offset POV";
                case CameraMode.StraightDown:
                    return "straight down";
                default:
                    return "unknown";
            }
        }
    }

    // select next camera mode, used by OpenSteerDemo
    public void SelectNextMode()
    {
        Mode = SuccessorMode(Mode);
        if (Mode >= CameraMode.EndMode) Mode = SuccessorMode(CameraMode.StartMode);
    }

    // the mode that comes after the given mode (used by selectNextMode)
    static CameraMode SuccessorMode(CameraMode cm) => (CameraMode)((int)cm + 1);
}