namespace XRApartment.Simulation
{
    /// Pure progress/easing math behind the kinematic door and drawer motions (FR-04).
    /// Kept engine-free so EditMode tests can exercise it without a device (EVAL-02) and so
    /// the motion stays reproducible: same inputs, same pose, no physics involved (R04).
    public static class MotionKinematics
    {
        public const double Min01 = 0.0;
        public const double Max01 = 1.0;

        public static double Clamp01(double value)
        {
            if (value < Min01) return Min01;
            if (value > Max01) return Max01;
            return value;
        }

        /// Smoothstep ease-in-out over a 0..1 progress value.
        public static double SmoothStep(double t01)
        {
            t01 = Clamp01(t01);
            return t01 * t01 * (3.0 - 2.0 * t01);
        }

        /// Moves a 0..1 progress value toward the target by at most delta01 per call.
        public static double StepToward(double current, double target, double delta01)
        {
            current = Clamp01(current);
            target = Clamp01(target);
            if (delta01 <= 0.0 || current == target) return current;
            return current < target
                ? System.Math.Min(target, current + delta01)
                : System.Math.Max(target, current - delta01);
        }

        /// Eased swing angle for a door: 0 = closed, openAngleDegrees = fully open,
        /// signed by hinge side so one formula serves left- and right-hung panels.
        public static double SwingAngle(double progress01, double openAngleDegrees, double swingSign)
        {
            return swingSign * openAngleDegrees * SmoothStep(progress01);
        }

        /// Eased slide distance for a drawer at the given progress.
        public static double SlideDistance(double progress01, double openDistanceMetres)
        {
            return openDistanceMetres * SmoothStep(progress01);
        }
    }
}
