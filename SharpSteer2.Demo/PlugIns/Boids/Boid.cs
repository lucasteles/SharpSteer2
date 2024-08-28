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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Boids;
// spherical obstacle group

public class Boid : SimpleVehicle
{
    readonly Trail trail;

    const float AvoidancePredictTimeMin = 0.9f;
    public const float AvoidancePredictTimeMax = 2;
    public static float AvoidancePredictTime = AvoidancePredictTimeMin;

    // a pointer to this boid's interface object for the proximity database
    ITokenForProximityDatabase<IVehicle> proximityToken;

    // allocate one and share amoung instances just to save memory usage
    // (change to per-instance allocation to be more MP-safe)
    static readonly List<IVehicle> neighbors = new();
    public static int BoundaryCondition;
    public const float WorldRadius = 50;

    public override float MaxForce => 27;
    public override float MaxSpeed => 9;

    // constructor
    public Boid(IProximityDatabase<IVehicle> pd, IAnnotationService annotations = null)
        :base(annotations)
    {
        // allocate a token for this boid in the proximity database
        proximityToken = null;
        NewPd(pd);

        trail = new(2f, 60);

        // reset all boid state
        Reset();
    }

    // reset state
    public override void Reset()
    {
        // reset the vehicle
        base.Reset();

        // initial slow speed
        Speed = MaxSpeed * 0.3f;

        // randomize initial orientation
        //RegenerateOrthonormalBasisUF(Vector3Helpers.RandomUnitVector());
        var d = Vector3Helpers.RandomUnitVector();
        d.X = Math.Abs(d.X);
        d.Y = 0;
        d.Z = Math.Abs(d.Z);
        RegenerateOrthonormalBasisUF(d);

        // randomize initial position
        Position = Vector3.UnitX * 10 + (Vector3Helpers.RandomVectorInUnitRadiusSphere() * 20);

        // notify proximity database that our position has changed
        //FIXME: SimpleVehicle::SimpleVehicle() calls reset() before proximityToken is set
        if (proximityToken is not null) proximityToken.UpdateForNewPosition(Position);
    }

    // draw this boid into the scene
    public void Draw()
    {
        trail.Draw(Annotation);

        Drawing.DrawBasic3dSphericalVehicle(this, Color.LightGray);
    }

    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        trail.Record(currentTime, Position);

        // steer to flock and perhaps to stay within the spherical boundary
        ApplySteeringForce(SteerToFlock() + HandleBoundary(), elapsedTime);

        // notify proximity database that our position has changed
        proximityToken.UpdateForNewPosition(Position);
    }

    // basic flocking
    Vector3 SteerToFlock()
    {
        const float separationRadius = 5.0f;
        const float separationAngle = -0.707f;
        const float separationWeight = 12.0f;

        const float alignmentRadius = 7.5f;
        const float alignmentAngle = 0.7f;
        const float alignmentWeight = 8.0f;

        const float cohesionRadius = 9.0f;
        const float cohesionAngle = -0.15f;
        const float cohesionWeight = 8.0f;

        var maxRadius = Math.Max(separationRadius, Math.Max(alignmentRadius, cohesionRadius));

        // find all flockmates within maxRadius using proximity database
        neighbors.Clear();
        proximityToken.FindNeighbors(Position, maxRadius, neighbors);

        // determine each of the three component behaviors of flocking
        var separation = SteerForSeparation(separationRadius, separationAngle, neighbors);
        var alignment = SteerForAlignment(alignmentRadius, alignmentAngle, neighbors);
        var cohesion = SteerForCohesion(cohesionRadius, cohesionAngle, neighbors);

        // apply weights to components (save in variables for annotation)
        var separationW = separation * separationWeight;
        var alignmentW = alignment * alignmentWeight;
        var cohesionW = cohesion * cohesionWeight;

        var avoidance = SteerToAvoidObstacles(AvoidancePredictTimeMin, AllObstacles);

        // saved for annotation
        var avoiding = avoidance != Vector3.Zero;
        var steer = separationW + alignmentW + cohesionW;

        if (avoiding)
            steer = avoidance;
#if IGNORED
			// annotation
			const float s = 0.1f;
			AnnotationLine(Position, Position + (separationW * s), Color.Red);
			AnnotationLine(Position, Position + (alignmentW * s), Color.Orange);
			AnnotationLine(Position, Position + (cohesionW * s), Color.Yellow);
#endif
        return steer;
    }

    // Take action to stay within sphereical boundary.  Returns steering
    // value (which is normally zero) and may take other side-effecting
    // actions such as kinematically changing the Boid's position.
    Vector3 HandleBoundary()
    {
        // while inside the sphere do noting
        if (Position.Length() < WorldRadius)
            return Vector3.Zero;

        // once outside, select strategy
        switch (BoundaryCondition)
        {
            case 0:
            {
                // steer back when outside
                var seek = SteerForSeek(Vector3.Zero);
                var lateral = Vector3Helpers.PerpendicularComponent(seek, Forward);
                return lateral;
            }
            case 1:
            {
                // wrap around (teleport)
                Position = Position.SphericalWrapAround(Vector3.Zero, WorldRadius);
                return Vector3.Zero;
            }
        }
        return Vector3.Zero; // should not reach here
    }

    // make boids "bank" as they fly
    protected override void RegenerateLocalSpace(Vector3 newVelocity, float elapsedTime) => RegenerateLocalSpaceForBanking(newVelocity, elapsedTime);

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

    // cycle through various boundary conditions
    public static void NextBoundaryCondition()
    {
        const int max = 2;
        BoundaryCondition = (BoundaryCondition + 1) % max;
    }

    // dynamic obstacle registry
    public static void InitializeObstacles()
    {
        // start with 40% of possible obstacles
        if (obstacleCount == -1)
        {
            obstacleCount = 0;
            for (var i = 0; i < MaxObstacleCount * 1.0; i++)
                AddOneObstacle();
        }
    }

    static void AddOneObstacle()
    {
        if (obstacleCount >= MaxObstacleCount)
            return;

        // pick a random center and radius,
        // loop until no overlap with other obstacles and the home base
        //float r = 15;
        //Vector3 c = Vector3.Up * r * (-0.5f * maxObstacleCount + obstacleCount);
        var r = RandomHelpers.Random(0.5f, 2);
        var c = Vector3Helpers.RandomVectorInUnitRadiusSphere() * WorldRadius * 1.1f;

        // add new non-overlapping obstacle to registry
        AllObstacles.Add(new(r, c));
        obstacleCount++;
    }

    public static void RemoveOneObstacle()
    {
        if (obstacleCount > 0)
        {
            obstacleCount--;
            AllObstacles.RemoveAt(obstacleCount);
        }
    }

    public float MinDistanceToObstacle(Vector3 point)
    {
        const float r = 0;
        var c = point;
        return AllObstacles.Aggregate(float.MaxValue, (current, obstacle) => TestOneObstacleOverlap(current, r, obstacle.Radius, c, obstacle.Center));
    }

    static float TestOneObstacleOverlap(float minClearance, float r, float radius, Vector3 c, Vector3 center)
    {
        var d = Vector3.Distance(c, center);
        var clearance = d - (r + radius);
        if (minClearance > clearance)
            minClearance = clearance;
        return minClearance;
    }

    static int obstacleCount = -1;
    const int MaxObstacleCount = 100;
    public static readonly List<SphericalObstacle> AllObstacles = new();
}
