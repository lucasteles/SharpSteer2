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
using Microsoft.Xna.Framework;
using SharpSteer2;
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace Demo.PlugIns.MapDrive
{
	public class MapDriver : SimpleVehicle
	{
		Trail _trail;

        public override float MaxForce { get { return 20 * 0.4f; } }
        public override float MaxSpeed { get { return 20; } }

		// constructor
        public MapDriver(IAnnotationService annotations = null)
            :base(annotations)
		{
			Map = MakeMap();
			Path = MakePath();

			Reset();

			// to compute mean time between collisions
			SumOfCollisionFreeTimes = 0;
			CountOfCollisionFreeTimes = 0;

			// keep track for reliability statistics
			_collisionLastTime = false;
			TimeOfLastCollision = Demo.Clock.TotalSimulationTime;

			// keep track of average speed
			TotalDistance = 0;
			TotalTime = 0;

			// innitialize counters for various performance data
			StuckCount = 0;
			StuckCycleCount = 0;
			StuckOffPathCount = 0;
			LapsStarted = 0;
			LapsFinished = 0;
			HintGivenCount = 0;
			HintTakenCount = 0;

			// follow the path "upstream or downstream" (+1/-1)
			PathFollowDirection = -1;

			// use curved prediction and incremental steering:
			CurvedSteering = true;
			IncrementalSteering = true;
		}

		// reset state
		public override void Reset()
		{
			// reset the underlying vehicle class
			base.Reset();

			// initially stopped
			Speed = 0;

			// vehicle is 2 meters wide and 3 meters long
			_halfWidth = 1.0f;
			_halfLength = 1.5f;

			// init dynamically controlled radius
			AdjustVehicleRadiusForSpeed();

			// not previously avoiding
			_annotateAvoid = Vector3.Zero;

			// 10 seconds with 200 points along the trail
			if (_trail == null) _trail = new Trail(10, 200);

			// prevent long streaks due to teleportation 
			_trail.Clear();

			// first pass at detecting "stuck" state
			Stuck = false;

			// QQQ need to clean up this hack
			_qqqLastNearestObstacle = Vector3.Zero;

			// master look ahead (prediction) time
			_baseLookAheadTime = 3;

			if (DemoSelect == 2)
			{
				LapsStarted++;
				const float S = WORLD_SIZE;
				float d = PathFollowDirection;
				Position = (new Vector3(S * d * 0.6f, 0, S * -0.4f));
				RegenerateOrthonormalBasisUF(Vector3.UnitX * d);
			}

			// reset bookeeping to detect stuck cycles
			ResetStuckCycleDetection();

			// assume no previous steering
			_currentSteering = Vector3.Zero;

			// assume normal running state
			_dtZero = false;

			// QQQ temporary global QQQoaJustScraping
			_qqQoaJustScraping = false;

			// state saved for speedometer
			AnnoteMaxRelSpeed = AnnoteMaxRelSpeedCurve = AnnoteMaxRelSpeedPath = 0;
			AnnoteMaxRelSpeed = AnnoteMaxRelSpeedCurve = AnnoteMaxRelSpeedPath = 1;
		}


		// per frame simulation update
		public void Update(float currentTime, float elapsedTime)
		{
			// take note when current dt is zero (as in paused) for stat counters
// ReSharper disable CompareOfFloatsByEqualityOperator
			_dtZero = (elapsedTime == 0);
// ReSharper restore CompareOfFloatsByEqualityOperator

			// pretend we are bigger when going fast
			AdjustVehicleRadiusForSpeed();

			// state saved for speedometer
			//      annoteMaxRelSpeed = annoteMaxRelSpeedCurve = annoteMaxRelSpeedPath = 0;
			AnnoteMaxRelSpeed = AnnoteMaxRelSpeedCurve = AnnoteMaxRelSpeedPath = 1;

			// determine combined steering
			Vector3 steering = Vector3.Zero;
			bool offPath = !IsBodyInsidePath();
			if (Stuck || offPath || DetectImminentCollision())
			{
				// bring vehicle to a stop if we are stuck (newly or previously
				// stuck, because off path or collision seemed imminent)
				// (QQQ combine with stuckCycleCount code at end of this function?)
				//ApplyBrakingForce (curvedSteering ? 3 : 2, elapsedTime); // QQQ
				ApplyBrakingForce((CurvedSteering ? 3.0f : 2.0f), elapsedTime); // QQQ
				// count "off path" events
				if (offPath && !Stuck && (DemoSelect == 2)) StuckOffPathCount++;
				Stuck = true;

				// QQQ trying to prevent "creep" during emergency stops
				ResetAcceleration();
				_currentSteering = Vector3.Zero;
			}
			else
			{
				// determine steering for obstacle avoidance (save for annotation)
				Vector3 avoid = _annotateAvoid = SteerToAvoidObstaclesOnMap(LookAheadTimeOA(), HintForObstacleAvoidance());
				bool needToAvoid = avoid != Vector3.Zero;

				// any obstacles to avoid?
				if (needToAvoid)
				{
					// slow down and turn to avoid the obstacles
					float targetSpeed = ((CurvedSteering && _qqQoaJustScraping) ? MaxSpeedForCurvature() : 0);
					AnnoteMaxRelSpeed = targetSpeed / MaxSpeed;
					float avoidWeight = 3 + (3 * RelativeSpeed()); // ad hoc
					steering = avoid * avoidWeight;
					steering += SteerForTargetSpeed(targetSpeed);
				}
				else
				{
					// otherwise speed up and...
					steering = SteerForTargetSpeed(MaxSpeedForCurvature());

					// wander for demo 1
					if (DemoSelect == 1)
					{
						Vector3 wander = SteerForWander(elapsedTime);
						wander.Y = 0;
						Vector3 flat = wander;
						Vector3 weighted = flat.TruncateLength(MaxForce) * 6;
						Vector3 a = Position + new Vector3(0, 0.2f, 0);
                        annotation.Line(a, a + (weighted * 0.3f), Color.White.ToVector3().FromXna());
						steering += weighted;
					}

					// follow the path in demo 2
					if (DemoSelect == 2)
					{
						Vector3 pf = SteerToFollowPath(PathFollowDirection, LookAheadTimePF());
						if (pf != Vector3.Zero)
						{
							// steer to remain on path
							if (Vector3.Dot(pf, Forward) < 0)
								steering = pf;
							else
								steering = pf + steering;
						}
						else
						{
							// path aligment: when neither obstacle avoidance nor
							// path following is required, align with path segment
							Vector3 pathHeading = Path.TangentAt(Position, PathFollowDirection);
							{
								Vector3 b = (Position + (Up * 0.2f) + (Forward * _halfLength * 1.4f));
								const float L = 2;
                                annotation.Line(b, b + (Forward * L), Color.Cyan.ToVector3().FromXna());
                                annotation.Line(b, b + (pathHeading * L), Color.Cyan.ToVector3().FromXna());
							}
							steering += (SteerTowardHeading(pathHeading) *
										 (Path.NearWaypoint(Position) ?
										  0.5f : 0.1f));
						}
					}
				}
			}

			if (!Stuck)
			{
				// convert from absolute to incremental steering signal
				if (IncrementalSteering)
					steering = ConvertAbsoluteToIncrementalSteering(steering, elapsedTime);
				// enforce minimum turning radius
				steering = AdjustSteeringForMinimumTurningRadius(steering);
			}

			// apply selected steering force to vehicle, record data
			ApplySteeringForce(steering, elapsedTime);
			CollectReliabilityStatistics(currentTime, elapsedTime);

			// detect getting stuck in cycles -- we are moving but not
			// making progress down the route (annotate smoothedPosition)
			if (DemoSelect == 2)
			{
				bool circles = WeAreGoingInCircles();
				if (circles && !Stuck) StuckCycleCount++;
				if (circles) Stuck = true;
                annotation.CircleOrDisk(0.5f, Up, SmoothedPosition, Color.White.ToVector3().FromXna(), 12, circles, false);
			}

			// annotation
			PerFrameAnnotation();
			_trail.Record(currentTime, Position);
		}

	    private void AdjustVehicleRadiusForSpeed()
		{
            float minRadius = (float)Math.Sqrt(_halfWidth * _halfWidth + _halfLength * _halfLength);
			float safetyMargin = (CurvedSteering ? MathHelper.Lerp(0.0f, 1.5f, RelativeSpeed()) : 0.0f);
			Radius = (minRadius + safetyMargin);
		}

	    private void CollectReliabilityStatistics(float currentTime, float elapsedTime)
		{
			// detect obstacle avoidance failure and keep statistics
			_collisionDetected = Map.ScanLocalXZRectangle(this,
														   -_halfWidth, _halfWidth,
														   -_halfLength, _halfLength);

			// record stats to compute mean time between collisions
			float timeSinceLastCollision = currentTime - TimeOfLastCollision;
			if (_collisionDetected && !_collisionLastTime && timeSinceLastCollision > 1)
			{
				SumOfCollisionFreeTimes += timeSinceLastCollision;
				CountOfCollisionFreeTimes++;
				TimeOfLastCollision = currentTime;
			}
			_collisionLastTime = _collisionDetected;

			// keep track of average speed
			TotalDistance += Speed * elapsedTime;
			TotalTime += elapsedTime;
		}

	    private Vector3 HintForObstacleAvoidance()
		{
			// used only when path following, return zero ("no hint") otherwise
			if (DemoSelect != 2) return Vector3.Zero;

			// are we heading roughly parallel to the current path segment?
			Vector3 p = Position;
			Vector3 pathHeading = Path.TangentAt(p, PathFollowDirection);
			if (Vector3.Dot(pathHeading, Forward) < 0.8f)
			{
				// if not, the "hint" is to turn to align with path heading
				Vector3 s = Side * _halfWidth;
				float f = _halfLength * 2;
                annotation.Line(p + s, p + s + (Forward * f), Color.Black.ToVector3().FromXna());
                annotation.Line(p - s, p - s + (Forward * f), Color.Black.ToVector3().FromXna());
                annotation.Line(p, p + (pathHeading * 5), Color.Magenta.ToVector3().FromXna());
				return pathHeading;
			}
			else
			{
				// when there is a valid nearest obstacle position
				Vector3 obstacle = _qqqLastNearestObstacle;
				Vector3 o = obstacle + (Up * 0.1f);
				if (obstacle != Vector3.Zero)
				{
					// get offset, distance from obstacle to its image on path
					float outside;
					Vector3 onPath = Path.MapPointToPath(obstacle, out outside);
					Vector3 offset = onPath - obstacle;
					float offsetDistance = offset.Length();

					// when the obstacle is inside the path tube
					if (outside < 0)
					{
						// when near the outer edge of a sufficiently wide tube
						int segmentIndex = Path.IndexOfNearestSegment(onPath);
						float segmentRadius = Path.Radii[segmentIndex];
						float w = _halfWidth * 6;
						bool nearEdge = offsetDistance > w;
						bool wideEnough = segmentRadius > (w * 2);
						if (nearEdge && wideEnough)
						{
							float obstacleDistance = (obstacle - p).Length();
							float range = Speed * LookAheadTimeOA();
							float farThreshold = range * 0.8f;
							bool usableHint = obstacleDistance > farThreshold;
							if (usableHint)
							{
								Vector3 temp = Vector3.Normalize(offset);
								Vector3 q = p + (temp * 5);
                                annotation.Line(p, q, Color.Magenta.ToVector3().FromXna());
                                annotation.CircleOrDisk(0.4f, Up, o, Color.White.ToVector3().FromXna(), 12, false, false);
								return offset;
							}
						}
					}
                    annotation.CircleOrDisk(0.4f, Up, o, Color.Black.ToVector3().FromXna(), 12, false, false);
				}
			}
			// otherwise, no hint
			return Vector3.Zero;
		}

		// given a map of obstacles (currently a global, binary map) steer so as
		// to avoid collisions within the next minTimeToCollision seconds.
		//
	    private Vector3 SteerToAvoidObstaclesOnMap(float minTimeToCollision, Vector3 steerHint)
		{
			float spacing = Map.MinSpacing() / 2;
			float maxSide = Radius;
			float maxForward = minTimeToCollision * Speed;
			int maxSamples = (int)(maxForward / spacing);
			Vector3 step = Forward * spacing;
			Vector3 fOffset = Position;
			Vector3 sOffset = Vector3.Zero;
			float s = spacing / 2;

			const int INFINITY = 9999; // qqq
			int nearestL = INFINITY;
			int nearestR = INFINITY;
			int nearestWL = INFINITY;
			int nearestWR = INFINITY;
			Vector3 nearestO = Vector3.Zero;
			_wingDrawFlagL = false;
			_wingDrawFlagR = false;

			bool hintGiven = steerHint != Vector3.Zero;
			if (hintGiven && !_dtZero)
				HintGivenCount++;
			if (hintGiven)
                annotation.CircleOrDisk(_halfWidth * 0.9f, Up, Position + (Up * 0.2f), Color.White.ToVector3().FromXna(), 12, false, false);

			// QQQ temporary global QQQoaJustScraping
			_qqQoaJustScraping = true;

			float signedRadius = 1 / NonZeroCurvatureQQQ();
			Vector3 localCenterOfCurvature = Side * signedRadius;
			Vector3 center = Position + localCenterOfCurvature;
			float sign = signedRadius < 0 ? 1.0f : -1.0f;
			float arcRadius = signedRadius * -sign;
			const float TWO_PI = 2 * (float)Math.PI;
			float circumference = TWO_PI * arcRadius;
			float rawLength = Speed * minTimeToCollision * sign;
			const float FRAC_LIMIT = 1.0f / 6.0f;
			float distLimit = circumference * FRAC_LIMIT;
			float arcLength = ArcLengthLimit(rawLength, distLimit);
			float arcAngle = TWO_PI * arcLength / circumference;

			// XXX temp annotation to show limit on arc angle
			if (CurvedSteering)
			{
				if ((Speed * minTimeToCollision) > (circumference * FRAC_LIMIT))
				{
					const float Q = TWO_PI * FRAC_LIMIT;
					Vector3 fooz = Position - center;
					Vector3 booz = fooz.RotateAboutGlobalY(sign * Q);
                    annotation.Line(center, center + fooz, Color.Red.ToVector3().FromXna());
                    annotation.Line(center, center + booz, Color.Red.ToVector3().FromXna());
				}
			}

            // scan corridor straight ahead of vehicle,
            // keep track of nearest obstacle on left and right sides
            for (; s < maxSide; s += spacing, sOffset = Side * s)
	        {
				Vector3 lOffset = fOffset + sOffset;
				Vector3 rOffset = fOffset - sOffset;

				Vector3 lObsPos = Vector3.Zero, rObsPos = Vector3.Zero;

				int l = (CurvedSteering ?
							   (int)(ScanObstacleMap(lOffset,
													   center,
													   arcAngle,
													   maxSamples,
													   0,
													   Color.Yellow,
													   Color.Red,
													   out lObsPos)
									  / spacing) :
							   Map.ScanXZray(lOffset, step, maxSamples));
				int r = (CurvedSteering ?
							   (int)(ScanObstacleMap(rOffset,
														center,
													   arcAngle,
													   maxSamples,
													   0,
													   Color.Yellow,
													   Color.Red,
													   out rObsPos)
									  / spacing) :
							   Map.ScanXZray(rOffset, step, maxSamples));

				if ((l > 0) && (l < nearestL))
				{
					nearestL = l;
					if (l < nearestR) nearestO = ((CurvedSteering) ?
												  lObsPos :
												  lOffset + (step * l));
				}
				if ((r > 0) && (r < nearestR))
				{
					nearestR = r;
					if (r < nearestL) nearestO = ((CurvedSteering) ?
												  rObsPos :
												  rOffset + (step * r));
				}

				if (!CurvedSteering)
				{
					AnnotateAvoidObstaclesOnMap(lOffset, l, step);
					AnnotateAvoidObstaclesOnMap(rOffset, r, step);
				}

				if (CurvedSteering)
				{
					// QQQ temporary global QQQoaJustScraping
					bool outermost = s >= maxSide;
					bool eitherSide = (l > 0) || (r > 0);
					if (!outermost && eitherSide) _qqQoaJustScraping = false;
				}
			}
			_qqqLastNearestObstacle = nearestO;

			// scan "wings"
			{
				const int WING_SCANS = 4;
				// see duplicated code at: QQQ draw sensing "wings"
				// QQQ should be a parameter of this method
				Vector3 wingWidth = Side * WingSlope() * maxForward;

				Color beforeColor = new Color((byte)(255.0f * 0.75f), (byte)(255.0f * 0.9f), (byte)(255.0f * 0.0f));  // for annotation
				Color afterColor = new Color((byte)(255.0f * 0.9f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.0f));  // for annotation

				for (int i = 1; i <= WING_SCANS; i++)
				{
					float fraction = i / (float)WING_SCANS;
					Vector3 endside = sOffset + (wingWidth * fraction);
					Vector3 corridorFront = Forward * maxForward;

					// "loop" from -1 to 1
					for (int j = -1; j < 2; j += 2)
					{
						float k = j; // prevent VC7.1 warning
						Vector3 start = fOffset + (sOffset * k);
						Vector3 end = fOffset + corridorFront + (endside * k);
						Vector3 ray = end - start;
						float rayLength = ray.Length();
						Vector3 step2 = ray * spacing / rayLength;
						int raySamples = (int)(rayLength / spacing);
						float endRadius =
							WingSlope() * maxForward * fraction *
							(signedRadius < 0 ? 1 : -1) * (j == 1 ? 1 : -1);
						Vector3 ignore;
						int scan = (CurvedSteering ?
										  (int)(ScanObstacleMap(start,
																  center,
																  arcAngle,
																  raySamples,
																  endRadius,
																  beforeColor,
																  afterColor,
																  out ignore)
												 / spacing) :
										  Map.ScanXZray(start, step2, raySamples));

						if (!CurvedSteering)
							AnnotateAvoidObstaclesOnMap(start, scan, step2);

						if (j == 1)
						{
							if ((scan > 0) && (scan < nearestWL)) nearestWL = scan;
						}
						else
						{
							if ((scan > 0) && (scan < nearestWR)) nearestWR = scan;
						}
					}
				}
				_wingDrawFlagL = nearestWL != INFINITY;
				_wingDrawFlagR = nearestWR != INFINITY;
			}

			// for annotation
			SavedNearestWR = nearestWR;
			SavedNearestR = nearestR;
			SavedNearestL = nearestL;
			SavedNearestWL = nearestWL;

			// flags for compound conditions, used below
			bool obstacleFreeC = nearestL == INFINITY && nearestR == INFINITY;
			bool obstacleFreeL = nearestL == INFINITY && nearestWL == INFINITY;
			bool obstacleFreeR = nearestR == INFINITY && nearestWR == INFINITY;
			bool obstacleFreeWL = nearestWL == INFINITY;
			bool obstacleFreeWR = nearestWR == INFINITY;
			bool obstacleFreeW = obstacleFreeWL && obstacleFreeWR;

			// when doing curved steering and we have already detected "just
			// scarping" but neither wing is free, recind the "just scarping"
			// QQQ temporary global QQQoaJustScraping
			bool js = CurvedSteering && _qqQoaJustScraping;
			bool cancelJs = !obstacleFreeWL && !obstacleFreeWR;
			if (js && cancelJs) _qqQoaJustScraping = false;


			// ----------------------------------------------------------
			// now we have measured everything, decide which way to steer
			// ----------------------------------------------------------


			// no obstacles found on path, return zero steering
			if (obstacleFreeC)
			{
				_qqqLastNearestObstacle = Vector3.Zero;

				// qqq  this may be in the wrong place (what would be the right
				// qqq  place?!) but I'm trying to say "even if the path is
				// qqq  clear, don't go too fast when driving between obstacles
				if (obstacleFreeWL || obstacleFreeWR || RelativeSpeed() < 0.7f)
					return Vector3.Zero;
				else
					return -Forward;
			}

			// if the nearest obstacle is way out there, take hint if any
			//      if (hintGiven && (Math.Min (nearestL, nearestR) > (maxSamples * 0.8f)))
			if (hintGiven && (Math.Min(nearestL, nearestR) > (maxSamples * 0.8f)))
			{
				AnnotationHintWasTaken();
				if (Vector3.Dot(steerHint, Side) > 0)
					return Side;
				else
					return -Side;
			}

			// QQQ experiment 3-9-04
			//
			// since there are obstacles ahead, if we are already near
			// maximum curvature, we MUST turn in opposite direction
			//
			// are we turning more sharply than the minimum turning radius?
			// (code from adjustSteeringForMinimumTurningRadius)
			float maxCurvature = 1 / (MinimumTurningRadius() * 1.2f);
			if (Math.Abs(Curvature) > maxCurvature)
			{
				Color blue = new Color(0, 0, (byte)(255.0f * 0.8f));
				annotation.CircleOrDisk(MinimumTurningRadius() * 1.2f, Up,
                                        center, blue.ToVector3().FromXna(), 40, false, false);
				return Side * sign;
			}


			if (obstacleFreeL) return Side;
			if (obstacleFreeR) return -Side;

			// if wings are clear, turn away from nearest obstacle straight ahead
			if (obstacleFreeW)
			{
				// distance to obs on L and R side of corridor roughtly the same
				bool same = Math.Abs(nearestL - nearestR) < 5; // within 5
				// if they are about the same and a hint is given, use hint
				if (same && hintGiven)
				{
					AnnotationHintWasTaken();
					if (Vector3.Dot(steerHint, Side) > 0)
						return Side;
					else
						return -Side;
				}
				else
				{
					// otherwise steer toward the less cluttered side
					if (nearestL > nearestR)
						return Side;
					else
						return -Side;
				}
			}

			// if the two wings are about equally clear and a steering hint is
			// provided, use it
			bool equallyClear = Math.Abs(nearestWL - nearestWR) < 2; // within 2
			if (equallyClear && hintGiven)
			{
				AnnotationHintWasTaken();
				if (Vector3.Dot(steerHint, Side) > 0) return Side; else return -Side;
			}

			// turn towards the side whose "wing" region is less cluttered
			// (the wing whose nearest obstacle is furthest away)
			if (nearestWL > nearestWR) return Side; else return -Side;
		}

		// QQQ reconsider calling sequence
		// called when steerToAvoidObstaclesOnMap decides steering is required
		// (default action is to do nothing, layered classes can overload it)
	    private void AnnotateAvoidObstaclesOnMap(Vector3 scanOrigin, int scanIndex, Vector3 scanStep)
		{
			if (scanIndex > 0)
			{
				Vector3 hit = scanOrigin + (scanStep * scanIndex);
                annotation.Line(scanOrigin, hit, new Color((byte)(255.0f * 0.7f), (byte)(255.0f * 0.3f), (byte)(255.0f * 0.3f)).ToVector3().FromXna());
			}
		}

	    private void AnnotationHintWasTaken()
		{
			if (!_dtZero) HintTakenCount++;

			float r = _halfWidth * 0.9f;
			Vector3 ff = Forward * r;
			Vector3 ss = Side * r;
			Vector3 pp = Position + (Up * 0.2f);
            annotation.Line(pp + ff + ss, pp - ff + ss, Color.White.ToVector3().FromXna());
            annotation.Line(pp - ff - ss, pp - ff + ss, Color.White.ToVector3().FromXna());
            annotation.Line(pp - ff - ss, pp + ff - ss, Color.White.ToVector3().FromXna());
            annotation.Line(pp + ff + ss, pp + ff - ss, Color.White.ToVector3().FromXna());

			//OpenSteerDemo.clock.setPausedState (true);
		}

		// scan across the obstacle map along a given arc
		// (possibly with radius adjustment ramp)
		// returns approximate distance to first obstacle found
		//
		// QQQ 1: this calling sequence does not allow for zero curvature case
		// QQQ 2: in library version of this, "map" should be a parameter
		// QQQ 3: instead of passing in colors, call virtual annotation function?
		// QQQ 4: need flag saying to continue after a hit, for annotation
		// QQQ 5: I needed to return both distance-to and position-of the first
		//        obstacle. I added returnObstaclePosition but maybe this should
		//        return a "scan results object" with a flag for obstacle found,
		//        plus distant and position if so.
		//
	    private float ScanObstacleMap(Vector3 start, Vector3 center, float arcAngle, int segments, float endRadiusChange, Color beforeColor, Color afterColor, out Vector3 returnObstaclePosition)
		{
			// "spoke" is initially the vector from center to start,
			// which is then rotated step by step around center
			Vector3 spoke = start - center;
			// determine the angular step per segment
			float step = arcAngle / segments;
			// store distance to, and position of first obstacle
			float obstacleDistance = 0;
			returnObstaclePosition = Vector3.Zero;
			// for spiral "ramps" of changing radius
// ReSharper disable CompareOfFloatsByEqualityOperator
			float startRadius = (endRadiusChange == 0) ? 0 : spoke.Length();
// ReSharper restore CompareOfFloatsByEqualityOperator

			// traverse each segment along arc
			float sin = 0, cos = 0;
			Vector3 oldPoint = start;
			bool obstacleFound = false;
			for (int i = 0; i < segments; i++)
			{
				// rotate "spoke" to next step around circle
				// (sin and cos values get filled in on first call)
				spoke = spoke.RotateAboutGlobalY(step, ref sin, ref cos);

				// for spiral "ramps" of changing radius
				float adjust = ((Math.Abs(endRadiusChange - 0) < float.Epsilon) ?
									  1.0f :
									  MathHelper.Lerp(1.0f,
												   (Math.Max(0,
												             (startRadius +
												              endRadiusChange))
												    / startRadius), (i + 1) / (float)segments));

				// construct new scan point: center point, offset by rotated
				// spoke (possibly adjusting the radius if endRadiusChange!=0)
				Vector3 newPoint = center + (spoke * adjust);

				// once an obstacle if found "our work here is done" -- continue
				// to loop only for the sake of annotation (make that optional?)
				if (obstacleFound)
				{
                    annotation.Line(oldPoint, newPoint, afterColor.ToVector3().FromXna());
				}
				else
				{
					// no obstacle found on this scan so far,
					// scan map along current segment (a chord of the arc)
					Vector3 offset = newPoint - oldPoint;
					float d2 = offset.Length() * 2;

					// when obstacle found: set flag, save distance and position
					if (!Map.IsPassable(newPoint))
					{
						obstacleFound = true;
						obstacleDistance = d2 * 0.5f * (i + 1);
						returnObstaclePosition = newPoint;
					}
                    annotation.Line(oldPoint, newPoint, beforeColor.ToVector3().FromXna());
				}
				// save new point for next time around loop
				oldPoint = newPoint;
			}
			// return distance to first obstacle (or zero if none found)
			return obstacleDistance;
		}

	    private bool DetectImminentCollision()
		{
			// QQQ  this should be integrated into steerToAvoidObstaclesOnMap
			// QQQ  since it shares so much infrastructure
			// QQQ  less so after changes on 3-16-04
			bool returnFlag = false;
			float spacing = Map.MinSpacing() / 2;
			float maxSide = _halfWidth + spacing;
			float minDistance = CurvedSteering ? 2.0f : 2.5f; // meters
			float predictTime = CurvedSteering ? .75f : 1.3f; // seconds
			float maxForward = Speed * CombinedLookAheadTime(predictTime, minDistance);
			Vector3 step = Forward * spacing;
			float s = CurvedSteering ? (spacing / 4) : (spacing / 2);

			float signedRadius = 1 / NonZeroCurvatureQQQ();
			Vector3 localCenterOfCurvature = Side * signedRadius;
			Vector3 center = Position + localCenterOfCurvature;
			float sign = signedRadius < 0 ? 1.0f : -1.0f;
			float arcRadius = signedRadius * -sign;
			const float TWO_PI = 2 * (float)Math.PI;
			float circumference = TWO_PI * arcRadius;
			Vector3 qqqLift = new Vector3(0, 0.2f, 0);

		    // scan region ahead of vehicle
			while (s < maxSide)
			{
				Vector3 sOffset = Side * s;
				Vector3 lOffset = Position + sOffset;
				Vector3 rOffset = Position - sOffset;
				const float BEVEL = 0.3f;
				float fraction = s / maxSide;
				float scanDist = (_halfLength +
										MathHelper.Lerp(maxForward,
													 maxForward * BEVEL, fraction));
				float angle = (scanDist * TWO_PI * sign) / circumference;
				int samples = (int)(scanDist / spacing);
			    Vector3 ignore;
			    int l = (CurvedSteering ?
							   (int)(ScanObstacleMap(lOffset + qqqLift,
													   center,
													   angle,
													   samples,
													   0,
													   Color.Magenta,
													   Color.Cyan,
													   out ignore)
									  / spacing) :
							   Map.ScanXZray(lOffset, step, samples));
				int r = (CurvedSteering ?
							   (int)(ScanObstacleMap(rOffset + qqqLift,
													   center,
													   angle,
													   samples,
													   0,
													   Color.Magenta,
													   Color.Cyan,
													   out ignore)
									  / spacing) :
							   Map.ScanXZray(rOffset, step, samples));

				returnFlag = returnFlag || (l > 0);
				returnFlag = returnFlag || (r > 0);

				// annotation
				if (!CurvedSteering)
				{
					Vector3 d = step * samples;
                    annotation.Line(lOffset, lOffset + d, Color.White.ToVector3().FromXna());
                    annotation.Line(rOffset, rOffset + d, Color.White.ToVector3().FromXna());
				}

				// increment sideways displacement of scan line
				s += spacing;
			}
			return returnFlag;
		}

		// see comments at SimpleVehicle.predictFuturePosition, in this instance
		// I just need the future position (not a LocalSpace), so I'll keep the
		// calling sequence and just conditionalize its body
		//
		// this should be const, but easier for now to ignore that
		public override Vector3 PredictFuturePosition(float predictionTime)
		{
			if (CurvedSteering)
			{
				// QQQ this chunk of code is repeated in far too many places,
				// QQQ it has to be moved inside some utility
				// QQQ 
				// QQQ and now, worse, I rearranged it to try the "limit arc
				// QQQ angle" trick
				float signedRadius = 1 / NonZeroCurvatureQQQ();
				Vector3 localCenterOfCurvature = Side * signedRadius;
				Vector3 center = Position + localCenterOfCurvature;
				float sign = signedRadius < 0 ? 1.0f : -1.0f;
				float arcRadius = signedRadius * -sign;
				const float TWO_PI = 2 * (float)Math.PI;
				float circumference = TWO_PI * arcRadius;
				float rawLength = Speed * predictionTime * sign;
				float arcLength = ArcLengthLimit(rawLength, circumference * 0.25f);
				float arcAngle = TWO_PI * arcLength / circumference;

				Vector3 spoke = Position - center;
				Vector3 newSpoke = spoke.RotateAboutGlobalY(arcAngle);
				Vector3 prediction = newSpoke + center;

				// QQQ unify with annotatePathFollowing
				Color futurePositionColor = new Color((byte)(255.0f * 0.5f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.6f));
				AnnotationXZArc(Position, center, arcLength, 20, futurePositionColor);
				return prediction;
			}
			else
			{
				return Position + (Velocity * predictionTime);
			}
		}

		// QQQ experimental fix for arcLength limit in predictFuturePosition
		// QQQ and steerToAvoidObstaclesOnMap.
		//
		// args are the intended arc length (signed!), and the limit which is
		// a given (positive!) fraction of the arc's (circle's) circumference
		//
	    private static float ArcLengthLimit(float length, float limit)
		{
			if (length > 0)
				return Math.Min(length, limit);
			else
				return -Math.Min(-length, limit);
		}

		// this is a version of the one in SteerLibrary.h modified for "slow when
		// heading off path".  I put it here because the changes were not
		// compatible with Pedestrians.cpp.  It needs to be merged back after
		// things settle down.
		//
		// its been modified in other ways too (such as "reduce the offset if
		// facing in the wrong direction" and "increase the target offset to
		// compensate the fold back") plus I changed the type of "path" from
		// Pathway to GCRoute to use methods like indexOfNearestSegment and
		// dotSegmentUnitTangents
		//
		// and now its been modified again for curvature-based prediction
		//
	    private Vector3 SteerToFollowPath(int direction, float predictionTime)
		{
			if (CurvedSteering)
				return SteerToFollowPathCurve(direction, predictionTime);
			else
				return SteerToFollowPathLinear(direction, predictionTime);
		}

	    private Vector3 SteerToFollowPathLinear(int direction, float predictionTime)
		{
			// our goal will be offset from our path distance by this amount
			float pathDistanceOffset = direction * predictionTime * Speed;

			// predict our future position
			Vector3 futurePosition = PredictFuturePosition(predictionTime);

			// measure distance along path of our current and predicted positions
			float nowPathDistance =
				Path.MapPointToPathDistance(Position);

			// are we facing in the correction direction?
			Vector3 pathHeading = Path.TangentAt(Position) * direction;
			bool correctDirection = Vector3.Dot(pathHeading, Forward) > 0;

			// find the point on the path nearest the predicted future position
			// XXX need to improve calling sequence, maybe change to return a
			// XXX special path-defined object which includes two Vector3s and a 
			// XXX bool (onPath,tangent (ignored), withinPath)
			float futureOutside;
			Vector3 onPath = Path.MapPointToPath(futurePosition, out futureOutside);

			// determine if we are currently inside the path tube
			float nowOutside;
			Vector3 nowOnPath = Path.MapPointToPath(Position, out nowOutside);

			// no steering is required if our present and future positions are
			// inside the path tube and we are facing in the correct direction
			float m = -Radius;
			bool whollyInside = (futureOutside < m) && (nowOutside < m);
			if (whollyInside && correctDirection)
			{
				// all is well, return zero steering
				return Vector3.Zero;
			}
			else
			{
				// otherwise we need to steer towards a target point obtained
				// by adding pathDistanceOffset to our current path position
				// (reduce the offset if facing in the wrong direction)
				float targetPathDistance = (nowPathDistance +
												  (pathDistanceOffset *
												   (correctDirection ? 1 : 0.1f)));
				Vector3 target = Path.MapPathDistanceToPoint(targetPathDistance);


				// if we are on one segment and target is on the next segment and
				// the dot of the tangents of the two segments is negative --
				// increase the target offset to compensate the fold back
				int ip = Path.IndexOfNearestSegment(Position);
				int it = Path.IndexOfNearestSegment(target);
				if (((ip + direction) == it) &&
					(Path.DotSegmentUnitTangents(it, ip) < -0.1f))
				{
					float newTargetPathDistance =
						nowPathDistance + (pathDistanceOffset * 2);
					target = Path.MapPathDistanceToPoint(newTargetPathDistance);
				}

				AnnotatePathFollowing(futurePosition, onPath, target, futureOutside);

				// if we are currently outside head directly in
				// (QQQ new, experimental, makes it turn in more sharply)
				if (nowOutside > 0) return SteerForSeek(nowOnPath);

				// steering to seek target on path
				Vector3 seek = SteerForSeek(target).TruncateLength(MaxForce);

				// return that seek steering -- except when we are heading off
				// the path (currently on path and future position is off path)
				// in which case we put on the brakes.
				if ((nowOutside < 0) && (futureOutside > 0))
					return (Vector3Helpers.PerpendicularComponent(seek, Forward) - (Forward * MaxForce));
				else
					return seek;
			}
		}

		// Path following case for curved prediction and incremental steering
		// (called from steerToFollowPath for the curvedSteering case)
		//
		// QQQ this does not handle the case when we AND futurePosition
		// QQQ are outside, say when approach the path from far away
		//
	    private Vector3 SteerToFollowPathCurve(int direction, float predictionTime)
		{
			// predict our future position (based on current curvature and speed)
			Vector3 futurePosition = PredictFuturePosition(predictionTime);
			// find the point on the path nearest the predicted future position
			float futureOutside;
			Vector3 onPath = Path.MapPointToPath(futurePosition, out futureOutside);
			Vector3 pathHeading = Path.TangentAt(onPath, direction);
			Vector3 rawBraking = Forward * MaxForce * -1;
			Vector3 braking = ((futureOutside < 0) ? Vector3.Zero : rawBraking);
			//qqq experimental wrong-way-fixer
			float nowOutside;
			Vector3 nowTangent;
			Vector3 p = Position;
			Vector3 nowOnPath = Path.MapPointToPath(p, out nowTangent, out nowOutside);
			nowTangent *= (float)direction;
			float alignedness = Vector3.Dot(nowTangent, Forward);

			// facing the wrong way?
			if (alignedness < 0)
			{
                annotation.Line(p, p + (nowTangent * 10), Color.Cyan.ToVector3().FromXna());

				// if nearly anti-parallel
				if (alignedness < -0.707f)
				{
					Vector3 towardCenter = nowOnPath - p;
					Vector3 turn = (Vector3.Dot(towardCenter, Side) > 0 ?
									   Side * MaxForce :
									   Side * MaxForce * -1);
					return (turn + rawBraking);
				}
				else
				{
					return (Vector3Helpers.PerpendicularComponent(SteerTowardHeading(pathHeading), Forward) + braking);
				}
			}

			// is the predicted future position(+radius+margin) inside the path?
			if (futureOutside < -(Radius + 1.0f)) //QQQ
			{
				// then no steering is required
				return Vector3.Zero;
			}
			else
			{
				// otherwise determine corrective steering (including braking)
                annotation.Line(futurePosition, futurePosition + pathHeading, Color.Red.ToVector3().FromXna());
				AnnotatePathFollowing(futurePosition, onPath,
									   Position, futureOutside);

				// two cases, if entering a turn (a waypoint between path segments)
				if (Path.NearWaypoint(onPath) && (futureOutside > 0))
				{
					// steer to align with next path segment
                    annotation.Circle3D(0.5f, futurePosition, Up, Color.Red.ToVector3().FromXna(), 8);
					return SteerTowardHeading(pathHeading) + braking;
				}
				else
				{
					// otherwise steer away from the side of the path we
					// are heading for
                    Vector3 pathSide = this.LocalRotateForwardToSide(pathHeading);
					Vector3 towardFP = futurePosition - onPath;
					float whichSide = (Vector3.Dot(pathSide, towardFP) < 0) ? 1.0f : -1.0f;
					return (Side * MaxForce * whichSide) + braking;
				}
			}
		}

	    private void PerFrameAnnotation()
		{
			Vector3 p = Position;

			// draw the circular collision boundary
            annotation.CircleOrDisk(Radius, Up, p, Color.Black.ToVector3().FromXna(), 32, false, false);

			// draw forward sensing corridor and wings ( for non-curved case)
			if (!CurvedSteering)
			{
				float corLength = Speed * LookAheadTimeOA();
				if (corLength > _halfLength)
				{
					Vector3 corFront = Forward * corLength;
					Vector3 corBack = Vector3.Zero; // (was bbFront)
					Vector3 corSide = Side * Radius;
					Vector3 c1 = p + corSide + corBack;
					Vector3 c2 = p + corSide + corFront;
					Vector3 c3 = p - corSide + corFront;
					Vector3 c4 = p - corSide + corBack;
					Color color = ((_annotateAvoid != Vector3.Zero) ? Color.Red : Color.Yellow);
                    annotation.Line(c1, c2, color.ToVector3().FromXna());
                    annotation.Line(c2, c3, color.ToVector3().FromXna());
                    annotation.Line(c3, c4, color.ToVector3().FromXna());

					// draw sensing "wings"
					Vector3 wingWidth = Side * WingSlope() * corLength;
					Vector3 wingTipL = c2 + wingWidth;
					Vector3 wingTipR = c3 - wingWidth;
					Color wingColor = Color.Orange;
                    if (_wingDrawFlagL) annotation.Line(c2, wingTipL, wingColor.ToVector3().FromXna());
                    if (_wingDrawFlagL) annotation.Line(c1, wingTipL, wingColor.ToVector3().FromXna());
                    if (_wingDrawFlagR) annotation.Line(c3, wingTipR, wingColor.ToVector3().FromXna());
                    if (_wingDrawFlagR) annotation.Line(c4, wingTipR, wingColor.ToVector3().FromXna());
				}
			}

			// annotate steering acceleration
			Vector3 above = Position + new Vector3(0, 0.2f, 0);
			Vector3 accel = Acceleration * 5 / MaxForce;
			Color aColor = new Color((byte)(255.0f * 0.4f), (byte)(255.0f * 0.4f), (byte)(255.0f * 0.8f));
            annotation.Line(above, above + accel, aColor.ToVector3().FromXna());
		}

		// draw vehicle's body and annotation
		public void Draw()
		{
			// for now: draw as a 2d bounding box on the ground
			Color bodyColor = Color.Black;
			if (Stuck) bodyColor = Color.Yellow;
			if (!IsBodyInsidePath()) bodyColor = Color.Orange;
			if (_collisionDetected) bodyColor = Color.Red;

			// draw vehicle's bounding box on gound plane (its "shadow")
			Vector3 p = Position;
			Vector3 bbSide = Side * _halfWidth;
			Vector3 bbFront = Forward * _halfLength;
			Vector3 bbHeight = new Vector3(0, 0.1f, 0);
			Drawing.DrawQuadrangle(p - bbFront + bbSide + bbHeight,
							p + bbFront + bbSide + bbHeight,
							p + bbFront - bbSide + bbHeight,
							p - bbFront - bbSide + bbHeight,
							bodyColor);

			// annotate trail
			Color darkGreen = new Color(0, (byte)(255.0f * 0.6f), 0);
			_trail.TrailColor = darkGreen;
			_trail.TickColor = Color.Black;
            _trail.Draw(annotation);
		}

		// called when steerToFollowPath decides steering is required
	    private void AnnotatePathFollowing(Vector3 future, Vector3 onPath, Vector3 target, float outside)
		{
			Color toTargetColor = new Color(0, (byte)(255.0f * 0.6f), 0);
			Color insidePathColor = new Color((byte)(255.0f * 0.6f), (byte)(255.0f * 0.6f), 0);
			Color outsidePathColor = new Color(0, 0, (byte)(255.0f * 0.6f));
			Color futurePositionColor = new Color((byte)(255.0f * 0.5f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.6f));

			// draw line from our position to our predicted future position
			if (!CurvedSteering)
                annotation.Line(Position, future, futurePositionColor.ToVector3().FromXna());

			// draw line from our position to our steering target on the path
            annotation.Line(Position, target, toTargetColor.ToVector3().FromXna());

			// draw a two-toned line between the future test point and its
			// projection onto the path, the change from dark to light color
			// indicates the boundary of the tube.

			float o = outside + Radius + (CurvedSteering ? 1.0f : 0.0f);
			Vector3 boundaryOffset = Vector3.Normalize(onPath - future);
			boundaryOffset *= o;

			Vector3 onPathBoundary = future + boundaryOffset;
            annotation.Line(onPath, onPathBoundary, insidePathColor.ToVector3().FromXna());
            annotation.Line(onPathBoundary, future, outsidePathColor.ToVector3().FromXna());
		}

		public void DrawMap()
		{
			float xs = Map.XSize / Map.Resolution;
			float zs = Map.ZSize / Map.Resolution;
			Vector3 alongRow = new Vector3(xs, 0, 0);
			Vector3 nextRow = new Vector3(-Map.XSize, 0, zs);
			Vector3 g = new Vector3((Map.XSize - xs) / -2, 0, (Map.ZSize - zs) / -2);
			g += Map.Center;
			for (int j = 0; j < Map.Resolution; j++)
			{
				for (int i = 0; i < Map.Resolution; i++)
				{
					if (Map.GetMapBit(i, j))
					{
						// spikes
						// Vector3 spikeTop (0, 5.0f, 0);
						// drawLine (g, g+spikeTop, Color.White);

						// squares
					    const float ROCK_HEIGHT = 0;
						Vector3 v1 = new Vector3(+xs / 2, ROCK_HEIGHT, +zs / 2);
						Vector3 v2 = new Vector3(+xs / 2, ROCK_HEIGHT, -zs / 2);
						Vector3 v3 = new Vector3(-xs / 2, ROCK_HEIGHT, -zs / 2);
						Vector3 v4 = new Vector3(-xs / 2, ROCK_HEIGHT, +zs / 2);
						// Vector3 redRockColor (0.6f, 0.1f, 0.0f);
						Color orangeRockColor = new Color((byte)(255.0f * 0.5f), (byte)(255.0f * 0.2f), (byte)(255.0f * 0.0f));
						Drawing.DrawQuadrangle(g + v1, g + v2, g + v3, g + v4, orangeRockColor);

						// pyramids
						// Vector3 top (0, xs/2, 0);
						// Vector3 redRockColor (0.6f, 0.1f, 0.0f);
						// Vector3 orangeRockColor (0.5f, 0.2f, 0.0f);
						// drawTriangle (g+v1, g+v2, g+top, redRockColor);
						// drawTriangle (g+v2, g+v3, g+top, orangeRockColor);
						// drawTriangle (g+v3, g+v4, g+top, redRockColor);
						// drawTriangle (g+v4, g+v1, g+top, orangeRockColor);
					}
					g += alongRow;
				}
				g += nextRow;
			}
		}

		// draw the GCRoute as a series of circles and "wide lines"
		// (QQQ this should probably be a method of Path (or a
		// closely-related utility function) in which case should pass
		// color in, certainly shouldn't be recomputing it each draw)
		public void DrawPath()
		{
			Vector3 pathColor = new Vector3(0, 0.5f, 0.5f);
			Vector3 sandColor = new Vector3(0.8f, 0.7f, 0.5f);
			Vector3 vColor = Vector3.Lerp(sandColor, pathColor, 0.1f);
			Color color = new Color(vColor.ToXna());

			Vector3 down = new Vector3(0, -0.1f, 0);
			for (int i = 0; i < Path.PointCount; i++)
			{
				Vector3 endPoint0 = Path.Points[i] + down;
				if (i > 0)
				{
					Vector3 endPoint1 = Path.Points[i - 1] + down;

					float legWidth = Path.Radii[i];

					Drawing.DrawXZWideLine(endPoint0, endPoint1, color, legWidth * 2);
					Drawing.DrawLine(Path.Points[i], Path.Points[i - 1], new Color(pathColor.ToXna()));
					Drawing.DrawXZDisk(legWidth, endPoint0, color, 24);
					Drawing.DrawXZDisk(legWidth, endPoint1, color, 24);
				}
			}
		}

	    private static GCRoute MakePath()
		{
			// a few constants based on world size
			const float M = WORLD_SIZE * 0.4f; // main diamond size
			const float N = WORLD_SIZE / 8; // notch size
			const float O = WORLD_SIZE * 2; // outside of the sand

			// construction vectors
			Vector3 p = new Vector3(0, 0, M);
			Vector3 q = new Vector3(0, 0, M - N);
			Vector3 r = new Vector3(-M, 0, 0);
			Vector3 s = new Vector3(2 * N, 0, 0);
			Vector3 t = new Vector3(O, 0, 0);
			Vector3 u = new Vector3(-O, 0, 0);
			Vector3 v = new Vector3(N, 0, 0);
			Vector3 w = new Vector3(0, 0, 0);


			// path vertices
			Vector3 a = t - p;
			Vector3 b = s + v - p;
			Vector3 c = s - q;
			Vector3 d = s + q;
			Vector3 e = s - v + p;
			Vector3 f = p - w;
			Vector3 g = r - w;
			Vector3 h = -p - w;
			Vector3 i = u - p;

			// return Path object
	        Vector3[] pathPoints = { a, b, c, d, e, f, g, h, i };
			const float K = 10.0f;
			float[] pathRadii = { K, K, K, K, K, K, K, K, K };
			return new GCRoute(pathPoints, pathRadii, false);
		}

	    private static TerrainMap MakeMap()
		{
			return new TerrainMap(Vector3.Zero, WORLD_SIZE, WORLD_SIZE, (int)WORLD_SIZE + 1);
		}

		public bool HandleExitFromMap()
		{
			if (DemoSelect == 2)
			{
				// for path following, do wrap-around (teleport) and make new map
				float px = Position.X;
				float fx = Forward.X;
				const float WS = WORLD_SIZE * 0.51f; // slightly past edge
				if (((fx > 0) && (px > WS)) || ((fx < 0) && (px < -WS)))
				{
					// bump counters
					LapsStarted++;
					LapsFinished++;

					Vector3 camOffsetBefore = Demo.Camera.Position - Position;

					// set position on other side of the map (set new X coordinate)
					Position = new Vector3((((px < 0) ? 1 : -1) * ((WORLD_SIZE * 0.5f) + (Speed * LookAheadTimePF()))), Position.Y, Position.Z);

					// reset bookeeping to detect stuck cycles
					ResetStuckCycleDetection();

					// new camera position and aimpoint to compensate for teleport
					Demo.Camera.Target = Position;
					Demo.Camera.Position = (Position + camOffsetBefore);

					// make camera jump immediately to new position
					Demo.Camera.DoNotSmoothNextMove();

					// prevent long streaks due to teleportation 
					_trail.Clear();

					return true;
				}
			}
			else
			{
				// for the non-path-following demos:
				// reset simulation if the vehicle drives through the fence
				if (Position.Length() > _worldDiag) Reset();
			}
			return false;
		}


		// QQQ move this utility to SimpleVehicle?
		public float RelativeSpeed() { return Speed / MaxSpeed; }

	    private float WingSlope()
		{
			return MathHelper.Lerp((CurvedSteering ? 0.3f : 0.35f),
								0.06f, RelativeSpeed());
		}

	    private void ResetStuckCycleDetection()
		{
			ResetSmoothedPosition(Position + (Forward * -80)); // qqq
		}

		// QQQ just a stop gap, not quite right
		// (say for example we were going around a circle with radius > 10)
	    private bool WeAreGoingInCircles()
		{
			Vector3 offset = SmoothedPosition - Position;
			return offset.Length() < 10;
		}

	    private float LookAheadTimeOA()
		{
			float minTime = (_baseLookAheadTime *
								   (CurvedSteering ?
									MathHelper.Lerp(0.4f, 0.7f, RelativeSpeed()) :
									0.66f));
			return CombinedLookAheadTime(minTime, 3);
		}

	    private float LookAheadTimePF()
		{
			return CombinedLookAheadTime(_baseLookAheadTime, 3);
		}

		// QQQ maybe move to SimpleVehicle ?
		// compute a "look ahead time" with two components, one based on
		// minimum time to (say) a collision and one based on minimum distance
		// arg 1 is "seconds into the future", arg 2 is "meters ahead"
	    private float CombinedLookAheadTime(float minTime, float minDistance)
		{
// ReSharper disable CompareOfFloatsByEqualityOperator
			if (Speed == 0) return 0;
// ReSharper restore CompareOfFloatsByEqualityOperator
			return Math.Max(minTime, minDistance / Speed);
		}

		// is vehicle body inside the path?
		// (actually tests if all four corners of the bounbding box are inside)
		//
	    private bool IsBodyInsidePath()
		{
			if (DemoSelect == 2)
			{
				Vector3 bbSide = Side * _halfWidth;
				Vector3 bbFront = Forward * _halfLength;
				return (Path.IsInsidePath(Position - bbFront + bbSide) &&
						Path.IsInsidePath(Position + bbFront + bbSide) &&
						Path.IsInsidePath(Position + bbFront - bbSide) &&
						Path.IsInsidePath(Position - bbFront - bbSide));
			}
			return true;
		}

	    private Vector3 ConvertAbsoluteToIncrementalSteering(Vector3 absolute, float elapsedTime)
		{
			Vector3 curved = ConvertLinearToCurvedSpaceGlobal(absolute);
			Utilities.BlendIntoAccumulator(elapsedTime * 8.0f, curved, ref _currentSteering);
			{
				// annotation
				Vector3 u = new Vector3(0, 0.5f, 0);
				Vector3 p = Position;
				annotation.Line(p + u, p + u + absolute, Color.Red.ToVector3().FromXna());
                annotation.Line(p + u, p + u + curved, Color.Yellow.ToVector3().FromXna());
                annotation.Line(p + u * 2, p + u * 2 + _currentSteering, Color.Green.ToVector3().FromXna());
			}
			return _currentSteering;
		}

		// QQQ new utility 2-25-04 -- may replace inline code elsewhere
		//
		// Given a location in this vehicle's linear local space, convert it into
		// the curved space defined by the vehicle's current path curvature.  For
		// example, forward() gets mapped on a point 1 unit along the circle
		// centered on the current center of curvature and passing through the
		// vehicle's position().
		//
	    private Vector3 ConvertLinearToCurvedSpaceGlobal(Vector3 linear)
		{
			Vector3 trimmedLinear = linear.TruncateLength(MaxForce);

			// ---------- this block imported from steerToAvoidObstaclesOnMap
			float signedRadius = 1 / (NonZeroCurvatureQQQ() /*QQQ*/ * 1);
			Vector3 localCenterOfCurvature = Side * signedRadius;
			Vector3 center = Position + localCenterOfCurvature;
			float sign = signedRadius < 0 ? 1.0f : -1.0f;
			float arcLength = Vector3.Dot(trimmedLinear, Forward);
			//
			float arcRadius = signedRadius * -sign;
			const float TWO_PI = 2 * (float)Math.PI;
			float circumference = TWO_PI * arcRadius;
			float arcAngle = TWO_PI * arcLength / circumference;
			// ---------- this block imported from steerToAvoidObstaclesOnMap

			// ---------- this block imported from scanObstacleMap
			// vector from center of curvature to position of vehicle
			Vector3 initialSpoke = Position - center;
			// rotate by signed arc angle
			Vector3 spoke = initialSpoke.RotateAboutGlobalY(arcAngle * sign);
			// ---------- this block imported from scanObstacleMap

	        Vector3 fromCenter = Vector3.Normalize(-localCenterOfCurvature);
			float dRadius = Vector3.Dot(trimmedLinear, fromCenter);
			float radiusChangeFactor = (dRadius + arcRadius) / arcRadius;
			Vector3 resultLocation = center + (spoke * radiusChangeFactor);
			{
				Vector3 center2 = Position + localCenterOfCurvature;
				AnnotationXZArc(Position, center2, Speed * sign * -3, 20, Color.White);
			}
			// return the vector from vehicle position to the coimputed location
			// of the curved image of the original linear offset
			return resultLocation - Position;
		}

		// approximate value for the Polaris Ranger 6x6: 16 feet, 5 meters
	    private static float MinimumTurningRadius() { return 5.0f; }

	    private Vector3 AdjustSteeringForMinimumTurningRadius(Vector3 steering)
		{
			float maxCurvature = 1 / (MinimumTurningRadius() * 1.1f);

			// are we turning more sharply than the minimum turning radius?
			if (Math.Abs(Curvature) > maxCurvature)
			{
				// remove the tangential (non-thrust) component of the steering
				// force, replace it with a force pointing away from the center
				// of curvature, causing us to "widen out" easing off from the
				// minimum turing radius
				float signedRadius = 1 / NonZeroCurvatureQQQ();
				float sign = signedRadius < 0 ? 1.0f : -1.0f;
				Vector3 thrust = Vector3Helpers.ParallelComponent(steering, Forward);
				Vector3 trimmed = thrust.TruncateLength(MaxForce);
				Vector3 widenOut = Side * MaxForce * sign;
				{
					// annotation
					Vector3 localCenterOfCurvature = Side * signedRadius;
					Vector3 center = Position + localCenterOfCurvature;
					annotation.CircleOrDisk(MinimumTurningRadius(), Up,
                                            center, Color.Blue.ToVector3().FromXna(), 40, false, false);
				}
				return trimmed + widenOut;
			}

			// otherwise just return unmodified input
			return steering;
		}

		// QQQ This is to work around the bug that scanObstacleMap's current
		// QQQ arguments preclude the driving straight [curvature()==0] case.
		// QQQ This routine returns the current vehicle path curvature, unless it
		// QQQ is *very* close to zero, in which case a small positive number is
		// QQQ returned (corresponding to a radius of 100,000 meters).  
		// QQQ
		// QQQ Presumably it would be better to get rid of this routine and
		// QQQ redesign the arguments of scanObstacleMap
		//
	    private float NonZeroCurvatureQQQ()
		{
			float c = Curvature;
			const float MIN_CURVATURE = 1.0f / 100000.0f; // 100,000 meter radius
			bool tooSmall = (c < MIN_CURVATURE) && (c > -MIN_CURVATURE);
			return (tooSmall ? MIN_CURVATURE : c);
		}

		// QQQ ad hoc speed limitation based on path orientation...
		// QQQ should be renamed since it is based on more than curvature
		//
	    private float MaxSpeedForCurvature()
		{
			float maxRelativeSpeed = 1;

			if (CurvedSteering)
			{
				// compute an ad hoc "relative curvature"
				float absC = Math.Abs(Curvature);
				float maxC = 1 / MinimumTurningRadius();
				float relativeCurvature = (float)Math.Sqrt(MathHelper.Clamp(absC / maxC, 0, 1));

				// map from full throttle when straight to 10% at max curvature
				float curveSpeed = MathHelper.Lerp(1.0f, 0.1f, relativeCurvature);
				AnnoteMaxRelSpeedCurve = curveSpeed;

				if (DemoSelect != 2)
				{
					maxRelativeSpeed = curveSpeed;
				}
				else
				{
					// heading (unit tangent) of the path segment of interest
					Vector3 pathHeading = Path.TangentAt(Position, PathFollowDirection);
					// measure how parallel we are to the path
					float parallelness = Vector3.Dot(pathHeading, Forward);

					// determine relative speed for this heading
					const float MW = 0.2f;
					float headingSpeed = ((parallelness < 0) ? MW :
												MathHelper.Lerp(MW, 1.0f, parallelness));
					maxRelativeSpeed = Math.Min(curveSpeed, headingSpeed);
					AnnoteMaxRelSpeedPath = headingSpeed;
				}
			}
			AnnoteMaxRelSpeed = maxRelativeSpeed;
			return MaxSpeed * maxRelativeSpeed;
		}

		// xxx library candidate
		// xxx assumes (but does not check or enforce) heading is unit length
		//
	    private Vector3 SteerTowardHeading(Vector3 desiredGlobalHeading)
		{
			Vector3 headingError = Vector3.Normalize(desiredGlobalHeading - Forward);
			headingError *= MaxForce;

			return headingError;
		}

		// XXX this should eventually be in a library, make it a first
		// XXX class annotation queue, tie in with drawXZArc
	    private void AnnotationXZArc(Vector3 start, Vector3 center, float arcLength, int segments, Color color)
		{
			// "spoke" is initially the vector from center to start,
			// it is then rotated around its tail
			Vector3 spoke = start - center;

			// determine the angular step per segment
			float radius = spoke.Length();
			const float TWO_PI = 2 * (float)Math.PI;
			float circumference = TWO_PI * radius;
			float arcAngle = TWO_PI * arcLength / circumference;
			float step = arcAngle / segments;

			// draw each segment along arc
			float sin = 0, cos = 0;
			for (int i = 0; i < segments; i++)
			{
				Vector3 old = spoke + center;

				// rotate point to next step around circle
				spoke = spoke.RotateAboutGlobalY(step, ref sin, ref cos);

                annotation.Line(spoke + center, old, color.ToVector3().FromXna());
			}
		}

		// map of obstacles
		public readonly TerrainMap Map;

		// route for path following (waypoints and legs)
		public readonly GCRoute Path;

		// follow the path "upstream or downstream" (+1/-1)
		public int PathFollowDirection;

		// master look ahead (prediction) time
	    private float _baseLookAheadTime;

		// vehicle dimentions in meters
	    private float _halfWidth;
	    private float _halfLength;

		// keep track of failure rate (when vehicle is on top of obstacle)
	    private bool _collisionDetected;
	    private bool _collisionLastTime;
		public float TimeOfLastCollision;
		public float SumOfCollisionFreeTimes;
		public int CountOfCollisionFreeTimes;

		// keep track of average speed
		public float TotalDistance;
		public float TotalTime;

		// take note when current dt is zero (as in paused) for stat counters
	    private bool _dtZero;

		// state saved for annotation
	    private Vector3 _annotateAvoid;
	    private bool _wingDrawFlagL;
	    private bool _wingDrawFlagR;

	    // QQQ first pass at detecting "stuck" state
		public bool Stuck;
		public int StuckCount;
		public int StuckCycleCount;
		public int StuckOffPathCount;

	    private Vector3 _qqqLastNearestObstacle;

		public int LapsStarted;
		public int LapsFinished;

		// QQQ temporary global QQQoaJustScraping
		// QQQ replace this global flag with a cleaner mechanism
	    private bool _qqQoaJustScraping;

		public int HintGivenCount;
		public int HintTakenCount;

		// for "curvature-based incremental steering" -- contains the current
		// steering into which new incremental steering is blended
	    private Vector3 _currentSteering;

		// use curved prediction and incremental steering:
		public bool CurvedSteering;
		public bool IncrementalSteering;

		// save obstacle avoidance stats for annotation
		// (nearest obstacle in each of the four zones)
		public static float SavedNearestWR = 0;
		public static float SavedNearestR = 0;
		public static float SavedNearestL = 0;
		public static float SavedNearestWL = 0;

		public float AnnoteMaxRelSpeed;
		public float AnnoteMaxRelSpeedCurve;
		public float AnnoteMaxRelSpeedPath;

		// which of the three demo modes is selected
		public static int DemoSelect = 2;

		// size of the world (the map actually)
	    public const float WORLD_SIZE = 200;
	    private static readonly float _worldDiag = (float)Math.Sqrt((WORLD_SIZE * WORLD_SIZE) / 2);
	}
}
