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
using SharpSteer2.Obstacles;
using SharpSteer2.Pathway;

namespace SharpSteer2;

public abstract class SteerLibrary : BaseVehicle
{
    protected IAnnotationService Annotation { get; private set; }

    // Constructor: initializes state
    protected SteerLibrary(IAnnotationService annotationService = null)
    {
        Annotation = annotationService ?? new NullAnnotationService();

        // set inital state
        Reset();
    }

    // reset state
    public virtual void Reset()
    {
        // initial state of wander behavior
        wanderSide = 0;
        wanderUp = 0;
    }

    #region steering behaviours

    float wanderSide;
    float wanderUp;
    protected Vector3 SteerForWander(float dt) => this.SteerForWander(dt, ref wanderSide, ref wanderUp);

    protected Vector3 SteerForFlee(Vector3 target) => this.SteerForFlee(target, MaxSpeed);

    protected Vector3 SteerForSeek(Vector3 target) => this.SteerForSeek(target, MaxSpeed);

    protected Vector3 SteerForArrival(Vector3 target, float slowingDistance) => this.SteerForArrival(target, MaxSpeed, slowingDistance, Annotation);

    protected Vector3 SteerToFollowFlowField(IFlowField field, float predictionTime) => this.SteerToFollowFlowField(field, MaxSpeed, predictionTime, Annotation);

    protected Vector3 SteerToFollowPath(bool direction, float predictionTime, IPathway path) => this.SteerToFollowPath(direction, predictionTime, path, MaxSpeed, Annotation);

    protected Vector3 SteerToStayOnPath(float predictionTime, IPathway path) => this.SteerToStayOnPath(predictionTime, path, MaxSpeed, Annotation);

    protected Vector3 SteerToAvoidObstacle(float minTimeToCollision, IObstacle obstacle) => this.SteerToAvoidObstacle(minTimeToCollision, obstacle, Annotation);

    protected Vector3 SteerToAvoidObstacles(float minTimeToCollision, IEnumerable<IObstacle> obstacles) => this.SteerToAvoidObstacles(minTimeToCollision, obstacles, Annotation);

    protected Vector3 SteerToAvoidNeighbors(float minTimeToCollision, IEnumerable<IVehicle> others) => this.SteerToAvoidNeighbors(minTimeToCollision, others, Annotation);

    protected Vector3 SteerToAvoidCloseNeighbors<TVehicle>(float minSeparationDistance, IEnumerable<TVehicle> others) where TVehicle : IVehicle => this.SteerToAvoidCloseNeighbors<TVehicle>(minSeparationDistance, others, Annotation);

    protected Vector3 SteerForSeparation(float maxDistance, float cosMaxAngle, IEnumerable<IVehicle> flock) => this.SteerForSeparation(maxDistance, cosMaxAngle, flock, Annotation);

    protected Vector3 SteerForAlignment(float maxDistance, float cosMaxAngle, IEnumerable<IVehicle> flock) => this.SteerForAlignment(maxDistance, cosMaxAngle, flock, Annotation);

    protected Vector3 SteerForCohesion(float maxDistance, float cosMaxAngle, IEnumerable<IVehicle> flock) => this.SteerForCohesion(maxDistance, cosMaxAngle, flock, Annotation);

    protected Vector3 SteerForPursuit(IVehicle quarry, float maxPredictionTime = float.MaxValue) => this.SteerForPursuit(quarry, maxPredictionTime, MaxSpeed, Annotation);

    protected Vector3 SteerForEvasion(IVehicle menace, float maxPredictionTime) => this.SteerForEvasion(menace, maxPredictionTime, MaxSpeed, Annotation);

    protected Vector3 SteerForTargetSpeed(float targetSpeed) => this.SteerForTargetSpeed(targetSpeed, MaxForce, Annotation);

    #endregion
}