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

namespace Demo.PlugIns.LowSpeedTurn
{
	class LowSpeedTurnPlugIn : PlugIn
	{
		const int LST_COUNT = 5;
		const float LST_LOOK_DOWN_DISTANCE = 18;
		static readonly Vector3 _lstViewCenter = new Vector3(7, 0, -2);
		static readonly Vector3 _lstPlusZ = new Vector3(0, 0, 1);

		public LowSpeedTurnPlugIn(IAnnotationService annotations)
            :base(annotations)
		{
			_all = new List<LowSpeedTurn>();
		}

		public override String Name { get { return "Low Speed Turn"; } }

		public override float SelectionOrderSortKey { get { return 0.05f; } }

		public override void Open()
		{
			// create a given number of agents with stepped inital parameters,
			// store pointers to them in an array.
			LowSpeedTurn.ResetStarts();
            for (int i = 0; i < LST_COUNT; i++)
                _all.Add(new LowSpeedTurn(Annotations));

			// initial selected vehicle
			Demo.SelectedVehicle = _all[0];

			// initialize camera
			Demo.Camera.Mode = Camera.CameraMode.Fixed;
			Demo.Camera.FixedUp = _lstPlusZ;
			Demo.Camera.FixedTarget = _lstViewCenter;
			Demo.Camera.FixedPosition = _lstViewCenter;
			Demo.Camera.FixedPosition.Y += LST_LOOK_DOWN_DISTANCE;
			Demo.Camera.LookDownDistance = LST_LOOK_DOWN_DISTANCE;
			Demo.Camera.FixedDistanceVerticalOffset = Demo.CAMERA2_D_ELEVATION;
			Demo.Camera.FixedDistanceDistance = Demo.CAMERA_TARGET_DISTANCE;
		}

		public override void Update(float currentTime, float elapsedTime)
		{
		    // update, draw and annotate each agent
		    foreach (LowSpeedTurn t in _all)
		        t.Update(currentTime, elapsedTime);
		}

	    public override void Redraw(float currentTime, float elapsedTime)
		{
			// selected vehicle (user can mouse click to select another)
			IVehicle selected = Demo.SelectedVehicle;

			// vehicle nearest mouse (to be highlighted)
			IVehicle nearMouse = Demo.VehicleNearestToMouse();

			// update camera
			Demo.UpdateCamera(elapsedTime, selected);

			// draw "ground plane"
			Demo.GridUtility(selected.Position);

			// update, draw and annotate each agent
			foreach (LowSpeedTurn agent in _all)
			{
			    agent.Draw();

			    // display speed near agent's screen position
			    Color textColor = new Color(new Vector3(0.8f, 0.8f, 1.0f).ToXna());
			    Vector3 textOffset = new Vector3(0, 0.25f, 0);
			    Vector3 textPosition = agent.Position + textOffset;
			    String annote = String.Format("{0:0.00}", agent.Speed);
			    Drawing.Draw2dTextAt3dLocation(annote, textPosition, textColor);
			}

			// highlight vehicle nearest mouse
			Demo.HighlightVehicleUtility(nearMouse);
		}

		public override void Close()
		{
			_all.Clear();
		}

		public override void Reset()
		{
		    // reset each agent
			LowSpeedTurn.ResetStarts();
		    foreach (LowSpeedTurn t in _all)
		        t.Reset();
		}

	    public override IEnumerable<IVehicle> Vehicles
		{
			get { return _all.ConvertAll<IVehicle>(v => (IVehicle) v); }
		}

	    readonly List<LowSpeedTurn> _all; // for allVehicles
	}
}
