// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Numerics;

namespace SharpSteer2.Demo.PlugIns.Soccer;

class Globals
{
    public static readonly Vector3[] PlayerPosition = {
        new(4,0,0),
        new(7,0,-5),
        new(7,0,5),
        new(10,0,-3),
        new(10,0,3),
        new(15,0, -8),
        new(15,0,0),
        new(15,0,8),
        new(4,0,0)
    };
}