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

namespace SharpSteer2.Demo.PlugIns.Soccer;

public class Ball : SimpleVehicle
{
    Trail trail;

    public override float MaxForce => 9;
    public override float MaxSpeed => 9;

    public Ball(AabBox bbox, IAnnotationService annotations = null)
        :base(annotations)
    {
        mBbox = bbox;
        Reset();
    }

    // reset state
    public override void Reset()
    {
        base.Reset(); // reset the vehicle
        Speed = 0.0f;         // speed along Forward direction.

        Position = new(0, 0, 0);
        if (trail == null) trail = new(100, 6000);
        trail.Clear();    // prevent long streaks due to teleportation
    }

    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        ApplyBrakingForce(1.5f, elapsedTime);
        ApplySteeringForce(Velocity, elapsedTime);
        // are we now outside the field?
        if (!mBbox.IsInsideX(Position))
        {
            var d = Velocity;
            RegenerateOrthonormalBasis(new(-d.X, d.Y, d.Z));
            ApplySteeringForce(Velocity, elapsedTime);
        }
        if (!mBbox.IsInsideZ(Position))
        {
            var d = Velocity;
            RegenerateOrthonormalBasis(new(d.X, d.Y, -d.Z));
            ApplySteeringForce(Velocity, elapsedTime);
        }
        trail.Record(currentTime, Position);
    }

    public void Kick(Vector3 dir)
    {
        Speed = dir.Length();
        RegenerateOrthonormalBasis(dir);
    }

    // draw this character/vehicle into the scene
    public void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, Color.Green);
        trail.Draw(Annotation);
    }

    readonly AabBox mBbox;
}
