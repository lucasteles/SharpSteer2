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

namespace SharpSteer2.Demo.PlugIns.LowSpeedTurn;

public class LowSpeedTurn : SimpleVehicle
{
    Trail trail;

    public override float MaxForce => 0.3f;
    public override float MaxSpeed => 1.5f;

    // constructor
    public LowSpeedTurn(IAnnotationService annotations = null)
        :base(annotations) =>
        Reset();

    // reset state
    public override void Reset()
    {
        // reset vehicle state
        base.Reset();

        // speed along Forward direction.
        Speed = startSpeed;

        // initial position along X axis
        Position = new(startX, 0, 0);

        // for next instance: step starting location
        startX += 2;

        // for next instance: step speed
        startSpeed += 0.15f;

        // 15 seconds and 150 points along the trail
        trail = new(15, 150);
    }

    // draw into the scene
    public void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, Color.Gray);
        trail.Draw(Annotation);
    }

    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        ApplySteeringForce(Steering, elapsedTime);

        // annotation
        Annotation.VelocityAcceleration(this);
        trail.Record(currentTime, Position);
    }

    // reset starting positions
    public static void ResetStarts()
    {
        startX = 0;
        startSpeed = 0;
    }

    // constant steering force
    static Vector3 Steering => new(1, 0, -1);

    // for stepping the starting conditions for next vehicle
    static float startX;
    static float startSpeed;
}
