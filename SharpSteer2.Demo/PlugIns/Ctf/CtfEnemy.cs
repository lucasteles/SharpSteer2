// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Ctf;

public class CtfEnemy : CtfBase
{
    // constructor
    public CtfEnemy(CtfPlugIn plugin, IAnnotationService annotations = null)
        : base(plugin, annotations) =>
        Reset();

    // reset state
    public override void Reset()
    {
        base.Reset();
        bodyColor = new((byte)(255.0f * 0.6f), (byte)(255.0f * 0.4f), (byte)(255.0f * 0.4f)); // redish
    }

    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        // determine upper bound for pursuit prediction time
        var seekerToGoalDist = Vector3.Distance(Globals.HomeBaseCenter, Globals.Seeker.Position);
        var adjustedDistance = seekerToGoalDist - Radius - plugin.BaseRadius;
        var seekerToGoalTime = adjustedDistance < 0 ? 0 : adjustedDistance / Globals.Seeker.Speed;
        var maxPredictionTime = seekerToGoalTime * 0.9f;

        // determine steering (pursuit, obstacle avoidance, or braking)
        var steer = Vector3.Zero;
        if (Globals.Seeker.State == SeekerState.Running)
        {
            var avoidance = SteerToAvoidObstacles(Globals.AvoidancePredictTimeMin, AllObstacles);

            // saved for annotation
            avoiding = avoidance == Vector3.Zero;

            steer = avoiding ? SteerForPursuit(Globals.Seeker, maxPredictionTime) : avoidance;
        }
        else
        {
            ApplyBrakingForce(Globals.BrakingRate, elapsedTime);
        }

        ApplySteeringForce(steer, elapsedTime);

        // annotation
        Annotation.VelocityAcceleration(this);
        trail.Record(currentTime, Position);

        // detect and record interceptions ("tags") of seeker
        var seekerToMeDist = Vector3.Distance(Position, Globals.Seeker.Position);
        var sumOfRadii = Radius + Globals.Seeker.Radius;
        if (seekerToMeDist < sumOfRadii)
        {
            if (Globals.Seeker.State == SeekerState.Running) Globals.Seeker.State = SeekerState.Tagged;

            // annotation:
            if (Globals.Seeker.State == SeekerState.Tagged)
            {
                var color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.5f));
                Annotation.DiskXZ(sumOfRadii, (Position + Globals.Seeker.Position) / 2, color.ToVector3().ToNumerics(),
                    20);
            }
        }
    }
}
