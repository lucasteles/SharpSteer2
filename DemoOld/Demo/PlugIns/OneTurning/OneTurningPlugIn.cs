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
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.OneTurning
{
	public class OneTurningPlugIn : PlugIn
	{
		public OneTurningPlugIn(IAnnotationService annotations)
            :base(annotations)
		{
			_theVehicle = new List<OneTurning>();
		}

		public override String Name { get { return "One Turning Away"; } }

		public override float SelectionOrderSortKey { get { return 0.06f; } }

		public override void Open()
		{
            _oneTurning = new OneTurning(Annotations);
			Demo.SelectedVehicle = _oneTurning;
			_theVehicle.Add(_oneTurning);

			// initialize camera
			Demo.Init2dCamera(_oneTurning);
			Demo.Camera.Position = new Vector3(10, Demo.CAMERA2_D_ELEVATION, 10);
			Demo.Camera.FixedPosition = new Vector3(40);
		}

		public override void Update(float currentTime, float elapsedTime)
		{
			// update simulation of test vehicle
			_oneTurning.Update(currentTime, elapsedTime);
		}

		public override void Redraw(float currentTime, float elapsedTime)
		{
			// draw "ground plane"
			Demo.GridUtility(_oneTurning.Position);

			// draw test vehicle
			_oneTurning.Draw();

			// textual annotation (following the test vehicle's screen position)
			String annote = String.Format("      speed: {0:0.00}", _oneTurning.Speed);
			Drawing.Draw2dTextAt3dLocation(annote, _oneTurning.Position, Color.Red);
			Drawing.Draw2dTextAt3dLocation("start", Vector3.Zero, Color.Green);

			// update camera, tracking test vehicle
			Demo.UpdateCamera(elapsedTime, _oneTurning);
		}

		public override void Close()
		{
			_theVehicle.Clear();
			_oneTurning = null;
		}

		public override void Reset()
		{
			// reset vehicle
			_oneTurning.Reset();
		}

        public override IEnumerable<IVehicle> Vehicles
		{
			get { return _theVehicle.ConvertAll<IVehicle>(v => (IVehicle) v); }
		}

		OneTurning _oneTurning;
	    readonly List<OneTurning> _theVehicle; // for allVehicles
	}
}
