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
using SharpSteer2;
using Vector3 = System.Numerics.Vector3;

namespace Demo
{
	public sealed class Annotation : IAnnotationService
	{
		bool _isEnabled;

	    //HACK: change the IDraw to a IDrawService
		public static Drawing Drawer;

		// constructor
		public Annotation()
		{
			_isEnabled = true;
		}

		/// <summary>
		/// Indicates whether annotation is enabled.
		/// </summary>
		public bool IsEnabled
		{
			get { return _isEnabled; }
			set { _isEnabled = value; }
		}

		// ------------------------------------------------------------------------
		// drawing of lines, circles and (filled) disks to annotate steering
		// behaviors.  When called during OpenSteerDemo's simulation update phase,
		// these functions call a "deferred draw" routine which buffer the
		// arguments for use during the redraw phase.
		//
		// note: "circle" means unfilled
		//       "disk" means filled
		//       "XZ" means on a plane parallel to the X and Z axes (perp to Y)
		//       "3d" means the circle is perpendicular to the given "axis"
		//       "segments" is the number of line segments used to draw the circle

		// draw an opaque colored line segment between two locations in space
		public void Line(Vector3 startPoint, Vector3 endPoint, Vector3 color, float opacity = 1)
		{
			if (_isEnabled && Drawer != null)
			{
				Drawer.Line(startPoint, endPoint, new Color(new Microsoft.Xna.Framework.Vector3(color.X, color.Y, color.Z)), opacity);
			}
		}

		// draw a circle on the XZ plane
        public void CircleXZ(float radius, Vector3 center, Vector3 color, int segments)
		{
			CircleOrDiskXZ(radius, center, color, segments, false);
		}

		// draw a disk on the XZ plane
        public void DiskXZ(float radius, Vector3 center, Vector3 color, int segments)
		{
			CircleOrDiskXZ(radius, center, color, segments, true);
		}

		// draw a circle perpendicular to the given axis
        public void Circle3D(float radius, Vector3 center, Vector3 axis, Vector3 color, int segments)
		{
			CircleOrDisk3D(radius, center, axis, color, segments, false);
		}

		// draw a disk perpendicular to the given axis
        public void Disk3D(float radius, Vector3 center, Vector3 axis, Vector3 color, int segments)
		{
			CircleOrDisk3D(radius, center, axis, color, segments, true);
		}

		// ------------------------------------------------------------------------
		// support for annotation circles
        public void CircleOrDiskXZ(float radius, Vector3 center, Vector3 color, int segments, bool filled)
		{
			CircleOrDisk(radius, Vector3.Zero, center, color, segments, filled, false);
		}

        public void CircleOrDisk3D(float radius, Vector3 center, Vector3 axis, Vector3 color, int segments, bool filled)
		{
			CircleOrDisk(radius, axis, center, color, segments, filled, true);
		}

        public void CircleOrDisk(float radius, Vector3 axis, Vector3 center, Vector3 color, int segments, bool filled, bool in3D)
		{
			if (_isEnabled && Drawer != null)
			{
				Drawer.CircleOrDisk(radius, axis, center, new Color(new Microsoft.Xna.Framework.Vector3(color.X, color.Y, color.Z)), segments, filled, in3D);
			}
		}

		// called when steerToAvoidObstacles decides steering is required
		// (default action is to do nothing, layered classes can overload it)
		public void AvoidObstacle(float minDistanceToCollision)
		{
		}

		// called when steerToFollowPath decides steering is required
		// (default action is to do nothing, layered classes can overload it)
		public void PathFollowing(Vector3 future, Vector3 onPath, Vector3 target, float outside)
		{
		}

		// called when steerToAvoidCloseNeighbors decides steering is required
		// (default action is to do nothing, layered classes can overload it)
		public void AvoidCloseNeighbor(IVehicle other, float additionalDistance)
		{
		}

		// called when steerToAvoidNeighbors decides steering is required
		// (default action is to do nothing, layered classes can overload it)
		public void AvoidNeighbor(IVehicle threat, float steer, Vector3 ourFuture, Vector3 threatFuture)
		{
		}

		public void VelocityAcceleration(IVehicle vehicle)
		{
			VelocityAcceleration(vehicle, 3, 3);
		}

		public void VelocityAcceleration(IVehicle vehicle, float maxLength)
		{
			VelocityAcceleration(vehicle, maxLength, maxLength);
		}

		public void VelocityAcceleration(IVehicle vehicle, float maxLengthAcceleration, float maxLengthVelocity)
		{
            Vector3 vColor = new Vector3(255, 102, 255); // pinkish
            Vector3 aColor = new Vector3(102, 102, 255); // bluish

			float aScale = maxLengthAcceleration / vehicle.MaxForce;
			float vScale = maxLengthVelocity / vehicle.MaxSpeed;
			Vector3 p = vehicle.Position;

			Line(p, p + (vehicle.Velocity * vScale), vColor);
			Line(p, p + (vehicle.Acceleration * aScale), aColor);
		}
	}
}
