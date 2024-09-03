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
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Pedestrian;

public class Pedestrian : SimpleVehicle
{
    Trail trail;

    public override float MaxForce => 16;
    public override float MaxSpeed => 2;

    // called when steerToFollowPath decides steering is required
    public void AnnotatePathFollowing(Vector3 future, Vector3 onPath, Vector3 target, float outside)
    {
        var yellow = Color.Yellow;
        var lightOrange = new Color((byte)(255.0f * 1.0f), (byte)(255.0f * 0.5f), 0);
        var darkOrange = new Color((byte)(255.0f * 0.6f), (byte)(255.0f * 0.3f), 0);

        // draw line from our position to our predicted future position
        Annotation.Line(Position, future, yellow.ToVector3().ToNumerics());

        // draw line from our position to our steering target on the path
        Annotation.Line(Position, target, Color.Orange.ToVector3().ToNumerics());

        // draw a two-toned line between the future test point and its
        // projection onto the path, the change from dark to light color
        // indicates the boundary of the tube.
        var boundaryOffset = Vector3.Normalize(onPath - future);
        boundaryOffset *= outside;
        var onPathBoundary = future + boundaryOffset;
        Annotation.Line(onPath, onPathBoundary, darkOrange.ToVector3().ToNumerics());
        Annotation.Line(onPathBoundary, future, lightOrange.ToVector3().ToNumerics());
    }

    // called when steerToAvoidCloseNeighbors decides steering is required
    public void AnnotateAvoidCloseNeighbor(IVehicle other)
    {
        // draw the word "Ouch!" above colliding vehicles
        var headOn = Vector3.Dot(Forward, other.Forward) < 0;
        var green = new Color((byte)(255.0f * 0.4f), (byte)(255.0f * 0.8f), (byte)(255.0f * 0.1f));
        var red = new Color((byte)(255.0f * 1), (byte)(255.0f * 0.1f), 0);
        var color = headOn ? red : green;
        var text = headOn ? "OUCH!" : "pardon me";
        var location = Position + new Vector3(0, 0.5f, 0);
        if (Annotation.IsEnabled)
            Drawing.Draw2dTextAt3dLocation(text, location, color);
    }

    public void AnnotateAvoidNeighbor(IVehicle threat, Vector3 ourFuture, Vector3 threatFuture)
    {
        var green = new Color((byte)(255.0f * 0.15f), (byte)(255.0f * 0.6f), 0);

        Annotation.Line(Position, ourFuture, green.ToVector3().ToNumerics());
        Annotation.Line(threat.Position, threatFuture, green.ToVector3().ToNumerics());
        Annotation.Line(ourFuture, threatFuture, Color.Red.ToVector3().ToNumerics());
        Annotation.CircleXZ(Radius, ourFuture, green.ToVector3().ToNumerics(), 12);
        Annotation.CircleXZ(Radius, threatFuture, green.ToVector3().ToNumerics(), 12);
    }

    // xxx perhaps this should be a call to a general purpose annotation for
    // xxx "local xxx axis aligned box in XZ plane" -- same code in in
    // xxx CaptureTheFlag.cpp
    public void AnnotateAvoidObstacle(float minDistanceToCollision)
    {
        var boxSide = Side * Radius;
        var boxFront = Forward * minDistanceToCollision;
        var fr = Position + boxFront - boxSide;
        var fl = Position + boxFront + boxSide;
        var br = Position - boxSide;
        var bl = Position + boxSide;
        Annotation.Line(fr, fl, Color.White.ToVector3().ToNumerics());
        Annotation.Line(fl, bl, Color.White.ToVector3().ToNumerics());
        Annotation.Line(bl, br, Color.White.ToVector3().ToNumerics());
        Annotation.Line(br, fr, Color.White.ToVector3().ToNumerics());
    }

    // constructor
    public Pedestrian(IProximityDatabase<IVehicle> pd, IAnnotationService annotations = null)
        :base(annotations)
    {
        // allocate a token for this boid in the proximity database
        proximityToken = null;
        NewPd(pd);

        // reset Pedestrian state
        Reset();
    }

    // reset all instance state
    public override void Reset()
    {
        // reset the vehicle
        base.Reset();

        // initially stopped
        Speed = 0;

        // size of bounding sphere, for obstacle avoidance, etc.
        Radius = 0.5f; // width = 0.7, add 0.3 margin, take half

        // set the path for this Pedestrian to follow
        path = Globals.GetTestPath();

        // set initial position
        // (random point on path + random horizontal offset)
        var d = path.TotalPathLength * RandomHelpers.Random();
        var r = path.Radius;
        var randomOffset = Vector3Helpers.RandomVectorOnUnitRadiusXZDisk() * r;
        Position = path.MapPathDistanceToPoint(d) + randomOffset;

        // randomize 2D heading
        RandomizeHeadingOnXZPlane();

        // pick a random direction for path following (upstream or downstream)
        pathDirection = RandomHelpers.Random() <= 0.5;

        // trail parameters: 3 seconds with 60 points along the trail
        trail = new(3, 60);

        // notify proximity database that our position has changed
        if (proximityToken is not null) proximityToken.UpdateForNewPosition(Position);
    }

    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        // apply steering force to our momentum
        ApplySteeringForce(DetermineCombinedSteering(elapsedTime), elapsedTime);

