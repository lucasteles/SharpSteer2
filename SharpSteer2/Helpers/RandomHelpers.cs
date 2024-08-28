namespace SharpSteer2.Helpers;

public static class RandomHelpers
{
    static Random Rng => System.Random.Shared;

    /// <summary>
    ///     Returns a float randomly distributed between 0 and 1
    /// </summary>
    /// <returns></returns>
    public static float Random() => (float)Rng.NextDouble();

    /// <summary>
    ///     Returns a float randomly distributed between lowerBound and upperBound
    /// </summary>
    /// <param name="lowerBound"></param>
    /// <param name="upperBound"></param>
    /// <returns></returns>
    public static float Random(float lowerBound, float upperBound) =>
        lowerBound + (Random() * (upperBound - lowerBound));

    public static int RandomInt(int min, int max) => (int)Random(min, max);
}
