using System;
using System.Numerics;
using SharpSteer2.Helpers;

namespace SharpSteer2.Demo;

public class SimpleFlowField
    : IFlowField
{
    readonly Vector3 center;

    readonly Vector3[,,] field;

    public SimpleFlowField(int x, int y, int z, Vector3 center)
    {
        this.center = center;
        field = new Vector3[x, y, z];
    }

    public Vector3 Sample(Vector3 location)
    {
        var sampleLocation = location + center;
        var sample = field[
            (int)Utilities.Clamp(sampleLocation.X, 0, field.GetLength(0) - 1),
            (int)Utilities.Clamp(sampleLocation.Y, 0, field.GetLength(1) - 1),
            (int)Utilities.Clamp(sampleLocation.Z, 0, field.GetLength(2) - 1)
        ];

        return sample;
    }

    public void Func(Func<Vector3, Vector3> func, float weight)
    {
        for (var i = 0; i < field.GetLength(0); i++)
        {
            for (var j = 0; j < field.GetLength(1); j++)
            {
                for (var k = 0; k < field.GetLength(2); k++)
                {
                    var pos = new Vector3(i, j, k) - center;
                    field[i, j, k] = Vector3.Lerp(field[i, j, k], func(pos), weight);
                }
            }
        }
    }

    public void Randomize(float weight) => Func(_ => Vector3Helpers.RandomUnitVector(), weight);

    public void ClampXz()
    {
        for (var i = 0; i < field.GetLength(0); i++)
        {
            for (var j = 0; j < field.GetLength(1); j++)
            {
                for (var k = 0; k < field.GetLength(2); k++)
                {
                    field[i, j, k] = new(field[i, j, k].X, 0, field[i, j, k].Z);
                }
            }
        }
    }

    public void Normalize()
    {
        for (var i = 0; i < field.GetLength(0); i++)
        {
            for (var j = 0; j < field.GetLength(1); j++)
            {
                for (var k = 0; k < field.GetLength(2); k++)
                {
                    field[i, j, k] = Vector3.Normalize(field[i, j, k]);
                }
            }
        }
    }

    public void Clean()
    {
        for (var i = 0; i < field.GetLength(0); i++)
        {
            for (var j = 0; j < field.GetLength(1); j++)
            {
                for (var k = 0; k < field.GetLength(2); k++)
                {
                    var v = field[i, j, k];
                    if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z))
                        field[i, j, k] = Vector3.Zero;
                }
            }
        }
    }
}