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

namespace Demo.PlugIns.Soccer
{
	public class AABBox
	{
		public AABBox(Vector3 min, Vector3 max)
		{
			_min = min;
			_max = max;
		}
		public bool IsInsideX(Vector3 p)
		{
			return !(p.X < _min.X || p.X > _max.X);
		}
		public bool IsInsideZ(Vector3 p)
		{
			return !(p.Z < _min.Z || p.Z > _max.Z);
		}
		public void Draw()
		{
			Vector3 b = new Vector3(_min.X, 0, _max.Z);
			Vector3 c = new Vector3(_max.X, 0, _min.Z);
			Color color = new Color(255, 255, 0);
			Drawing.DrawLineAlpha(_min, b, color, 1.0f);
			Drawing.DrawLineAlpha(b, _max, color, 1.0f);
			Drawing.DrawLineAlpha(_max, c, color, 1.0f);
			Drawing.DrawLineAlpha(c, _min, color, 1.0f);
		}

		Vector3 _min;
		Vector3 _max;
	}
}
