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
using SharpSteer2.Helpers;
using SharpSteer2.Pathway;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.MapDrive;

public class MapDrivePlugIn : PlugIn
{
    public MapDrivePlugIn(IAnnotationService annotations)
        :base(annotations) =>
        vehicles = new();

    public override string Name => "Driving through map based obstacles";

    public override float SelectionOrderSortKey => 0.07f;

    public override void Open()
    {
        // make new MapDriver
        vehicle = new(annotations);
        vehicles.Add(vehicle);
        GameDemo.SelectedVehicle = vehicle;

        // marks as obstacles map cells adjacent to the path
        usePathFences = true;

        // scatter random rock clumps over map
        useRandomRocks = true;

        // init Demo camera
        initCamDist = 30;
        initCamElev = 15;
        GameDemo.Init2dCamera(vehicle, initCamDist, initCamElev);
        // "look straight down at vehicle" camera mode parameters
        GameDemo.Camera.LookDownDistance = 50;
        // "static" camera mode parameters
        GameDemo.Camera.FixedPosition = new(145);
        GameDemo.Camera.FixedTarget.X = 40;
        GameDemo.Camera.FixedTarget.Y = 0;
        GameDemo.Camera.FixedTarget.Z = 40;
        GameDemo.Camera.FixedUp = Vector3.UnitY;

        // reset this plugin
        Reset();
    }


    public override void Update(float currentTime, float elapsedTime)
    {
        // update simulation of test vehicle
        vehicle.Update(currentTime, elapsedTime);

        // when vehicle drives outside the world
        if (vehicle.HandleExitFromMap()) RegenerateMap();

        // QQQ first pass at detecting "stuck" state
        if (vehicle.Stuck && vehicle.RelativeSpeed() < 0.001f)
        {
            vehicle.StuckCount++;
            Reset();
        }
    }


