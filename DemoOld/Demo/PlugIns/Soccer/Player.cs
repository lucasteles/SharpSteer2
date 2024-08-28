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
using SharpSteer2;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.Soccer
{
	public class Player : SimpleVehicle
	{
		Trail _trail;

        public override float MaxForce { get { return 3000.7f; } }
        public override float MaxSpeed { get { return 10; } }

		// constructor
        public Player(List<Player> others, List<Player> allplayers, Ball ball, bool isTeamA, int id, IAnnotationService annotations = null)
            :base(annotations)
		{
            _allPlayers = allplayers;
			_ball = ball;
			_imTeamA = isTeamA;
			_myID = id;

			Reset();
		}

		// reset state
		public override void Reset()
		{
			base.Reset(); // reset the vehicle 
			Speed = 0.0f;         // speed along Forward direction.

			// Place me on my part of the field, looking at oponnents goal
			Position = new Vector3(_imTeamA ? RandomHelpers.Random() * 20 : -RandomHelpers.Random() * 20, 0, (RandomHelpers.Random() - 0.5f) * 20);
			if (_myID < 9)
			{
				Position = _imTeamA ? (Globals.PlayerPosition[_myID]) : (new Vector3(-Globals.PlayerPosition[_myID].X, Globals.PlayerPosition[_myID].Y, Globals.PlayerPosition[_myID].Z));
			}
			_home = Position;

			if (_trail == null) _trail = new Trail(10, 60);
			_trail.Clear();    // prevent long streaks due to teleportation 
		}

		// per frame simulation update
		public void Update(float elapsedTime)
		{
			// if I hit the ball, kick it.
			float distToBall = Vector3.Distance(Position, _ball.Position);
			float sumOfRadii = Radius + _ball.Radius;
			if (distToBall < sumOfRadii)
				_ball.Kick((_ball.Position - Position) * 50);

			// otherwise consider avoiding collisions with others
			Vector3 collisionAvoidance = SteerToAvoidNeighbors(1, _allPlayers);
			if (collisionAvoidance != Vector3.Zero)
				ApplySteeringForce(collisionAvoidance, elapsedTime);
			else
			{
				float distHomeToBall = Vector3.Distance(_home, _ball.Position);
				if (distHomeToBall < 12)
				{
					// go for ball if I'm on the 'right' side of the ball
					if (_imTeamA ? Position.X > _ball.Position.X : Position.X < _ball.Position.X)
					{
						Vector3 seekTarget = SteerForSeek(_ball.Position);
						ApplySteeringForce(seekTarget, elapsedTime);
					}
					else
					{
						if (distHomeToBall < 12)
						{
							float z = _ball.Position.Z - Position.Z > 0 ? -1.0f : 1.0f;
							Vector3 behindBall = _ball.Position + (_imTeamA ? new Vector3(2, 0, z) : new Vector3(-2, 0, z));
							Vector3 behindBallForce = SteerForSeek(behindBall);
                            annotation.Line(Position, behindBall, Color.Green.ToVector3().FromXna());
							Vector3 evadeTarget = SteerForFlee(_ball.Position);
							ApplySteeringForce(behindBallForce * 10 + evadeTarget, elapsedTime);
						}
					}
				}
				else	// Go home
				{
					Vector3 seekTarget = SteerForSeek(_home);
					Vector3 seekHome = SteerForSeek(_home);
					ApplySteeringForce(seekTarget + seekHome, elapsedTime);
				}

			}
		}

		// draw this character/vehicle into the scene
		public void Draw()
		{
			Drawing.DrawBasic2dCircularVehicle(this, _imTeamA ? Color.Red : Color.Blue);
            _trail.Draw(annotation);
		}

		// per-instance reference to its group
	    readonly List<Player> _allPlayers;
	    readonly Ball _ball;
	    readonly bool _imTeamA;
	    readonly int _myID;
		Vector3 _home;
	}
}
