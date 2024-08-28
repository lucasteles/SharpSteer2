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
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Boids
{ // spherical obstacle group

	public class BoidsPlugIn : PlugIn
	{
		public BoidsPlugIn(IAnnotationService annotations)
            :base(annotations)
		{
			_flock = new List<Boid>();
		}

		public override String Name { get { return "Boids"; } }

		public override float SelectionOrderSortKey
		{
			get { return -0.03f; }
		}

		public override void Open()
		{
			// make the database used to accelerate proximity queries
			_cyclePD = -1;
			NextPD();

			// make default-sized flock
			_population = 0;
			for (int i = 0; i < 200; i++)
                AddBoidToFlock();

			// initialize camera
			Demo.Init3dCamera(Demo.SelectedVehicle);
			Demo.Camera.Mode = Camera.CameraMode.Fixed;
			Demo.Camera.FixedDistanceDistance = Demo.CAMERA_TARGET_DISTANCE;
			Demo.Camera.FixedDistanceVerticalOffset = 0;
			Demo.Camera.LookDownDistance = 20;
			Demo.Camera.AimLeadTime = 0.5f;
			Demo.Camera.PovOffset.X = 0;
            Demo.Camera.PovOffset.Y = 0.5f;
            Demo.Camera.PovOffset.Z = -2;

			Boid.InitializeObstacles();
		}

		public override void Update(float currentTime, float elapsedTime)
		{
		    // update flock simulation for each boid
		    foreach (Boid boid in _flock)
		        boid.Update(currentTime, elapsedTime);
		}

	    public override void Redraw(float currentTime, float elapsedTime)
		{
			// selected vehicle (user can mouse click to select another)
			IVehicle selected = Demo.SelectedVehicle;

			// vehicle nearest mouse (to be highlighted)
			IVehicle nearMouse = Demo.VehicleNearestToMouse();

			// update camera
			Demo.UpdateCamera(elapsedTime, selected);

			DrawObstacles();

			// draw each boid in flock
			foreach (Boid boid in _flock)
			    boid.Draw();

	        // highlight vehicle nearest mouse
			Demo.DrawCircleHighlightOnVehicle(nearMouse, 1, Color.LightGray);

			// highlight selected vehicle
			Demo.DrawCircleHighlightOnVehicle(selected, 1, Color.Gray);

			// display status in the upper left corner of the window
			StringBuilder status = new StringBuilder();
			status.AppendFormat("[F1/F2] {0} boids", _population);
			status.Append("\n[F3]    PD type: ");
			switch (_cyclePD)
			{
			case 0: status.Append("LQ bin lattice"); break;
			case 1: status.Append("brute force"); break;
			}
			status.Append("\n[F4]    Boundary: ");
			switch (Boid.BoundaryCondition)
			{
			case 0: status.Append("steer back when outside"); break;
			case 1: status.Append("wrap around (teleport)"); break;
			}
			Vector3 screenLocation = new Vector3(15, 50, 0);
			Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, Color.LightGray);
		}

		public override void Close()
		{
			// delete each member of the flock
			while (_population > 0)
                RemoveBoidFromFlock();

			// delete the proximity database
			_pd = null;
		}

		public override void Reset()
		{
            // reset each boid in flock
            foreach (Boid boid in _flock)
                boid.Reset();

		    // reset camera position
            Demo.Position3dCamera(Demo.SelectedVehicle);

            // make camera jump immediately to new position
            Demo.Camera.DoNotSmoothNextMove();
		}

		// for purposes of demonstration, allow cycling through various
		// types of proximity databases.  this routine is called when the
		// Demo user pushes a function key.
	    private void NextPD()
		{
	        // allocate new PD
			const int TOTAL_PD = 1;
			switch (_cyclePD = (_cyclePD + 1) % TOTAL_PD)
			{
			case 0:
				{
					Vector3 center = Vector3.Zero;
					const float DIV = 10.0f;
					Vector3 divisions = new Vector3(DIV, DIV, DIV);
					const float DIAMETER = Boid.WORLD_RADIUS * 1.1f * 2;
					Vector3 dimensions = new Vector3(DIAMETER, DIAMETER, DIAMETER);
					_pd = new LocalityQueryProximityDatabase<IVehicle>(center, dimensions, divisions);
					break;
				}
			}

			// switch each boid to new PD
			foreach (Boid boid in _flock)
			    boid.NewPD(_pd);

	        // delete old PD (if any)
		}

		public override void HandleFunctionKeys(Keys key)
		{
			switch (key)
			{
			case Keys.F1: AddBoidToFlock(); break;
			case Keys.F2: RemoveBoidFromFlock(); break;
			case Keys.F3: NextPD(); break;
			case Keys.F4: Boid.NextBoundaryCondition(); break;
			}
		}

		public override void PrintMiniHelpForFunctionKeys()
		{
#if IGNORED
        std.ostringstream message;
        message << "Function keys handled by ";
        message << '"' << name() << '"' << ':' << std.ends;
        Demo.printMessage (message);
        Demo.printMessage ("  F1     add a boid to the flock.");
        Demo.printMessage ("  F2     remove a boid from the flock.");
        Demo.printMessage ("  F3     use next proximity database.");
        Demo.printMessage ("  F4     next flock boundary condition.");
        Demo.printMessage ("");
#endif
		}

	    private void AddBoidToFlock()
		{
			_population++;
			Boid boid = new Boid(_pd, Annotations);
			_flock.Add(boid);
			if (_population == 1) Demo.SelectedVehicle = boid;
		}

	    private void RemoveBoidFromFlock()
		{
	        if (_population <= 0)
	            return;

	        // save a pointer to the last boid, then remove it from the flock
	        _population--;
	        Boid boid = _flock[_population];
	        _flock.RemoveAt(_population);

	        // if it is Demo's selected vehicle, unselect it
	        if (boid == Demo.SelectedVehicle)
	            Demo.SelectedVehicle = null;
		}

		// return an AVGroup containing each boid of the flock
		public override IEnumerable<IVehicle> Vehicles
		{
			get { return _flock.ConvertAll<IVehicle>(v => (IVehicle) v); }
		}

		// flock: a group (STL vector) of pointers to all boids
	    private readonly List<Boid> _flock;

		// pointer to database used to accelerate proximity queries
	    private IProximityDatabase<IVehicle> _pd;

		// keep track of current flock size
	    private int _population;

		// which of the various proximity databases is currently in use
	    private int _cyclePD;

	    private static void DrawObstacles()
	    {
	        //Color color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.4f));
			List<SphericalObstacle> allSO = Boid.AllObstacles;
	        foreach (SphericalObstacle obstacle in allSO)
	        {
                //Drawing.DrawBasic3dSphere(allSO[so].Center, allSO[so].Radius, Color.Red);
	            Drawing.Draw3dCircleOrDisk(obstacle.Radius, obstacle.Center, Vector3.UnitY, Color.Red, 10, true);
	            Drawing.Draw3dCircleOrDisk(obstacle.Radius, obstacle.Center, Vector3.UnitX, Color.Red, 10, true);
	            Drawing.Draw3dCircleOrDisk(obstacle.Radius, obstacle.Center, Vector3.UnitZ, Color.Red, 10, true);
	        }
	    }
	}
}
