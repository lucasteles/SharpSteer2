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
using SharpSteer2;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Boids
{ // spherical obstacle group

	public class Boid : SimpleVehicle
	{
	    private readonly Trail _trail;

	    private const float AVOIDANCE_PREDICT_TIME_MIN = 0.9f;
		public const float AVOIDANCE_PREDICT_TIME_MAX = 2;
		public static float AvoidancePredictTime = AVOIDANCE_PREDICT_TIME_MIN;

		// a pointer to this boid's interface object for the proximity database
	    private ITokenForProximityDatabase<IVehicle> _proximityToken;

		// allocate one and share amoung instances just to save memory usage
		// (change to per-instance allocation to be more MP-safe)
	    private static readonly List<IVehicle> _neighbors = new List<IVehicle>();
		public static int BoundaryCondition = 0;
		public const float WORLD_RADIUS = 50;

        public override float MaxForce { get { return 27; } }
        public override float MaxSpeed { get { return 9; } }

		// constructor
		public Boid(IProximityDatabase<IVehicle> pd, IAnnotationService annotations = null)
            :base(annotations)
		{
			// allocate a token for this boid in the proximity database
			_proximityToken = null;
			NewPD(pd);

		    _trail = new Trail(2f, 60);

			// reset all boid state
			Reset();
		}

		// reset state
		public override void Reset()
		{
			// reset the vehicle
			base.Reset();

			// initial slow speed
			Speed = (MaxSpeed * 0.3f);

			// randomize initial orientation
			//RegenerateOrthonormalBasisUF(Vector3Helpers.RandomUnitVector());
			Vector3 d = Vector3Helpers.RandomUnitVector();
			d.X = Math.Abs(d.X);
			d.Y = 0;
			d.Z = Math.Abs(d.Z);
			RegenerateOrthonormalBasisUF(d);

			// randomize initial position
			Position = Vector3.UnitX * 10 + (Vector3Helpers.RandomVectorInUnitRadiusSphere() * 20);

			// notify proximity database that our position has changed
			//FIXME: SimpleVehicle::SimpleVehicle() calls reset() before proximityToken is set
			if (_proximityToken != null) _proximityToken.UpdateForNewPosition(Position);
		}

		// draw this boid into the scene
		public void Draw()
		{
		    _trail.Draw(annotation);

			Drawing.DrawBasic3dSphericalVehicle(this, Color.LightGray);
		}

		// per frame simulation update
		public void Update(float currentTime, float elapsedTime)
		{
		    _trail.Record(currentTime, Position);

			// steer to flock and perhaps to stay within the spherical boundary
		    ApplySteeringForce(SteerToFlock() + HandleBoundary(), elapsedTime);

			// notify proximity database that our position has changed
			_proximityToken.UpdateForNewPosition(Position);
		}

		// basic flocking
	    private Vector3 SteerToFlock()
		{
			const float SEPARATION_RADIUS = 5.0f;
			const float SEPARATION_ANGLE = -0.707f;
			const float SEPARATION_WEIGHT = 12.0f;

			const float ALIGNMENT_RADIUS = 7.5f;
			const float ALIGNMENT_ANGLE = 0.7f;
			const float ALIGNMENT_WEIGHT = 8.0f;

			const float COHESION_RADIUS = 9.0f;
			const float COHESION_ANGLE = -0.15f;
			const float COHESION_WEIGHT = 8.0f;

			float maxRadius = Math.Max(SEPARATION_RADIUS, Math.Max(ALIGNMENT_RADIUS, COHESION_RADIUS));

			// find all flockmates within maxRadius using proximity database
			_neighbors.Clear();
			_proximityToken.FindNeighbors(Position, maxRadius, _neighbors);

			// determine each of the three component behaviors of flocking
			Vector3 separation = SteerForSeparation(SEPARATION_RADIUS, SEPARATION_ANGLE, _neighbors);
			Vector3 alignment = SteerForAlignment(ALIGNMENT_RADIUS, ALIGNMENT_ANGLE, _neighbors);
			Vector3 cohesion = SteerForCohesion(COHESION_RADIUS, COHESION_ANGLE, _neighbors);

			// apply weights to components (save in variables for annotation)
			Vector3 separationW = separation * SEPARATION_WEIGHT;
			Vector3 alignmentW = alignment * ALIGNMENT_WEIGHT;
			Vector3 cohesionW = cohesion * COHESION_WEIGHT;

			Vector3 avoidance = SteerToAvoidObstacles(AVOIDANCE_PREDICT_TIME_MIN, AllObstacles);

			// saved for annotation
			bool avoiding = (avoidance != Vector3.Zero);
			Vector3 steer = separationW + alignmentW + cohesionW;

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
	    private Vector3 HandleBoundary()
		{
			// while inside the sphere do noting
			if (Position.Length() < WORLD_RADIUS)
				return Vector3.Zero;

			// once outside, select strategy
			switch (BoundaryCondition)
			{
			case 0:
				{
					// steer back when outside
					Vector3 seek = SteerForSeek(Vector3.Zero);
                    Vector3 lateral = Vector3Helpers.PerpendicularComponent(seek, Forward);
                    return lateral;
				}
			case 1:
				{
					// wrap around (teleport)
                    Position = (Position.SphericalWrapAround(Vector3.Zero, WORLD_RADIUS));
					return Vector3.Zero;
				}
			}
			return Vector3.Zero; // should not reach here
		}

		// make boids "bank" as they fly
	    protected override void RegenerateLocalSpace(Vector3 newVelocity, float elapsedTime)
		{
			RegenerateLocalSpaceForBanking(newVelocity, elapsedTime);
		}

		// switch to new proximity database -- just for demo purposes
		public void NewPD(IProximityDatabase<IVehicle> pd)
		{
			// delete this boid's token in the old proximity database
			if (_proximityToken != null)
			{
				_proximityToken.Dispose();
				_proximityToken = null;
			}

			// allocate a token for this boid in the proximity database
			_proximityToken = pd.AllocateToken(this);
		}

		// cycle through various boundary conditions
		public static void NextBoundaryCondition()
		{
			const int MAX = 2;
			BoundaryCondition = (BoundaryCondition + 1) % MAX;
		}

		// dynamic obstacle registry
		public static void InitializeObstacles()
		{
			// start with 40% of possible obstacles
			if (_obstacleCount == -1)
			{
				_obstacleCount = 0;
				for (int i = 0; i < (MAX_OBSTACLE_COUNT * 1.0); i++)
					AddOneObstacle();
			}
		}

	    private static void AddOneObstacle()
		{
	        if (_obstacleCount >= MAX_OBSTACLE_COUNT)
	            return;

	        // pick a random center and radius,
	        // loop until no overlap with other obstacles and the home base
	        //float r = 15;
	        //Vector3 c = Vector3.Up * r * (-0.5f * maxObstacleCount + obstacleCount);
	        float r = RandomHelpers.Random(0.5f, 2);
	        Vector3 c = Vector3Helpers.RandomVectorInUnitRadiusSphere() * WORLD_RADIUS * 1.1f;

	        // add new non-overlapping obstacle to registry
	        AllObstacles.Add(new SphericalObstacle(r, c));
	        _obstacleCount++;
		}

		public static void RemoveOneObstacle()
		{
			if (_obstacleCount > 0)
			{
				_obstacleCount--;
				AllObstacles.RemoveAt(_obstacleCount);
			}
		}

		public float MinDistanceToObstacle(Vector3 point)
		{
			const float R = 0;
			Vector3 c = point;
		    return AllObstacles.Aggregate(float.MaxValue, (current, obstacle) => TestOneObstacleOverlap(current, R, obstacle.Radius, c, obstacle.Center));
		}

		static float TestOneObstacleOverlap(float minClearance, float r, float radius, Vector3 c, Vector3 center)
		{
			float d = Vector3.Distance(c, center);
			float clearance = d - (r + radius);
			if (minClearance > clearance)
				minClearance = clearance;
			return minClearance;
		}

	    private static int _obstacleCount = -1;
	    private const int MAX_OBSTACLE_COUNT = 100;
		public static readonly List<SphericalObstacle> AllObstacles = new List<SphericalObstacle>();
	}
}
