// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using Microsoft.Xna.Framework;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.Soccer;

public class AabBox
{
    public AabBox(Vector3 min, Vector3 max)
    {
        this.min = min;
        this.max = max;
    }
    public bool IsInsideX(Vector3 p) => !(p.X < min.X || p.X > max.X);

    public bool IsInsideZ(Vector3 p) => !(p.Z < min.Z || p.Z > max.Z);

    public void Draw()
    {
        var b = new Vector3(min.X, 0, max.Z);
        var c = new Vector3(max.X, 0, min.Z);
        var color = new Color(255, 255, 0);
        Drawing.DrawLineAlpha(min, b, color, 1.0f);
        Drawing.DrawLineAlpha(b, max, color, 1.0f);
        Drawing.DrawLineAlpha(max, c, color, 1.0f);
        Drawing.DrawLineAlpha(c, min, color, 1.0f);
    }

    Vector3 min;
    Vector3 max;
}