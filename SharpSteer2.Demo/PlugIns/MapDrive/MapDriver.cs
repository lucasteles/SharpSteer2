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
using SharpSteer2.Helpers;
using Vector3 = System.Numerics.Vector3;

namespace SharpSteer2.Demo.PlugIns.MapDrive;

public class MapDriver : SimpleVehicle
{
    Trail trail;

    public override float MaxForce => 20 * 0.4f;
    public override float MaxSpeed => 20;

    // constructor
    public MapDriver(IAnnotationService annotations = null)
        : base(annotations)
    {
        Map = MakeMap();
        Path = MakePath();

        Reset();

        // to compute mean time between collisions
        SumOfCollisionFreeTimes = 0;
        CountOfCollisionFreeTimes = 0;

        // keep track for reliability statistics
        collisionLastTime = false;
        TimeOfLastCollision = GameDemo.Clock.TotalSimulationTime;

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
        halfWidth = 1.0f;
        halfLength = 1.5f;

        // init dynamically controlled radius
        AdjustVehicleRadiusForSpeed();

        // not previously avoiding
        annotateAvoid = Vector3.Zero;

        // 10 seconds with 200 points along the trail
        if (trail == null) trail = new(10, 200);

        // prevent long streaks due to teleportation
        trail.Clear();

        // first pass at detecting "stuck" state
        Stuck = false;

        // QQQ need to clean up this hack
        qqqLastNearestObstacle = Vector3.Zero;

        // master look ahead (prediction) time
        baseLookAheadTime = 3;

        if (DemoSelect == 2)
        {
            LapsStarted++;
            const float s = WorldSize;
            float d = PathFollowDirection;
            Position = new(s * d * 0.6f, 0, s * -0.4f);
            RegenerateOrthonormalBasisUF(Vector3.UnitX * d);
        }

        // reset bookeeping to detect stuck cycles
        ResetStuckCycleDetection();

        // assume no previous steering
        currentSteering = Vector3.Zero;

        // assume normal running state
        dtZero = false;

        // QQQ temporary global QQQoaJustScraping
        qqQoaJustScraping = false;

        // state saved for speedometer
        AnnoteMaxRelSpeed = AnnoteMaxRelSpeedCurve = AnnoteMaxRelSpeedPath = 0;
        AnnoteMaxRelSpeed = AnnoteMaxRelSpeedCurve = AnnoteMaxRelSpeedPath = 1;
    }


