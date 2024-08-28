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
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Helpers;
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Ctf;
// spherical obstacle group

public class CtfBase : SimpleVehicle
{
    protected readonly CtfPlugIn plugin;
    readonly float baseRadius;

    protected Trail trail;

    public override float MaxForce => 3;
    public override float MaxSpeed => 3;

    // constructor
    protected CtfBase(CtfPlugIn plugin, IAnnotationService annotations = null, float baseRadius = 1.5f)
        :base(annotations)
    {
        this.plugin = plugin;
        this.baseRadius = baseRadius;

        Reset();
    }

    // reset state
    public override void Reset()
    {
        base.Reset();  // reset the vehicle

        Speed = 3;             // speed along Forward direction.

        avoiding = false;         // not actively avoiding

        RandomizeStartingPositionAndHeading();  // new starting position

        trail = new();
        trail.Clear();     // prevent long streaks due to teleportation
    }

    // draw this character/vehicle into the scene
    public virtual void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, bodyColor);
        trail.Draw(Annotation);
    }

    // annotate when actively avoiding obstacles
    // xxx perhaps this should be a call to a general purpose annotation
    // xxx for "local xxx axis aligned box in XZ plane" -- same code in in
    // xxx Pedestrian.cpp
    public void AnnotateAvoidObstacle(float minDistanceToCollision)
    {
        var boxSide = Side * Radius;
        var boxFront = Forward * minDistanceToCollision;
        var fr = Position + boxFront - boxSide;
        var fl = Position + boxFront + boxSide;
        var br = Position - boxSide;
        var bl = Position + boxSide;
        Annotation.Line(fr, fl, Color.White.ToVector3().FromXna());
        Annotation.Line(fl, bl, Color.White.ToVector3().FromXna());
        Annotation.Line(bl, br, Color.White.ToVector3().FromXna());
        Annotation.Line(br, fr, Color.White.ToVector3().FromXna());
    }

    public void DrawHomeBase()
    {
        var up = new Vector3(0, 0.01f, 0);
        var atColor = new Color((byte)(255.0f * 0.3f), (byte)(255.0f * 0.3f), (byte)(255.0f * 0.5f));
        var noColor = Color.Gray;
        var reached = plugin.CtfSeeker.State == SeekerState.AtGoal;
        var baseColor = reached ? atColor : noColor;
        Drawing.DrawXzDisk(baseRadius, Globals.HomeBaseCenter, baseColor, 40);
        Drawing.DrawXzDisk(baseRadius / 15, Globals.HomeBaseCenter + up, Color.Black, 20);
    }

    void RandomizeStartingPositionAndHeading()
    {
        // randomize position on a ring between inner and outer radii
        // centered around the home base
        var rRadius = RandomHelpers.Random(Globals.MinStartRadius, Globals.MaxStartRadius);
        var randomOnRing = Vector3Helpers.RandomUnitVectorOnXZPlane() * rRadius;
        Position = Globals.HomeBaseCenter + randomOnRing;

        // are we are too close to an obstacle?
        if (MinDistanceToObstacle(Position) < Radius * 5)
        {
            // if so, retry the randomization (this recursive call may not return
            // if there is too little free space)
            RandomizeStartingPositionAndHeading();
        }
        else
        {
            // otherwise, if the position is OK, randomize 2D heading
            RandomizeHeadingOnXZPlane();
        }
    }

    public enum SeekerState
    {
        Running,
        Tagged,
        AtGoal
    }

    // for draw method
    protected Color bodyColor;

    // xxx store steer sub-state for anotation
    protected bool avoiding;

    // dynamic obstacle registry
    public static void InitializeObstacles(float radius, int obstacles)
    {
        // start with 40% of possible obstacles
        if (obstacleCount == -1)
        {
            obstacleCount = 0;
            for (var i = 0; i < obstacles; i++)
                AddOneObstacle(radius);
        }
    }

    public static void AddOneObstacle(float radius)
    {
        // pick a random center and radius,
        // loop until no overlap with other obstacles and the home base
        float r;
        Vector3 c;
        float minClearance;
        var requiredClearance = Globals.Seeker.Radius * 4; // 2 x diameter
        do
        {
            r = RandomHelpers.Random(1.5f, 4);
            c = Vector3Helpers.RandomVectorOnUnitRadiusXZDisk() * Globals.MaxStartRadius * 1.1f;
            minClearance = AllObstacles.Aggregate(float.MaxValue, (current, t) => TestOneObstacleOverlap(current, r, t.Radius, c, t.Center));

            minClearance = TestOneObstacleOverlap(minClearance, r, radius - requiredClearance, c, Globals.HomeBaseCenter);
        }
        while (minClearance < requiredClearance);

        // add new non-overlapping obstacle to registry
        AllObstacles.Add(new(r, c));
        obstacleCount++;
    }

    public static void RemoveOneObstacle()
    {
        if (obstacleCount <= 0)
            return;

        obstacleCount--;
        AllObstacles.RemoveAt(obstacleCount);
    }

    static float MinDistanceToObstacle(Vector3 point)
    {
        const float r = 0;
        var c = point;
        return AllObstacles.Aggregate(float.MaxValue, (current, t) => TestOneObstacleOverlap(current, r, t.Radius, c, t.Center));
    }

    static float TestOneObstacleOverlap(float minClearance, float r, float radius, Vector3 c, Vector3 center)
    {
        var d = Vector3.Distance(c, center);
        var clearance = d - (r + radius);
        if (minClearance > clearance) minClearance = clearance;
        return minClearance;
    }

    protected static int obstacleCount = -1;
    public static readonly List<SphericalObstacle> AllObstacles = new();
}
