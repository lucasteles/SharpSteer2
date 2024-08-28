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
using SharpSteer2;
using SharpSteer2.Helpers;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Demo
{
	public struct TextEntry
	{
		public Color Color;
		public Vector2 Position;
		public String Text;
	}

	public class Drawing
	{
		public static Demo Game = null;
	    static Color _curColor;
		static PrimitiveType _curMode;
		static readonly List<VertexPositionColor> _vertices = new List<VertexPositionColor>();
		static readonly LocalSpace _localSpace = new LocalSpace();

		static void SetColor(Color color)
		{
			_curColor = color;
		}

        static void drawBegin(PrimitiveType mode)
		{
			_curMode = mode;
		}

		static void drawEnd()
		{
			int primitiveCount = 0;

			switch (_curMode)
			{
			case PrimitiveType.LineList:
	
				primitiveCount = _vertices.Count / 2;
				break;
			case PrimitiveType.LineStrip:
				_vertices.Add(_vertices[0]);
				primitiveCount = _vertices.Count - 1;
				break;
            case PrimitiveType.TriangleList:
				primitiveCount = _vertices.Count / 3;
				break;
            case PrimitiveType.TriangleStrip:
				primitiveCount = _vertices.Count - 2;
				break;
			}

            Game.Graphics.GraphicsDevice.DrawUserPrimitives(_curMode, _vertices.ToArray(), 0, primitiveCount);

			_vertices.Clear();
		}

		static void AddVertex(Vector3 v)
		{
			_vertices.Add(new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(v.X, v.Y, v.Z), _curColor));
		}

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

		public static void iDrawLine(Vector3 startPoint, Vector3 endPoint, Color color)
		{
            SetColor(color);
			drawBegin(PrimitiveType.LineList);
			AddVertex(startPoint);
			AddVertex(endPoint);
            drawEnd();
		}

		static void iDrawTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
		{
            SetColor(color);
			drawBegin(PrimitiveType.TriangleList);
			{
				AddVertex(a);
                AddVertex(b);
                AddVertex(c);
			}
			drawEnd();
		}

		// Draw a single OpenGL quadrangle given four Vector3 vertices, and color.
		static void iDrawQuadrangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
		{
            SetColor(color);
            drawBegin(PrimitiveType.TriangleStrip);
			{
                AddVertex(a);
                AddVertex(b);
                AddVertex(d);
                AddVertex(c);
			}
			drawEnd();
		}

		// draw a line with alpha blending
        public void Line(Vector3 startPoint, Vector3 endPoint, Color color, float alpha = 1)
		{
			DrawLineAlpha(startPoint, endPoint, color, alpha);
		}

        public void CircleOrDisk(float radius, Vector3 axis, Vector3 center, Color color, int segments, bool filled, bool in3D)
		{
			DrawCircleOrDisk(radius, axis, center, color, segments, filled, in3D);
		}

        public static void DrawLine(Vector3 startPoint, Vector3 endPoint, Color color)
		{
			if (Demo.IsDrawPhase)
			{
				iDrawLine(startPoint, endPoint, color);
			}
			else
			{
				DeferredLine.AddToBuffer(startPoint, endPoint, color);
			}
		}

		// draw a line with alpha blending
		public static void DrawLineAlpha(Vector3 startPoint, Vector3 endPoint, Color color, float alpha)
		{
			Color c = new Color(color.R, color.G, color.B, (byte)(255.0f * alpha));
			if (Demo.IsDrawPhase)
			{
				iDrawLine(startPoint, endPoint, c);
			}
			else
			{
				DeferredLine.AddToBuffer(startPoint, endPoint, c);
			}
		}

		// draw 2d lines in screen space: x and y are the relevant coordinates
		public static void Draw2dLine(Vector3 startPoint, Vector3 endPoint, Color color)
		{
			iDrawLine(startPoint, endPoint, color);
		}

		// draws a "wide line segment": a rectangle of the given width and color
		// whose mid-line connects two given endpoints
		public static void DrawXZWideLine(Vector3 startPoint, Vector3 endPoint, Color color, float width)
		{
			Vector3 offset = Vector3.Normalize(endPoint - startPoint);
            Vector3 perp = _localSpace.LocalRotateForwardToSide(offset);
			Vector3 radius = perp * width / 2;

			Vector3 a = startPoint + radius;
			Vector3 b = endPoint + radius;
			Vector3 c = endPoint - radius;
			Vector3 d = startPoint - radius;

			iDrawQuadrangle(a, b, c, d, color);
		}

		// draw a (filled-in, polygon-based) square checkerboard grid on the XZ
		// (horizontal) plane.
		//
		// ("size" is the length of a side of the overall grid, "subsquares" is the
		// number of subsquares along each edge (for example a standard checkboard
		// has eight), "center" is the 3d position of the center of the grid,
		// color1 and color2 are used for alternating subsquares.)
		public static void DrawXZCheckerboardGrid(float size, int subsquares, Vector3 center, Color color1, Color color2)
		{
			float half = size / 2;
			float spacing = size / subsquares;

			BeginDoubleSidedDrawing();
			{
				bool flag1 = false;
				float p = -half;
				Vector3 corner = new Vector3();
				for (int i = 0; i < subsquares; i++)
				{
					bool flag2 = flag1;
					float q = -half;
					for (int j = 0; j < subsquares; j++)
					{
						corner.X = p;
                        corner.Y = 0;
                        corner.Z = q;

						corner += center;
						iDrawQuadrangle(corner,
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
		public static void DrawXZLineGrid(float size, int subsquares, Vector3 center, Color color)
		{
			float half = size / 2;
			float spacing = size / subsquares;

			// set grid drawing color
            SetColor(color);

			// draw a square XZ grid with the given size and line count
			drawBegin(PrimitiveType.LineList);
			float q = -half;
			for (int i = 0; i < (subsquares + 1); i++)
			{
				Vector3 x1 = new Vector3(q, 0, +half); // along X parallel to Z
				Vector3 x2 = new Vector3(q, 0, -half);
				Vector3 z1 = new Vector3(+half, 0, q); // along Z parallel to X
				Vector3 z2 = new Vector3(-half, 0, q);

                AddVertex(x1 + center);
                AddVertex(x2 + center);
                AddVertex(z1 + center);
                AddVertex(z2 + center);

				q += spacing;
			}
			drawEnd();
		}

		// draw the three axes of a LocalSpace: three lines parallel to the
		// basis vectors of the space, centered at its origin, of lengths
		// given by the coordinates of "size".
		public static void DrawAxes(ILocalSpaceBasis ls, Vector3 size, Color color)
		{
			Vector3 x = new Vector3(size.X / 2, 0, 0);
			Vector3 y = new Vector3(0, size.Y / 2, 0);
			Vector3 z = new Vector3(0, 0, size.Z / 2);

			iDrawLine(ls.GlobalizePosition(x), ls.GlobalizePosition(x * -1), color);
			iDrawLine(ls.GlobalizePosition(y), ls.GlobalizePosition(y * -1), color);
			iDrawLine(ls.GlobalizePosition(z), ls.GlobalizePosition(z * -1), color);
		}

		public static void DrawQuadrangle(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
		{
			iDrawQuadrangle(a, b, c, d, color);
		}

		// draw the edges of a box with a given position, orientation, size
		// and color.  The box edges are aligned with the axes of the given
		// LocalSpace, and it is centered at the origin of that LocalSpace.
		// "size" is the main diagonal of the box.
		//
		// use gGlobalSpace to draw a box aligned with global space
		public static void DrawBoxOutline(ILocalSpaceBasis localSpace, Vector3 size, Color color)
		{
			Vector3 s = size / 2.0f;  // half of main diagonal

			Vector3 a = localSpace.GlobalizePosition(new Vector3(+s.X, +s.Y, +s.Z));
			Vector3 b = localSpace.GlobalizePosition(new Vector3(+s.X, -s.Y, +s.Z));
			Vector3 c = localSpace.GlobalizePosition(new Vector3(-s.X, -s.Y, +s.Z));
			Vector3 d = localSpace.GlobalizePosition(new Vector3(-s.X, +s.Y, +s.Z));

			Vector3 e = localSpace.GlobalizePosition(new Vector3(+s.X, +s.Y, -s.Z));
			Vector3 f = localSpace.GlobalizePosition(new Vector3(+s.X, -s.Y, -s.Z));
			Vector3 g = localSpace.GlobalizePosition(new Vector3(-s.X, -s.Y, -s.Z));
			Vector3 h = localSpace.GlobalizePosition(new Vector3(-s.X, +s.Y, -s.Z));

			iDrawLine(a, b, color);
			iDrawLine(b, c, color);
			iDrawLine(c, d, color);
			iDrawLine(d, a, color);

			iDrawLine(a, e, color);
			iDrawLine(b, f, color);
			iDrawLine(c, g, color);
			iDrawLine(d, h, color);

			iDrawLine(e, f, color);
			iDrawLine(f, g, color);
			iDrawLine(g, h, color);
			iDrawLine(h, e, color);
		}

		public static void DrawXZCircle(float radius, Vector3 center, Color color, int segments)
		{
			DrawXZCircleOrDisk(radius, center, color, segments, false);
		}

		public static void DrawXZDisk(float radius, Vector3 center, Color color, int segments)
		{
			DrawXZCircleOrDisk(radius, center, color, segments, true);
		}

		// drawing utility used by both drawXZCircle and drawXZDisk
	    private static void DrawXZCircleOrDisk(float radius, Vector3 center, Color color, int segments, bool filled)
		{
			// draw a circle-or-disk on the XZ plane
			DrawCircleOrDisk(radius, Vector3.Zero, center, color, segments, filled, false);
		}

		// a simple 2d vehicle on the XZ plane
		public static void DrawBasic2dCircularVehicle(IVehicle vehicle, Color color)
		{
			// "aspect ratio" of body (as seen from above)
			const float X = 0.5f;
			float y = (float)Math.Sqrt(1 - (X * X));

			// radius and position of vehicle
			float r = vehicle.Radius;
			Vector3 p = vehicle.Position;

			// shape of triangular body
			Vector3 u = new Vector3(0, 1, 0) * r * 0.05f; // slightly up
			Vector3 f = Vector3.UnitZ * r;
			Vector3 s = Vector3.UnitX * X * r;
			Vector3 b = Vector3.UnitZ * -y * r;

		    var matrix = vehicle.ToMatrix();

			// draw double-sided triangle (that is: no (back) face culling)
			BeginDoubleSidedDrawing();
            iDrawTriangle(Vector3.Transform(f + u, matrix),
						  Vector3.Transform(b - s + u, matrix),
						  Vector3.Transform(b + s + u, matrix),
						  color);
			EndDoubleSidedDrawing();

			// draw the circular collision boundary
			DrawXZCircle(r, p + u, Color.White, 20);
		}

		// a simple 3d vehicle
		public static void DrawBasic3dSphericalVehicle(IVehicle vehicle, Color color)
		{
		    var vColor = color.ToVector3();

			// "aspect ratio" of body (as seen from above)
			const float X = 0.5f;
			float y = (float)Math.Sqrt(1 - (X * X));

			// radius and position of vehicle
			float r = vehicle.Radius;
			Vector3 p = vehicle.Position;

			// body shape parameters
			Vector3 f = vehicle.Forward * r;
			Vector3 s = vehicle.Side * r * X;
			Vector3 u = vehicle.Up * r * X * 0.5f;
			Vector3 b = vehicle.Forward * r * -y;

			// vertex positions
			Vector3 nose = p + f;
			Vector3 side1 = p + b - s;
			Vector3 side2 = p + b + s;
			Vector3 top = p + b + u;
			Vector3 bottom = p + b - u;

			// colors
			const float J = +0.05f;
			const float K = -0.05f;
			Color color1 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(J, J, K));
            Color color2 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(J, K, J));
            Color color3 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(K, J, J));
            Color color4 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(K, J, K));
            Color color5 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(K, K, J));

			// draw body
			iDrawTriangle(nose, side1, top, color1);  // top, side 1
			iDrawTriangle(nose, top, side2, color2);  // top, side 2
			iDrawTriangle(nose, bottom, side1, color3);  // bottom, side 1
			iDrawTriangle(nose, side2, bottom, color4);  // bottom, side 2
			iDrawTriangle(side1, side2, top, color5);  // top back
			iDrawTriangle(side2, side1, bottom, color5);  // bottom back
		}

		// a simple sphere
		public static void DrawBasic3dSphere(Vector3 position, float radius, Color color)
		{
		    var vColor = color.ToVector3();

			// "aspect ratio" of body (as seen from above)
			const float X = 0.5f;
			float y = (float)Math.Sqrt(1 - (X * X));

			// radius and position of vehicle
			float r = radius;
			Vector3 p = position;

			// body shape parameters
			Vector3 f = -Vector3.UnitZ * r;
			Vector3 s = -Vector3.UnitX * r * X;
			Vector3 u = Vector3.UnitY * r * X;
			Vector3 b = -Vector3.UnitZ * r * -y;

			// vertex positions
			Vector3 nose = p + f;
			Vector3 side1 = p + b - s;
			Vector3 side2 = p + b + s;
			Vector3 top = p + b + u;
			Vector3 bottom = p + b - u;

			// colors
			const float J = +0.05f;
			const float K = -0.05f;
            Color color1 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(J, J, K));
            Color color2 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(J, K, J));
            Color color3 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(K, J, J));
            Color color4 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(K, J, K));
            Color color5 = new Color(vColor + new Microsoft.Xna.Framework.Vector3(K, K, J));

			// draw body
			iDrawTriangle(nose, side1, top, color1);  // top, side 1
			iDrawTriangle(nose, top, side2, color2);  // top, side 2
			iDrawTriangle(nose, bottom, side1, color3);  // bottom, side 1
			iDrawTriangle(nose, side2, bottom, color4);  // bottom, side 2
			iDrawTriangle(side1, side2, top, color5);  // top back
			iDrawTriangle(side2, side1, bottom, color5);  // bottom back
		}

		// General purpose circle/disk drawing routine.  Draws circles or disks (as
		// specified by "filled" argument) and handles both special case 2d circles
		// on the XZ plane or arbitrary circles in 3d space (as specified by "in3d"
		// argument)
		public static void DrawCircleOrDisk(float radius, Vector3 axis, Vector3 center, Color color, int segments, bool filled, bool in3D)
		{
			if (Demo.IsDrawPhase)
			{
				LocalSpace ls = new LocalSpace();
				if (in3D)
				{
					// define a local space with "axis" as the Y/up direction
					// (XXX should this be a method on  LocalSpace?)
					Vector3 unitAxis = Vector3.Normalize(axis);
					Vector3 unitPerp = Vector3.Normalize(axis.FindPerpendicularIn3d());
					ls.Up = unitAxis;
					ls.Forward = unitPerp;
					ls.Position = (center);
					ls.SetUnitSideFromForwardAndUp();
				}

				// make disks visible (not culled) from both sides 
				if (filled) BeginDoubleSidedDrawing();

				// point to be rotated about the (local) Y axis, angular step size
				Vector3 pointOnCircle = new Vector3(radius, 0, 0);
				float step = (float)(2 * Math.PI) / segments;

				// set drawing color
                SetColor(color);

				// begin drawing a triangle fan (for disk) or line loop (for circle)
				drawBegin(filled ? PrimitiveType.TriangleStrip : PrimitiveType.LineStrip);

				// for the filled case, first emit the center point
                if (filled) AddVertex(in3D ? ls.Position : center);

				// rotate p around the circle in "segments" steps
				float sin = 0, cos = 0;
				int vertexCount = filled ? segments + 1 : segments;
				for (int i = 0; i < vertexCount; i++)
				{
					// emit next point on circle, either in 3d (globalized out
					// of the local space), or in 2d (offset from the center)
                    AddVertex(in3D ? ls.GlobalizePosition(pointOnCircle) : pointOnCircle + center);

					// rotate point one more step around circle
                    pointOnCircle = pointOnCircle.RotateAboutGlobalY(step, ref sin, ref cos);
				}

				// close drawing operation
				drawEnd();
				if (filled) EndDoubleSidedDrawing();
			}
			else
			{
				DeferredCircle.AddToBuffer(radius, axis, center, color, segments, filled, in3D);
			}
		}

		public static void Draw3dCircleOrDisk(float radius, Vector3 center, Vector3 axis, Color color, int segments, bool filled)
		{
			// draw a circle-or-disk in the given local space
			DrawCircleOrDisk(radius, axis, center, color, segments, filled, true);
		}

		public static void Draw3dCircle(float radius, Vector3 center, Vector3 axis, Color color, int segments)
		{
			Draw3dCircleOrDisk(radius, center, axis, color, segments, false);
		}

		public static void AllDeferredLines()
		{
			DeferredLine.DrawAll();
		}

		public static void AllDeferredCirclesOrDisks()
		{
			DeferredCircle.DrawAll();
		}

		public static void Draw2dTextAt3dLocation(String text, Vector3 location, Color color)
		{
			// XXX NOTE: "it would be nice if" this had a 2d screenspace offset for
			// the origin of the text relative to the screen space projection of
			// the 3d point.

			// set text color and raster position
			var p = Game.Graphics.GraphicsDevice.Viewport.Project(new Microsoft.Xna.Framework.Vector3(location.X, location.Y, location.Z), Game.ProjectionMatrix, Game.ViewMatrix, Game.WorldMatrix);
			TextEntry textEntry = new TextEntry { Color = color, Position = new Vector2(p.X, p.Y), Text = text };
		    Game.AddText(textEntry);
		}

		public static void Draw2dTextAt2dLocation(String text, Vector3 location, Color color)
		{
			// set text color and raster position
			TextEntry textEntry = new TextEntry { Color = color, Position = new Vector2(location.X, location.Y), Text = text };
		    Game.AddText(textEntry);
		}

		public static float GetWindowWidth()
		{
			return 1024;
		}

		public static float GetWindowHeight()
		{
			return 640;
		}
	}
}
