// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Text;
using Microsoft.Xna.Framework;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

// ReSharper disable PossiblyImpureMethodCallOnReadonlyVariable

namespace SharpSteer2.Demo.PlugIns.Ctf;

public class CtfSeeker : CtfBase
{
    readonly bool arrive;

    // constructor
    public CtfSeeker(CtfPlugIn plugin, IAnnotationService annotations = null, bool arrive = false)
        : base(plugin, annotations)
    {
        this.arrive = arrive;
        Reset();
    }

    // reset state
    public override void Reset()
    {
        base.Reset();
        bodyColor = new((byte)(255.0f * 0.4f), (byte)(255.0f * 0.4f), (byte)(255.0f * 0.6f)); // blueish
        Globals.Seeker = this;
        State = SeekerState.Running;
        evading = false;
    }

    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        // do behavioral state transitions, as needed
        UpdateState(currentTime);

        // determine and apply steering/braking forces
        var steer = Vector3.Zero;
        if (State == SeekerState.Running)
        {
            steer = SteeringForSeeker();
        }
        else
        {
            ApplyBrakingForce(Globals.BrakingRate, elapsedTime);
        }

        ApplySteeringForce(steer, elapsedTime);

        // annotation
        Annotation.VelocityAcceleration(this);
        trail.Record(currentTime, Position);
    }

    // is there a clear path to the goal?
    bool IsPathToGoalClear()
    {
        var sideThreshold = Radius * 8.0f;
        var behindThreshold = Radius * 2.0f;

        var goalOffset = Globals.HomeBaseCenter - Position;
        var goalDistance = goalOffset.Length();
        var goalDirection = goalOffset / goalDistance;

        var goalIsAside = this.IsAside(Globals.HomeBaseCenter, 0.5f);

        // for annotation: loop over all and save result, instead of early return
        var xxxReturn = true;

        // loop over enemies
        foreach (var e in plugin.CtfEnemies)
        {
            var eDistance = Vector3.Distance(Position, e.Position);
            var timeEstimate = 0.3f * eDistance / e.Speed; //xxx
            var eFuture = e.PredictFuturePosition(timeEstimate);
            var eOffset = eFuture - Position;
            var alongCorridor = Vector3.Dot(goalDirection, eOffset);
            var inCorridor = alongCorridor > -behindThreshold && alongCorridor < goalDistance;
            var eForwardDistance = Vector3.Dot(Forward, eOffset);

            // xxx temp move this up before the conditionals
            // ReSharper disable once PossiblyImpureMethodCallOnReadonlyVariable
            Annotation.CircleXZ(e.Radius, eFuture, Globals.ClearPathColor.ToVector3().ToNumerics(), 20); //xxx

            // consider as potential blocker if within the corridor
            if (inCorridor)
            {
                var perp = eOffset - (goalDirection * alongCorridor);
                var acrossCorridor = perp.Length();
                if (acrossCorridor < sideThreshold)
                {
                    // not a blocker if behind us and we are perp to corridor
                    var eFront = eForwardDistance + e.Radius;

                    //annotation.annotationLine (position, forward*eFront, gGreen); // xxx
                    //annotation.annotationLine (e.position, forward*eFront, gGreen); // xxx

                    // xxx
                    // std::ostringstream message;
                    // message << "eFront = " << std::setprecision(2)
                    //         << std::setiosflags(std::ios::fixed) << eFront << std::ends;
                    // draw2dTextAt3dLocation (*message.str(), eFuture, gWhite);

                    var eIsBehind = eFront < -behindThreshold;
                    var eIsWayBehind = eFront < -2 * behindThreshold;
                    var safeToTurnTowardsGoal = (eIsBehind && goalIsAside) || eIsWayBehind;

                    if (!safeToTurnTowardsGoal)
                    {
                        // this enemy blocks the path to the goal, so return false
                        Annotation.Line(Position, e.Position,
                            Globals.ClearPathColor.ToVector3().ToNumerics());
                        // return false;
                        xxxReturn = false;
                    }
                }
            }
        }

        // no enemies found along path, return true to indicate path is clear
        // clearPathAnnotation (sideThreshold, behindThreshold, goalDirection);
        // return true;
        //if (xxxReturn)
        ClearPathAnnotation(sideThreshold, behindThreshold, goalDirection);
        return xxxReturn;
    }

    Vector3 SteeringForSeeker()
    {
        // determine if obstacle avodiance is needed
        var clearPath = IsPathToGoalClear();
        AdjustObstacleAvoidanceLookAhead(clearPath);
        var obstacleAvoidance = SteerToAvoidObstacles(Globals.AvoidancePredictTime, AllObstacles);

        // saved for annotation
        avoiding = obstacleAvoidance != Vector3.Zero;

        if (avoiding)
        {
            // use pure obstacle avoidance if needed
            return obstacleAvoidance;
        }

        // otherwise seek home base and perhaps evade defenders
        var seek = !arrive
            ? SteerForSeek(Globals.HomeBaseCenter)
            : this.SteerForArrival(Globals.HomeBaseCenter, MaxSpeed, 10, Annotation);


        if (clearPath)
        {
            // we have a clear path (defender-free corridor), use pure seek

            Annotation.Line(Position, Position + (seek * 0.2f), Globals.SeekColor.ToVector3().ToNumerics());
            return seek;
        }

        var evade = XxxSteerToEvadeAllDefenders();
        var steer = (seek + evade).LimitMaxDeviationAngle(0.707f, Forward);

        Annotation.Line(Position, Position + seek, Color.Red.ToVector3().ToNumerics());
        Annotation.Line(Position, Position + evade, Color.Green.ToVector3().ToNumerics());

        // annotation: show evasion steering force
        Annotation.Line(Position, Position + (steer * 0.2f), Globals.EvadeColor.ToVector3().ToNumerics());
        return steer;
    }

    void UpdateState(float currentTime)
    {
        // if we reach the goal before being tagged, switch to atGoal state
        if (State == SeekerState.Running)
        {
            var baseDistance = Vector3.Distance(Position, Globals.HomeBaseCenter);
            if (baseDistance < Radius + plugin.BaseRadius) State = SeekerState.AtGoal;
        }

        // update lastRunningTime (holds off reset time)
        if (State == SeekerState.Running)
        {
            lastRunningTime = currentTime;
        }
        else
        {
            const float resetDelay = 4;
            var resetTime = lastRunningTime + resetDelay;
            if (currentTime > resetTime)
            {
                // xxx a royal hack (should do this internal to CTF):
                GameDemo.QueueDelayedResetPlugInXxx();
            }
        }
    }

    public override void Draw()
    {
        // first call the draw method in the base class
        base.Draw();

        // select string describing current seeker state
        var seekerStateString = "";
        switch (State)
        {
            case SeekerState.Running:
                if (avoiding)
                    seekerStateString = "avoid obstacle";
                else if (evading)
                    seekerStateString = "seek and evade";
                else
                    seekerStateString = "seek goal";
                break;
            case SeekerState.Tagged:
                seekerStateString = "tagged";
                break;
            case SeekerState.AtGoal:
                seekerStateString = "reached goal";
                break;
        }

        // annote seeker with its state as text
        var textOrigin = Position + new Vector3(0, 0.25f, 0);
        var annote = new StringBuilder();
        annote.Append(seekerStateString);
        annote.AppendFormat("\n{0:0.00}", Speed);
        Drawing.Draw2dTextAt3dLocation(annote.ToString(), textOrigin, Color.White);

        // display status in the upper left corner of the window
        var status = new StringBuilder();
        status.Append(seekerStateString);
        status.AppendFormat("\n{0} obstacles [F1/F2]", obstacleCount);
        status.AppendFormat("\n{0} restarts", Globals.ResetCount);
        var screenLocation = new Vector3(15, 50, 0);
        Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, Color.LightGray);
    }

    public Vector3 SteerToEvadeAllDefenders()
    {
        var evade = Vector3.Zero;
        var goalDistance = Vector3.Distance(Globals.HomeBaseCenter, Position);

        // sum up weighted evasion
        foreach (var e in plugin.CtfEnemies)
        {
            var eOffset = e.Position - Position;
            var eDistance = eOffset.Length();

            var eForwardDistance = Vector3.Dot(Forward, eOffset);
            var behindThreshold = Radius * 2;
            var behind = eForwardDistance < behindThreshold;
            if (!behind || eDistance < 5)
            {
                if (eDistance < goalDistance * 1.2) //xxx
                {
                    // const float timeEstimate = 0.5f * eDistance / e.speed;//xxx
                    var timeEstimate = 0.15f * eDistance / e.Speed; //xxx
                    var future = e.PredictFuturePosition(timeEstimate);

                    Annotation.CircleXZ(e.Radius, future, Globals.EvadeColor.ToVector3().ToNumerics(),
                        20); // xxx

                    var offset = future - Position;
                    var lateral = Vector3Helpers.PerpendicularComponent(offset, Forward);
                    var d = lateral.Length();
                    var weight = -1000 / (d * d);
                    evade += lateral / d * weight;
                }
            }
        }

        return evade;
    }

    Vector3 XxxSteerToEvadeAllDefenders()
    {
        // sum up weighted evasion
        var evade = Vector3.Zero;
        foreach (var e in plugin.CtfEnemies)
        {
            var eOffset = e.Position - Position;
            var eDistance = eOffset.Length();

            // xxx maybe this should take into account e's heading? xxx
            var timeEstimate = 0.5f * eDistance / e.Speed; //xxx
            var eFuture = e.PredictFuturePosition(timeEstimate);

            // annotation
            Annotation.CircleXZ(e.Radius, eFuture, Globals.EvadeColor.ToVector3().ToNumerics(), 20);

            // steering to flee from eFuture (enemy's future position)
            var flee = SteerForFlee(eFuture);

            var eForwardDistance = Vector3.Dot(Forward, eOffset);
            var behindThreshold = Radius * -2;

            var distanceWeight = 4 / eDistance;
            var forwardWeight = eForwardDistance > behindThreshold ? 1.0f : 0.5f;

            var adjustedFlee = flee * distanceWeight * forwardWeight;

            evade += adjustedFlee;
        }

        return evade;
    }

    void AdjustObstacleAvoidanceLookAhead(bool clearPath)
    {
        if (clearPath)
        {
            evading = false;
            var goalDistance = Vector3.Distance(Globals.HomeBaseCenter, Position);
            var headingTowardGoal = this.IsAhead(Globals.HomeBaseCenter, 0.98f);
            var isNear = goalDistance / Speed < Globals.AvoidancePredictTimeMax;
            var useMax = headingTowardGoal && !isNear;
            Globals.AvoidancePredictTime = useMax ? Globals.AvoidancePredictTimeMax : Globals.AvoidancePredictTimeMin;
        }
        else
        {
            evading = true;
            Globals.AvoidancePredictTime = Globals.AvoidancePredictTimeMin;
        }
    }

    void ClearPathAnnotation(float sideThreshold, float behindThreshold, Vector3 goalDirection)
    {
        var behindBack = Forward * -behindThreshold;
        var pbb = Position + behindBack;
        var gun = this.LocalRotateForwardToSide(goalDirection);
        var gn = gun * sideThreshold;
        var hbc = Globals.HomeBaseCenter;
        Annotation.Line(pbb + gn, hbc + gn, Globals.ClearPathColor.ToVector3().ToNumerics());
        Annotation.Line(pbb - gn, hbc - gn, Globals.ClearPathColor.ToVector3().ToNumerics());
        Annotation.Line(hbc - gn, hbc + gn, Globals.ClearPathColor.ToVector3().ToNumerics());
        Annotation.Line(pbb - gn, pbb + gn, Globals.ClearPathColor.ToVector3().ToNumerics());
        //annotation.AnnotationLine(pbb - behindSide, pbb + behindSide, Globals.clearPathColor);
    }

    public SeekerState State;
    bool evading; // xxx store steer sub-state for anotation
    float lastRunningTime; // for auto-reset
}
