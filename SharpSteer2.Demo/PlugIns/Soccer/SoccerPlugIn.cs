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
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Soccer;

public class SoccerPlugIn : PlugIn
{
    public SoccerPlugIn(IAnnotationService annotations = null)
        :base(annotations)
    {
        teamA = new();
        teamB = new();
        allPlayers = new();
    }

    public override string Name => "Michael's Simple Soccer";

    public override void Open()
    {
        // Make a field
        bbox = new(new(-20, 0, -10), new(20, 0, 10));
        // Red goal
        teamAGoal = new(new(-21, 0, -7), new(-19, 0, 7));
        // Blue Goal
        teamBGoal = new(new(19, 0, -7), new(21, 0, 7));
        // Make a ball
        ball = new(bbox);
        // Build team A
        const int playerCountA = 8;
        for (var i = 0; i < playerCountA; i++)
        {
            var pMicTest = new Player(teamA, allPlayers, ball, true, i, annotations);
            GameDemo.SelectedVehicle = pMicTest;
            teamA.Add(pMicTest);
            allPlayers.Add(pMicTest);
        }
        // Build Team B
        const int playerCountB = 8;
        for (var i = 0; i < playerCountB; i++)
        {
            var pMicTest = new Player(teamB, allPlayers, ball, false, i, annotations);
            GameDemo.SelectedVehicle = pMicTest;
            teamB.Add(pMicTest);
            allPlayers.Add(pMicTest);
        }
        // initialize camera
        GameDemo.Init2dCamera(ball);
        GameDemo.Camera.Position = new(10, GameDemo.Camera2DElevation, 10);
        GameDemo.Camera.FixedPosition = new(40);
        GameDemo.Camera.Mode = Camera.CameraMode.Fixed;
        redScore = 0;
        blueScore = 0;
    }

    public override void Update(float currentTime, float elapsedTime)
    {
        // update simulation of test vehicle
        foreach (var player in teamA)
            player.Update(elapsedTime);
        foreach (var player in teamB)
            player.Update(elapsedTime);
        ball.Update(currentTime, elapsedTime);

        if (teamAGoal.IsInsideX(ball.Position) && teamAGoal.IsInsideZ(ball.Position))
        {
            ball.Reset();	// Ball in blue teams goal, red scores
            redScore++;
        }
        if (teamBGoal.IsInsideX(ball.Position) && teamBGoal.IsInsideZ(ball.Position))
        {
            ball.Reset();	// Ball in red teams goal, blue scores
            blueScore++;
        }
    }

    public override void Redraw(float currentTime, float elapsedTime)
    {
        // draw "ground plane"
        GameDemo.GridUtility(Vector3.Zero);

        // draw test vehicle
        foreach (var player in teamA)
            player.Draw();
        foreach (var player in teamB)
            player.Draw();
        ball.Draw();
        bbox.Draw();
        teamAGoal.Draw();
        teamBGoal.Draw();

        var annote = new StringBuilder();
        annote.AppendFormat("Red: {0}", redScore);
        Drawing.Draw2dTextAt3dLocation(annote.ToString(), new(23, 0, 0), new((byte)(255.0f * 1), (byte)(255.0f * 0.7f), (byte)(255.0f * 0.7f)));

        annote = new();
        annote.AppendFormat("Blue: {0}", blueScore);
        Drawing.Draw2dTextAt3dLocation(annote.ToString(), new(-23, 0, 0), new((byte)(255.0f * 0.7f), (byte)(255.0f * 0.7f), (byte)(255.0f * 1)));

        // textual annotation (following the test vehicle's screen position)
#if IGNORED
			for (int i = 0; i < TeamA.Count; i++)
			{
				String anno = String.Format("      speed: {0:0.00} ID: {1} ", TeamA[i].speed(), i);
				Drawing.Draw2dTextAt3dLocation(anno, TeamA[i].position(), Color.Red);
			}
			Drawing.Draw2dTextAt3dLocation("start", Vector3.zero, Color.Green);
#endif
        // update camera, tracking test vehicle
        GameDemo.UpdateCamera(elapsedTime, GameDemo.SelectedVehicle);
    }

    public override void Close()
    {
        teamA.Clear();
        teamB.Clear();
        allPlayers.Clear();
    }

    public override void Reset()
    {
        // reset vehicle
        foreach (var player in teamA)
            player.Reset();
        foreach (var player in teamB)
            player.Reset();
        ball.Reset();
    }

    //const AVGroup& allVehicles () {return (const AVGroup&) TeamA;}
    public override IEnumerable<IVehicle> Vehicles => teamA.ConvertAll<IVehicle>(p => (IVehicle) p);

    readonly List<Player> teamA;
    readonly List<Player> teamB;
    readonly List<Player> allPlayers;

    Ball ball;
    AabBox bbox;
    AabBox teamAGoal;
    AabBox teamBGoal;
    int redScore;
    int blueScore;
}