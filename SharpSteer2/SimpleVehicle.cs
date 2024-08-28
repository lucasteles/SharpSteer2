// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using SharpSteer2.Helpers;

namespace SharpSteer2;

public class SimpleVehicle(IAnnotationService annotations = null) : SteerLibrary(annotations)
{
    Vector3 lastForward;
    Vector3 lastPosition;
    float smoothedCurvature;
    // The acceleration is smoothed
    Vector3 acceleration;

    // reset vehicle state
    public override void Reset()
    {
        base.Reset();

        // reset LocalSpace state
        ResetLocalSpace();

        Mass = 1;          // Mass (defaults to 1 so acceleration=force)
        Speed = 0;         // speed along Forward direction.

        Radius = 0.5f;     // size of bounding sphere

        // reset bookkeeping to do running averages of these quanities
        ResetSmoothedPosition();
        ResetSmoothedCurvature();
        ResetAcceleration();
    }

    // get/set Mass
    // Mass (defaults to unity so acceleration=force)
    public override float Mass { get; set; }

    // get velocity of vehicle
    public override Vector3 Velocity => Forward * Speed;

    // get/set speed of vehicle  (may be faster than taking mag of velocity)
    // speed along Forward direction. Because local space is
    // velocity-aligned, velocity = Forward * Speed
    public override float Speed { get; set; }

    // size of bounding sphere, for obstacle avoidance, etc.
    public override float Radius { get; set; }

    // get/set maxForce
    // the maximum steering force this vehicle can apply
    // (steering force is clipped to this magnitude)
    public override float MaxForce => 0.1f;

    // get/set maxSpeed
    // the maximum speed this vehicle is allowed to move
    // (velocity is clipped to this magnitude)
    public override float MaxSpeed => 1;

    // apply a given steering force to our momentum,
    // adjusting our orientation to maintain velocity-alignment.
    public void ApplySteeringForce(Vector3 force, float elapsedTime)
    {
        Vector3 adjustedForce = AdjustRawSteeringForce(force, elapsedTime);

        // enforce limit on magnitude of steering force
        Vector3 clippedForce = adjustedForce.TruncateLength(MaxForce);

        // compute acceleration and velocity
        Vector3 newAcceleration = (clippedForce / Mass);
        Vector3 newVelocity = Velocity;

        // damp out abrupt changes and oscillations in steering acceleration
        // (rate is proportional to time step, then clipped into useful range)
        if (elapsedTime > 0)
        {
            float smoothRate = Utilities.Clamp(9 * elapsedTime, 0.15f, 0.4f);
            Utilities.BlendIntoAccumulator(smoothRate, newAcceleration, ref acceleration);
        }

        // Euler integrate (per frame) acceleration into velocity
        newVelocity += acceleration * elapsedTime;

        // enforce speed limit
        newVelocity = newVelocity.TruncateLength(MaxSpeed);

        // update Speed
        Speed = (newVelocity.Length());

        // Euler integrate (per frame) velocity into position
        Position = (Position + (newVelocity * elapsedTime));

        // regenerate local space (by default: align vehicle's forward axis with
        // new velocity, but this behavior may be overridden by derived classes.)
        RegenerateLocalSpace(newVelocity, elapsedTime);

        // maintain path curvature information
        MeasurePathCurvature(elapsedTime);

        // running average of recent positions
        Utilities.BlendIntoAccumulator(elapsedTime * 0.06f, // QQQ
            Position,
            ref smoothedPosition);
    }

    // the default version: keep FORWARD parallel to velocity, change
    // UP as little as possible.
    protected virtual void RegenerateLocalSpace(Vector3 newVelocity, float elapsedTime)
    {
        // adjust orthonormal basis vectors to be aligned with new velocity
        if (Speed > 0)
        {
            RegenerateOrthonormalBasisUF(newVelocity / Speed);
        }
    }

