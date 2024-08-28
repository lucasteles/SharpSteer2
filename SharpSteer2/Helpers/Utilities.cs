// Copyright (c) 2002-2003, Sony Computer Entertainment America
// Copyright (c) 2002-2003, Craig Reynolds <craig_reynolds@playstation.sony.com>
// Copyright (C) 2007 Bjoern Graf <bjoern.graf@gmx.net>
// Copyright (C) 2007 Michael Coles <michael@digini.com>
// All rights reserved.
//
// This software is licensed as described in the file license.txt, which
// you should have received as part of this distribution. The terms
// are also available at http://www.codeplex.com/SharpSteer/Project/License.aspx.

namespace SharpSteer2.Helpers;

public class Utilities
{
    /// <summary>
    ///     Linearly interpolate from A to B by amount T
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    public static float Lerp(float a, float b, float t) => a + ((b - a) * t);

    /// <summary>
    ///     Clamp value between min and max
    /// </summary>
    /// <param name="value"></param>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static float Clamp(float value, float min, float max)
    {
        if (value < min)
            return min;
        if (value > max)
            return max;
        return value;
    }

    /// <summary>
    ///     remap a value specified relative to a pair of bounding values to the corresponding value relative to another pair
    ///     of bounds.
    /// </summary>
    /// <remarks>Inspired by (dyna:remap-interval y y0 y1 z0 z1)</remarks>
    /// <param name="x">A value</param>
    /// <param name="in0">Starting lower bound</param>
    /// <param name="in1">Starting upper bound</param>
    /// <param name="out0">Ending lower bound</param>
    /// <param name="out1">Ending upper bound</param>
    /// <returns></returns>
    public static float RemapInterval(float x, float in0, float in1, float out0, float out1)
    {
        // uninterpolate: what is x relative to the interval in0:in1?
        var relative = (x - in0) / (in1 - in0);

        // now interpolate between output interval based on relative x
        return Lerp(out0, out1, relative);
    }

    /// <summary>
    ///     Like remapInterval but the result is clipped to remain between out0 and out1
    /// </summary>
    /// <param name="x">A value</param>
    /// <param name="in0">Starting lower bound</param>
    /// <param name="in1">Starting upper bound</param>
    /// <param name="out0">Ending lower bound</param>
    /// <param name="out1">Ending upper bound</param>
    /// <returns></returns>
    public static float RemapIntervalClip(float x, float in0, float in1, float out0, float out1)
    {
        // uninterpolate: what is x relative to the interval in0:in1?
        var relative = (x - in0) / (in1 - in0);

        // now interpolate between output interval based on relative x
        return Lerp(out0, out1, Clamp(relative, 0, 1));
    }

    /// <summary>
    ///     classify a value relative to the interval between two bounds:
    ///     returns -1 when below the lower bound, returns  0 when between the bounds (inside the interval), returns +1 when
    ///     above the upper bound
    /// </summary>
    /// <param name="x"></param>
    /// <param name="lowerBound"></param>
    /// <param name="upperBound"></param>
    /// <returns></returns>
    public static int IntervalComparison(float x, float lowerBound, float upperBound)
    {
        if (x < lowerBound) return -1;
        if (x > upperBound) return +1;
        return 0;
    }

    public static float ScalarRandomWalk(float initial, float walkspeed, float min, float max)
    {
        var next = initial + (((RandomHelpers.Random() * 2) - 1) * walkspeed);
        if (next < min) return min;
        if (next > max) return max;
        return next;
    }

    /// <summary>
    ///     blends new values into an accumulator to produce a smoothed time series
    /// </summary>
    /// <remarks>
    ///     Modifies its third argument, a reference to the float accumulator holding
    ///     the "smoothed time series."
    ///     The first argument (smoothRate) is typically made proportional to "dt" the
    ///     simulation time step.  If smoothRate is 0 the accumulator will not change,
    ///     if smoothRate is 1 the accumulator will be set to the new value with no
    ///     smoothing.  Useful values are "near zero".
    /// </remarks>
    /// <param name="smoothRate"></param>
    /// <param name="newValue"></param>
    /// <param name="smoothedAccumulator"></param>
    /// <example>blendIntoAccumulator (dt * 0.4f, currentFPS, smoothedFPS)</example>
    public static void BlendIntoAccumulator(float smoothRate, float newValue, ref float smoothedAccumulator) =>
        smoothedAccumulator = Lerp(smoothedAccumulator, newValue, Clamp(smoothRate, 0, 1));

    public static void BlendIntoAccumulator(float smoothRate, Vector3 newValue, ref Vector3 smoothedAccumulator) =>
        smoothedAccumulator = Vector3.Lerp(smoothedAccumulator, newValue, Clamp(smoothRate, 0, 1));
}
