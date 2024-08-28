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
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Database;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Pedestrian
{
    public class Pedestrian : SimpleVehicle
	{
		Trail _trail;

        public override float MaxForce { get { return 16; } }
        public override float MaxSpeed { get { return 2; } }

		// called when steerToFollowPath decides steering is required
		public void AnnotatePathFollowing(Vector3 future, Vector3 onPath, Vector3 target, float outside)
		{
			Color yellow = Color.Yellow;
			Color lightOrange = new Color((byte)(255.0f * 1.0f), (byte)(255.0f * 0.5f), 0);
			Color darkOrange = new Color((byte)(255.0f * 0.6f), (byte)(255.0f * 0.3f), 0);

			// draw line from our position to our predicted future position
            annotation.Line(Position, future, yellow.ToVector3().FromXna());

			// draw line from our position to our steering target on the path
            annotation.Line(Position, target, Color.Orange.ToVector3().FromXna());

			// draw a two-toned line between the future test point and its
			// projection onto the path, the change from dark to light color
			// indicates the boundary of the tube.
            Vector3 boundaryOffset = Vector3.Normalize(onPath - future);
            boundaryOffset *= outside;
			Vector3 onPathBoundary = future + boundaryOffset;
            annotation.Line(onPath, onPathBoundary, darkOrange.ToVector3().FromXna());
            annotation.Line(onPathBoundary, future, lightOrange.ToVector3().FromXna());
		}

		// called when steerToAvoidCloseNeighbors decides steering is required
		public void AnnotateAvoidCloseNeighbor(IVehicle other)
		{
			// draw the word "Ouch!" above colliding vehicles
            bool headOn = Vector3.Dot(Forward, other.Forward) < 0;
			Color green = new Color((byte)(255.0f * 0.4f), (byte)(255.0f * 0.8f), (byte)(255.0f * 0.1f));
			Color red = new Color((byte)(255.0f * 1), (byte)(255.0f * 0.1f), 0);
			Color color = headOn ? red : green;
			String text = headOn ? "OUCH!" : "pardon me";
			Vector3 location = Position + new Vector3(0, 0.5f, 0);
			if (annotation.IsEnabled)
				Drawing.Draw2dTextAt3dLocation(text, location, color);
		}

		public void AnnotateAvoidNeighbor(IVehicle threat, Vector3 ourFuture, Vector3 threatFuture)
		{
			Color green = new Color((byte)(255.0f * 0.15f), (byte)(255.0f * 0.6f), 0);

            annotation.Line(Position, ourFuture, green.ToVector3().FromXna());
            annotation.Line(threat.Position, threatFuture, green.ToVector3().FromXna());
            annotation.Line(ourFuture, threatFuture, Color.Red.ToVector3().FromXna());
            annotation.CircleXZ(Radius, ourFuture, green.ToVector3().FromXna(), 12);
            annotation.CircleXZ(Radius, threatFuture, green.ToVector3().FromXna(), 12);
		}

		// xxx perhaps this should be a call to a general purpose annotation for
		// xxx "local xxx axis aligned box in XZ plane" -- same code in in
		// xxx CaptureTheFlag.cpp
		public void AnnotateAvoidObstacle(float minDistanceToCollision)
		{
			Vector3 boxSide = Side * Radius;
			Vector3 boxFront = Forward * minDistanceToCollision;
			Vector3 fr = Position + boxFront - boxSide;
			Vector3 fl = Position + boxFront + boxSide;
			Vector3 br = Position - boxSide;
			Vector3 bl = Position + boxSide;
            annotation.Line(fr, fl, Color.White.ToVector3().FromXna());
            annotation.Line(fl, bl, Color.White.ToVector3().FromXna());
            annotation.Line(bl, br, Color.White.ToVector3().FromXna());
            annotation.Line(br, fr, Color.White.ToVector3().FromXna());
		}

		// constructor
        public Pedestrian(IProximityDatabase<IVehicle> pd, IAnnotationService annotations = null)
            :base(annotations)
		{
			// allocate a token for this boid in the proximity database
			_proximityToken = null;
			NewPD(pd);

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
			_path = Globals.GetTestPath();

			// set initial position
			// (random point on path + random horizontal offset)
			float d = _path.TotalPathLength * RandomHelpers.Random();
			float r = _path.Radius;
			Vector3 randomOffset = Vector3Helpers.RandomVectorOnUnitRadiusXZDisk() * r;
			Position = (_path.MapPathDistanceToPoint(d) + randomOffset);

			// randomize 2D heading
			RandomizeHeadingOnXZPlane();

			// pick a random direction for path following (upstream or downstream)
			_pathDirection = RandomHelpers.Random() <= 0.5;

			// trail parameters: 3 seconds with 60 points along the trail
			_trail = new Trail(3, 60);

			// notify proximity database that our position has changed
			if (_proximityToken != null) _proximityToken.UpdateForNewPosition(Position);
		}

		// per frame simulation update
		public void Update(float currentTime, float elapsedTime)
		{
			// apply steering force to our momentum
			ApplySteeringForce(DetermineCombinedSteering(elapsedTime), elapsedTime);

			// reverse direction when we reach an endpoint
			if (Globals.UseDirectedPathFollowing)
			{
				if (Vector3.Distance(Position, Globals.Endpoint0) < _path.Radius)
				{
					_pathDirection = true;
                    annotation.CircleXZ(_path.Radius, Globals.Endpoint0, Color.DarkRed.ToVector3().FromXna(), 20);
				}
				if (Vector3.Distance(Position, Globals.Endpoint1) < _path.Radius)
				{
					_pathDirection = false;
                    annotation.CircleXZ(_path.Radius, Globals.Endpoint1, Color.DarkRed.ToVector3().FromXna(), 20);
				}
			}

			// annotation
			annotation.VelocityAcceleration(this, 5, 0);
			_trail.Record(currentTime, Position);

			// notify proximity database that our position has changed
			_proximityToken.UpdateForNewPosition(Position);
		}

		// compute combined steering force: move forward, avoid obstacles
		// or neighbors if needed, otherwise follow the path and wander
        private Vector3 DetermineCombinedSteering(float elapsedTime)
		{
			// move forward
			Vector3 steeringForce = Forward;

			// probability that a lower priority behavior will be given a
			// chance to "drive" even if a higher priority behavior might
			// otherwise be triggered.
			const float LEAK_THROUGH = 0.1f;

			// determine if obstacle avoidance is required
			Vector3 obstacleAvoidance = Vector3.Zero;
			if (LEAK_THROUGH < RandomHelpers.Random())
			{
				const float O_TIME = 6; // minTimeToCollision = 6 seconds
				obstacleAvoidance = SteerToAvoidObstacles(O_TIME, Globals.Obstacles);
			}

			// if obstacle avoidance is needed, do it
			if (obstacleAvoidance != Vector3.Zero)
			{
				steeringForce += obstacleAvoidance;
			}
			else
			{
				// otherwise consider avoiding collisions with others
				Vector3 collisionAvoidance = Vector3.Zero;
				const float CA_LEAD_TIME = 3;

				// find all neighbors within maxRadius using proximity database
				// (radius is largest distance between vehicles traveling head-on
				// where a collision is possible within caLeadTime seconds.)
				float maxRadius = CA_LEAD_TIME * MaxSpeed * 2;
				_neighbors.Clear();
				_proximityToken.FindNeighbors(Position, maxRadius, _neighbors);

				if (_neighbors.Count > 0 && LEAK_THROUGH < RandomHelpers.Random())
					collisionAvoidance = SteerToAvoidNeighbors(CA_LEAD_TIME, _neighbors) * 10;

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
					const float PF_LEAD_TIME = 3;
					Vector3 pathFollow =
						(Globals.UseDirectedPathFollowing ?
						 SteerToFollowPath(_pathDirection, PF_LEAD_TIME, _path) :
						 SteerToStayOnPath(PF_LEAD_TIME, _path));

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
            _trail.Draw(annotation);
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

		// a pointer to this boid's interface object for the proximity database
		ITokenForProximityDatabase<IVehicle> _proximityToken;

		// allocate one and share amoung instances just to save memory usage
		// (change to per-instance allocation to be more MP-safe)
		static readonly List<IVehicle> _neighbors = new List<IVehicle>();

		// path to be followed by this pedestrian
		// XXX Ideally this should be a generic Pathway, but we use the
		// XXX getTotalPathLength and radius methods (currently defined only
		// XXX on PolylinePathway) to set random initial positions.  Could
		// XXX there be a "random position inside path" method on Pathway?
		PolylinePathway _path;

		// direction for path following (upstream or downstream)
		bool _pathDirection;
	}
}