    // per frame simulation update
    public void Update(float currentTime, float elapsedTime)
    {
        // take note when current dt is zero (as in paused) for stat counters
// ReSharper disable CompareOfFloatsByEqualityOperator
        dtZero = elapsedTime == 0;
// ReSharper restore CompareOfFloatsByEqualityOperator

        // pretend we are bigger when going fast
        AdjustVehicleRadiusForSpeed();

        // state saved for speedometer
        //      annoteMaxRelSpeed = annoteMaxRelSpeedCurve = annoteMaxRelSpeedPath = 0;
        AnnoteMaxRelSpeed = AnnoteMaxRelSpeedCurve = AnnoteMaxRelSpeedPath = 1;

        // determine combined steering
        var steering = Vector3.Zero;
        var offPath = !IsBodyInsidePath();
        if (Stuck || offPath || DetectImminentCollision())
        {
            // bring vehicle to a stop if we are stuck (newly or previously
            // stuck, because off path or collision seemed imminent)
            // (QQQ combine with stuckCycleCount code at end of this function?)
            //ApplyBrakingForce (curvedSteering ? 3 : 2, elapsedTime); // QQQ
            ApplyBrakingForce(CurvedSteering ? 3.0f : 2.0f, elapsedTime); // QQQ
            // count "off path" events
            if (offPath && !Stuck && DemoSelect == 2) StuckOffPathCount++;
            Stuck = true;

            // QQQ trying to prevent "creep" during emergency stops
            ResetAcceleration();
            currentSteering = Vector3.Zero;
        }
        else
        {
            // determine steering for obstacle avoidance (save for annotation)
            var avoid = annotateAvoid = SteerToAvoidObstaclesOnMap(LookAheadTimeOa(), HintForObstacleAvoidance());
            var needToAvoid = avoid != Vector3.Zero;

            // any obstacles to avoid?
            if (needToAvoid)
            {
                // slow down and turn to avoid the obstacles
                var targetSpeed = CurvedSteering && qqQoaJustScraping ? MaxSpeedForCurvature() : 0;
                AnnoteMaxRelSpeed = targetSpeed / MaxSpeed;
                var avoidWeight = 3 + (3 * RelativeSpeed()); // ad hoc
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
                    var wander = SteerForWander(elapsedTime);
                    wander.Y = 0;
                    var flat = wander;
                    var weighted = flat.TruncateLength(MaxForce) * 6;
                    var a = Position + new Vector3(0, 0.2f, 0);
                    Annotation.Line(a, a + (weighted * 0.3f), Color.White.ToVector3().FromXna());
                    steering += weighted;
                }

                // follow the path in demo 2
                if (DemoSelect == 2)
                {
                    var pf = SteerToFollowPath(PathFollowDirection, LookAheadTimePf());
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
                        var pathHeading = Path.TangentAt(Position, PathFollowDirection);
                        {
                            var b = Position + (Up * 0.2f) + (Forward * halfLength * 1.4f);
                            const float l = 2;
                            Annotation.Line(b, b + (Forward * l), Color.Cyan.ToVector3().FromXna());
                            Annotation.Line(b, b + (pathHeading * l), Color.Cyan.ToVector3().FromXna());
                        }
                        steering += SteerTowardHeading(pathHeading) *
                                    (Path.NearWaypoint(Position) ? 0.5f : 0.1f);
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
            var circles = WeAreGoingInCircles();
            if (circles && !Stuck) StuckCycleCount++;
            if (circles) Stuck = true;
            Annotation.CircleOrDisk(0.5f, Up, SmoothedPosition, Color.White.ToVector3().FromXna(), 12, circles, false);
        }

        // annotation
        PerFrameAnnotation();
        trail.Record(currentTime, Position);
    }

    void AdjustVehicleRadiusForSpeed()
    {
        var minRadius = (float)Math.Sqrt((halfWidth * halfWidth) + (halfLength * halfLength));
        var safetyMargin = CurvedSteering ? MathHelper.Lerp(0.0f, 1.5f, RelativeSpeed()) : 0.0f;
        Radius = minRadius + safetyMargin;
    }

    void CollectReliabilityStatistics(float currentTime, float elapsedTime)
    {
        // detect obstacle avoidance failure and keep statistics
        collisionDetected = Map.ScanLocalXzRectangle(this,
            -halfWidth, halfWidth,
            -halfLength, halfLength);

        // record stats to compute mean time between collisions
        var timeSinceLastCollision = currentTime - TimeOfLastCollision;
        if (collisionDetected && !collisionLastTime && timeSinceLastCollision > 1)
        {
            SumOfCollisionFreeTimes += timeSinceLastCollision;
            CountOfCollisionFreeTimes++;
            TimeOfLastCollision = currentTime;
        }

        collisionLastTime = collisionDetected;

        // keep track of average speed
        TotalDistance += Speed * elapsedTime;
        TotalTime += elapsedTime;
    }

    Vector3 HintForObstacleAvoidance()
    {
        // used only when path following, return zero ("no hint") otherwise
        if (DemoSelect != 2) return Vector3.Zero;

        // are we heading roughly parallel to the current path segment?
        var p = Position;
        var pathHeading = Path.TangentAt(p, PathFollowDirection);
        if (Vector3.Dot(pathHeading, Forward) < 0.8f)
        {
            // if not, the "hint" is to turn to align with path heading
            var s = Side * halfWidth;
            var f = halfLength * 2;
            Annotation.Line(p + s, p + s + (Forward * f), Color.Black.ToVector3().FromXna());
            Annotation.Line(p - s, p - s + (Forward * f), Color.Black.ToVector3().FromXna());
            Annotation.Line(p, p + (pathHeading * 5), Color.Magenta.ToVector3().FromXna());
            return pathHeading;
        }
        else
        {
            // when there is a valid nearest obstacle position
            var obstacle = qqqLastNearestObstacle;
            var o = obstacle + (Up * 0.1f);
            if (obstacle != Vector3.Zero)
            {
                // get offset, distance from obstacle to its image on path
                float outside;
                var onPath = Path.MapPointToPath(obstacle, out outside);
                var offset = onPath - obstacle;
                var offsetDistance = offset.Length();

                // when the obstacle is inside the path tube
                if (outside < 0)
                {
                    // when near the outer edge of a sufficiently wide tube
                    var segmentIndex = Path.IndexOfNearestSegment(onPath);
                    var segmentRadius = Path.Radii[segmentIndex];
                    var w = halfWidth * 6;
                    var nearEdge = offsetDistance > w;
                    var wideEnough = segmentRadius > w * 2;
                    if (nearEdge && wideEnough)
                    {
                        var obstacleDistance = (obstacle - p).Length();
                        var range = Speed * LookAheadTimeOa();
                        var farThreshold = range * 0.8f;
                        var usableHint = obstacleDistance > farThreshold;
                        if (usableHint)
                        {
                            var temp = Vector3.Normalize(offset);
                            var q = p + (temp * 5);
                            Annotation.Line(p, q, Color.Magenta.ToVector3().FromXna());
                            Annotation.CircleOrDisk(0.4f, Up, o, Color.White.ToVector3().FromXna(), 12, false, false);
                            return offset;
                        }
                    }
                }

                Annotation.CircleOrDisk(0.4f, Up, o, Color.Black.ToVector3().FromXna(), 12, false, false);
            }
        }

        // otherwise, no hint
        return Vector3.Zero;
    }

    // given a map of obstacles (currently a global, binary map) steer so as
    // to avoid collisions within the next minTimeToCollision seconds.
    //
    Vector3 SteerToAvoidObstaclesOnMap(float minTimeToCollision, Vector3 steerHint)
    {
        var spacing = Map.MinSpacing() / 2;
        var maxSide = Radius;
        var maxForward = minTimeToCollision * Speed;
        var maxSamples = (int)(maxForward / spacing);
        var step = Forward * spacing;
        var fOffset = Position;
        var sOffset = Vector3.Zero;
        var s = spacing / 2;

        const int infinity = 9999; // qqq
        var nearestL = infinity;
        var nearestR = infinity;
        var nearestWl = infinity;
        var nearestWr = infinity;
        var nearestO = Vector3.Zero;
        wingDrawFlagL = false;
        wingDrawFlagR = false;

        var hintGiven = steerHint != Vector3.Zero;
        if (hintGiven && !dtZero)
            HintGivenCount++;
        if (hintGiven)
            Annotation.CircleOrDisk(halfWidth * 0.9f, Up, Position + (Up * 0.2f), Color.White.ToVector3().FromXna(), 12,
                false, false);

        // QQQ temporary global QQQoaJustScraping
        qqQoaJustScraping = true;

        var signedRadius = 1 / NonZeroCurvatureQqq();
        var localCenterOfCurvature = Side * signedRadius;
        var center = Position + localCenterOfCurvature;
        var sign = signedRadius < 0 ? 1.0f : -1.0f;
        var arcRadius = signedRadius * -sign;
        const float twoPi = 2 * (float)Math.PI;
        var circumference = twoPi * arcRadius;
        var rawLength = Speed * minTimeToCollision * sign;
        const float fracLimit = 1.0f / 6.0f;
        var distLimit = circumference * fracLimit;
        var arcLength = ArcLengthLimit(rawLength, distLimit);
        var arcAngle = twoPi * arcLength / circumference;

        // XXX temp annotation to show limit on arc angle
        if (CurvedSteering)
        {
            if (Speed * minTimeToCollision > circumference * fracLimit)
            {
                const float q = twoPi * fracLimit;
                var fooz = Position - center;
                var booz = fooz.RotateAboutGlobalY(sign * q);
                Annotation.Line(center, center + fooz, Color.Red.ToVector3().FromXna());
                Annotation.Line(center, center + booz, Color.Red.ToVector3().FromXna());
            }
        }

        // scan corridor straight ahead of vehicle,
        // keep track of nearest obstacle on left and right sides
        for (; s < maxSide; s += spacing, sOffset = Side * s)
        {
            var lOffset = fOffset + sOffset;
            var rOffset = fOffset - sOffset;

            Vector3 lObsPos = Vector3.Zero, rObsPos = Vector3.Zero;

            var l = CurvedSteering
                ? (int)(ScanObstacleMap(lOffset,
                            center,
                            arcAngle,
                            maxSamples,
                            0,
                            Color.Yellow,
                            Color.Red,
                            out lObsPos)
                        / spacing)
                : Map.ScanXZray(lOffset, step, maxSamples);
            var r = CurvedSteering
                ? (int)(ScanObstacleMap(rOffset,
                            center,
                            arcAngle,
                            maxSamples,
                            0,
                            Color.Yellow,
                            Color.Red,
                            out rObsPos)
                        / spacing)
                : Map.ScanXZray(rOffset, step, maxSamples);

            if (l > 0 && l < nearestL)
            {
                nearestL = l;
                if (l < nearestR) nearestO = CurvedSteering ? lObsPos : lOffset + (step * l);
            }

            if (r > 0 && r < nearestR)
            {
                nearestR = r;
                if (r < nearestL) nearestO = CurvedSteering ? rObsPos : rOffset + (step * r);
            }

            if (!CurvedSteering)
            {
                AnnotateAvoidObstaclesOnMap(lOffset, l, step);
                AnnotateAvoidObstaclesOnMap(rOffset, r, step);
            }

            if (CurvedSteering)
            {
                // QQQ temporary global QQQoaJustScraping
                var outermost = s >= maxSide;
                var eitherSide = l > 0 || r > 0;
                if (!outermost && eitherSide) qqQoaJustScraping = false;
            }
        }

        qqqLastNearestObstacle = nearestO;

        // scan "wings"
        {
            const int wingScans = 4;
            // see duplicated code at: QQQ draw sensing "wings"
            // QQQ should be a parameter of this method
            var wingWidth = Side * WingSlope() * maxForward;

            var beforeColor =
                new Color((byte)(255.0f * 0.75f), (byte)(255.0f * 0.9f), (byte)(255.0f * 0.0f)); // for annotation
            var afterColor =
                new Color((byte)(255.0f * 0.9f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.0f)); // for annotation

            for (var i = 1; i <= wingScans; i++)
            {
                var fraction = i / (float)wingScans;
                var endside = sOffset + (wingWidth * fraction);
                var corridorFront = Forward * maxForward;

                // "loop" from -1 to 1
                for (var j = -1; j < 2; j += 2)
                {
                    float k = j; // prevent VC7.1 warning
                    var start = fOffset + (sOffset * k);
                    var end = fOffset + corridorFront + (endside * k);
                    var ray = end - start;
                    var rayLength = ray.Length();
                    var step2 = ray * spacing / rayLength;
                    var raySamples = (int)(rayLength / spacing);
                    var endRadius =
                        WingSlope() * maxForward * fraction *
                        (signedRadius < 0 ? 1 : -1) * (j == 1 ? 1 : -1);
                    Vector3 ignore;
                    var scan = CurvedSteering
                        ? (int)(ScanObstacleMap(start,
                                    center,
                                    arcAngle,
                                    raySamples,
                                    endRadius,
                                    beforeColor,
                                    afterColor,
                                    out ignore)
                                / spacing)
                        : Map.ScanXZray(start, step2, raySamples);

                    if (!CurvedSteering)
                        AnnotateAvoidObstaclesOnMap(start, scan, step2);

                    if (j == 1)
                    {
                        if (scan > 0 && scan < nearestWl) nearestWl = scan;
                    }
                    else
                    {
                        if (scan > 0 && scan < nearestWr) nearestWr = scan;
                    }
                }
            }

            wingDrawFlagL = nearestWl != infinity;
            wingDrawFlagR = nearestWr != infinity;
        }

        // for annotation
        SavedNearestWr = nearestWr;
        SavedNearestR = nearestR;
        SavedNearestL = nearestL;
        SavedNearestWl = nearestWl;

        // flags for compound conditions, used below
        var obstacleFreeC = nearestL == infinity && nearestR == infinity;
        var obstacleFreeL = nearestL == infinity && nearestWl == infinity;
        var obstacleFreeR = nearestR == infinity && nearestWr == infinity;
        var obstacleFreeWl = nearestWl == infinity;
        var obstacleFreeWr = nearestWr == infinity;
        var obstacleFreeW = obstacleFreeWl && obstacleFreeWr;

        // when doing curved steering and we have already detected "just
        // scarping" but neither wing is free, recind the "just scarping"
        // QQQ temporary global QQQoaJustScraping
        var js = CurvedSteering && qqQoaJustScraping;
        var cancelJs = !obstacleFreeWl && !obstacleFreeWr;
        if (js && cancelJs) qqQoaJustScraping = false;


        // ----------------------------------------------------------
        // now we have measured everything, decide which way to steer
        // ----------------------------------------------------------


        // no obstacles found on path, return zero steering
        if (obstacleFreeC)
        {
            qqqLastNearestObstacle = Vector3.Zero;

            // qqq  this may be in the wrong place (what would be the right
            // qqq  place?!) but I'm trying to say "even if the path is
            // qqq  clear, don't go too fast when driving between obstacles
            if (obstacleFreeWl || obstacleFreeWr || RelativeSpeed() < 0.7f)
                return Vector3.Zero;
            else
                return -Forward;
        }

        // if the nearest obstacle is way out there, take hint if any
        //      if (hintGiven && (Math.Min (nearestL, nearestR) > (maxSamples * 0.8f)))
        if (hintGiven && Math.Min(nearestL, nearestR) > maxSamples * 0.8f)
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
        var maxCurvature = 1 / (MinimumTurningRadius() * 1.2f);
        if (Math.Abs(Curvature) > maxCurvature)
        {
            var blue = new Color(0, 0, (byte)(255.0f * 0.8f));
            Annotation.CircleOrDisk(MinimumTurningRadius() * 1.2f, Up,
                center, blue.ToVector3().FromXna(), 40, false, false);
            return Side * sign;
        }


        if (obstacleFreeL) return Side;
        if (obstacleFreeR) return -Side;

        // if wings are clear, turn away from nearest obstacle straight ahead
        if (obstacleFreeW)
        {
            // distance to obs on L and R side of corridor roughtly the same
            var same = Math.Abs(nearestL - nearestR) < 5; // within 5
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
        var equallyClear = Math.Abs(nearestWl - nearestWr) < 2; // within 2
        if (equallyClear && hintGiven)
        {
            AnnotationHintWasTaken();
            if (Vector3.Dot(steerHint, Side) > 0) return Side;
            else return -Side;
        }

        // turn towards the side whose "wing" region is less cluttered
        // (the wing whose nearest obstacle is furthest away)
        if (nearestWl > nearestWr) return Side;
        else return -Side;
    }

    // QQQ reconsider calling sequence
    // called when steerToAvoidObstaclesOnMap decides steering is required
    // (default action is to do nothing, layered classes can overload it)
    void AnnotateAvoidObstaclesOnMap(Vector3 scanOrigin, int scanIndex, Vector3 scanStep)
    {
        if (scanIndex > 0)
        {
            var hit = scanOrigin + (scanStep * scanIndex);
            Annotation.Line(scanOrigin, hit,
                new Color((byte)(255.0f * 0.7f), (byte)(255.0f * 0.3f), (byte)(255.0f * 0.3f)).ToVector3().FromXna());
        }
    }

    void AnnotationHintWasTaken()
    {
        if (!dtZero) HintTakenCount++;

        var r = halfWidth * 0.9f;
        var ff = Forward * r;
        var ss = Side * r;
        var pp = Position + (Up * 0.2f);
        Annotation.Line(pp + ff + ss, pp - ff + ss, Color.White.ToVector3().FromXna());
        Annotation.Line(pp - ff - ss, pp - ff + ss, Color.White.ToVector3().FromXna());
        Annotation.Line(pp - ff - ss, pp + ff - ss, Color.White.ToVector3().FromXna());
        Annotation.Line(pp + ff + ss, pp + ff - ss, Color.White.ToVector3().FromXna());

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
    float ScanObstacleMap(Vector3 start, Vector3 center, float arcAngle, int segments, float endRadiusChange,
        Color beforeColor, Color afterColor, out Vector3 returnObstaclePosition)
    {
        // "spoke" is initially the vector from center to start,
        // which is then rotated step by step around center
        var spoke = start - center;
        // determine the angular step per segment
        var step = arcAngle / segments;
        // store distance to, and position of first obstacle
        float obstacleDistance = 0;
        returnObstaclePosition = Vector3.Zero;
        // for spiral "ramps" of changing radius
// ReSharper disable CompareOfFloatsByEqualityOperator
        var startRadius = endRadiusChange == 0 ? 0 : spoke.Length();
// ReSharper restore CompareOfFloatsByEqualityOperator

        // traverse each segment along arc
        float sin = 0, cos = 0;
        var oldPoint = start;
        var obstacleFound = false;
        for (var i = 0; i < segments; i++)
        {
            // rotate "spoke" to next step around circle
            // (sin and cos values get filled in on first call)
            spoke = spoke.RotateAboutGlobalY(step, ref sin, ref cos);

            // for spiral "ramps" of changing radius
            var adjust = Math.Abs(endRadiusChange - 0) < float.Epsilon
                ? 1.0f
                : MathHelper.Lerp(1.0f,
                    Math.Max(0,
                        startRadius +
                        endRadiusChange)
                    / startRadius, (i + 1) / (float)segments);

            // construct new scan point: center point, offset by rotated
            // spoke (possibly adjusting the radius if endRadiusChange!=0)
            var newPoint = center + (spoke * adjust);

            // once an obstacle if found "our work here is done" -- continue
            // to loop only for the sake of annotation (make that optional?)
            if (obstacleFound)
            {
                Annotation.Line(oldPoint, newPoint, afterColor.ToVector3().FromXna());
            }
            else
            {
                // no obstacle found on this scan so far,
                // scan map along current segment (a chord of the arc)
                var offset = newPoint - oldPoint;
                var d2 = offset.Length() * 2;

                // when obstacle found: set flag, save distance and position
                if (!Map.IsPassable(newPoint))
                {
                    obstacleFound = true;
                    obstacleDistance = d2 * 0.5f * (i + 1);
                    returnObstaclePosition = newPoint;
                }

                Annotation.Line(oldPoint, newPoint, beforeColor.ToVector3().FromXna());
            }

            // save new point for next time around loop
            oldPoint = newPoint;
        }

        // return distance to first obstacle (or zero if none found)
        return obstacleDistance;
    }

    bool DetectImminentCollision()
    {
        // QQQ  this should be integrated into steerToAvoidObstaclesOnMap
        // QQQ  since it shares so much infrastructure
        // QQQ  less so after changes on 3-16-04
        var returnFlag = false;
        var spacing = Map.MinSpacing() / 2;
        var maxSide = halfWidth + spacing;
        var minDistance = CurvedSteering ? 2.0f : 2.5f; // meters
        var predictTime = CurvedSteering ? .75f : 1.3f; // seconds
        var maxForward = Speed * CombinedLookAheadTime(predictTime, minDistance);
        var step = Forward * spacing;
        var s = CurvedSteering ? spacing / 4 : spacing / 2;

        var signedRadius = 1 / NonZeroCurvatureQqq();
        var localCenterOfCurvature = Side * signedRadius;
        var center = Position + localCenterOfCurvature;
        var sign = signedRadius < 0 ? 1.0f : -1.0f;
        var arcRadius = signedRadius * -sign;
        const float twoPi = 2 * (float)Math.PI;
        var circumference = twoPi * arcRadius;
        var qqqLift = new Vector3(0, 0.2f, 0);

        // scan region ahead of vehicle
        while (s < maxSide)
        {
            var sOffset = Side * s;
            var lOffset = Position + sOffset;
            var rOffset = Position - sOffset;
            const float bevel = 0.3f;
            var fraction = s / maxSide;
            var scanDist = halfLength +
                           MathHelper.Lerp(maxForward,
                               maxForward * bevel, fraction);
            var angle = scanDist * twoPi * sign / circumference;
            var samples = (int)(scanDist / spacing);
            Vector3 ignore;
            var l = CurvedSteering
                ? (int)(ScanObstacleMap(lOffset + qqqLift,
                            center,
                            angle,
                            samples,
                            0,
                            Color.Magenta,
                            Color.Cyan,
                            out ignore)
                        / spacing)
                : Map.ScanXZray(lOffset, step, samples);
            var r = CurvedSteering
                ? (int)(ScanObstacleMap(rOffset + qqqLift,
                            center,
                            angle,
                            samples,
                            0,
                            Color.Magenta,
                            Color.Cyan,
                            out ignore)
                        / spacing)
                : Map.ScanXZray(rOffset, step, samples);

            returnFlag = returnFlag || l > 0;
            returnFlag = returnFlag || r > 0;

            // annotation
            if (!CurvedSteering)
            {
                var d = step * samples;
                Annotation.Line(lOffset, lOffset + d, Color.White.ToVector3().FromXna());
                Annotation.Line(rOffset, rOffset + d, Color.White.ToVector3().FromXna());
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
            var signedRadius = 1 / NonZeroCurvatureQqq();
            var localCenterOfCurvature = Side * signedRadius;
            var center = Position + localCenterOfCurvature;
            var sign = signedRadius < 0 ? 1.0f : -1.0f;
            var arcRadius = signedRadius * -sign;
            const float twoPi = 2 * (float)Math.PI;
            var circumference = twoPi * arcRadius;
            var rawLength = Speed * predictionTime * sign;
            var arcLength = ArcLengthLimit(rawLength, circumference * 0.25f);
            var arcAngle = twoPi * arcLength / circumference;

            var spoke = Position - center;
            var newSpoke = spoke.RotateAboutGlobalY(arcAngle);
            var prediction = newSpoke + center;

            // QQQ unify with annotatePathFollowing
            var futurePositionColor = new Color((byte)(255.0f * 0.5f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.6f));
            AnnotationXzArc(Position, center, arcLength, 20, futurePositionColor);
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
    static float ArcLengthLimit(float length, float limit)
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
    Vector3 SteerToFollowPath(int direction, float predictionTime)
    {
        if (CurvedSteering)
            return SteerToFollowPathCurve(direction, predictionTime);
        else
            return SteerToFollowPathLinear(direction, predictionTime);
    }

    Vector3 SteerToFollowPathLinear(int direction, float predictionTime)
    {
        // our goal will be offset from our path distance by this amount
        var pathDistanceOffset = direction * predictionTime * Speed;

        // predict our future position
        var futurePosition = PredictFuturePosition(predictionTime);

        // measure distance along path of our current and predicted positions
        var nowPathDistance =
            Path.MapPointToPathDistance(Position);

        // are we facing in the correction direction?
        var pathHeading = Path.TangentAt(Position) * direction;
        var correctDirection = Vector3.Dot(pathHeading, Forward) > 0;

        // find the point on the path nearest the predicted future position
        // XXX need to improve calling sequence, maybe change to return a
        // XXX special path-defined object which includes two Vector3s and a
        // XXX bool (onPath,tangent (ignored), withinPath)
        float futureOutside;
        var onPath = Path.MapPointToPath(futurePosition, out futureOutside);

        // determine if we are currently inside the path tube
        float nowOutside;
        var nowOnPath = Path.MapPointToPath(Position, out nowOutside);

        // no steering is required if our present and future positions are
        // inside the path tube and we are facing in the correct direction
        var m = -Radius;
        var whollyInside = futureOutside < m && nowOutside < m;
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
            var targetPathDistance = nowPathDistance +
                                     (pathDistanceOffset *
                                      (correctDirection ? 1 : 0.1f));
            var target = Path.MapPathDistanceToPoint(targetPathDistance);


            // if we are on one segment and target is on the next segment and
            // the dot of the tangents of the two segments is negative --
            // increase the target offset to compensate the fold back
            var ip = Path.IndexOfNearestSegment(Position);
            var it = Path.IndexOfNearestSegment(target);
            if (ip + direction == it &&
                Path.DotSegmentUnitTangents(it, ip) < -0.1f)
            {
                var newTargetPathDistance =
                    nowPathDistance + (pathDistanceOffset * 2);
                target = Path.MapPathDistanceToPoint(newTargetPathDistance);
            }

            AnnotatePathFollowing(futurePosition, onPath, target, futureOutside);

            // if we are currently outside head directly in
            // (QQQ new, experimental, makes it turn in more sharply)
            if (nowOutside > 0) return SteerForSeek(nowOnPath);

            // steering to seek target on path
            var seek = SteerForSeek(target).TruncateLength(MaxForce);

            // return that seek steering -- except when we are heading off
            // the path (currently on path and future position is off path)
            // in which case we put on the brakes.
            if (nowOutside < 0 && futureOutside > 0)
                return Vector3Helpers.PerpendicularComponent(seek, Forward) - (Forward * MaxForce);
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
    Vector3 SteerToFollowPathCurve(int direction, float predictionTime)
    {
        // predict our future position (based on current curvature and speed)
        var futurePosition = PredictFuturePosition(predictionTime);
        // find the point on the path nearest the predicted future position
        float futureOutside;
        var onPath = Path.MapPointToPath(futurePosition, out futureOutside);
        var pathHeading = Path.TangentAt(onPath, direction);
        var rawBraking = Forward * MaxForce * -1;
        var braking = futureOutside < 0 ? Vector3.Zero : rawBraking;
        //qqq experimental wrong-way-fixer
        float nowOutside;
        Vector3 nowTangent;
        var p = Position;
        var nowOnPath = Path.MapPointToPath(p, out nowTangent, out nowOutside);
        nowTangent *= (float)direction;
        var alignedness = Vector3.Dot(nowTangent, Forward);

        // facing the wrong way?
        if (alignedness < 0)
        {
            Annotation.Line(p, p + (nowTangent * 10), Color.Cyan.ToVector3().FromXna());

            // if nearly anti-parallel
            if (alignedness < -0.707f)
            {
                var towardCenter = nowOnPath - p;
                var turn = Vector3.Dot(towardCenter, Side) > 0 ? Side * MaxForce : Side * MaxForce * -1;
                return turn + rawBraking;
            }
            else
            {
                return Vector3Helpers.PerpendicularComponent(SteerTowardHeading(pathHeading), Forward) + braking;
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
            Annotation.Line(futurePosition, futurePosition + pathHeading, Color.Red.ToVector3().FromXna());
            AnnotatePathFollowing(futurePosition, onPath,
                Position, futureOutside);

            // two cases, if entering a turn (a waypoint between path segments)
            if (Path.NearWaypoint(onPath) && futureOutside > 0)
            {
                // steer to align with next path segment
                Annotation.Circle3D(0.5f, futurePosition, Up, Color.Red.ToVector3().FromXna(), 8);
                return SteerTowardHeading(pathHeading) + braking;
            }
            else
            {
                // otherwise steer away from the side of the path we
                // are heading for
                var pathSide = this.LocalRotateForwardToSide(pathHeading);
                var towardFp = futurePosition - onPath;
                var whichSide = Vector3.Dot(pathSide, towardFp) < 0 ? 1.0f : -1.0f;
                return (Side * MaxForce * whichSide) + braking;
            }
        }
    }

    void PerFrameAnnotation()
    {
        var p = Position;

        // draw the circular collision boundary
        Annotation.CircleOrDisk(Radius, Up, p, Color.Black.ToVector3().FromXna(), 32, false, false);

        // draw forward sensing corridor and wings ( for non-curved case)
        if (!CurvedSteering)
        {
            var corLength = Speed * LookAheadTimeOa();
            if (corLength > halfLength)
            {
                var corFront = Forward * corLength;
                var corBack = Vector3.Zero; // (was bbFront)
                var corSide = Side * Radius;
                var c1 = p + corSide + corBack;
                var c2 = p + corSide + corFront;
                var c3 = p - corSide + corFront;
                var c4 = p - corSide + corBack;
                var color = annotateAvoid != Vector3.Zero ? Color.Red : Color.Yellow;
                Annotation.Line(c1, c2, color.ToVector3().FromXna());
                Annotation.Line(c2, c3, color.ToVector3().FromXna());
                Annotation.Line(c3, c4, color.ToVector3().FromXna());

                // draw sensing "wings"
                var wingWidth = Side * WingSlope() * corLength;
                var wingTipL = c2 + wingWidth;
                var wingTipR = c3 - wingWidth;
                var wingColor = Color.Orange;
                if (wingDrawFlagL) Annotation.Line(c2, wingTipL, wingColor.ToVector3().FromXna());
                if (wingDrawFlagL) Annotation.Line(c1, wingTipL, wingColor.ToVector3().FromXna());
                if (wingDrawFlagR) Annotation.Line(c3, wingTipR, wingColor.ToVector3().FromXna());
                if (wingDrawFlagR) Annotation.Line(c4, wingTipR, wingColor.ToVector3().FromXna());
            }
        }

        // annotate steering acceleration
        var above = Position + new Vector3(0, 0.2f, 0);
        var accel = Acceleration * 5 / MaxForce;
        var aColor = new Color((byte)(255.0f * 0.4f), (byte)(255.0f * 0.4f), (byte)(255.0f * 0.8f));
        Annotation.Line(above, above + accel, aColor.ToVector3().FromXna());
    }

    // draw vehicle's body and annotation
    public void Draw()
    {
        // for now: draw as a 2d bounding box on the ground
        var bodyColor = Color.Black;
        if (Stuck) bodyColor = Color.Yellow;
        if (!IsBodyInsidePath()) bodyColor = Color.Orange;
        if (collisionDetected) bodyColor = Color.Red;

        // draw vehicle's bounding box on gound plane (its "shadow")
        var p = Position;
        var bbSide = Side * halfWidth;
        var bbFront = Forward * halfLength;
        var bbHeight = new Vector3(0, 0.1f, 0);
        Drawing.DrawQuadrangle(p - bbFront + bbSide + bbHeight,
            p + bbFront + bbSide + bbHeight,
            p + bbFront - bbSide + bbHeight,
            p - bbFront - bbSide + bbHeight,
            bodyColor);

        // annotate trail
        var darkGreen = new Color(0, (byte)(255.0f * 0.6f), 0);
        trail.TrailColor = darkGreen;
        trail.TickColor = Color.Black;
        trail.Draw(Annotation);
    }

    // called when steerToFollowPath decides steering is required
    void AnnotatePathFollowing(Vector3 future, Vector3 onPath, Vector3 target, float outside)
    {
        var toTargetColor = new Color(0, (byte)(255.0f * 0.6f), 0);
        var insidePathColor = new Color((byte)(255.0f * 0.6f), (byte)(255.0f * 0.6f), 0);
        var outsidePathColor = new Color(0, 0, (byte)(255.0f * 0.6f));
        var futurePositionColor = new Color((byte)(255.0f * 0.5f), (byte)(255.0f * 0.5f), (byte)(255.0f * 0.6f));

        // draw line from our position to our predicted future position
        if (!CurvedSteering)
            Annotation.Line(Position, future, futurePositionColor.ToVector3().FromXna());

        // draw line from our position to our steering target on the path
        Annotation.Line(Position, target, toTargetColor.ToVector3().FromXna());

        // draw a two-toned line between the future test point and its
        // projection onto the path, the change from dark to light color
        // indicates the boundary of the tube.

        var o = outside + Radius + (CurvedSteering ? 1.0f : 0.0f);
        var boundaryOffset = Vector3.Normalize(onPath - future);
        boundaryOffset *= o;

        var onPathBoundary = future + boundaryOffset;
        Annotation.Line(onPath, onPathBoundary, insidePathColor.ToVector3().FromXna());
        Annotation.Line(onPathBoundary, future, outsidePathColor.ToVector3().FromXna());
    }

    public void DrawMap()
    {
        var xs = Map.XSize / Map.Resolution;
        var zs = Map.ZSize / Map.Resolution;
        var alongRow = new Vector3(xs, 0, 0);
        var nextRow = new Vector3(-Map.XSize, 0, zs);
        var g = new Vector3((Map.XSize - xs) / -2, 0, (Map.ZSize - zs) / -2);
        g += Map.Center;
        for (var j = 0; j < Map.Resolution; j++)
        {
            for (var i = 0; i < Map.Resolution; i++)
            {
                if (Map.GetMapBit(i, j))
                {
                    // spikes
                    // Vector3 spikeTop (0, 5.0f, 0);
                    // drawLine (g, g+spikeTop, Color.White);

                    // squares
                    const float rockHeight = 0;
                    var v1 = new Vector3(+xs / 2, rockHeight, +zs / 2);
                    var v2 = new Vector3(+xs / 2, rockHeight, -zs / 2);
                    var v3 = new Vector3(-xs / 2, rockHeight, -zs / 2);
                    var v4 = new Vector3(-xs / 2, rockHeight, +zs / 2);
                    // Vector3 redRockColor (0.6f, 0.1f, 0.0f);
                    var orangeRockColor =
                        new Color((byte)(255.0f * 0.5f), (byte)(255.0f * 0.2f), (byte)(255.0f * 0.0f));
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
        var pathColor = new Vector3(0, 0.5f, 0.5f);
        var sandColor = new Vector3(0.8f, 0.7f, 0.5f);
        var vColor = Vector3.Lerp(sandColor, pathColor, 0.1f);
        var color = new Color(vColor.ToXna());

        var down = new Vector3(0, -0.1f, 0);
        for (var i = 0; i < Path.PointCount; i++)
        {
            var endPoint0 = Path.Points[i] + down;
            if (i > 0)
            {
                var endPoint1 = Path.Points[i - 1] + down;

                var legWidth = Path.Radii[i];

                Drawing.DrawXzWideLine(endPoint0, endPoint1, color, legWidth * 2);
                Drawing.DrawLine(Path.Points[i], Path.Points[i - 1], new(pathColor.ToXna()));
                Drawing.DrawXzDisk(legWidth, endPoint0, color, 24);
                Drawing.DrawXzDisk(legWidth, endPoint1, color, 24);
            }
        }
    }

    static GcRoute MakePath()
    {
        // a few constants based on world size
        const float m = WorldSize * 0.4f; // main diamond size
        const float n = WorldSize / 8; // notch size
        const float o = WorldSize * 2; // outside of the sand

        // construction vectors
        var p = new Vector3(0, 0, m);
        var q = new Vector3(0, 0, m - n);
        var r = new Vector3(-m, 0, 0);
        var s = new Vector3(2 * n, 0, 0);
        var t = new Vector3(o, 0, 0);
        var u = new Vector3(-o, 0, 0);
        var v = new Vector3(n, 0, 0);
        var w = new Vector3(0, 0, 0);


        // path vertices
        var a = t - p;
        var b = s + v - p;
        var c = s - q;
        var d = s + q;
        var e = s - v + p;
        var f = p - w;
        var g = r - w;
        var h = -p - w;
        var i = u - p;

        // return Path object
        Vector3[] pathPoints = { a, b, c, d, e, f, g, h, i };
        const float k = 10.0f;
        float[] pathRadii = { k, k, k, k, k, k, k, k, k };
        return new(pathPoints, pathRadii, false);
    }

    static TerrainMap MakeMap() => new(Vector3.Zero, WorldSize, WorldSize, (int)WorldSize + 1);

    public bool HandleExitFromMap()
    {
        if (DemoSelect == 2)
        {
            // for path following, do wrap-around (teleport) and make new map
            var px = Position.X;
            var fx = Forward.X;
            const float ws = WorldSize * 0.51f; // slightly past edge
            if ((fx > 0 && px > ws) || (fx < 0 && px < -ws))
            {
                // bump counters
                LapsStarted++;
                LapsFinished++;

                var camOffsetBefore = GameDemo.Camera.Position - Position;

                // set position on other side of the map (set new X coordinate)
                Position = new((px < 0 ? 1 : -1) * ((WorldSize * 0.5f) + (Speed * LookAheadTimePf())), Position.Y,
                    Position.Z);

                // reset bookeeping to detect stuck cycles
                ResetStuckCycleDetection();

                // new camera position and aimpoint to compensate for teleport
                GameDemo.Camera.Target = Position;
                GameDemo.Camera.Position = Position + camOffsetBefore;

                // make camera jump immediately to new position
                GameDemo.Camera.DoNotSmoothNextMove();

                // prevent long streaks due to teleportation
                trail.Clear();

                return true;
            }
        }
        else
        {
            // for the non-path-following demos:
            // reset simulation if the vehicle drives through the fence
            if (Position.Length() > worldDiag) Reset();
        }

        return false;
    }


    // QQQ move this utility to SimpleVehicle?
    public float RelativeSpeed() => Speed / MaxSpeed;

    float WingSlope() =>
        MathHelper.Lerp(CurvedSteering ? 0.3f : 0.35f,
            0.06f, RelativeSpeed());

    void ResetStuckCycleDetection() => ResetSmoothedPosition(Position + (Forward * -80)); // qqq

    // QQQ just a stop gap, not quite right
    // (say for example we were going around a circle with radius > 10)
    bool WeAreGoingInCircles()
    {
        var offset = SmoothedPosition - Position;
        return offset.Length() < 10;
    }

    float LookAheadTimeOa()
    {
        var minTime = baseLookAheadTime *
                      (CurvedSteering ? MathHelper.Lerp(0.4f, 0.7f, RelativeSpeed()) : 0.66f);
        return CombinedLookAheadTime(minTime, 3);
    }

    float LookAheadTimePf() => CombinedLookAheadTime(baseLookAheadTime, 3);

    // QQQ maybe move to SimpleVehicle ?
    // compute a "look ahead time" with two components, one based on
    // minimum time to (say) a collision and one based on minimum distance
    // arg 1 is "seconds into the future", arg 2 is "meters ahead"
    float CombinedLookAheadTime(float minTime, float minDistance)
    {
// ReSharper disable CompareOfFloatsByEqualityOperator
        if (Speed == 0) return 0;
// ReSharper restore CompareOfFloatsByEqualityOperator
        return Math.Max(minTime, minDistance / Speed);
    }

    // is vehicle body inside the path?
    // (actually tests if all four corners of the bounbding box are inside)
    //
    bool IsBodyInsidePath()
    {
        if (DemoSelect == 2)
        {
            var bbSide = Side * halfWidth;
            var bbFront = Forward * halfLength;
            return Path.IsInsidePath(Position - bbFront + bbSide) &&
                   Path.IsInsidePath(Position + bbFront + bbSide) &&
                   Path.IsInsidePath(Position + bbFront - bbSide) &&
                   Path.IsInsidePath(Position - bbFront - bbSide);
        }

        return true;
    }

    Vector3 ConvertAbsoluteToIncrementalSteering(Vector3 absolute, float elapsedTime)
    {
        var curved = ConvertLinearToCurvedSpaceGlobal(absolute);
        Utilities.BlendIntoAccumulator(elapsedTime * 8.0f, curved, ref currentSteering);
        {
            // annotation
            var u = new Vector3(0, 0.5f, 0);
            var p = Position;
            Annotation.Line(p + u, p + u + absolute, Color.Red.ToVector3().FromXna());
            Annotation.Line(p + u, p + u + curved, Color.Yellow.ToVector3().FromXna());
            Annotation.Line(p + u * 2, p + u * 2 + currentSteering, Color.Green.ToVector3().FromXna());
        }
        return currentSteering;
    }

    // QQQ new utility 2-25-04 -- may replace inline code elsewhere
    //
    // Given a location in this vehicle's linear local space, convert it into
    // the curved space defined by the vehicle's current path curvature.  For
    // example, forward() gets mapped on a point 1 unit along the circle
    // centered on the current center of curvature and passing through the
    // vehicle's position().
    //
    Vector3 ConvertLinearToCurvedSpaceGlobal(Vector3 linear)
    {
        var trimmedLinear = linear.TruncateLength(MaxForce);

        // ---------- this block imported from steerToAvoidObstaclesOnMap
        var signedRadius = 1 / (NonZeroCurvatureQqq() /*QQQ*/ * 1);
        var localCenterOfCurvature = Side * signedRadius;
        var center = Position + localCenterOfCurvature;
        var sign = signedRadius < 0 ? 1.0f : -1.0f;
        var arcLength = Vector3.Dot(trimmedLinear, Forward);
        //
        var arcRadius = signedRadius * -sign;
        const float twoPi = 2 * (float)Math.PI;
        var circumference = twoPi * arcRadius;
        var arcAngle = twoPi * arcLength / circumference;
        // ---------- this block imported from steerToAvoidObstaclesOnMap

        // ---------- this block imported from scanObstacleMap
        // vector from center of curvature to position of vehicle
        var initialSpoke = Position - center;
        // rotate by signed arc angle
        var spoke = initialSpoke.RotateAboutGlobalY(arcAngle * sign);
        // ---------- this block imported from scanObstacleMap

        var fromCenter = Vector3.Normalize(-localCenterOfCurvature);
        var dRadius = Vector3.Dot(trimmedLinear, fromCenter);
        var radiusChangeFactor = (dRadius + arcRadius) / arcRadius;
        var resultLocation = center + (spoke * radiusChangeFactor);
        {
            var center2 = Position + localCenterOfCurvature;
            AnnotationXzArc(Position, center2, Speed * sign * -3, 20, Color.White);
        }
        // return the vector from vehicle position to the coimputed location
        // of the curved image of the original linear offset
        return resultLocation - Position;
    }

    // approximate value for the Polaris Ranger 6x6: 16 feet, 5 meters
    static float MinimumTurningRadius() => 5.0f;

    Vector3 AdjustSteeringForMinimumTurningRadius(Vector3 steering)
    {
        var maxCurvature = 1 / (MinimumTurningRadius() * 1.1f);

        // are we turning more sharply than the minimum turning radius?
        if (Math.Abs(Curvature) > maxCurvature)
        {
            // remove the tangential (non-thrust) component of the steering
            // force, replace it with a force pointing away from the center
            // of curvature, causing us to "widen out" easing off from the
            // minimum turing radius
            var signedRadius = 1 / NonZeroCurvatureQqq();
            var sign = signedRadius < 0 ? 1.0f : -1.0f;
            var thrust = Vector3Helpers.ParallelComponent(steering, Forward);
            var trimmed = thrust.TruncateLength(MaxForce);
            var widenOut = Side * MaxForce * sign;
            {
                // annotation
                var localCenterOfCurvature = Side * signedRadius;
                var center = Position + localCenterOfCurvature;
                Annotation.CircleOrDisk(MinimumTurningRadius(), Up,
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
    float NonZeroCurvatureQqq()
    {
        var c = Curvature;
        const float minCurvature = 1.0f / 100000.0f; // 100,000 meter radius
        var tooSmall = c < minCurvature && c > -minCurvature;
        return tooSmall ? minCurvature : c;
    }

    // QQQ ad hoc speed limitation based on path orientation...
    // QQQ should be renamed since it is based on more than curvature
    //
    float MaxSpeedForCurvature()
    {
        float maxRelativeSpeed = 1;

        if (CurvedSteering)
        {
            // compute an ad hoc "relative curvature"
            var absC = Math.Abs(Curvature);
            var maxC = 1 / MinimumTurningRadius();
            var relativeCurvature = (float)Math.Sqrt(MathHelper.Clamp(absC / maxC, 0, 1));

            // map from full throttle when straight to 10% at max curvature
            var curveSpeed = MathHelper.Lerp(1.0f, 0.1f, relativeCurvature);
            AnnoteMaxRelSpeedCurve = curveSpeed;

            if (DemoSelect != 2)
            {
                maxRelativeSpeed = curveSpeed;
            }
            else
            {
                // heading (unit tangent) of the path segment of interest
                var pathHeading = Path.TangentAt(Position, PathFollowDirection);
                // measure how parallel we are to the path
                var parallelness = Vector3.Dot(pathHeading, Forward);

                // determine relative speed for this heading
                const float mw = 0.2f;
                var headingSpeed = parallelness < 0 ? mw : MathHelper.Lerp(mw, 1.0f, parallelness);
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
    Vector3 SteerTowardHeading(Vector3 desiredGlobalHeading)
    {
        var headingError = Vector3.Normalize(desiredGlobalHeading - Forward);
        headingError *= MaxForce;

        return headingError;
    }

    // XXX this should eventually be in a library, make it a first
    // XXX class annotation queue, tie in with drawXZArc
    void AnnotationXzArc(Vector3 start, Vector3 center, float arcLength, int segments, Color color)
    {
        // "spoke" is initially the vector from center to start,
        // it is then rotated around its tail
        var spoke = start - center;

        // determine the angular step per segment
        var radius = spoke.Length();
        const float twoPi = 2 * (float)Math.PI;
        var circumference = twoPi * radius;
        var arcAngle = twoPi * arcLength / circumference;
        var step = arcAngle / segments;

        // draw each segment along arc
        float sin = 0, cos = 0;
        for (var i = 0; i < segments; i++)
        {
            var old = spoke + center;

            // rotate point to next step around circle
            spoke = spoke.RotateAboutGlobalY(step, ref sin, ref cos);

            Annotation.Line(spoke + center, old, color.ToVector3().FromXna());
        }
    }

    // map of obstacles
    public readonly TerrainMap Map;

    // route for path following (waypoints and legs)
    public readonly GcRoute Path;

    // follow the path "upstream or downstream" (+1/-1)
    public int PathFollowDirection;

    // master look ahead (prediction) time
    float baseLookAheadTime;

    // vehicle dimentions in meters
    float halfWidth;
    float halfLength;

    // keep track of failure rate (when vehicle is on top of obstacle)
    bool collisionDetected;
    bool collisionLastTime;
    public float TimeOfLastCollision;
    public float SumOfCollisionFreeTimes;
    public int CountOfCollisionFreeTimes;

    // keep track of average speed
    public float TotalDistance;
    public float TotalTime;

    // take note when current dt is zero (as in paused) for stat counters
    bool dtZero;

    // state saved for annotation
    Vector3 annotateAvoid;
    bool wingDrawFlagL;
    bool wingDrawFlagR;

    // QQQ first pass at detecting "stuck" state
    public bool Stuck;
    public int StuckCount;
    public int StuckCycleCount;
    public int StuckOffPathCount;

    Vector3 qqqLastNearestObstacle;

    public int LapsStarted;
    public int LapsFinished;

    // QQQ temporary global QQQoaJustScraping
    // QQQ replace this global flag with a cleaner mechanism
    bool qqQoaJustScraping;

    public int HintGivenCount;
    public int HintTakenCount;

    // for "curvature-based incremental steering" -- contains the current
    // steering into which new incremental steering is blended
    Vector3 currentSteering;

    // use curved prediction and incremental steering:
    public bool CurvedSteering;
    public bool IncrementalSteering;

    // save obstacle avoidance stats for annotation
    // (nearest obstacle in each of the four zones)
    public static float SavedNearestWr;
    public static float SavedNearestR;
    public static float SavedNearestL;
    public static float SavedNearestWl;

    public float AnnoteMaxRelSpeed;
    public float AnnoteMaxRelSpeedCurve;
    public float AnnoteMaxRelSpeedPath;

    // which of the three demo modes is selected
    public static int DemoSelect = 2;

    // size of the world (the map actually)
    public const float WorldSize = 200;
    static readonly float worldDiag = (float)Math.Sqrt(WorldSize * WorldSize / 2);
}
