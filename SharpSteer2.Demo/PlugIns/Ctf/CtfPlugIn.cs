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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SharpSteer2.Helpers;
using SharpSteer2.Obstacles;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Ctf;
// spherical obstacle group

// Capture the Flag   (a portion of the traditional game)
//
// The "Capture the Flag" sample steering problem, proposed by Marcin
// Chady of the Working Group on Steering of the IGDA's AI Interface
// Standards Committee (http://www.igda.org/Committees/ai.htm) in this
// message (http://sourceforge.net/forum/message.php?msg_id=1642243):
//
//     "An agent is trying to reach a physical location while trying
//     to stay clear of a group of enemies who are actively seeking
//     him. The environment is littered with obstacles, so collision
//     avoidance is also necessary."
//
// Note that the enemies do not make use of their knowledge of the 
// seeker's goal by "guarding" it.  
//
// XXX hmm, rename them "attacker" and "defender"?
//
// 08-12-02 cwr: created 

public class CtfPlugIn : PlugIn
{
    readonly bool arrive;
    public readonly float BaseRadius;
    readonly int obstacles;

    public CtfSeeker CtfSeeker;
    public readonly CtfEnemy[] CtfEnemies;

    public CtfPlugIn(IAnnotationService annotations, int enemyCount = 6, bool arrive = false, float baseRadius = 1.5f, int obstacles = 50)
        :base(annotations)
    {
        this.arrive = arrive;
        BaseRadius = baseRadius;
        this.obstacles = obstacles;
        CtfEnemies = new CtfEnemy[enemyCount];

        all = new();
    }

    public override string Name => "Capture the Flag";

    public override float SelectionOrderSortKey => 0.01f;

    public override void Open()
    {
        // create the seeker ("hero"/"attacker")
        CtfSeeker = new(this, annotations, arrive);
        all.Add(CtfSeeker);

        // create the specified number of enemies, 
        // storing pointers to them in an array.
        for (var i = 0; i < CtfEnemies.Length; i++)
        {
            CtfEnemies[i] = new(this, annotations);
            all.Add(CtfEnemies[i]);
        }

        // initialize camera
        GameDemo.Init2dCamera(CtfSeeker);
        GameDemo.Camera.Mode = Camera.CameraMode.FixedDistanceOffset;
        GameDemo.Camera.FixedTarget = Vector3.Zero;
        GameDemo.Camera.FixedTarget.X = 15;
        GameDemo.Camera.FixedPosition.X = 80;
        GameDemo.Camera.FixedPosition.Y = 60;
        GameDemo.Camera.FixedPosition.Z = 0;

        CtfBase.InitializeObstacles(BaseRadius, obstacles);
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        // update the seeker
        CtfSeeker.Update(currentTime, elapsedTime);

        // update each enemy
        foreach (var enemy in CtfEnemies)
            enemy.Update(currentTime, elapsedTime);
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // selected vehicle (user can mouse click to select another)
        var selected = GameDemo.SelectedVehicle;

        // vehicle nearest mouse (to be highlighted)
        var nearMouse = GameDemo.VehicleNearestToMouse ();

        // update camera
        GameDemo.UpdateCamera(elapsedTime, selected);

        // draw "ground plane" centered between base and selected vehicle
        var goalOffset = Globals.HomeBaseCenter - GameDemo.Camera.Position;
        var goalDirection = Vector3.Normalize(goalOffset);
        var cameraForward = GameDemo.Camera.Xxxls().Forward;
        var goalDot = Vector3.Dot(cameraForward, goalDirection);
        var blend = Utilities.RemapIntervalClip(goalDot, 1, 0, 0.5f, 0);
        var gridCenter = Vector3.Lerp(selected.Position, Globals.HomeBaseCenter, blend);
        GameDemo.GridUtility(gridCenter);

        // draw the seeker, obstacles and home base
        CtfSeeker.Draw();
        DrawObstacles();
        DrawHomeBase();

        // draw each enemy
        foreach (var enemy in CtfEnemies)
            enemy.Draw();

        // highlight vehicle nearest mouse
        GameDemo.HighlightVehicleUtility(nearMouse);
    }

    public override void Close()
    {
        // delete seeker
        CtfSeeker = null;

        // delete each enemy
        for (var i = 0; i < CtfEnemies.Length; i++)
        {
            CtfEnemies[i] = null;
        }

        // clear the group of all vehicles
        all.Clear();
    }

    public override void Reset()
    {
        // count resets
        Globals.ResetCount++;

        // reset the seeker ("hero"/"attacker") and enemies
        CtfSeeker.Reset();
        foreach (var enemy in CtfEnemies)
            enemy.Reset();

        // reset camera position
        GameDemo.Position2dCamera(CtfSeeker);

        // make camera jump immediately to new position
        GameDemo.Camera.DoNotSmoothNextMove();
    }

    public override void HandleFunctionKeys(Keys key)
    {
        switch (key)
        {
            case Keys.F1: CtfBase.AddOneObstacle(BaseRadius); break;
            case Keys.F2: CtfBase.RemoveOneObstacle(); break;
        }
    }

    public override void PrintMiniHelpForFunctionKeys()
    {
#if TODO
			std.ostringstream message;
			message << "Function keys handled by ";
			message << '"' << name() << '"' << ':' << std.ends;
			Demo.printMessage (message);
			Demo.printMessage ("  F1     add one obstacle.");
			Demo.printMessage ("  F2     remove one obstacle.");
			Demo.printMessage ("");
#endif
    }

    public override IEnumerable<IVehicle> Vehicles => all.ConvertAll<IVehicle>(v => (IVehicle) v);

    void DrawHomeBase()
    {
        var up = new Vector3(0, 0.01f, 0);
        var atColor = new Color((byte)(255.0f * 0.3f), (byte)(255.0f * 0.3f), (byte)(255.0f * 0.5f));
        var noColor = Color.Gray;
        var reached = CtfSeeker.State == CtfBase.SeekerState.AtGoal;
        var baseColor = reached ? atColor : noColor;
        Drawing.DrawXzDisk(BaseRadius, Globals.HomeBaseCenter, baseColor, 40);
        Drawing.DrawXzDisk(BaseRadius / 15, Globals.HomeBaseCenter + up, Color.Black, 20);
    }

    static void DrawObstacles()
    {
        var color = new Color((byte)(255.0f * 0.8f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.4f));
        var allSo = CtfBase.AllObstacles;
        foreach (var soObstacle in allSo)
            Drawing.DrawXzCircle(soObstacle.Radius, soObstacle.Center, color, 40);
    }

    // a group (STL vector) of all vehicles in the PlugIn
    readonly List<CtfBase> all;
}