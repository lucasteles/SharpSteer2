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

namespace SharpSteer2.Demo.PlugIns.Ctf;

static class Globals
{
    public static readonly Vector3 HomeBaseCenter = new(0, 0, 0);

    public const float MinStartRadius = 30;
    public const float MaxStartRadius = 40;

    public const float BrakingRate = 0.75f;

    public static readonly Color
        EvadeColor = new((byte)(255.0f * 0.6f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.3f)); // annotation

    public static readonly Color
        SeekColor = new((byte)(255.0f * 0.3f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.6f)); // annotation

    public static readonly Color ClearPathColor =
        new((byte)(255.0f * 0.3f), (byte)(255.0f * 0.6f), (byte)(255.0f * 0.3f)); // annotation

    public const float AvoidancePredictTimeMin = 0.9f;
    public const float AvoidancePredictTimeMax = 2;
    public static float AvoidancePredictTime = AvoidancePredictTimeMin;

    public static CtfSeeker Seeker = null;

    // count the number of times the simulation has reset (e.g. for overnight runs)
    public static int ResetCount = 0;
}
