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
using SharpSteer2;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Ctf
{
	public class CtfEnemy : CtfBase
	{
		// constructor
		public CtfEnemy(CtfPlugIn plugin, IAnnotationService annotations = null)
            : base(plugin, annotations)
		{
			Reset();
		}

		// reset state
		public override void Reset()
		{
			base.Reset();
			BodyColor = new Color((byte)(255.0f * 0.6f), (byte)(255.0f * 0.4f), (byte)(255.0f * 0.4f)); // redish
		}

		// per frame simulation update
		public void Update(float currentTime, float elapsedTime)
		{
			// determine upper bound for pursuit prediction time
			float seekerToGoalDist = Vector3.Distance(Globals.HomeBaseCenter, Globals.Seeker.Position);
			float adjustedDistance = seekerToGoalDist - Radius - Plugin.BaseRadius;
			float seekerToGoalTime = ((adjustedDistance < 0) ? 0 : (adjustedDistance / Globals.Seeker.Speed));
			float maxPredictionTime = seekerToGoalTime * 0.9f;

			// determine steering (pursuit, obstacle avoidance, or braking)
			Vector3 steer = Vector3.Zero;
			if (Globals.Seeker.State == SeekerState.Running)
			{
				Vector3 avoidance = SteerToAvoidObstacles(Globals.AVOIDANCE_PREDICT_TIME_MIN, AllObstacles);

				// saved for annotation
				Avoiding = (avoidance == Vector3.Zero);

				steer = Avoiding ? SteerForPursuit(Globals.Seeker, maxPredictionTime) : avoidance;
			}
			else
			{
				ApplyBrakingForce(Globals.BRAKING_RATE, elapsedTime);
			}
			ApplySteeringForce(steer, elapsedTime);

			// annotation
			annotation.VelocityAcceleration(this);
			Trail.Record(currentTime, Position);

			// detect and record interceptions ("tags") of seeker
			float seekerToMeDist = Vector3.Distance(Position, Globals.Seeker.Position);
			float sumOfRadii = Radius + Globals.Seeker.Radius;
			if (seekerToMeDist < sumOfRadii)
			{
				if (Globals.Seeker.State == SeekerState.Running) Globals.Seeker.State = SeekerState.Tagged;

				// annotation:
				if (Globals.Seeker.State == SeekerState.Tagged)
				{
					Color color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.5f));
					annotation.DiskXZ(sumOfRadii, (Position + Globals.Seeker.Position) / 2, color.ToVector3().FromXna(), 20);
				}
			}
		}
	}
}
