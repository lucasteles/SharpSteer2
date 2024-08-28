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
using System.Numerics;

namespace Demo.PlugIns.MapDrive
{
	// A variation on PolylinePathway (whose path tube radius is constant)
	// GCRoute (Grand Challenge Route) has an array of radii-per-segment
	//
	// XXX The OpenSteer path classes are long overdue for a rewrite.  When
	// XXX that happens, support should be provided for constant-radius,
	// XXX radius-per-segment (as in GCRoute), and radius-per-vertex.
	public class GCRoute : PolylinePathway
	{
		// construct a GCRoute given the number of points (vertices), an
		// array of points, an array of per-segment path radii, and a flag
		// indiating if the path is connected at the end.
		public GCRoute(Vector3[] points, IList<float> radii, bool cyclic)
		{
			Initialize(points, radii[0], cyclic);

			Radii = new float[PointCount];

			// loop over all points
			for (int i = 0; i < PointCount; i++)
			{
				// copy in point locations, closing cycle when appropriate
				bool closeCycle = Cyclic && (i == PointCount - 1);
				int j = closeCycle ? 0 : i;
				Points[i] = points[j];
				Radii[i] = radii[i];
			}
		}

		// override the PolylinePathway method to allow for GCRoute-style
		// per-leg radii

		// Given an arbitrary point ("A"), returns the nearest point ("P") on
		// this path.  Also returns, via output arguments, the path tangent at
		// P and a measure of how far A is outside the Pathway's "tube".  Note
		// that a negative distance indicates A is inside the Pathway.
		public override Vector3 MapPointToPath(Vector3 point, out Vector3 tangent, out float outside)
		{
			Vector3 onPath = Vector3.Zero;
			tangent = Vector3.Zero;
			outside = float.MaxValue;

			// loop over all segments, find the one nearest to the given point
			for (int i = 1; i < PointCount; i++)
			{
			    Vector3 chosen;
                float d = PointToSegmentDistance(point, Points[i - 1], Points[i], Tangents[i], Lengths[i], out chosen);

				// measure how far original point is outside the Pathway's "tube"
				// (negative values (from 0 to -radius) measure "insideness")
				float o = d - Radii[i];

				// when this is the smallest "outsideness" seen so far, take
				// note and save the corresponding point-on-path and tangent
				if (o < outside)
				{
					outside = o;
                    onPath = chosen;
                    tangent = Tangents[i];
				}
			}

			// return point on path
			return onPath;
		}

		// ignore that "tangent" output argument which is never used
		// XXX eventually move this to Pathway class
		public Vector3 MapPointToPath(Vector3 point, out float outside)
		{
			Vector3 tangent;
			return MapPointToPath(point, out tangent, out outside);
		}

		// get the index number of the path segment nearest the given point
		// XXX consider moving this to path class
		public int IndexOfNearestSegment(Vector3 point)
		{
			int index = 0;
			float minDistance = float.MaxValue;

			// loop over all segments, find the one nearest the given point
			for (int i = 1; i < PointCount; i++)
			{
			    Vector3 chosen;
				float d = PointToSegmentDistance(point, Points[i - 1], Points[i], Tangents[i], Lengths[i], out chosen);
				if (d < minDistance)
				{
					minDistance = d;
					index = i;
				}
			}
			return index;
		}

		// returns the dot product of the tangents of two path segments, 
		// used to measure the "angle" at a path vertex: how sharp is the turn?
		public float DotSegmentUnitTangents(int segmentIndex0, int segmentIndex1)
		{
			return Vector3.Dot(Tangents[segmentIndex0], Tangents[segmentIndex1]);
		}

		// return path tangent at given point (its projection on path)
		public Vector3 TangentAt(Vector3 point)
		{
			return Tangents[IndexOfNearestSegment(point)];
		}

		// return path tangent at given point (its projection on path),
		// multiplied by the given pathfollowing direction (+1/-1 =
		// upstream/downstream).  Near path vertices (waypoints) use the
		// tangent of the "next segment" in the given direction
		public Vector3 TangentAt(Vector3 point, int pathFollowDirection)
		{
			int segmentIndex = IndexOfNearestSegment(point);
			int nextIndex = segmentIndex + pathFollowDirection;
			bool insideNextSegment = IsInsidePathSegment(point, nextIndex);
			int i = (segmentIndex + (insideNextSegment ? pathFollowDirection : 0));
			return Tangents[i] * pathFollowDirection;
		}

		// is the given point "near" a waypoint of this path?  ("near" == closer
		// to the waypoint than the max of radii of two adjacent segments)
		public bool NearWaypoint(Vector3 point)
		{
			// loop over all waypoints
			for (int i = 1; i < PointCount; i++)
			{
				// return true if near enough to this waypoint
				float r = Math.Max(Radii[i], Radii[(i + 1) % PointCount]);
				float d = (point - Points[i]).Length();
				if (d < r) return true;
			}
			return false;
		}

		// is the given point inside the path tube of the given segment
		// number?  (currently not used. this seemed like a useful utility,
		// but wasn't right for the problem I was trying to solve)
	    private bool IsInsidePathSegment(Vector3 point, int segmentIndex)
		{
			if (segmentIndex < 1 || segmentIndex >= PointCount) return false;

			int i = segmentIndex;

	        Vector3 chosen;
            float d = PointToSegmentDistance(point, Points[i - 1], Points[i], Tangents[i], Lengths[i], out chosen);

			// measure how far original point is outside the Pathway's "tube"
			// (negative values (from 0 to -radius) measure "insideness")
			float o = d - Radii[i];

			// return true if point is inside the tube
			return o < 0;
		}

		// per-segment radius (width) array
		public readonly float[] Radii;
	}
}