    public override void Redraw(float currentTime, float elapsedTime)
    {
        // update camera, tracking test vehicle
        GameDemo.UpdateCamera(elapsedTime, vehicle);

        // draw "ground plane"  (make it 4x map size)
        const float s = MapDriver.WorldSize * 2;
        const float u = -0.2f;
        Drawing.DrawQuadrangle(new(+s, u, +s),
            new(+s, u, -s),
            new(-s, u, -s),
            new(-s, u, +s),
            new((byte)(255.0f * 0.8f), (byte)(255.0f * 0.7f), (byte)(255.0f * 0.5f))); // "sand"

        // draw map and path
        if (MapDriver.DemoSelect == 2) vehicle.DrawPath();
        vehicle.DrawMap();

        // draw test vehicle
        vehicle.Draw();

        // QQQ mark origin to help spot artifacts
        const float tick = 2;
        Drawing.DrawLine(new(tick, 0, 0), new(-tick, 0, 0), Color.Green);
        Drawing.DrawLine(new(0, 0, tick), new(0, 0, -tick), Color.Green);

        // compute conversion factor miles-per-hour to meters-per-second
        const float metersPerMile = 1609.344f;
        const float secondsPerHour = 3600;
// ReSharper disable InconsistentNaming
        const float MPSperMPH = metersPerMile / secondsPerHour;
// ReSharper restore InconsistentNaming

        // display status in the upper left corner of the window
        var status = new StringBuilder();
        status.AppendFormat("Speed: {0} mps ({1} mph), average: {2:0.0} mps\n\n",
            (int)vehicle.Speed,
            (int)(vehicle.Speed / MPSperMPH),
            vehicle.TotalDistance / vehicle.TotalTime);
        status.AppendFormat("collisions avoided for {0} seconds",
            (int)(GameDemo.Clock.TotalSimulationTime - vehicle.TimeOfLastCollision));
        if (vehicle.CountOfCollisionFreeTimes > 0)
        {
            status.AppendFormat("\nmean time between collisions: {0} ({1}/{2})",
                (int)(vehicle.SumOfCollisionFreeTimes / vehicle.CountOfCollisionFreeTimes),
                (int)vehicle.SumOfCollisionFreeTimes,
                vehicle.CountOfCollisionFreeTimes);
        }

        status.AppendFormat("\n\nStuck count: {0} ({1} cycles, {2} off path)",
            vehicle.StuckCount,
            vehicle.StuckCycleCount,
            vehicle.StuckOffPathCount);
        status.Append("\n\n[F1] ");
        if (1 == MapDriver.DemoSelect) status.Append("wander, ");
        if (2 == MapDriver.DemoSelect) status.Append("follow path, ");
        status.Append("avoid obstacle");

        if (2 == MapDriver.DemoSelect)
        {
            status.Append("\n[F2] path following direction: ");
            status.Append(vehicle.PathFollowDirection > 0 ? "+1" : "-1");
            status.Append("\n[F3] path fence: ");
            status.Append(usePathFences ? "on" : "off");
        }

        status.Append("\n[F4] rocks: ");
        status.Append(useRandomRocks ? "on" : "off");
        status.Append("\n[F5] prediction: ");
        status.Append(vehicle.CurvedSteering ? "curved" : "linear");
        if (2 == MapDriver.DemoSelect)
        {
            status.AppendFormat("\n\nLap {0} (completed: {1}%)",
                vehicle.LapsStarted,
                vehicle.LapsStarted < 2 ? 0 :
                    (int)(100 * ((float)vehicle.LapsFinished /
                                 (vehicle.LapsStarted - 1)))
            );

            status.AppendFormat("\nHints given: {0}, taken: {1}",
                vehicle.HintGivenCount,
                vehicle.HintTakenCount);
        }
        status.Append("\n");
        QqqRange("WR ", MapDriver.SavedNearestWr, status);
        QqqRange("R  ", MapDriver.SavedNearestR, status);
        QqqRange("L  ", MapDriver.SavedNearestL, status);
        QqqRange("WL ", MapDriver.SavedNearestWl, status);
        var screenLocation = new Vector3(15, 50, 0);
        var color = new Vector3(0.15f, 0.15f, 0.5f);
        Drawing.Draw2dTextAt2dLocation(status.ToString(), screenLocation, new(color));

        {
            var v = Drawing.GetWindowHeight() - 5;
            const float m = 10;
            var w = Drawing.GetWindowWidth();
            var f = w - (2 * m);

            // limit tick mark
            var l = vehicle.AnnoteMaxRelSpeed;
            Drawing.Draw2dLine(new(m + (f * l), v - 3, 0), new(m + (f * l), v + 3, 0), Color.Black);
            // two "inverse speedometers" showing limits due to curvature and
            // path alignment
            if (Math.Abs(l) > float.Epsilon)
            {
                var c = vehicle.AnnoteMaxRelSpeedCurve;
                var p = vehicle.AnnoteMaxRelSpeedPath;
                Drawing.Draw2dLine(new(m + (f * c), v + 1, 0), new(w - m, v + 1, 0), Color.Red);
                Drawing.Draw2dLine(new(m + (f * p), v - 2, 0), new(w - m, v - 1, 0), Color.Green);
            }
            // speedometer: horizontal line with length proportional to speed
            Drawing.Draw2dLine(new(m, v, 0), new(m + (f * s), v, 0), Color.White);
            // min and max tick marks
            Drawing.Draw2dLine(new(m, v, 0), new(m, v - 2, 0), Color.White);
            Drawing.Draw2dLine(new(w - m, v, 0), new(w - m, v - 2, 0), Color.White);
        }
    }

    static void QqqRange(string text, float range, StringBuilder status)
    {
        status.AppendFormat("\n{0}", text);
        if (range >= 9999.0f)
            status.Append("--");
        else
            status.Append((int)range);
    }

    public override void Close() => vehicles.Clear();