    // alternate version: keep FORWARD parallel to velocity, adjust UP
    // according to a no-basis-in-reality "banking" behavior, something
    // like what birds and airplanes do.  (XXX experimental cwr 6-5-03)
    protected void RegenerateLocalSpaceForBanking(Vector3 newVelocity, float elapsedTime)
    {
        // the length of this global-upward-pointing vector controls the vehicle's
        // tendency to right itself as it is rolled over from turning acceleration
        Vector3 globalUp = new Vector3(0, 0.2f, 0);

        // acceleration points toward the center of local path curvature, the
        // length determines how much the vehicle will roll while turning
        Vector3 accelUp = acceleration * 0.05f;

        // combined banking, sum of UP due to turning and global UP
        Vector3 bankUp = accelUp + globalUp;

        // blend bankUp into vehicle's UP basis vector
        float smoothRate = elapsedTime * 3;
        Vector3 tempUp = Up;
        Utilities.BlendIntoAccumulator(smoothRate, bankUp, ref tempUp);
        Up = Vector3.Normalize(tempUp);

        Annotation.Line(Position, Position + (globalUp * 4), Colors.White);
        Annotation.Line(Position, Position + (bankUp * 4), Colors.Orange);
        Annotation.Line(Position, Position + (accelUp * 4), Colors.Red);
        Annotation.Line(Position, Position + (Up * 1), Colors.Gold);

        // adjust orthonormal basis vectors to be aligned with new velocity
        if (Speed > 0) RegenerateOrthonormalBasisUF(newVelocity / Speed);
    }

    /// <summary>
    /// adjust the steering force passed to applySteeringForce.
    /// allows a specific vehicle class to redefine this adjustment.
    /// default is to disallow backward-facing steering at low speed.
    /// </summary>
    /// <param name="force"></param>
    /// <param name="deltaTime"></param>
    /// <returns></returns>
    protected virtual Vector3 AdjustRawSteeringForce(Vector3 force, float deltaTime)
    {
        float maxAdjustedSpeed = 0.2f * MaxSpeed;

        if ((Speed > maxAdjustedSpeed) || (force == Vector3.Zero))
            return force;

        float range = Speed / maxAdjustedSpeed;
        float cosine = Utilities.Lerp(1.0f, -1.0f, (float)Math.Pow(range, 20));
        return force.LimitMaxDeviationAngle(cosine, Forward);
    }

    /// <summary>
    /// apply a given braking force (for a given dt) to our momentum.
    /// </summary>
    /// <param name="rate"></param>
    /// <param name="deltaTime"></param>
    public void ApplyBrakingForce(float rate, float deltaTime)
    {
        float rawBraking = Speed * rate;
        float clipBraking = ((rawBraking < MaxForce) ? rawBraking : MaxForce);
        Speed = (Speed - (clipBraking * deltaTime));
    }

    /// <summary>
    /// predict position of this vehicle at some time in the future (assumes velocity remains constant)
    /// </summary>
    /// <param name="predictionTime"></param>
    /// <returns></returns>
    public override Vector3 PredictFuturePosition(float predictionTime) => Position + (Velocity * predictionTime);

    // get instantaneous curvature (since last update)
    protected float Curvature { get; private set; }

    // get/reset smoothedCurvature, smoothedAcceleration and smoothedPosition
    public float SmoothedCurvature => smoothedCurvature;

    void ResetSmoothedCurvature(float value = 0)
    {
        lastForward = Vector3.Zero;
        lastPosition = Vector3.Zero;
        smoothedCurvature = value;
        Curvature = value;
    }

    public override Vector3 Acceleration => acceleration;

    protected void ResetAcceleration() => ResetAcceleration(Vector3.Zero);

    void ResetAcceleration(Vector3 value) => acceleration = value;

    Vector3 smoothedPosition;
    public Vector3 SmoothedPosition => smoothedPosition;

    void ResetSmoothedPosition() => ResetSmoothedPosition(Vector3.Zero);

    protected void ResetSmoothedPosition(Vector3 value) => smoothedPosition = value;

    // set a random "2D" heading: set local Up to global Y, then effectively
    // rotate about it by a random angle (pick random forward, derive side).
    protected void RandomizeHeadingOnXZPlane()
    {
        Up = Vector3.UnitY;
        Forward = Vector3Helpers.RandomUnitVectorOnXZPlane();
        Side = Vector3.Cross(Forward, Up);
    }

    // measure path curvature (1/turning-radius), maintain smoothed version
    void MeasurePathCurvature(float elapsedTime)
    {
        if (elapsedTime > 0)
        {
            Vector3 dP = lastPosition - Position;
            Vector3 dF = (lastForward - Forward) / dP.Length();
            Vector3 lateral = Vector3Helpers.PerpendicularComponent(dF, Forward);
            float sign = (Vector3.Dot(lateral, Side) < 0) ? 1.0f : -1.0f;
            Curvature = lateral.Length() * sign;
            Utilities.BlendIntoAccumulator(elapsedTime * 4.0f, Curvature, ref smoothedCurvature);
            lastForward = Forward;
            lastPosition = Position;
        }
    }
}