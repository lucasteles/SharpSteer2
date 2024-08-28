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
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Pedestrian;

public class PedestrianPlugIn : PlugIn
{
    public PedestrianPlugIn(IAnnotationService annotations)
        :base(annotations) =>
        crowd = new();

    public override string Name => "Pedestrians";

    public override float SelectionOrderSortKey => 0.02f;

    public override void Open()
    {
        // make the database used to accelerate proximity queries
        cyclePd = -1;
        NextPd();

        // create the specified number of Pedestrians
        population = 0;
        for (var i = 0; i < 100; i++) AddPedestrianToCrowd();

        // initialize camera and selectedVehicle
        var firstPedestrian = crowd[0];
        GameDemo.Init3dCamera(firstPedestrian);
        GameDemo.Camera.Mode = Camera.CameraMode.FixedDistanceOffset;

        GameDemo.Camera.FixedTarget.X = 15;
        GameDemo.Camera.FixedTarget.Y = 0;
        GameDemo.Camera.FixedTarget.Z = 30;

        GameDemo.Camera.FixedPosition.X = 15;
        GameDemo.Camera.FixedPosition.Y = 70;
        GameDemo.Camera.FixedPosition.Z = -70;
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        // update each Pedestrian
        foreach (var pedestrian in crowd)
            pedestrian.Update(currentTime, elapsedTime);
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // selected Pedestrian (user can mouse click to select another)
        var selected = GameDemo.SelectedVehicle;

        // Pedestrian nearest mouse (to be highlighted)
        var nearMouse = GameDemo.VehicleNearestToMouse();

        // update camera
        GameDemo.UpdateCamera(elapsedTime, selected);

        // draw "ground plane"
        if (GameDemo.SelectedVehicle is not null) gridCenter = selected.Position;
        GameDemo.GridUtility(gridCenter);

        // draw and annotate each Pedestrian
        foreach (var pedestrian in crowd)
            pedestrian.Draw();

        // draw the path they follow and obstacles they avoid
        DrawPathAndObstacles();

        // highlight Pedestrian nearest mouse
        GameDemo.HighlightVehicleUtility(nearMouse);

        // textual annotation (at the vehicle's screen position)
        SerialNumberAnnotationUtility(selected);

        // textual annotation for selected Pedestrian
        if (GameDemo.SelectedVehicle is not null)//FIXME: && annotation.IsEnabled)
        {
            var color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.8f), (byte)(255.0f * 1.0f));
            var textOffset = new Vector3(0, 0.25f, 0);
            var textPosition = selected.Position + textOffset;
            var camPosition = GameDemo.Camera.Position;
            var camDistance = Vector3.Distance(selected.Position, camPosition);

