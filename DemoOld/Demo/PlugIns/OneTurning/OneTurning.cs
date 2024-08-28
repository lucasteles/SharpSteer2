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

namespace Demo.PlugIns.OneTurning
{
	public class OneTurning : SimpleVehicle
	{
		Trail _trail;

        public override float MaxForce { get { return 0.3f; } }
        public override float MaxSpeed { get { return 5; } }

		// constructor
		public OneTurning(IAnnotationService annotations = null)
            :base(annotations)
		{
			Reset();
		}

		// reset state
		public override void Reset()
		{
			base.Reset(); // reset the vehicle 
			Speed = 1.5f;         // speed along Forward direction.
			_trail = new Trail();
			_trail.Clear();    // prevent long streaks due to teleportation 
		}

		// per frame simulation update
		public void Update(float currentTime, float elapsedTime)
		{
			ApplySteeringForce(new Vector3(-2, 0, -3), elapsedTime);
			annotation.VelocityAcceleration(this);
			_trail.Record(currentTime, Position);
		}

		// draw this character/vehicle into the scene
		public void Draw()
		{
			Drawing.DrawBasic2dCircularVehicle(this, Color.Gray);
            _trail.Draw(annotation);
		}
	}
}
