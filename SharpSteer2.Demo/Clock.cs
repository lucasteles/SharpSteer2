// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

using System.Diagnostics;
using SharpSteer2.Helpers;

namespace SharpSteer2.Demo;

public class Clock
{
    readonly Stopwatch stopwatch;

    // constructor
    public Clock()
    {
        // default is "real time, variable frame rate" and not paused
        FixedFrameRate = 0;
        PausedState = false;
        AnimationMode = false;
        VariableFrameRateMode = true;

        // real "wall clock" time since launch
        TotalRealTime = 0;

        // time simulation has run
        TotalSimulationTime = 0;

        // time spent paused
        TotalPausedTime = 0;

        // sum of (non-realtime driven) advances to simulation time
        TotalAdvanceTime = 0;

        // interval since last simulation time 
        ElapsedSimulationTime = 0;

        // interval since last clock update time 
        ElapsedRealTime = 0;

        // interval since last clock update,
        // exclusive of time spent waiting for frame boundary when targetFPS>0
        ElapsedNonWaitRealTime = 0;

        // "manually" advance clock by this amount on next update
        newAdvanceTime = 0;

        // "Calendar time" when this clock was first updated
        stopwatch = new();

        // clock keeps track of "smoothed" running average of recent frame rates.
        // When a fixed frame rate is used, a running average of "CPU load" is
        // kept (aka "non-wait time", the percentage of each frame time (time
        // step) that the CPU is busy).
        smoothedFps = 0;
        smoothedUsage = 0;
    }

    // update this clock, called exactly once per simulation step ("frame")
    public void Update()
    {
        // keep track of average frame rate and average usage percentage
        UpdateSmoothedRegisters();

        // wait for next frame time (when targetFPS>0)
        // XXX should this be at the end of the update function?
        FrameRateSync();

        // save previous real time to measure elapsed time
        var previousRealTime = TotalRealTime;

        // real "wall clock" time since this application was launched
        TotalRealTime = RealTimeSinceFirstClockUpdate();

        // time since last clock update
        ElapsedRealTime = TotalRealTime - previousRealTime;

        // accumulate paused time
        if (PausedState) TotalPausedTime += ElapsedRealTime;

        // save previous simulation time to measure elapsed time
        var previousSimulationTime = TotalSimulationTime;

        // update total simulation time
        if (AnimationMode)
        {
            // for "animation mode" use fixed frame time, ignore real time
            var frameDuration = 1.0f / FixedFrameRate;
            TotalSimulationTime += PausedState ? newAdvanceTime : frameDuration;
            if (!PausedState)
            {
                newAdvanceTime += frameDuration - ElapsedRealTime;
            }
        }
        else
        {
            // new simulation time is total run time minus time spent paused
            TotalSimulationTime = TotalRealTime + TotalAdvanceTime - TotalPausedTime;
        }


        // update total "manual advance" time
        TotalAdvanceTime += newAdvanceTime;

        // how much time has elapsed since the last simulation step?
        if (PausedState)
        {
            ElapsedSimulationTime = newAdvanceTime;
        }
        else
        {
            ElapsedSimulationTime = TotalSimulationTime - previousSimulationTime;
        }

        // reset advance amount
        newAdvanceTime = 0;
    }

    // returns the number of seconds of real time (represented as a float)
    // since the clock was first updated.
    public float RealTimeSinceFirstClockUpdate()
    {
        if (stopwatch.IsRunning == false)
        {
            stopwatch.Start();
        }
        return (float)stopwatch.Elapsed.TotalSeconds;
    }

    // "wait" until next frame time
    void FrameRateSync()
    {
        // when in real time fixed frame rate mode
        // (not animation mode and not variable frame rate mode)
        if (!AnimationMode && !VariableFrameRateMode)
        {
            // find next (real time) frame start time
            var targetStepSize = 1.0f / FixedFrameRate;
            var now = RealTimeSinceFirstClockUpdate();
            var lastFrameCount = (int)(now / targetStepSize);
            var nextFrameTime = (lastFrameCount + 1) * targetStepSize;

            // record usage ("busy time", "non-wait time") for OpenSteerDemo app
            ElapsedNonWaitRealTime = now - TotalRealTime;

            //FIXME: eek.
            // wait until next frame time
            do { } while (RealTimeSinceFirstClockUpdate() < nextFrameTime);
        }
    }


    // main clock modes: variable or fixed frame rate, real-time or animation
    // mode, running or paused.

    // run as fast as possible, simulation time is based on real time

    // fixed frame rate (ignored when in variable frame rate mode) in
    // real-time mode this is a "target", in animation mode it is absolute

    // used for offline, non-real-time applications

    // is simulation running or paused?

    public int FixedFrameRate { get; set; }

    public bool AnimationMode { get; set; }

    public bool VariableFrameRateMode { get; set; }

    public void TogglePausedState() => PausedState = !PausedState;

    public bool PausedState { get; private set; }

    // clock keeps track of "smoothed" running average of recent frame rates.
    // When a fixed frame rate is used, a running average of "CPU load" is
    // kept (aka "non-wait time", the percentage of each frame time (time
    // step) that the CPU is busy).
    float smoothedFps;
    float smoothedUsage;

    void UpdateSmoothedRegisters()
    {
        var rate = SmoothingRate;
        if (ElapsedRealTime > 0)
            Utilities.BlendIntoAccumulator(rate, 1 / ElapsedRealTime, ref smoothedFps);
        if (!VariableFrameRateMode)
            Utilities.BlendIntoAccumulator(rate, Usage, ref smoothedUsage);
    }

    public float SmoothedFps => smoothedFps;

    public float SmoothedUsage => smoothedUsage;

    public float SmoothingRate =>
        // ReSharper disable CompareOfFloatsByEqualityOperator
        smoothedFps == 0 ? 1 : ElapsedRealTime * 1.5f;
    // ReSharper restore CompareOfFloatsByEqualityOperator

    public float Usage =>
        // run time per frame over target frame time (as a percentage)
        100 * ElapsedNonWaitRealTime / (1.0f / FixedFrameRate);

    public float TotalRealTime { get; private set; }

    public float TotalSimulationTime { get; private set; }

    float TotalPausedTime { get; set; }

    float TotalAdvanceTime { get; set; }

    public float ElapsedSimulationTime { get; private set; }

    public float ElapsedRealTime { get; private set; }

    float ElapsedNonWaitRealTime { get; set; }

    // "manually" advance clock by this amount on next update
    float newAdvanceTime;
}