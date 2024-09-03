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
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SharpSteer2.Helpers;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo;

public struct TextEntry
{
    public Color Color;
    public Vector2 Position;
    public string Text;
}

public class Drawing
{
    public static GameDemo Game = null;
    static Color curColor;
    static PrimitiveType curMode;
    static readonly List<VertexPositionColor> vertices = new();
    static readonly LocalSpace localSpace = new();

    static void SetColor(Color color) => curColor = color;

    static void DrawBegin(PrimitiveType mode) => curMode = mode;

    static void DrawEnd()
    {
        var primitiveCount = 0;

        switch (curMode)
        {
            case PrimitiveType.LineList:

                primitiveCount = vertices.Count / 2;
                break;
            case PrimitiveType.LineStrip:
                vertices.Add(vertices[0]);
                primitiveCount = vertices.Count - 1;
                break;
            case PrimitiveType.TriangleList:
                primitiveCount = vertices.Count / 3;
                break;
            case PrimitiveType.TriangleStrip:
                primitiveCount = vertices.Count - 2;
                break;
        }

        Game.Graphics.GraphicsDevice.DrawUserPrimitives(curMode, vertices.ToArray(), 0, primitiveCount);

        vertices.Clear();
    }

    static void AddVertex(Vector3 v) => vertices.Add(new(new(v.X, v.Y, v.Z), curColor));

    static void BeginDoubleSidedDrawing()
    {
        //HACK
        //cullMode = game.graphics.GraphicsDevice.RasterizerState.CullMode;
        //game.graphics.GraphicsDevice.RasterizerState.CullMode = CullMode.None;
    }

    static void EndDoubleSidedDrawing()
    {
        //game.graphics.GraphicsDevice.RasterizerState.CullMode = cullMode;
    }

    public static void IDrawLine(Vector3 startPoint, Vector3 endPoint, Color color)
    {
        SetColor(color);
        DrawBegin(PrimitiveType.LineList);
        AddVertex(startPoint);
        AddVertex(endPoint);
        DrawEnd();
    }

