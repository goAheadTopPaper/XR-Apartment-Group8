using System;

namespace XRApartment.Simulation
{
    /// Hydrostatic approximation for an open circular vessel; local bottom is y=0.
    /// Equal-area disk quadrature keeps the result deterministic and device independent.
    public static class CylindricalLiquid
    {
        const int Rings = 4;
        const int Segments = 24;
        static readonly double[] X = new double[Rings * Segments];
        static readonly double[] Z = new double[Rings * Segments];

        static CylindricalLiquid()
        {
            for (int ring = 0; ring < Rings; ring++)
            for (int segment = 0; segment < Segments; segment++)
            {
                int i = ring * Segments + segment;
                double angle = 2 * Math.PI * (segment + 0.5 * (ring % 2)) / Segments;
                double r = Math.Sqrt((ring + 0.5) / Rings);
                X[i] = r * Math.Cos(angle);
                Z[i] = r * Math.Sin(angle);
            }
        }

        public static float FractionBelowPlane(float radius, float height, float nx, float ny, float nz, float offset)
        {
            if (radius <= 0f || height <= 0f) return 0f;
            double sum = 0;
            for (int i = 0; i < X.Length; i++)
            {
                double horizontal = radius * (nx * X[i] + nz * Z[i]);
                if (Math.Abs(ny) < 0.000001)
                    sum += horizontal <= offset ? 1 : 0;
                else
                {
                    double fraction = Math.Max(0, Math.Min(1, (offset - horizontal) / (ny * height)));
                    sum += ny > 0 ? fraction : 1 - fraction;
                }
            }
            return (float)(sum / X.Length);
        }

        public static float RetainedFraction(float radius, float height, float nx, float ny, float nz)
        {
            if (ny <= 0f) return 0f; // The open mouth faces down; no stable retained volume.
            float lowestRim = ny * height - radius * (float)Math.Sqrt(nx * nx + nz * nz);
            return FractionBelowPlane(radius, height, nx, ny, nz, lowestRim);
        }

        public static float SurfaceOffset(float radius, float height, float nx, float ny, float nz, float fraction)
        {
            float span = radius * (float)Math.Sqrt(nx * nx + nz * nz);
            float low = Math.Min(0, ny * height) - span;
            float high = Math.Max(0, ny * height) + span;
            if (fraction <= 0f) return low;
            if (fraction >= 1f) return high;
            for (int i = 0; i < 14; i++)
            {
                float middle = (low + high) * 0.5f;
                if (FractionBelowPlane(radius, height, nx, ny, nz, middle) < fraction) low = middle;
                else high = middle;
            }
            return (low + high) * 0.5f;
        }
    }
}
