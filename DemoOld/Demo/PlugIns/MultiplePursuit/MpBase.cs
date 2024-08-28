// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using Microsoft.Xna.Framework;
using SharpSteer2;

namespace Demo.PlugIns.MultiplePursuit
{
	public class MpBase : SimpleVehicle
	{
		protected Trail Trail;

        public override float MaxForce { get { return 5; } }
        public override float MaxSpeed { get { return 3; } }

		// constructor
	    protected MpBase(IAnnotationService annotations = null)
            :base(annotations)
		{
			Reset();
		}

		// reset state
		public override void Reset()
		{
			base.Reset();			// reset the vehicle 

			Speed = 0;            // speed along Forward direction.
			Trail = new Trail();
			Trail.Clear();    // prevent long streaks due to teleportation 
		}

		// draw into the scene
		public void Draw()
		{
			Drawing.DrawBasic2dCircularVehicle(this, BodyColor);
            Trail.Draw(annotation);
		}

		// for draw method
		protected Color BodyColor;
	}
}
