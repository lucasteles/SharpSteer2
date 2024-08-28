using System;
using System.Numerics;
using SharpSteer2;
using SharpSteer2.Helpers;

namespace Demo
{
    public class SimpleFlowField
        : IFlowField
    {
        private readonly Vector3 _center;

        private readonly Vector3[,,] _field;

        public SimpleFlowField(int x, int y, int z, Vector3 center)
        {
            _center = center;
            _field = new Vector3[x, y, z];
        }

        public Vector3 Sample(Vector3 location)
        {
            var sampleLocation = location + _center;
            var sample = _field[
                (int)Utilities.Clamp(sampleLocation.X, 0, _field.GetLength(0) - 1),
                (int)Utilities.Clamp(sampleLocation.Y, 0, _field.GetLength(1) - 1),
                (int)Utilities.Clamp(sampleLocation.Z, 0, _field.GetLength(2) - 1)
            ];

            return sample;
        }

        public void Func(Func<Vector3, Vector3> func, float weight)
        {
            for (int i = 0; i < _field.GetLength(0); i++)
            {
                for (int j = 0; j < _field.GetLength(1); j++)
                {
                    for (int k = 0; k < _field.GetLength(2); k++)
                    {
                        var pos = new Vector3(i, j, k) - _center;
                        _field[i, j, k] = Vector3.Lerp(_field[i, j, k], func(pos), weight);
                    }
                }
            }
        }

        public void Randomize(float weight)
        {
            Func(_ => Vector3Helpers.RandomUnitVector(), weight);
        }

        public void ClampXZ()
        {
            for (int i = 0; i < _field.GetLength(0); i++)
            {
                for (int j = 0; j < _field.GetLength(1); j++)
                {
                    for (int k = 0; k < _field.GetLength(2); k++)
                    {
                        _field[i, j, k] = new Vector3(_field[i, j, k].X, 0, _field[i, j, k].Z);
                    }
                }
            }
        }

        public void Normalize()
        {
            for (int i = 0; i < _field.GetLength(0); i++)
            {
                for (int j = 0; j < _field.GetLength(1); j++)
                {
                    for (int k = 0; k < _field.GetLength(2); k++)
                    {
                        _field[i, j, k] = Vector3.Normalize(_field[i, j, k]);
                    }
                }
            }
        }

        public void Clean()
        {
            for (int i = 0; i < _field.GetLength(0); i++)
            {
                for (int j = 0; j < _field.GetLength(1); j++)
                {
                    for (int k = 0; k < _field.GetLength(2); k++)
                    {
                        var v = _field[i, j, k];
                        if (float.IsNaN(v.X) || float.IsNaN(v.Y) || float.IsNaN(v.Z))
                            _field[i, j, k] = Vector3.Zero;
                    }
                }
            }
        }
    }
}
