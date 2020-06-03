using System.Runtime.InteropServices;
using Math = System.Math;

namespace SimFeedback.telemetry
{
    #region SharedMemoryData
    public struct FS2DataRaw
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 9)]
        public double[] FS2DataRawValues;
    }
    #endregion

    public struct TelemetryData
    {
        #region Private members
        private float _pitch;
        private float _roll;
        private float _yaw;
        #endregion

        #region For SimFeedback Available Values

        public float Pitch
        {
            get => LoopAngle(ConvertRadiansToDegrees(_pitch),90);
            set => _pitch = value;
        }

        public float Yaw
        {
            get => ConvertRadiansToDegrees(_yaw / 1000);
            set => _yaw = value;
        }

        public float Roll
        {
            get => LoopAngle(ConvertRadiansToDegrees(_roll), 90);
            set => _roll = value;
        }

        public float Heave { get; set; }
        public float Sway { get; set; }
        public float Surge { get; set; }
        public float AngularVelocityPitch { get; set; }
        public float AngularVelocityRoll { get; set; }
        public float AngularVelocityHeading { get; set; }

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