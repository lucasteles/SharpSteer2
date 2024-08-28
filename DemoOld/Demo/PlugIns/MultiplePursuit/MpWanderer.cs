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

namespace Demo.PlugIns.MultiplePursuit
{
	public class MpWanderer : MpBase
	{
		// constructor
		public MpWanderer(IAnnotationService annotations = null)
            :base(annotations)
		{
			Reset();
		}

		// reset state
		public override void Reset()
		{
			base.Reset();
			BodyColor = new Color((byte)(255.0f * 0.4f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.4f)); // greenish
		}

		// one simulation step
		public void Update(float currentTime, float elapsedTime)
		{
			Vector3 wander2D = SteerForWander(elapsedTime);
            wander2D.Y = 0;

			Vector3 steer = Forward + (wander2D * 3);
			ApplySteeringForce(steer, elapsedTime);

			// for annotation
			Trail.Record(currentTime, Position);
		}
	}
}
