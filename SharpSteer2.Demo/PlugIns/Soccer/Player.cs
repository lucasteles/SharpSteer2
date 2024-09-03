// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Soccer;

public class Player : SimpleVehicle
{
    Trail trail;

    public override float MaxForce => 3000.7f;
    public override float MaxSpeed => 10;

    // constructor
    public Player(List<Player> others, List<Player> allplayers, Ball ball, bool isTeamA, int id,
        IAnnotationService annotations = null)
        : base(annotations)
    {
        allPlayers = allplayers;
        this.ball = ball;
        imTeamA = isTeamA;
        myId = id;

        Reset();
    }

    // reset state
    public override void Reset()
    {
        base.Reset(); // reset the vehicle
        Speed = 0.0f; // speed along Forward direction.

        // Place me on my part of the field, looking at oponnents goal
        Position = new(imTeamA ? RandomHelpers.Random() * 20 : -RandomHelpers.Random() * 20, 0,
            (RandomHelpers.Random() - 0.5f) * 20);
        if (myId < 9)
        {
            Position = imTeamA
                ? Globals.PlayerPosition[myId]
                : new(-Globals.PlayerPosition[myId].X, Globals.PlayerPosition[myId].Y, Globals.PlayerPosition[myId].Z);
        }

        home = Position;

        if (trail == null) trail = new(10, 60);
        trail.Clear(); // prevent long streaks due to teleportation
    }

    // per frame simulation update
    public void Update(float elapsedTime)
    {
        // if I hit the ball, kick it.
        var distToBall = Vector3.Distance(Position, ball.Position);
        var sumOfRadii = Radius + ball.Radius;
        if (distToBall < sumOfRadii)
            ball.Kick((ball.Position - Position) * 50);

        // otherwise consider avoiding collisions with others
        var collisionAvoidance = SteerToAvoidNeighbors(1, allPlayers);
        if (collisionAvoidance != Vector3.Zero)
            ApplySteeringForce(collisionAvoidance, elapsedTime);
        else
        {
            var distHomeToBall = Vector3.Distance(home, ball.Position);
            if (distHomeToBall < 12)
            {
                // go for ball if I'm on the 'right' side of the ball
                if (imTeamA ? Position.X > ball.Position.X : Position.X < ball.Position.X)
                {
                    var seekTarget = SteerForSeek(ball.Position);
                    ApplySteeringForce(seekTarget, elapsedTime);
                }
                else
                {
                    if (distHomeToBall < 12)
                    {
                        var z = ball.Position.Z - Position.Z > 0 ? -1.0f : 1.0f;
                        var behindBall = ball.Position + (imTeamA ? new(2, 0, z) : new Vector3(-2, 0, z));
                        var behindBallForce = SteerForSeek(behindBall);
                        Annotation.Line(Position, behindBall, Color.Green.ToVector3().ToNumerics());
                        var evadeTarget = SteerForFlee(ball.Position);
                        ApplySteeringForce(behindBallForce * 10 + evadeTarget, elapsedTime);
                    }
                }
            }
            else // Go home
            {
                var seekTarget = SteerForSeek(home);
                var seekHome = SteerForSeek(home);
                ApplySteeringForce(seekTarget + seekHome, elapsedTime);
            }
        }
    }

    // draw this character/vehicle into the scene
    public void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, imTeamA ? Color.Red : Color.Blue);
        trail.Draw(Annotation);
    }

    // per-instance reference to its group
    readonly List<Player> allPlayers;
    readonly Ball ball;
    readonly bool imTeamA;
    readonly int myId;
    Vector3 home;
}
