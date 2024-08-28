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
using System.Numerics;
using SharpSteer2.Obstacles;

namespace Demo.PlugIns.Pedestrian
{
    class Globals
	{
		// create path for PlugIn 
		//
		//
		//        | gap |
		//
		//        f      b
		//        |\    /\        -
		//        | \  /  \       ^
		//        |  \/    \      |
		//        |  /\     \     |
		//        | /  \     c   top
		//        |/    \g  /     |
		//        /        /      |
		//       /|       /       V      z     y=0
		//      / |______/        -      ^
		//     /  e      d               |
		//   a/                          |
		//    |<---out-->|               o----> x
		//
		public static PolylinePathway GetTestPath()
		{
			if (_testPath == null)
			{
				const float PATH_RADIUS = 2;

				const int PATH_POINT_COUNT = 7;
				const float SIZE = 30;
				const float TOP = 2 * SIZE;
				const float GAP = 1.2f * SIZE;
				const float OUTTER = 2 * SIZE;
				const float H = 0.5f;
				Vector3[] pathPoints = new Vector3[PATH_POINT_COUNT]
					{
						new Vector3 (H+GAP-OUTTER,  0,  H+TOP-OUTTER), // 0 a
						new Vector3 (H+GAP,         0,  H+TOP),        // 1 b
						new Vector3 (H+GAP+(TOP/2), 0,  H+TOP/2),      // 2 c
						new Vector3 (H+GAP,         0,  H),            // 3 d
						new Vector3 (H,             0,  H),            // 4 e
						new Vector3 (H,             0,  H+TOP),        // 5 f
						new Vector3 (H+GAP,         0,  H+TOP/2)       // 6 g
					};

				Obstacle1.Center = Vector3.Lerp(pathPoints[0], pathPoints[1], 0.2f);
                Obstacle2.Center = Vector3.Lerp(pathPoints[2], pathPoints[3], 0.5f);
				Obstacle1.Radius = 3;
				Obstacle2.Radius = 5;
				Obstacles.Add(Obstacle1);
				Obstacles.Add(Obstacle2);

				Endpoint0 = pathPoints[0];
				Endpoint1 = pathPoints[PATH_POINT_COUNT - 1];

				_testPath = new PolylinePathway(pathPoints,
												 PATH_RADIUS,
												 false);
			}
			return _testPath;
		}

        private static PolylinePathway _testPath = null;
		public static readonly SphericalObstacle Obstacle1 = new SphericalObstacle();
		public static readonly SphericalObstacle Obstacle2 = new SphericalObstacle();
		public static readonly List<IObstacle> Obstacles = new List<IObstacle>();
		public static Vector3 Endpoint0 = Vector3.Zero;
		public static Vector3 Endpoint1 = Vector3.Zero;
		public static bool UseDirectedPathFollowing = true;

		// this was added for debugging tool, but I might as well leave it in
		public static bool WanderSwitch = true;
	}
}
