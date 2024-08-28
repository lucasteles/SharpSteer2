using System.Numerics;
using System.Runtime.CompilerServices;

namespace SharpSteer2.Demo;

static class Extensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 FromXna(this Microsoft.Xna.Framework.Vector3 v) => new(v.X, v.Y, v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microsoft.Xna.Framework.Vector3 ToXna(this Vector3 v) => new(v.X, v.Y, v.Z);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 FromXna(this Microsoft.Xna.Framework.Vector2 v) => new(v.X, v.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Microsoft.Xna.Framework.Vector2 ToXna(this Vector2 v) => new(v.X, v.Y);
}