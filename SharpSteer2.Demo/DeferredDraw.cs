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
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo;

public class DeferredLine
{
    static DeferredLine() => deferredLines = new(Size);

    public static void AddToBuffer(Vector3 s, Vector3 e, Color c)
    {
        if (index >= deferredLines.Count)
            deferredLines.Add(new());

        deferredLines[index].startPoint = s;
        deferredLines[index].endPoint = e;
        deferredLines[index].color = c;

        index++;
    }

    public static void DrawAll()
    {
        // draw all lines in the buffer
        for (var i = 0; i < index; i++)
        {
            var dl = deferredLines[i];
            Drawing.IDrawLine(dl.startPoint, dl.endPoint, dl.color);
        }

        // reset buffer index
        index = 0;
    }

    Vector3 startPoint;
    Vector3 endPoint;
    Color color;

    static int index;
    const int Size = 1000;
    static readonly List<DeferredLine> deferredLines;
}

public class DeferredCircle
{
    static DeferredCircle()
    {
        deferredCircleArray = new DeferredCircle[Size];
        for (var i = 0; i < Size; i++)
        {
            deferredCircleArray[i] = new();
        }
    }

    public static void AddToBuffer(float radius, Vector3 axis, Vector3 center, Color color, int segments, bool filled, bool in3D)
    {
        if (index < Size)
        {
            deferredCircleArray[index].radius = radius;
            deferredCircleArray[index].axis = axis;
            deferredCircleArray[index].center = center;
            deferredCircleArray[index].color = color;
            deferredCircleArray[index].segments = segments;
            deferredCircleArray[index].filled = filled;
            deferredCircleArray[index].in3D = in3D;
            index++;
        }
    }

    public static void DrawAll()
    {
        // draw all circles in the buffer
        for (var i = 0; i < index; i++)
        {
            var dc = deferredCircleArray[i];
            Drawing.DrawCircleOrDisk(dc.radius, dc.axis, dc.center, dc.color, dc.segments, dc.filled, dc.in3D);
        }

        // reset buffer index
        index = 0;
    }

    float radius;
    Vector3 axis;
    Vector3 center;
    Color color;
    int segments;
    bool filled;
    bool in3D;

    static int index;
    const int Size = 500;
    static readonly DeferredCircle[] deferredCircleArray;
}