    static void IDrawTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        SetColor(color);
        DrawBegin(PrimitiveType.TriangleList);
        {
            AddVertex(a);
            AddVertex(b);
            AddVertex(c);
        }
        DrawEnd();
    }

    // Draw a single OpenGL quadrangle given four Vector3 vertices, and color.
    static void IDrawQuadrangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        SetColor(color);
        DrawBegin(PrimitiveType.TriangleStrip);
        {
            AddVertex(a);
            AddVertex(b);
            AddVertex(d);
            AddVertex(c);
        }
        DrawEnd();
    }

    // draw a line with alpha blending
    public void Line(Vector3 startPoint, Vector3 endPoint, Color color, float alpha = 1) => DrawLineAlpha(startPoint, endPoint, color, alpha);

    public void CircleOrDisk(float radius, Vector3 axis, Vector3 center, Color color, int segments, bool filled, bool in3D) => DrawCircleOrDisk(radius, axis, center, color, segments, filled, in3D);

    public static void DrawLine(Vector3 startPoint, Vector3 endPoint, Color color)
    {
        if (GameDemo.IsDrawPhase)
        {
            IDrawLine(startPoint, endPoint, color);
        }
        else
        {
            DeferredLine.AddToBuffer(startPoint, endPoint, color);
        }
    }

    // draw a line with alpha blending
    public static void DrawLineAlpha(Vector3 startPoint, Vector3 endPoint, Color color, float alpha)
    {
        var c = new Color(color.R, color.G, color.B, (byte)(255.0f * alpha));
        if (GameDemo.IsDrawPhase)
        {
            IDrawLine(startPoint, endPoint, c);
        }
        else
        {
            DeferredLine.AddToBuffer(startPoint, endPoint, c);
        }
    }

    // draw 2d lines in screen space: x and y are the relevant coordinates
    public static void Draw2dLine(Vector3 startPoint, Vector3 endPoint, Color color) => IDrawLine(startPoint, endPoint, color);

    // draws a "wide line segment": a rectangle of the given width and color
    // whose mid-line connects two given endpoints
    public static void DrawXzWideLine(Vector3 startPoint, Vector3 endPoint, Color color, float width)
    {
        var offset = Vector3.Normalize(endPoint - startPoint);
        var perp = localSpace.LocalRotateForwardToSide(offset);
        var radius = perp * width / 2;

        var a = startPoint + radius;
        var b = endPoint + radius;
        var c = endPoint - radius;
        var d = startPoint - radius;

        IDrawQuadrangle(a, b, c, d, color);
    }

    // draw a (filled-in, polygon-based) square checkerboard grid on the XZ
    // (horizontal) plane.
    //
    // ("size" is the length of a side of the overall grid, "subsquares" is the
    // number of subsquares along each edge (for example a standard checkboard
    // has eight), "center" is the 3d position of the center of the grid,
    // color1 and color2 are used for alternating subsquares.)
    public static void DrawXzCheckerboardGrid(float size, int subsquares, Vector3 center, Color color1, Color color2)
    {
        var half = size / 2;
        var spacing = size / subsquares;

        BeginDoubleSidedDrawing();
        {
            var flag1 = false;
            var p = -half;
            var corner = new Vector3();
            for (var i = 0; i < subsquares; i++)
            {
                var flag2 = flag1;
                var q = -half;
                for (var j = 0; j < subsquares; j++)
                {
                    corner.X = p;
                    corner.Y = 0;
                    corner.Z = q;

                    corner += center;
                    IDrawQuadrangle(corner,
                        corner + new Vector3(spacing, 0, 0),
                        corner + new Vector3(spacing, 0, spacing),
                        corner + new Vector3(0, 0, spacing),
                        flag2 ? color1 : color2);
                    flag2 = !flag2;
                    q += spacing;
                }
                flag1 = !flag1;
                p += spacing;
            }
        }
        EndDoubleSidedDrawing();
    }

    // draw a square grid of lines on the XZ (horizontal) plane.
    //
    // ("size" is the length of a side of the overall grid, "subsquares" is the
    // number of subsquares along each edge (for example a standard checkboard
    // has eight), "center" is the 3d position of the center of the grid, lines
    // are drawn in the specified "color".)
    public static void DrawXzLineGrid(float size, int subsquares, Vector3 center, Color color)
    {
        var half = size / 2;
        var spacing = size / subsquares;

        // set grid drawing color
        SetColor(color);

        // draw a square XZ grid with the given size and line count
        DrawBegin(PrimitiveType.LineList);
        var q = -half;
        for (var i = 0; i < subsquares + 1; i++)
        {
            var x1 = new Vector3(q, 0, +half); // along X parallel to Z
            var x2 = new Vector3(q, 0, -half);
            var z1 = new Vector3(+half, 0, q); // along Z parallel to X
            var z2 = new Vector3(-half, 0, q);

            AddVertex(x1 + center);
            AddVertex(x2 + center);
            AddVertex(z1 + center);
            AddVertex(z2 + center);

            q += spacing;
        }
        DrawEnd();
    }

    // draw the three axes of a LocalSpace: three lines parallel to the
    // basis vectors of the space, centered at its origin, of lengths
    // given by the coordinates of "size".
    public static void DrawAxes(ILocalSpaceBasis ls, Vector3 size, Color color)
    {
        var x = new Vector3(size.X / 2, 0, 0);
        var y = new Vector3(0, size.Y / 2, 0);
        var z = new Vector3(0, 0, size.Z / 2);

        IDrawLine(ls.GlobalizePosition(x), ls.GlobalizePosition(x * -1), color);
        IDrawLine(ls.GlobalizePosition(y), ls.GlobalizePosition(y * -1), color);
        IDrawLine(ls.GlobalizePosition(z), ls.GlobalizePosition(z * -1), color);
    }

    public static void DrawQuadrangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color) => IDrawQuadrangle(a, b, c, d, color);

    // draw the edges of a box with a given position, orientation, size
    // and color.  The box edges are aligned with the axes of the given
    // LocalSpace, and it is centered at the origin of that LocalSpace.
    // "size" is the main diagonal of the box.
    //
    // use gGlobalSpace to draw a box aligned with global space
    public static void DrawBoxOutline(ILocalSpaceBasis localSpace, Vector3 size, Color color)
    {
        var s = size / 2.0f;  // half of main diagonal

        var a = localSpace.GlobalizePosition(new(+s.X, +s.Y, +s.Z));
        var b = localSpace.GlobalizePosition(new(+s.X, -s.Y, +s.Z));
        var c = localSpace.GlobalizePosition(new(-s.X, -s.Y, +s.Z));
        var d = localSpace.GlobalizePosition(new(-s.X, +s.Y, +s.Z));

        var e = localSpace.GlobalizePosition(new(+s.X, +s.Y, -s.Z));
        var f = localSpace.GlobalizePosition(new(+s.X, -s.Y, -s.Z));
        var g = localSpace.GlobalizePosition(new(-s.X, -s.Y, -s.Z));
        var h = localSpace.GlobalizePosition(new(-s.X, +s.Y, -s.Z));

        IDrawLine(a, b, color);
        IDrawLine(b, c, color);
        IDrawLine(c, d, color);
        IDrawLine(d, a, color);

        IDrawLine(a, e, color);
        IDrawLine(b, f, color);
        IDrawLine(c, g, color);
        IDrawLine(d, h, color);

        IDrawLine(e, f, color);
        IDrawLine(f, g, color);
        IDrawLine(g, h, color);
        IDrawLine(h, e, color);
    }

    public static void DrawXzCircle(float radius, Vector3 center, Color color, int segments) => DrawXzCircleOrDisk(radius, center, color, segments, false);

    public static void DrawXzDisk(float radius, Vector3 center, Color color, int segments) => DrawXzCircleOrDisk(radius, center, color, segments, true);

    // drawing utility used by both drawXZCircle and drawXZDisk
    static void DrawXzCircleOrDisk(float radius, Vector3 center, Color color, int segments, bool filled) =>
        // draw a circle-or-disk on the XZ plane
        DrawCircleOrDisk(radius, Vector3.Zero, center, color, segments, filled, false);

    // a simple 2d vehicle on the XZ plane
    public static void DrawBasic2dCircularVehicle(IVehicle vehicle, Color color)
    {
        // "aspect ratio" of body (as seen from above)
        const float x = 0.5f;
        var y = (float)Math.Sqrt(1 - (x * x));

        // radius and position of vehicle
        var r = vehicle.Radius;
        var p = vehicle.Position;

        // shape of triangular body
        var u = new Vector3(0, 1, 0) * r * 0.05f; // slightly up
        var f = Vector3.UnitZ * r;
        var s = Vector3.UnitX * x * r;
        var b = Vector3.UnitZ * -y * r;

        var matrix = vehicle.ToMatrix();

        // draw double-sided triangle (that is: no (back) face culling)
        BeginDoubleSidedDrawing();
        IDrawTriangle(Vector3.Transform(f + u, matrix),
            Vector3.Transform(b - s + u, matrix),
            Vector3.Transform(b + s + u, matrix),
            color);
        EndDoubleSidedDrawing();

        // draw the circular collision boundary
        DrawXzCircle(r, p + u, Color.White, 20);
    }

    // a simple 3d vehicle
    public static void DrawBasic3dSphericalVehicle(IVehicle vehicle, Color color)
    {
        var vColor = color.ToVector3().ToNumerics();

        // "aspect ratio" of body (as seen from above)
        const float x = 0.5f;
        var y = (float)Math.Sqrt(1 - (x * x));

        // radius and position of vehicle
        var r = vehicle.Radius;
        var p = vehicle.Position;

        // body shape parameters
        var f = vehicle.Forward * r;
        var s = vehicle.Side * r * x;
        var u = vehicle.Up * r * x * 0.5f;
        var b = vehicle.Forward * r * -y;

        // vertex positions
        var nose = p + f;
        var side1 = p + b - s;
        var side2 = p + b + s;
        var top = p + b + u;
        var bottom = p + b - u;

        // colors
        const float j = +0.05f;
        const float k = -0.05f;
        var color1 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(j, j, k));
        var color2 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(j, k, j));
        var color3 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(k, j, j));
        var color4 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(k, j, k));
        var color5 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(k, k, j));

        // draw body
        IDrawTriangle(nose, side1, top, color1);  // top, side 1
        IDrawTriangle(nose, top, side2, color2);  // top, side 2
        IDrawTriangle(nose, bottom, side1, color3);  // bottom, side 1
        IDrawTriangle(nose, side2, bottom, color4);  // bottom, side 2
        IDrawTriangle(side1, side2, top, color5);  // top back
        IDrawTriangle(side2, side1, bottom, color5);  // bottom back
    }

    // a simple sphere
    public static void DrawBasic3dSphere(Vector3 position, float radius, Color color)
    {
        var vColor = color.ToVector3().ToNumerics();

        // "aspect ratio" of body (as seen from above)
        const float x = 0.5f;
        var y = (float)Math.Sqrt(1 - (x * x));

        // radius and position of vehicle
        var r = radius;
        var p = position;

        // body shape parameters
        var f = -Vector3.UnitZ * r;
        var s = -Vector3.UnitX * r * x;
        var u = Vector3.UnitY * r * x;
        var b = -Vector3.UnitZ * r * -y;

        // vertex positions
        var nose = p + f;
        var side1 = p + b - s;
        var side2 = p + b + s;
        var top = p + b + u;
        var bottom = p + b - u;

        // colors
        const float j = +0.05f;
        const float k = -0.05f;
        var color1 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(j, j, k));
        var color2 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(j, k, j));
        var color3 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(k, j, j));
        var color4 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(k, j, k));
        var color5 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(k, k, j));

        // draw body
        IDrawTriangle(nose, side1, top, color1);  // top, side 1
        IDrawTriangle(nose, top, side2, color2);  // top, side 2
        IDrawTriangle(nose, bottom, side1, color3);  // bottom, side 1
        IDrawTriangle(nose, side2, bottom, color4);  // bottom, side 2
        IDrawTriangle(side1, side2, top, color5);  // top back
        IDrawTriangle(side2, side1, bottom, color5);  // bottom back
    }

    // General purpose circle/disk drawing routine.  Draws circles or disks (as
    // specified by "filled" argument) and handles both special case 2d circles
    // on the XZ plane or arbitrary circles in 3d space (as specified by "in3d"
    // argument)
    public static void DrawCircleOrDisk(float radius, Vector3 axis, Vector3 center, Color color, int segments, bool filled, bool in3D)
    {
        if (GameDemo.IsDrawPhase)
        {
            var ls = new LocalSpace();
            if (in3D)
            {
                // define a local space with "axis" as the Y/up direction
                // (XXX should this be a method on  LocalSpace?)
                var unitAxis = Vector3.Normalize(axis);
                var unitPerp = Vector3.Normalize(axis.FindPerpendicularIn3d());
                ls.Up = unitAxis;
                ls.Forward = unitPerp;
                ls.Position = center;
                ls.SetUnitSideFromForwardAndUp();
            }

            // make disks visible (not culled) from both sides
            if (filled) BeginDoubleSidedDrawing();

            // point to be rotated about the (local) Y axis, angular step size
            var pointOnCircle = new Vector3(radius, 0, 0);
            var step = (float)(2 * Math.PI) / segments;

            // set drawing color
            SetColor(color);

            // begin drawing a triangle fan (for disk) or line loop (for circle)
            DrawBegin(filled ? PrimitiveType.TriangleStrip : PrimitiveType.LineStrip);

            // for the filled case, first emit the center point
            if (filled) AddVertex(in3D ? ls.Position : center);

            // rotate p around the circle in "segments" steps
            float sin = 0, cos = 0;
            var vertexCount = filled ? segments + 1 : segments;
            for (var i = 0; i < vertexCount; i++)
            {
                // emit next point on circle, either in 3d (globalized out
                // of the local space), or in 2d (offset from the center)
                AddVertex(in3D ? ls.GlobalizePosition(pointOnCircle) : pointOnCircle + center);

                // rotate point one more step around circle
                pointOnCircle = pointOnCircle.RotateAboutGlobalY(step, ref sin, ref cos);
            }

            // close drawing operation
            DrawEnd();
            if (filled) EndDoubleSidedDrawing();
        }
        else
        {
            DeferredCircle.AddToBuffer(radius, axis, center, color, segments, filled, in3D);
        }
    }

    public static void Draw3dCircleOrDisk(float radius, Vector3 center, Vector3 axis, Color color, int segments, bool filled) =>
        // draw a circle-or-disk in the given local space
        DrawCircleOrDisk(radius, axis, center, color, segments, filled, true);

    public static void Draw3dCircle(float radius, Vector3 center, Vector3 axis, Color color, int segments) => Draw3dCircleOrDisk(radius, center, axis, color, segments, false);

    public static void AllDeferredLines() => DeferredLine.DrawAll();

    public static void AllDeferredCirclesOrDisks() => DeferredCircle.DrawAll();

    public static void Draw2dTextAt3dLocation(string text, Vector3 location, Color color)
    {
        // XXX NOTE: "it would be nice if" this had a 2d screenspace offset for
        // the origin of the text relative to the screen space projection of
        // the 3d point.

        // set text color and raster position
        var p = Game.Graphics.GraphicsDevice.Viewport.Project(new(location.X, location.Y, location.Z), Game.ProjectionMatrix, Game.ViewMatrix, Game.WorldMatrix);
        var textEntry = new TextEntry { Color = color, Position = new(p.X, p.Y), Text = text };
        Game.AddText(textEntry);
    }

    public static void Draw2dTextAt2dLocation(string text, Vector3 location, Color color)
    {
        // set text color and raster position
        var textEntry = new TextEntry { Color = color, Position = new(location.X, location.Y), Text = text };
        Game.AddText(textEntry);
    }

    public static float GetWindowWidth() => 1024;

    public static float GetWindowHeight() => 640;
}