        // reverse direction when we reach an endpoint
        if (Globals.UseDirectedPathFollowing)
        {
            if (Vector3.Distance(Position, Globals.Endpoint0) < path.Radius)
            {
                pathDirection = true;
                Annotation.CircleXZ(path.Radius, Globals.Endpoint0, Color.DarkRed.ToVector3().ToNumerics(), 20);
            }
            if (Vector3.Distance(Position, Globals.Endpoint1) < path.Radius)
            {
                pathDirection = false;
                Annotation.CircleXZ(path.Radius, Globals.Endpoint1, Color.DarkRed.ToVector3().ToNumerics(), 20);
            }
        }

        // annotation
        Annotation.VelocityAcceleration(this, 5, 0);
        trail.Record(currentTime, Position);

        // notify proximity database that our position has changed
        proximityToken.UpdateForNewPosition(Position);
    }

    // compute combined steering force: move forward, avoid obstacles
    // or neighbors if needed, otherwise follow the path and wander
    Vector3 DetermineCombinedSteering(float elapsedTime)
    {
        // move forward
        var steeringForce = Forward;

        // probability that a lower priority behavior will be given a
        // chance to "drive" even if a higher priority behavior might
        // otherwise be triggered.
        const float leakThrough = 0.1f;

        // determine if obstacle avoidance is required
        var obstacleAvoidance = Vector3.Zero;
        if (leakThrough < RandomHelpers.Random())
        {
            const float oTime = 6; // minTimeToCollision = 6 seconds
            obstacleAvoidance = SteerToAvoidObstacles(oTime, Globals.Obstacles);
        }

        // if obstacle avoidance is needed, do it
        if (obstacleAvoidance != Vector3.Zero)
        {
            steeringForce += obstacleAvoidance;
        }
        else
        {
            // otherwise consider avoiding collisions with others
            var collisionAvoidance = Vector3.Zero;
            const float caLeadTime = 3;

            // find all neighbors within maxRadius using proximity database
            // (radius is largest distance between vehicles traveling head-on
            // where a collision is possible within caLeadTime seconds.)
            var maxRadius = caLeadTime * MaxSpeed * 2;
            neighbors.Clear();
            proximityToken.FindNeighbors(Position, maxRadius, neighbors);

            if (neighbors.Count > 0 && leakThrough < RandomHelpers.Random())
                collisionAvoidance = SteerToAvoidNeighbors(caLeadTime, neighbors) * 10;

            // if collision avoidance is needed, do it
            if (collisionAvoidance != Vector3.Zero)
            {
                steeringForce += collisionAvoidance;
            }
            else
            {
                // add in wander component (according to user switch)
                if (Globals.WanderSwitch)
                    steeringForce += SteerForWander(elapsedTime);

                // do (interactively) selected type of path following
                const float pfLeadTime = 3;
                var pathFollow =
                    Globals.UseDirectedPathFollowing ?
                        SteerToFollowPath(pathDirection, pfLeadTime, path) :
                        SteerToStayOnPath(pfLeadTime, path);

                // add in to steeringForce
                steeringForce += pathFollow * 0.5f;
            }
        }

        // return steering constrained to global XZ "ground" plane
        steeringForce.Y = 0;
        return steeringForce;
    }


    // draw this pedestrian into scene
    public void Draw()
    {
        Drawing.DrawBasic2dCircularVehicle(this, Color.Gray);
        trail.Draw(Annotation);
    }

    // switch to new proximity database -- just for demo purposes
    public void NewPd(IProximityDatabase<IVehicle> pd)
    {
        // delete this boid's token in the old proximity database
        if (proximityToken is not null)
        {
            proximityToken.Dispose();
            proximityToken = null;
        }

        // allocate a token for this boid in the proximity database
        proximityToken = pd.AllocateToken(this);
    }

    // a pointer to this boid's interface object for the proximity database
    ITokenForProximityDatabase<IVehicle> proximityToken;

    // allocate one and share amoung instances just to save memory usage
    // (change to per-instance allocation to be more MP-safe)
    static readonly List<IVehicle> neighbors = new();

    // path to be followed by this pedestrian
    // XXX Ideally this should be a generic Pathway, but we use the
    // XXX getTotalPathLength and radius methods (currently defined only
    // XXX on PolylinePathway) to set random initial positions.  Could
    // XXX there be a "random position inside path" method on Pathway?
    PolylinePathway path;

    // direction for path following (upstream or downstream)
    bool pathDirection;
}
