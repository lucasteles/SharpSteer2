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
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SharpSteer2.Database;
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Boids;
// spherical obstacle group

public class BoidsPlugIn : PlugIn
{
    public BoidsPlugIn(IAnnotationService annotations)
        :base(annotations) =>
        flock = new();

    public override string Name => "Boids";

    public override float SelectionOrderSortKey => -0.03f;

    public override void Open()
    {
        // make the database used to accelerate proximity queries
        cyclePd = -1;
        NextPd();

        // make default-sized flock
        population = 0;
        for (var i = 0; i < 200; i++)
            AddBoidToFlock();

        // initialize camera
        GameDemo.Init3dCamera(GameDemo.SelectedVehicle);
        GameDemo.Camera.Mode = Camera.CameraMode.Fixed;
        GameDemo.Camera.FixedDistanceDistance = GameDemo.CameraTargetDistance;
        GameDemo.Camera.FixedDistanceVerticalOffset = 0;
        GameDemo.Camera.LookDownDistance = 20;
        GameDemo.Camera.AimLeadTime = 0.5f;
        GameDemo.Camera.PovOffset.X = 0;
        GameDemo.Camera.PovOffset.Y = 0.5f;
        GameDemo.Camera.PovOffset.Z = -2;

        Boid.InitializeObstacles();
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        // update flock simulation for each boid
        foreach (var boid in flock)
            boid.Update(currentTime, elapsedTime);
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // selected vehicle (user can mouse click to select another)
        var selected = GameDemo.SelectedVehicle;

        // vehicle nearest mouse (to be highlighted)
        var nearMouse = GameDemo.VehicleNearestToMouse();

        // update camera
        GameDemo.UpdateCamera(elapsedTime, selected);

        DrawObstacles();

        // draw each boid in flock
        foreach (var boid in flock)
            boid.Draw();

        // highlight vehicle nearest mouse
        GameDemo.DrawCircleHighlightOnVehicle(nearMouse, 1, Color.LightGray);

        // highlight selected vehicle
        GameDemo.DrawCircleHighlightOnVehicle(selected, 1, Color.Gray);

        // display status in the upper left corner of the window
        var status = new StringBuilder();
        status.AppendFormat("[F1/F2] {0} boids", population);
        status.Append("\n[F3]    PD type: ");
        switch (cyclePd)
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
        var screenLocation = new Vector3(15, 50, 0);
        Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, Color.LightGray);
    }

    public override void Close()
    {
        // delete each member of the flock
        while (population > 0)
            RemoveBoidFromFlock();

        // delete the proximity database
        pd = null;
    }

    public override void Reset()
    {
        // reset each boid in flock
        foreach (var boid in flock)
            boid.Reset();

        // reset camera position
        GameDemo.Position3dCamera(GameDemo.SelectedVehicle);

        // make camera jump immediately to new position
        GameDemo.Camera.DoNotSmoothNextMove();
    }

    // for purposes of demonstration, allow cycling through various
    // types of proximity databases.  this routine is called when the
    // Demo user pushes a function key.
    void NextPd()
    {
        // allocate new PD
        const int totalPd = 1;
        switch (cyclePd = (cyclePd + 1) % totalPd)
        {
            case 0:
            {
                var center = Vector3.Zero;
                const float div = 10.0f;
                var divisions = new Vector3(div, div, div);
                const float diameter = Boid.WorldRadius * 1.1f * 2;
                var dimensions = new Vector3(diameter, diameter, diameter);
                pd = new LocalityQueryProximityDatabase<IVehicle>(center, dimensions, divisions);
                break;
            }
        }

        // switch each boid to new PD
        foreach (var boid in flock)
            boid.NewPd(pd);

        // delete old PD (if any)
    }

    public override void HandleFunctionKeys(Keys key)
    {
        switch (key)
        {
            case Keys.F1: AddBoidToFlock(); break;
            case Keys.F2: RemoveBoidFromFlock(); break;
            case Keys.F3: NextPd(); break;
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

    void AddBoidToFlock()
    {
        population++;
        var boid = new Boid(pd, annotations);
        flock.Add(boid);
        if (population == 1) GameDemo.SelectedVehicle = boid;
    }

    void RemoveBoidFromFlock()
    {
        if (population <= 0)
            return;

        // save a pointer to the last boid, then remove it from the flock
        population--;
        var boid = flock[population];
        flock.RemoveAt(population);

        // if it is Demo's selected vehicle, unselect it
        if (boid == GameDemo.SelectedVehicle)
            GameDemo.SelectedVehicle = null;
    }

    // return an AVGroup containing each boid of the flock
    public override IEnumerable<IVehicle> Vehicles => flock.ConvertAll<IVehicle>(v => (IVehicle) v);

    // flock: a group (STL vector) of pointers to all boids
    readonly List<Boid> flock;

    // pointer to database used to accelerate proximity queries
    IProximityDatabase<IVehicle> pd;

    // keep track of current flock size
    int population;

    // which of the various proximity databases is currently in use
    int cyclePd;

    static void DrawObstacles()
    {
        //Color color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.4f));
        var allSo = Boid.AllObstacles;
        foreach (var obstacle in allSo)
        {
            //Drawing.DrawBasic3dSphere(allSO[so].Center, allSO[so].Radius, Color.Red);
            Drawing.Draw3dCircleOrDisk(obstacle.Radius, obstacle.Center, Vector3.UnitY, Color.Red, 10, true);
            Drawing.Draw3dCircleOrDisk(obstacle.Radius, obstacle.Center, Vector3.UnitX, Color.Red, 10, true);
            Drawing.Draw3dCircleOrDisk(obstacle.Radius, obstacle.Center, Vector3.UnitZ, Color.Red, 10, true);
        }
    }
}