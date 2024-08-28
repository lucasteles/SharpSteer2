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

namespace SharpSteer2.Demo.PlugIns.Pedestrian;

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
        if (testPath == null)
        {
            const float pathRadius = 2;

            const int pathPointCount = 7;
            const float size = 30;
            const float top = 2 * size;
            const float gap = 1.2f * size;
            const float outter = 2 * size;
            const float h = 0.5f;
            var pathPoints = new Vector3[pathPointCount]
            {
                new(h+gap-outter,  0,  h+top-outter), // 0 a
                new(h+gap,         0,  h+top),        // 1 b
                new(h+gap+(top/2), 0,  h+top/2),      // 2 c
                new(h+gap,         0,  h),            // 3 d
                new(h,             0,  h),            // 4 e
                new(h,             0,  h+top),        // 5 f
                new(h+gap,         0,  h+top/2)       // 6 g
            };

            Obstacle1.Center = Vector3.Lerp(pathPoints[0], pathPoints[1], 0.2f);
            Obstacle2.Center = Vector3.Lerp(pathPoints[2], pathPoints[3], 0.5f);
            Obstacle1.Radius = 3;
            Obstacle2.Radius = 5;
            Obstacles.Add(Obstacle1);
            Obstacles.Add(Obstacle2);

            Endpoint0 = pathPoints[0];
            Endpoint1 = pathPoints[pathPointCount - 1];

            testPath = new(pathPoints,
                pathRadius,
                false);
        }
        return testPath;
    }

    static PolylinePathway testPath;
    public static readonly SphericalObstacle Obstacle1 = new();
    public static readonly SphericalObstacle Obstacle2 = new();
    public static readonly List<IObstacle> Obstacles = new();
    public static Vector3 Endpoint0 = Vector3.Zero;
    public static Vector3 Endpoint1 = Vector3.Zero;
    public static bool UseDirectedPathFollowing = true;

    // this was added for debugging tool, but I might as well leave it in
    public static bool WanderSwitch = true;
}