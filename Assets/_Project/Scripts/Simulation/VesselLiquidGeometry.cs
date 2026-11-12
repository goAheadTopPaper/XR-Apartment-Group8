using System;

namespace XRApartment.Simulation
{
    /// Convex cavity formed by linearly interpolating concentric elliptical sections.
    public sealed class VesselLiquidGeometry
    {
        [Serializable]
        public struct Section
        {
            public float heightFraction;
            public float radiusFraction;

            public Section(float heightFraction, float radiusFraction)
            {
                this.heightFraction = heightFraction;
                this.radiusFraction = radiusFraction;
            }
        }

        public const int MaximumSections = 8;
        static readonly Section[] Cylinder = { new Section(0f, 1f), new Section(1f, 1f) };
        static readonly double[] QuadraturePoints = { .04691008, .23076534, .5, .76923466, .95308992 };
        static readonly double[] QuadratureWeights = { .11846344, .23931434, .28444444, .23931434, .11846344 };
        readonly Section[] sections;
        readonly double unitVolume;

        public float RadiusX { get; }
        public float RadiusZ { get; }
        public float Height { get; }
        public float CapacityMillilitres { get; }
        public int SectionCount => sections.Length;
        public Section SectionAt(int index) => sections[index];

        public VesselLiquidGeometry(float radiusX, float radiusZ, float height, Section[] profile = null)
        {
            if (!(radiusX > 0f && radiusZ > 0f && height > 0f))
                throw new ArgumentOutOfRangeException(nameof(height), "A liquid cavity needs positive dimensions.");
            RadiusX = radiusX;
            RadiusZ = radiusZ;
            Height = height;
            sections = profile == null || profile.Length == 0 ? Cylinder : (Section[])profile.Clone();
            if (sections.Length < 2 || sections.Length > MaximumSections || sections[0].heightFraction != 0f ||
                sections[sections.Length - 1].heightFraction != 1f || sections[sections.Length - 1].radiusFraction != 1f)
                throw new ArgumentException("A cavity profile must span its bottom to its opening.", nameof(profile));

            double previousSlope = double.PositiveInfinity;
            for (var i = 1; i < sections.Length; i++)
            {
                var a = sections[i - 1];
                var b = sections[i];
                double dy = b.heightFraction - a.heightFraction;
                double slope = (b.radiusFraction - a.radiusFraction) / dy;
                if (!(dy > 0 && a.radiusFraction >= 0f && slope >= 0 && slope <= previousSlope + .0001))
                    throw new ArgumentException("Cavity sections must widen in height order and form a convex interior.", nameof(profile));
                previousSlope = slope;
                unitVolume += Frustum(dy, a.radiusFraction, b.radiusFraction);
            }
            CapacityMillilitres = (float)(Math.PI * RadiusX * RadiusZ * Height * unitVolume * 1e6);
        }

        public bool ContainsMouth(float x, float z)
        {
            var u = x / RadiusX;
            var v = z / RadiusZ;
            return u * u + v * v < 1f;
        }

        double HorizontalSpan(float nx, float nz)
        {
            double x = nx * RadiusX;
            double z = nz * RadiusZ;
            return Math.Sqrt(x * x + z * z);
        }

        public float RetainedFraction(float nx, float ny, float nz)
        {
            if (ny <= 0f) return 0f;
            var span = HorizontalSpan(nx, nz);
            return (float)FractionBelowPlane(span, ny, ny * Height - span);
        }

        public float SurfaceOffset(float nx, float ny, float nz, float fraction)
        {
            var span = HorizontalSpan(nx, nz);
            double low = double.PositiveInfinity;
            double high = double.NegativeInfinity;
            foreach (var section in sections)
            {
                double center = ny * Height * section.heightFraction;
                double reach = span * section.radiusFraction;
                low = Math.Min(low, center - reach);
                high = Math.Max(high, center + reach);
            }
            if (fraction <= 0f) return (float)low;
            if (fraction >= 1f) return (float)high;
            for (var i = 0; i < 14; i++)
            {
                double middle = (low + high) * .5;
                if (FractionBelowPlane(span, ny, middle) < fraction) low = middle;
                else high = middle;
            }
            return (float)((low + high) * .5);
        }

        double FractionBelowPlane(double span, float ny, double offset)
        {
            if (ReferenceEquals(sections, Cylinder))
                return CylindricalLiquid.FractionBelowPlane(RadiusX, Height, (float)(span / RadiusX), ny, 0f, (float)offset);

            double sum = 0;
            for (var i = 1; i < sections.Length; i++)
            {
                var a = sections[i - 1];
                var b = sections[i];
                double length = b.heightFraction - a.heightFraction;
                double radiusChange = b.radiusFraction - a.radiusFraction;
                double distance = offset - ny * Height * a.heightFraction;
                double distanceChange = -ny * Height * length;
                // Split at tangency with each side so the integrator sees a smooth partial slice.
                double full = Root(distance - span * a.radiusFraction, distanceChange - span * radiusChange);
                double empty = Root(distance + span * a.radiusFraction, distanceChange + span * radiusChange);
                double first = Math.Min(full, empty);
                double last = Math.Max(full, empty);
                sum += length * (Integrate(0, first, a.radiusFraction, radiusChange, distance, distanceChange, span) +
                                 Integrate(first, last, a.radiusFraction, radiusChange, distance, distanceChange, span) +
                                 Integrate(last, 1, a.radiusFraction, radiusChange, distance, distanceChange, span));
            }
            return sum / unitVolume;
        }

        static double Root(double value, double change) => change == 0 ? 0 : Math.Max(0, Math.Min(1, -value / change));

        static double Frustum(double length, double a, double b) => length * (a * a + a * b + b * b) / 3;

        static double Integrate(double start, double end, double radius, double radiusChange,
            double distance, double distanceChange, double span)
        {
            var length = end - start;
            if (length <= 0) return 0;
            var middle = (start + end) * .5;
            var reach = span * (radius + radiusChange * middle);
            var cutoff = distance + distanceChange * middle;
            if (cutoff >= reach) return Frustum(length, radius + radiusChange * start, radius + radiusChange * end);
            if (cutoff <= -reach) return 0;
            double sum = 0;
            for (var i = 0; i < QuadraturePoints.Length; i++)
            {
                var t = start + length * QuadraturePoints[i];
                var r = radius + radiusChange * t;
                var u = Math.Max(-1, Math.Min(1, (distance + distanceChange * t) / (span * r)));
                var areaFraction = (Math.Acos(-u) + u * Math.Sqrt(1 - u * u)) / Math.PI;
                sum += QuadratureWeights[i] * r * r * areaFraction;
            }
            return sum * length;
        }
    }
}
