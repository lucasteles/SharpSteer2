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
using SharpSteer2.Helpers;
using SharpSteer2.Pathway;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.MapDrive
{
	public class MapDrivePlugIn : PlugIn
	{
		public MapDrivePlugIn(IAnnotationService annotations)
            :base(annotations)
		{
			_vehicles = new List<MapDriver>();
		}

		public override String Name { get { return "Driving through map based obstacles"; } }

		public override float SelectionOrderSortKey { get { return 0.07f; } }

		public override void Open()
		{
			// make new MapDriver
            _vehicle = new MapDriver(Annotations);
			_vehicles.Add(_vehicle);
			Demo.SelectedVehicle = _vehicle;

			// marks as obstacles map cells adjacent to the path
			_usePathFences = true;

			// scatter random rock clumps over map
			_useRandomRocks = true;

			// init Demo camera
			_initCamDist = 30;
			_initCamElev = 15;
			Demo.Init2dCamera(_vehicle, _initCamDist, _initCamElev);
			// "look straight down at vehicle" camera mode parameters
			Demo.Camera.LookDownDistance = 50;
			// "static" camera mode parameters
			Demo.Camera.FixedPosition = new Vector3(145);
			Demo.Camera.FixedTarget.X = 40;
            Demo.Camera.FixedTarget.Y = 0;
            Demo.Camera.FixedTarget.Z = 40;
			Demo.Camera.FixedUp = Vector3.UnitY;

			// reset this plugin
			Reset();
		}


		public override void Update(float currentTime, float elapsedTime)
		{
			// update simulation of test vehicle
			_vehicle.Update(currentTime, elapsedTime);

			// when vehicle drives outside the world
			if (_vehicle.HandleExitFromMap()) RegenerateMap();

			// QQQ first pass at detecting "stuck" state
			if (_vehicle.Stuck && (_vehicle.RelativeSpeed() < 0.001f))
			{
				_vehicle.StuckCount++;
				Reset();
			}
		}


		public override void Redraw(float currentTime, float elapsedTime)
		{
			// update camera, tracking test vehicle
			Demo.UpdateCamera(elapsedTime, _vehicle);

			// draw "ground plane"  (make it 4x map size)
			const float S = MapDriver.WORLD_SIZE * 2;
			const float U = -0.2f;
			Drawing.DrawQuadrangle(new Vector3(+S, U, +S),
							new Vector3(+S, U, -S),
							new Vector3(-S, U, -S),
							new Vector3(-S, U, +S),
							new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.7f), (byte)(255.0f * 0.5f))); // "sand"

			// draw map and path
			if (MapDriver.DemoSelect == 2) _vehicle.DrawPath();
			_vehicle.DrawMap();

			// draw test vehicle
			_vehicle.Draw();

			// QQQ mark origin to help spot artifacts
			const float TICK = 2;
			Drawing.DrawLine(new Vector3(TICK, 0, 0), new Vector3(-TICK, 0, 0), Color.Green);
			Drawing.DrawLine(new Vector3(0, 0, TICK), new Vector3(0, 0, -TICK), Color.Green);

			// compute conversion factor miles-per-hour to meters-per-second
			const float METERS_PER_MILE = 1609.344f;
			const float SECONDS_PER_HOUR = 3600;
// ReSharper disable InconsistentNaming
			const float MPSperMPH = METERS_PER_MILE / SECONDS_PER_HOUR;
// ReSharper restore InconsistentNaming

			// display status in the upper left corner of the window
			StringBuilder status = new StringBuilder();
			status.AppendFormat("Speed: {0} mps ({1} mph), average: {2:0.0} mps\n\n",
				   (int)_vehicle.Speed,
				   (int)(_vehicle.Speed / MPSperMPH),
				   _vehicle.TotalDistance / _vehicle.TotalTime);
			status.AppendFormat("collisions avoided for {0} seconds",
				   (int)(Demo.Clock.TotalSimulationTime - _vehicle.TimeOfLastCollision));
			if (_vehicle.CountOfCollisionFreeTimes > 0)
			{
				status.AppendFormat("\nmean time between collisions: {0} ({1}/{2})",
					   (int)(_vehicle.SumOfCollisionFreeTimes / _vehicle.CountOfCollisionFreeTimes),
					   (int)_vehicle.SumOfCollisionFreeTimes,
					   _vehicle.CountOfCollisionFreeTimes);
			}

			status.AppendFormat("\n\nStuck count: {0} ({1} cycles, {2} off path)",
				_vehicle.StuckCount,
				_vehicle.StuckCycleCount,
				_vehicle.StuckOffPathCount);
			status.Append("\n\n[F1] ");
			if (1 == MapDriver.DemoSelect) status.Append("wander, ");
			if (2 == MapDriver.DemoSelect) status.Append("follow path, ");
			status.Append("avoid obstacle");

			if (2 == MapDriver.DemoSelect)
			{
				status.Append("\n[F2] path following direction: ");
			    status.Append(_vehicle.PathFollowDirection > 0 ? "+1" : "-1");
			    status.Append("\n[F3] path fence: ");
			    status.Append(_usePathFences ? "on" : "off");
			}

			status.Append("\n[F4] rocks: ");
		    status.Append(_useRandomRocks ? "on" : "off");
		    status.Append("\n[F5] prediction: ");
		    status.Append(_vehicle.CurvedSteering ? "curved" : "linear");
		    if (2 == MapDriver.DemoSelect)
			{
				status.AppendFormat("\n\nLap {0} (completed: {1}%)",
					_vehicle.LapsStarted,
					   ((_vehicle.LapsStarted < 2) ? 0 :
						   (int)(100 * ((float)_vehicle.LapsFinished /
										 (_vehicle.LapsStarted - 1))))
					   );

				status.AppendFormat("\nHints given: {0}, taken: {1}",
					_vehicle.HintGivenCount,
					_vehicle.HintTakenCount);
			}
			status.Append("\n");
			qqqRange("WR ", MapDriver.SavedNearestWR, status);
			qqqRange("R  ", MapDriver.SavedNearestR, status);
			qqqRange("L  ", MapDriver.SavedNearestL, status);
			qqqRange("WL ", MapDriver.SavedNearestWL, status);
			Vector3 screenLocation = new Vector3(15, 50, 0);
			Vector3 color = new Vector3(0.15f, 0.15f, 0.5f);
			Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, new Color(color.ToXna()));

			{
				float v = Drawing.GetWindowHeight() - 5;
				const float M = 10;
				float w = Drawing.GetWindowWidth();
				float f = w - (2 * M);

				// limit tick mark
				float l = _vehicle.AnnoteMaxRelSpeed;
				Drawing.Draw2dLine(new Vector3(M + (f * l), v - 3, 0), new Vector3(M + (f * l), v + 3, 0), Color.Black);
				// two "inverse speedometers" showing limits due to curvature and
				// path alignment
				if (Math.Abs(l) > float.Epsilon)
				{
					float c = _vehicle.AnnoteMaxRelSpeedCurve;
					float p = _vehicle.AnnoteMaxRelSpeedPath;
					Drawing.Draw2dLine(new Vector3(M + (f * c), v + 1, 0), new Vector3(w - M, v + 1, 0), Color.Red);
					Drawing.Draw2dLine(new Vector3(M + (f * p), v - 2, 0), new Vector3(w - M, v - 1, 0), Color.Green);
				}
				// speedometer: horizontal line with length proportional to speed
				Drawing.Draw2dLine(new Vector3(M, v, 0), new Vector3(M + (f * S), v, 0), Color.White);
				// min and max tick marks
				Drawing.Draw2dLine(new Vector3(M, v, 0), new Vector3(M, v - 2, 0), Color.White);
				Drawing.Draw2dLine(new Vector3(w - M, v, 0), new Vector3(w - M, v - 2, 0), Color.White);
			}
		}

	    static void qqqRange(String text, float range, StringBuilder status)
		{
			status.AppendFormat("\n{0}", text);
            if (range >= 9999.0f)
                status.Append("--");
            else
				status.Append((int)range);
		}

		public override void Close()
		{
			_vehicles.Clear();
		}

		public override void Reset()
		{
			RegenerateMap();

			// reset vehicle
			_vehicle.Reset();

			// make camera jump immediately to new position
			Demo.Camera.DoNotSmoothNextMove();

			// reset camera position
			Demo.Position2dCamera(_vehicle, _initCamDist, _initCamElev);
		}

		public override void HandleFunctionKeys(Keys key)
		{
			switch (key)
			{
			case Keys.F1: SelectNextDemo(); break;
			case Keys.F2: ReversePathFollowDirection(); break;
			case Keys.F3: TogglePathFences(); break;
			case Keys.F4: ToggleRandomRocks(); break;
			case Keys.F5: ToggleCurvedSteering(); break;

			case Keys.F6: // QQQ draw an enclosed "pen" of obstacles to test cycle-stuck
				{
					const float M = MapDriver.WORLD_SIZE * 0.4f; // main diamond size
					const float N = MapDriver.WORLD_SIZE / 8; // notch size
					Vector3 q = new Vector3(0, 0, M - N);
					Vector3 s = new Vector3(2 * N, 0, 0);
					Vector3 c = s - q;
					Vector3 d =s + q;
				    float[] pathRadii = { 10, 10 };
					Vector3[] pathPoints = { c, d };
					GCRoute r = new GCRoute(pathPoints, pathRadii, false);
					DrawPathFencesOnMap(_vehicle.Map, r);
					break;
				}
			}
		}

		void ReversePathFollowDirection()
		{
			_vehicle.PathFollowDirection = (_vehicle.PathFollowDirection > 0) ? -1 : +1;
		}

		void TogglePathFences()
		{
			_usePathFences = !_usePathFences;
			Reset();
		}

		void ToggleRandomRocks()
		{
			_useRandomRocks = !_useRandomRocks;
			Reset();
		}

		void ToggleCurvedSteering()
		{
			_vehicle.CurvedSteering = !_vehicle.CurvedSteering;
			_vehicle.IncrementalSteering = !_vehicle.IncrementalSteering;
			Reset();
		}

		void SelectNextDemo()
		{
			StringBuilder message = new StringBuilder();
			message.AppendFormat("{0}: ", Name);
			if (++MapDriver.DemoSelect > 2)
			{
				MapDriver.DemoSelect = 0;
			}
			switch (MapDriver.DemoSelect)
			{
			case 0:
				message.Append("obstacle avoidance and speed control");
				Reset();
				break;
			case 1:
				message.Append("wander, obstacle avoidance and speed control");
				Reset();
				break;
			case 2:
				message.Append("path following, obstacle avoidance and speed control");
				Reset();
				break;
			}
			//FIXME: Demo.printMessage (message);
		}

		// random utility, worth moving to Utilities.h?
	    

		void RegenerateMap()
		{
			// regenerate map: clear and add random "rocks"
			_vehicle.Map.Clear();
			DrawRandomClumpsOfRocksOnMap(_vehicle.Map);
			ClearCenterOfMap(_vehicle.Map);

			// draw fences for first two demo modes
			if (MapDriver.DemoSelect < 2) DrawBoundaryFencesOnMap(_vehicle.Map);

			// randomize path widths
			if (MapDriver.DemoSelect == 2)
			{
				int count = _vehicle.Path.PointCount;
				bool upstream = _vehicle.PathFollowDirection > 0;
				int entryIndex = upstream ? 1 : count - 1;
				int exitIndex = upstream ? count - 1 : 1;
				float lastExitRadius = _vehicle.Path.Radii[exitIndex];
				for (int i = 1; i < count; i++)
				{
					_vehicle.Path.Radii[i] = RandomHelpers.Random(4, 19);
				}
				_vehicle.Path.Radii[entryIndex] = lastExitRadius;
			}

			// mark path-boundary map cells as obstacles
			// (when in path following demo and appropriate mode is set)
			if (_usePathFences && (MapDriver.DemoSelect == 2))
				DrawPathFencesOnMap(_vehicle.Map, _vehicle.Path);
		}

		void DrawRandomClumpsOfRocksOnMap(TerrainMap map)
		{
			if (_useRandomRocks)
			{
				const int SPREAD = 4;
				int r = map.Cellwidth();
				int k = RandomHelpers.RandomInt(50, 150);

				for (int p = 0; p < k; p++)
				{
					int i = RandomHelpers.RandomInt(0, r - SPREAD);
					int j = RandomHelpers.RandomInt(0, r - SPREAD);
					int c = RandomHelpers.RandomInt(0, 10);

					for (int q = 0; q < c; q++)
					{
						int m = RandomHelpers.RandomInt(0, SPREAD);
						int n = RandomHelpers.RandomInt(0, SPREAD);
						map.SetMapBit(i + m, j + n, true);
					}
				}
			}
		}

	    static void DrawBoundaryFencesOnMap(TerrainMap map)
		{
			// QQQ it would make more sense to do this with a "draw line
			// QQQ on map" primitive, may need that for other things too

			int cw = map.Cellwidth();
			int ch = map.Cellheight();

			int r = cw - 1;
			int a = cw >> 3;
			int b = cw - a;
			int o = cw >> 4;
			int p = (cw - o) >> 1;
			int q = (cw + o) >> 1;

			for (int i = 0; i < cw; i++)
			{
				for (int j = 0; j < ch; j++)
				{
					bool c = i > a && i < b && (i < p || i > q);
					if (i == 0 || j == 0 || i == r || j == r || (c && (i == j || i + j == r)))
						map.SetMapBit(i, j, true);
				}
			}
		}

	    static void ClearCenterOfMap(TerrainMap map)
		{
			int o = map.Cellwidth() >> 4;
			int p = (map.Cellwidth() - o) >> 1;
			int q = (map.Cellwidth() + o) >> 1;
			for (int i = p; i <= q; i++)
				for (int j = p; j <= q; j++)
					map.SetMapBit(i, j, false);
		}

        static void DrawPathFencesOnMap(TerrainMap map, IPathway path)
		{
			float xs = map.XSize / map.Resolution;
			float zs = map.ZSize / map.Resolution;
			Vector3 alongRow = new Vector3(xs, 0, 0);
			Vector3 nextRow = new Vector3(-map.XSize, 0, zs);
			Vector3 g = new Vector3((map.XSize - xs) / -2, 0, (map.ZSize - zs) / -2);
			for (int j = 0; j < map.Resolution; j++)
			{
				for (int i = 0; i < map.Resolution; i++)
				{
					float outside = path.HowFarOutsidePath(g);
					const float WALL_THICKNESS = 1.0f;

					// set map cells adjacent to the outside edge of the path
					if ((outside > 0) && (outside < WALL_THICKNESS))
						map.SetMapBit(i, j, true);

					// clear all other off-path map cells 
					if (outside > WALL_THICKNESS) map.SetMapBit(i, j, false);

					g += alongRow;
				}
				g += nextRow;
			}
		}

        public override IEnumerable<IVehicle> Vehicles
		{
			get { return _vehicles.ConvertAll<IVehicle>(v => (IVehicle) v); }
		}

		MapDriver _vehicle;
	    readonly List<MapDriver> _vehicles; // for allVehicles

		float _initCamDist, _initCamElev;

		bool _usePathFences;
		bool _useRandomRocks;
	}
}
