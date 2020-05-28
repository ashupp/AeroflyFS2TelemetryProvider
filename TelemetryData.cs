using Math = System.Math;

namespace SimFeedback.telemetry
{
    public struct TelemetryData
    {
        private float pitch;
        private float roll;
        private float yaw;
        private float sway;
        private float surge;
        private float heave;
        public bool isSet;

        #region For SimFeedback Available Values

        public float Pitch
        {
            get => LoopAngle(ConvertRadiansToDegrees(pitch / 1000),90);
            set => pitch = value;
        }

        public float Yaw
        {
            get => ConvertRadiansToDegrees(yaw / 1000);
            set => yaw = value;
        }

        public float Roll
        {
            get => LoopAngle(ConvertRadiansToDegrees(roll / 1000), 90);
            set => roll = value;
        }

        
        public float Heave
        {
            get => (heave / 1000) *-1 ;
            set => heave = value;
        }

        public float Sway
        {
            get => (sway / 1000);
            set => sway = value;
        }

        public float Surge
        {
            get => (surge);
            set => surge = value;
        }

        public float AirSpeed { get; set; }
        public float GroundSpeed { get; set; }

        public bool isEmptyTelemetry()
        {
            if (Surge == 0 && Heave == 0 & Roll == 0 && Yaw == 0 && Pitch == 0 && AirSpeed == 0 && GroundSpeed == 0 && Sway == 0 && Surge == 0)
            {
                return true;
            }
            return false;
        }

        #endregion

        #region Conversion calculations
        private static float ConvertRadiansToDegrees(float radians)
        {
            var degrees = (float)(180 / Math.PI) * radians;
            return degrees;
        }

        private static float ConvertAccel(float accel)
        {
            return (float) (accel / 9.80665);
        }

        private float LoopAngle(float angle, float minMag)
        {

            float absAngle = Math.Abs(angle);

            if (absAngle <= minMag)
            {
                return angle;
            }

            float direction = angle / absAngle;

            //(180.0f * 1) - 135 = 45
            //(180.0f *-1) - -135 = -45
            float loopedAngle = (180.0f * direction) - angle;

            return loopedAngle;
        }

        #endregion
    }
}