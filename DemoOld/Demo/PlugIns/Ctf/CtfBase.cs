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
using SharpSteer2;
using SharpSteer2.Helpers;
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Ctf
{ // spherical obstacle group

	public class CtfBase : SimpleVehicle
	{
	    protected readonly CtfPlugIn Plugin;
	    private readonly float _baseRadius;

	    protected Trail Trail;

        public override float MaxForce { get { return 3; } }
        public override float MaxSpeed { get { return 3; } }

		// constructor
	    protected CtfBase(CtfPlugIn plugin, IAnnotationService annotations = null, float baseRadius = 1.5f)
            :base(annotations)
	    {
	        Plugin = plugin;
	        _baseRadius = baseRadius;

	        Reset();
	    }

	    // reset state
		public override void Reset()
		{
			base.Reset();  // reset the vehicle 

			Speed = 3;             // speed along Forward direction.

			Avoiding = false;         // not actively avoiding

			RandomizeStartingPositionAndHeading();  // new starting position

			Trail = new Trail();
			Trail.Clear();     // prevent long streaks due to teleportation
		}

		// draw this character/vehicle into the scene
		public virtual void Draw()
		{
			Drawing.DrawBasic2dCircularVehicle(this, BodyColor);
			Trail.Draw(annotation);
		}

		// annotate when actively avoiding obstacles
		// xxx perhaps this should be a call to a general purpose annotation
		// xxx for "local xxx axis aligned box in XZ plane" -- same code in in
		// xxx Pedestrian.cpp
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

		public void DrawHomeBase()
		{
			Vector3 up = new Vector3(0, 0.01f, 0);
			Color atColor = new Color((byte)(255.0f * 0.3f), (byte)(255.0f * 0.3f), (byte)(255.0f * 0.5f));
			Color noColor = Color.Gray;
			bool reached = Plugin.CtfSeeker.State == SeekerState.AtGoal;
			Color baseColor = (reached ? atColor : noColor);
            Drawing.DrawXZDisk(_baseRadius, Globals.HomeBaseCenter, baseColor, 40);
            Drawing.DrawXZDisk(_baseRadius / 15, Globals.HomeBaseCenter + up, Color.Black, 20);
		}

	    private void RandomizeStartingPositionAndHeading()
		{
			// randomize position on a ring between inner and outer radii
			// centered around the home base
			float rRadius = RandomHelpers.Random(Globals.MIN_START_RADIUS, Globals.MAX_START_RADIUS);
			Vector3 randomOnRing = Vector3Helpers.RandomUnitVectorOnXZPlane() * rRadius;
			Position = (Globals.HomeBaseCenter + randomOnRing);

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
	    protected Color BodyColor;

		// xxx store steer sub-state for anotation
	    protected bool Avoiding;

		// dynamic obstacle registry
		public static void InitializeObstacles(float radius, int obstacles)
		{
			// start with 40% of possible obstacles
			if (ObstacleCount == -1)
			{
				ObstacleCount = 0;
                for (int i = 0; i < obstacles; i++)
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
			float requiredClearance = Globals.Seeker.Radius * 4; // 2 x diameter
			do
			{
				r = RandomHelpers.Random(1.5f, 4);
				c = Vector3Helpers.RandomVectorOnUnitRadiusXZDisk() * Globals.MAX_START_RADIUS * 1.1f;
			    minClearance = AllObstacles.Aggregate(float.MaxValue, (current, t) => TestOneObstacleOverlap(current, r, t.Radius, c, t.Center));

			    minClearance = TestOneObstacleOverlap(minClearance, r, radius - requiredClearance, c, Globals.HomeBaseCenter);
			}
			while (minClearance < requiredClearance);

			// add new non-overlapping obstacle to registry
			AllObstacles.Add(new SphericalObstacle(r, c));
			ObstacleCount++;
		}

		public static void RemoveOneObstacle()
		{
		    if (ObstacleCount <= 0)
		        return;

		    ObstacleCount--;
		    AllObstacles.RemoveAt(ObstacleCount);
		}

	    private static float MinDistanceToObstacle(Vector3 point)
		{
			const float R = 0;
			Vector3 c = point;
	        return AllObstacles.Aggregate(float.MaxValue, (current, t) => TestOneObstacleOverlap(current, R, t.Radius, c, t.Center));
		}

		static float TestOneObstacleOverlap(float minClearance, float r, float radius, Vector3 c, Vector3 center)
		{
			float d = Vector3.Distance(c, center);
			float clearance = d - (r + radius);
			if (minClearance > clearance) minClearance = clearance;
			return minClearance;
		}

		protected static int ObstacleCount = -1;
		public static readonly List<SphericalObstacle> AllObstacles = new List<SphericalObstacle>();
	}
}
