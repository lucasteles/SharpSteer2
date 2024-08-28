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

namespace Demo
{
	public class DeferredLine
	{
		static DeferredLine()
		{
			_deferredLines = new List<DeferredLine>(SIZE);
		}

		public static void AddToBuffer(Vector3 s, Vector3 e, Color c)
		{
		    if (_index >= _deferredLines.Count)
		        _deferredLines.Add(new DeferredLine());

            _deferredLines[_index]._startPoint = s;
            _deferredLines[_index]._endPoint = e;
            _deferredLines[_index]._color = c;

            _index++;
		}

		public static void DrawAll()
		{
			// draw all lines in the buffer
			for (int i = 0; i < _index; i++)
			{
				DeferredLine dl = _deferredLines[i];
				Drawing.iDrawLine(dl._startPoint, dl._endPoint, dl._color);
			}

			// reset buffer index
			_index = 0;
		}

		Vector3 _startPoint;
		Vector3 _endPoint;
		Color _color;

		static int _index = 0;
		const int SIZE = 1000;
		static readonly List<DeferredLine> _deferredLines;
	}

	public class DeferredCircle
	{
		static DeferredCircle()
		{
			_deferredCircleArray = new DeferredCircle[SIZE];
			for (int i = 0; i < SIZE; i++)
			{
				_deferredCircleArray[i] = new DeferredCircle();
			}
		}

		public static void AddToBuffer(float radius, Vector3 axis, Vector3 center, Color color, int segments, bool filled, bool in3D)
		{
			if (_index < SIZE)
			{
				_deferredCircleArray[_index]._radius = radius;
				_deferredCircleArray[_index]._axis = axis;
				_deferredCircleArray[_index]._center = center;
				_deferredCircleArray[_index]._color = color;
				_deferredCircleArray[_index]._segments = segments;
				_deferredCircleArray[_index]._filled = filled;
				_deferredCircleArray[_index]._in3D = in3D;
				_index++;
			}
		}

		public static void DrawAll()
		{
			// draw all circles in the buffer
			for (int i = 0; i < _index; i++)
			{
				DeferredCircle dc = _deferredCircleArray[i];
				Drawing.DrawCircleOrDisk(dc._radius, dc._axis, dc._center, dc._color, dc._segments, dc._filled, dc._in3D);
			}

			// reset buffer index
			_index = 0;
		}

		float _radius;
		Vector3 _axis;
		Vector3 _center;
		Color _color;
		int _segments;
		bool _filled;
		bool _in3D;

		static int _index = 0;
		const int SIZE = 500;
		static readonly DeferredCircle[] _deferredCircleArray;
	}
}
