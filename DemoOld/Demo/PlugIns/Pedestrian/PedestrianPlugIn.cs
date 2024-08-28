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
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SharpSteer2;
using SharpSteer2.Database;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Pedestrian
{
    public class PedestrianPlugIn : PlugIn
	{
		public PedestrianPlugIn(IAnnotationService annotations)
            :base(annotations)
		{
			_crowd = new List<Pedestrian>();
		}

		public override String Name { get { return "Pedestrians"; } }

		public override float SelectionOrderSortKey { get { return 0.02f; } }

		public override void Open()
		{
			// make the database used to accelerate proximity queries
			_cyclePD = -1;
			NextPD();

			// create the specified number of Pedestrians
			_population = 0;
			for (int i = 0; i < 100; i++) AddPedestrianToCrowd();

			// initialize camera and selectedVehicle
			Pedestrian firstPedestrian = _crowd[0];
			Demo.Init3dCamera(firstPedestrian);
			Demo.Camera.Mode = Camera.CameraMode.FixedDistanceOffset;

			Demo.Camera.FixedTarget.X = 15;
            Demo.Camera.FixedTarget.Y = 0;
            Demo.Camera.FixedTarget.Z = 30;

			Demo.Camera.FixedPosition.X = 15;
            Demo.Camera.FixedPosition.Y = 70;
            Demo.Camera.FixedPosition.Z = -70;
		}

		public override void Update(float currentTime, float elapsedTime)
		{
		    // update each Pedestrian
		    foreach (Pedestrian pedestrian in _crowd)
		        pedestrian.Update(currentTime, elapsedTime);
		}

        public override void Redraw(float currentTime, float elapsedTime)
		{
			// selected Pedestrian (user can mouse click to select another)
			IVehicle selected = Demo.SelectedVehicle;

			// Pedestrian nearest mouse (to be highlighted)
			IVehicle nearMouse = Demo.VehicleNearestToMouse();

			// update camera
			Demo.UpdateCamera(elapsedTime, selected);

			// draw "ground plane"
			if (Demo.SelectedVehicle != null) _gridCenter = selected.Position;
			Demo.GridUtility(_gridCenter);

			// draw and annotate each Pedestrian
			foreach (Pedestrian pedestrian in _crowd)
			    pedestrian.Draw();

            // draw the path they follow and obstacles they avoid
			DrawPathAndObstacles();

			// highlight Pedestrian nearest mouse
			Demo.HighlightVehicleUtility(nearMouse);

			// textual annotation (at the vehicle's screen position)
			SerialNumberAnnotationUtility(selected);

			// textual annotation for selected Pedestrian
			if (Demo.SelectedVehicle != null)//FIXME: && annotation.IsEnabled)
			{
				Color color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.8f), (byte)(255.0f * 1.0f));
				Vector3 textOffset = new Vector3(0, 0.25f, 0);
				Vector3 textPosition = selected.Position + textOffset;
				Vector3 camPosition = Demo.Camera.Position;
				float camDistance = Vector3.Distance(selected.Position, camPosition);

				StringBuilder sb = new StringBuilder();
				sb.AppendFormat("1: speed: {0:0.00}\n", selected.Speed);
				sb.AppendFormat("2: cam dist: {0:0.0}\n", camDistance);
				Drawing.Draw2dTextAt3dLocation(sb.ToString(), textPosition, color);
			}

			// display status in the upper left corner of the window
			StringBuilder status = new StringBuilder();
			status.AppendFormat("[F1/F2] Crowd size: {0}\n", _population);
			status.Append("[F3] PD type: ");
			switch (_cyclePD)
			{
			case 0: status.Append("LQ bin lattice"); break;
			case 1: status.Append("brute force"); break;
			}
			status.Append("\n[F4] ");
            status.Append(Globals.UseDirectedPathFollowing ? "Directed path following." : "Stay on the path.");
            status.Append("\n[F5] Wander: ");
            status.Append(Globals.WanderSwitch ? "yes" : "no");
            status.Append("\n");
			Vector3 screenLocation = new Vector3(15, 50, 0);
			Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, Color.LightGray);
		}

        private void SerialNumberAnnotationUtility(ILocalSpaceBasis selected)
		{
			// display a Pedestrian's serial number as a text label near its
			// screen position when it is near the selected vehicle or mouse.
			if (selected != null)//FIXME: && IsAnnotationEnabled)
			{
				foreach (IVehicle vehicle in _crowd)
				{
				    const float NEAR_DISTANCE = 6;
				    Vector3 vp = vehicle.Position;
				    //Vector3 np = nearMouse.Position;
				    if ((Vector3.Distance(vp, selected.Position) < NEAR_DISTANCE)/* ||
						(nearMouse != null && (Vector3.Distance(vp, np) < nearDistance))*/)
				    {
				        String sn = String.Format("#{0}", vehicle.GetHashCode());
				        Color textColor = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 1), (byte)(255.0f * 0.8f));
				        Vector3 textOffset = new Vector3(0, 0.25f, 0);
				        Vector3 textPos = vehicle.Position + textOffset;
				        Drawing.Draw2dTextAt3dLocation(sn, textPos, textColor);
				    }
				}
			}
		}

        private static void DrawPathAndObstacles()
		{
			// draw a line along each segment of path
			PolylinePathway path = Globals.GetTestPath();
			for (int i = 0; i < path.PointCount; i++)
				if (i > 0) Drawing.DrawLine(path.Points[i], path.Points[i - 1], Color.Red);

			// draw obstacles
			Drawing.DrawXZCircle(Globals.Obstacle1.Radius, Globals.Obstacle1.Center, Color.White, 40);
			Drawing.DrawXZCircle(Globals.Obstacle2.Radius, Globals.Obstacle2.Center, Color.White, 40);
		}

		public override void Close()
		{
			// delete all Pedestrians
			while (_population > 0) RemovePedestrianFromCrowd();
		}

		public override void Reset()
		{
			// reset each Pedestrian
			foreach (Pedestrian pedestrian in _crowd)
			    pedestrian.Reset();

		    // reset camera position
			Demo.Position2dCamera(Demo.SelectedVehicle);

			// make camera jump immediately to new position
			Demo.Camera.DoNotSmoothNextMove();
		}

		public override void HandleFunctionKeys(Keys key)
		{
			switch (key)
			{
			case Keys.F1: AddPedestrianToCrowd(); break;
			case Keys.F2: RemovePedestrianFromCrowd(); break;
			case Keys.F3: NextPD(); break;
			case Keys.F4: Globals.UseDirectedPathFollowing = !Globals.UseDirectedPathFollowing; break;
			case Keys.F5: Globals.WanderSwitch = !Globals.WanderSwitch; break;
			}
		}

		public override void PrintMiniHelpForFunctionKeys()
		{
#if TODO
			std::ostringstream message;
			message << "Function keys handled by ";
			message << '"' << name() << '"' << ':' << std::ends;
			Demo.printMessage (message);
			Demo.printMessage (message);
			Demo.printMessage ("  F1     add a pedestrian to the crowd.");
			Demo.printMessage ("  F2     remove a pedestrian from crowd.");
			Demo.printMessage ("  F3     use next proximity database.");
			Demo.printMessage ("  F4     toggle directed path follow.");
			Demo.printMessage ("  F5     toggle wander component on/off.");
			Demo.printMessage ("");
#endif
		}

		void AddPedestrianToCrowd()
		{
			_population++;
			Pedestrian pedestrian = new Pedestrian(_pd, Annotations);
			_crowd.Add(pedestrian);
			if (_population == 1) Demo.SelectedVehicle = pedestrian;
		}

		void RemovePedestrianFromCrowd()
		{
			if (_population > 0)
			{
				// save pointer to last pedestrian, then remove it from the crowd
				_population--;
				Pedestrian pedestrian = _crowd[_population];
				_crowd.RemoveAt(_population);

				// if it is OpenSteerDemo's selected vehicle, unselect it
				if (pedestrian == Demo.SelectedVehicle)
					Demo.SelectedVehicle = null;
			}
		}

		// for purposes of demonstration, allow cycling through various
		// types of proximity databases.  this routine is called when the
		// OpenSteerDemo user pushes a function key.
		void NextPD()
		{
		    // allocate new PD
			const int TOTAL_PD = 1;
			switch (_cyclePD = (_cyclePD + 1) % TOTAL_PD)
			{
			case 0:
				{
					Vector3 center = Vector3.Zero;
					const float DIV = 20.0f;
					Vector3 divisions = new Vector3(DIV, 1.0f, DIV);
					const float DIAMETER = 80.0f; //XXX need better way to get this
					Vector3 dimensions = new Vector3(DIAMETER, DIAMETER, DIAMETER);
					_pd = new LocalityQueryProximityDatabase<IVehicle>(center, dimensions, divisions);
					break;
				}
			}

			// switch each boid to new PD
			foreach (Pedestrian pedestrian in _crowd)
			    pedestrian.NewPD(_pd);
		}

        public override IEnumerable<IVehicle> Vehicles
		{
			get { return _crowd.ConvertAll<IVehicle>(p => (IVehicle) p); }
		}

		// crowd: a group (STL vector) of all Pedestrians
        readonly List<Pedestrian> _crowd;

		Vector3 _gridCenter;

		// pointer to database used to accelerate proximity queries
		IProximityDatabase<IVehicle> _pd;

		// keep track of current flock size
		int _population;

		// which of the various proximity databases is currently in use
		int _cyclePD;
	}
}