    public override void Reset()
    {
        RegenerateMap();

        // reset vehicle
        vehicle.Reset();

        // make camera jump immediately to new position
        GameDemo.Camera.DoNotSmoothNextMove();

        // reset camera position
        GameDemo.Position2dCamera(vehicle, initCamDist, initCamElev);
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
                const float m = MapDriver.WorldSize * 0.4f; // main diamond size
                const float n = MapDriver.WorldSize / 8; // notch size
                var q = new Vector3(0, 0, m - n);
                var s = new Vector3(2 * n, 0, 0);
                var c = s - q;
                var d =s + q;
                float[] pathRadii = { 10, 10 };
                Vector3[] pathPoints = { c, d };
                var r = new GcRoute(pathPoints, pathRadii, false);
                DrawPathFencesOnMap(vehicle.Map, r);
                break;
            }
        }
    }

    void ReversePathFollowDirection() => vehicle.PathFollowDirection = vehicle.PathFollowDirection > 0 ? -1 : +1;

    void TogglePathFences()
    {
        usePathFences = !usePathFences;
        Reset();
    }

    void ToggleRandomRocks()
    {
        useRandomRocks = !useRandomRocks;
        Reset();
    }

    void ToggleCurvedSteering()
    {
        vehicle.CurvedSteering = !vehicle.CurvedSteering;
        vehicle.IncrementalSteering = !vehicle.IncrementalSteering;
        Reset();
    }

    void SelectNextDemo()
    {
        var message = new StringBuilder();
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
        vehicle.Map.Clear();
        DrawRandomClumpsOfRocksOnMap(vehicle.Map);
        ClearCenterOfMap(vehicle.Map);

        // draw fences for first two demo modes
        if (MapDriver.DemoSelect < 2) DrawBoundaryFencesOnMap(vehicle.Map);

        // randomize path widths
        if (MapDriver.DemoSelect == 2)
        {
            var count = vehicle.Path.PointCount;
            var upstream = vehicle.PathFollowDirection > 0;
            var entryIndex = upstream ? 1 : count - 1;
            var exitIndex = upstream ? count - 1 : 1;
            var lastExitRadius = vehicle.Path.Radii[exitIndex];
            for (var i = 1; i < count; i++)
            {
                vehicle.Path.Radii[i] = RandomHelpers.Random(4, 19);
            }
            vehicle.Path.Radii[entryIndex] = lastExitRadius;
        }

        // mark path-boundary map cells as obstacles
        // (when in path following demo and appropriate mode is set)
        if (usePathFences && MapDriver.DemoSelect == 2)
            DrawPathFencesOnMap(vehicle.Map, vehicle.Path);
    }

    void DrawRandomClumpsOfRocksOnMap(TerrainMap map)
    {
        if (useRandomRocks)
        {
            const int spread = 4;
            var r = map.Cellwidth();
            var k = RandomHelpers.RandomInt(50, 150);

            for (var p = 0; p < k; p++)
            {
                var i = RandomHelpers.RandomInt(0, r - spread);
                var j = RandomHelpers.RandomInt(0, r - spread);
                var c = RandomHelpers.RandomInt(0, 10);

                for (var q = 0; q < c; q++)
                {
                    var m = RandomHelpers.RandomInt(0, spread);
                    var n = RandomHelpers.RandomInt(0, spread);
                    map.SetMapBit(i + m, j + n, true);
                }
            }
        }
    }

    static void DrawBoundaryFencesOnMap(TerrainMap map)
    {
        // QQQ it would make more sense to do this with a "draw line
        // QQQ on map" primitive, may need that for other things too

        var cw = map.Cellwidth();
        var ch = map.Cellheight();

        var r = cw - 1;
        var a = cw >> 3;
        var b = cw - a;
        var o = cw >> 4;
        var p = (cw - o) >> 1;
        var q = (cw + o) >> 1;

        for (var i = 0; i < cw; i++)
        {
            for (var j = 0; j < ch; j++)
            {
                var c = i > a && i < b && (i < p || i > q);
                if (i == 0 || j == 0 || i == r || j == r || (c && (i == j || i + j == r)))
                    map.SetMapBit(i, j, true);
            }
        }
    }

    static void ClearCenterOfMap(TerrainMap map)
    {
        var o = map.Cellwidth() >> 4;
        var p = (map.Cellwidth() - o) >> 1;
        var q = (map.Cellwidth() + o) >> 1;
        for (var i = p; i <= q; i++)
        for (var j = p; j <= q; j++)
            map.SetMapBit(i, j, false);
    }

    static void DrawPathFencesOnMap(TerrainMap map, IPathway path)
    {
        var xs = map.XSize / map.Resolution;
        var zs = map.ZSize / map.Resolution;
        var alongRow = new Vector3(xs, 0, 0);
        var nextRow = new Vector3(-map.XSize, 0, zs);
        var g = new Vector3((map.XSize - xs) / -2, 0, (map.ZSize - zs) / -2);
        for (var j = 0; j < map.Resolution; j++)
        {
            for (var i = 0; i < map.Resolution; i++)
            {
                var outside = path.HowFarOutsidePath(g);
                const float wallThickness = 1.0f;

                // set map cells adjacent to the outside edge of the path
                if (outside > 0 && outside < wallThickness)
                    map.SetMapBit(i, j, true);

                // clear all other off-path map cells
                if (outside > wallThickness) map.SetMapBit(i, j, false);

                g += alongRow;
            }
            g += nextRow;
        }
    }

    public override IEnumerable<IVehicle> Vehicles => vehicles.ConvertAll<IVehicle>(v => (IVehicle) v);

    MapDriver vehicle;
    readonly List<MapDriver> vehicles; // for allVehicles

    float initCamDist, initCamElev;

    bool usePathFences;
    bool useRandomRocks;
}