            var sb = new StringBuilder();
            sb.AppendFormat("1: speed: {0:0.00}\n", selected.Speed);
            sb.AppendFormat("2: cam dist: {0:0.0}\n", camDistance);
            Drawing.Draw2dTextAt3dLocation(sb.ToString(), textPosition, color);
        }

        // display status in the upper left corner of the window
        var status = new StringBuilder();
        status.AppendFormat("[F1/F2] Crowd size: {0}\n", population);
        status.Append("[F3] PD type: ");
        switch (cyclePd)
        {
            case 0: status.Append("LQ bin lattice"); break;
            case 1: status.Append("brute force"); break;
        }
        status.Append("\n[F4] ");
        status.Append(Globals.UseDirectedPathFollowing ? "Directed path following." : "Stay on the path.");
        status.Append("\n[F5] Wander: ");
        status.Append(Globals.WanderSwitch ? "yes" : "no");
        status.Append("\n");
        var screenLocation = new Vector3(15, 50, 0);
        Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, Color.LightGray);
    }

    void SerialNumberAnnotationUtility(ILocalSpaceBasis selected)
    {
        // display a Pedestrian's serial number as a text label near its
        // screen position when it is near the selected vehicle or mouse.
        if (selected is not null)//FIXME: && IsAnnotationEnabled)
        {
            foreach (IVehicle vehicle in crowd)
            {
                const float nearDistance = 6;
                var vp = vehicle.Position;
                //Vector3 np = nearMouse.Position;
                if (Vector3.Distance(vp, selected.Position) < nearDistance/* ||
                    (nearMouse is not null && (Vector3.Distance(vp, np) < nearDistance))*/)
                {
                    var sn = $"#{vehicle.GetHashCode()}";
                    var textColor = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 1), (byte)(255.0f * 0.8f));
                    var textOffset = new Vector3(0, 0.25f, 0);
                    var textPos = vehicle.Position + textOffset;
                    Drawing.Draw2dTextAt3dLocation(sn, textPos, textColor);
                }
            }
        }
    }

    static void DrawPathAndObstacles()
    {
        // draw a line along each segment of path
        var path = Globals.GetTestPath();
        for (var i = 0; i < path.PointCount; i++)
            if (i > 0) Drawing.DrawLine(path.Points[i], path.Points[i - 1], Color.Red);

        // draw obstacles
        Drawing.DrawXzCircle(Globals.Obstacle1.Radius, Globals.Obstacle1.Center, Color.White, 40);
        Drawing.DrawXzCircle(Globals.Obstacle2.Radius, Globals.Obstacle2.Center, Color.White, 40);
    }

    public override void Close()
    {
        // delete all Pedestrians
        while (population > 0) RemovePedestrianFromCrowd();
    }

    public override void Reset()
    {
        // reset each Pedestrian
        foreach (var pedestrian in crowd)
            pedestrian.Reset();

        // reset camera position
        GameDemo.Position2dCamera(GameDemo.SelectedVehicle);

        // make camera jump immediately to new position
        GameDemo.Camera.DoNotSmoothNextMove();
    }

    public override void HandleFunctionKeys(Keys key)
    {
        switch (key)
        {
            case Keys.F1: AddPedestrianToCrowd(); break;
            case Keys.F2: RemovePedestrianFromCrowd(); break;
            case Keys.F3: NextPd(); break;
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
        population++;
        var pedestrian = new Pedestrian(pd, annotations);
        crowd.Add(pedestrian);
        if (population == 1) GameDemo.SelectedVehicle = pedestrian;
    }

    void RemovePedestrianFromCrowd()
    {
        if (population > 0)
        {
            // save pointer to last pedestrian, then remove it from the crowd
            population--;
            var pedestrian = crowd[population];
            crowd.RemoveAt(population);

            // if it is OpenSteerDemo's selected vehicle, unselect it
            if (pedestrian == GameDemo.SelectedVehicle)
                GameDemo.SelectedVehicle = null;
        }
    }

    // for purposes of demonstration, allow cycling through various
    // types of proximity databases.  this routine is called when the
    // OpenSteerDemo user pushes a function key.
    void NextPd()
    {
        // allocate new PD
        const int totalPd = 1;
        switch (cyclePd = (cyclePd + 1) % totalPd)
        {
            case 0:
            {
                var center = Vector3.Zero;
                const float div = 20.0f;
                var divisions = new Vector3(div, 1.0f, div);
                const float diameter = 80.0f; //XXX need better way to get this
                var dimensions = new Vector3(diameter, diameter, diameter);
                pd = new LocalityQueryProximityDatabase<IVehicle>(center, dimensions, divisions);
                break;
            }
        }

        // switch each boid to new PD
        foreach (var pedestrian in crowd)
            pedestrian.NewPd(pd);
    }

    public override IEnumerable<IVehicle> Vehicles => crowd.ConvertAll<IVehicle>(p => (IVehicle) p);

    // crowd: a group (STL vector) of all Pedestrians
    readonly List<Pedestrian> crowd;

    Vector3 gridCenter;

    // pointer to database used to accelerate proximity queries
    IProximityDatabase<IVehicle> pd;

    // keep track of current flock size
    int population;

    // which of the various proximity databases is currently in use
    int cyclePd;
}
