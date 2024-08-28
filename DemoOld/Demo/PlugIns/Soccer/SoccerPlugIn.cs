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
using SharpSteer2;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Soccer
{
	public class SoccerPlugIn : PlugIn
	{
		public SoccerPlugIn(IAnnotationService annotations = null)
            :base(annotations)
		{
			_teamA = new List<Player>();
			_teamB = new List<Player>();
			_allPlayers = new List<Player>();
		}

		public override String Name { get { return "Michael's Simple Soccer"; } }

		public override void Open()
		{
			// Make a field
			_bbox = new AABBox(new Vector3(-20, 0, -10), new Vector3(20, 0, 10));
			// Red goal
			_teamAGoal = new AABBox(new Vector3(-21, 0, -7), new Vector3(-19, 0, 7));
			// Blue Goal
			_teamBGoal = new AABBox(new Vector3(19, 0, -7), new Vector3(21, 0, 7));
			// Make a ball
			_ball = new Ball(_bbox);
			// Build team A
			const int PLAYER_COUNT_A = 8;
			for (int i = 0; i < PLAYER_COUNT_A; i++)
			{
                Player pMicTest = new Player(_teamA, _allPlayers, _ball, true, i, Annotations);
				Demo.SelectedVehicle = pMicTest;
				_teamA.Add(pMicTest);
				_allPlayers.Add(pMicTest);
			}
			// Build Team B
			const int PLAYER_COUNT_B = 8;
			for (int i = 0; i < PLAYER_COUNT_B; i++)
			{
                Player pMicTest = new Player(_teamB, _allPlayers, _ball, false, i, Annotations);
				Demo.SelectedVehicle = pMicTest;
				_teamB.Add(pMicTest);
				_allPlayers.Add(pMicTest);
			}
			// initialize camera
			Demo.Init2dCamera(_ball);
			Demo.Camera.Position = new Vector3(10, Demo.CAMERA2_D_ELEVATION, 10);
			Demo.Camera.FixedPosition = new Vector3(40);
			Demo.Camera.Mode = Camera.CameraMode.Fixed;
			_redScore = 0;
			_blueScore = 0;
		}

		public override void Update(float currentTime, float elapsedTime)
		{
			// update simulation of test vehicle
			foreach (Player player in _teamA)
			    player.Update(elapsedTime);
		    foreach (Player player in _teamB)
		        player.Update(elapsedTime);
		    _ball.Update(currentTime, elapsedTime);

			if (_teamAGoal.IsInsideX(_ball.Position) && _teamAGoal.IsInsideZ(_ball.Position))
			{
				_ball.Reset();	// Ball in blue teams goal, red scores
				_redScore++;
			}
			if (_teamBGoal.IsInsideX(_ball.Position) && _teamBGoal.IsInsideZ(_ball.Position))
			{
				_ball.Reset();	// Ball in red teams goal, blue scores
				_blueScore++;
			}
		}

		public override void Redraw(float currentTime, float elapsedTime)
		{
			// draw "ground plane"
			Demo.GridUtility(Vector3.Zero);

			// draw test vehicle
			foreach (Player player in _teamA)
			    player.Draw();
		    foreach (Player player in _teamB)
		        player.Draw();
		    _ball.Draw();
			_bbox.Draw();
			_teamAGoal.Draw();
			_teamBGoal.Draw();

			StringBuilder annote = new StringBuilder();
			annote.AppendFormat("Red: {0}", _redScore);
			Drawing.Draw2dTextAt3dLocation(annote.ToString(), new Vector3(23, 0, 0), new Color((byte)(255.0f * 1), (byte)(255.0f * 0.7f), (byte)(255.0f * 0.7f)));

			annote = new StringBuilder();
			annote.AppendFormat("Blue: {0}", _blueScore);
			Drawing.Draw2dTextAt3dLocation(annote.ToString(), new Vector3(-23, 0, 0), new Color((byte)(255.0f * 0.7f), (byte)(255.0f * 0.7f), (byte)(255.0f * 1)));

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
			Demo.UpdateCamera(elapsedTime, Demo.SelectedVehicle);
		}

		public override void Close()
		{
			_teamA.Clear();
			_teamB.Clear();
			_allPlayers.Clear();
		}

		public override void Reset()
		{
			// reset vehicle
			foreach (Player player in _teamA)
			    player.Reset();
		    foreach (Player player in _teamB)
			    player.Reset();
		    _ball.Reset();
		}

		//const AVGroup& allVehicles () {return (const AVGroup&) TeamA;}
        public override IEnumerable<IVehicle> Vehicles
		{
			get { return _teamA.ConvertAll<IVehicle>(p => (IVehicle) p); }
		}

	    readonly List<Player> _teamA;
	    readonly List<Player> _teamB;
	    readonly List<Player> _allPlayers;

		Ball _ball;
		AABBox _bbox;
		AABBox _teamAGoal;
		AABBox _teamBGoal;
		int _redScore;
		int _blueScore;
	}
}
