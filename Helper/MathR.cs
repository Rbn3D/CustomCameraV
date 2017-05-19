using System;
using GTA.Math;
using CustomCameraVScript;

namespace CustomCameraVScript
{
    public static class MathR
    {
        public static float Magnitude(this Vector3 vector)
        {
            return (vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }

        public static Vector3 ToEulerAngles(this Quaternion qt)
        {
            var vector = new Vector3();

            double sqw = qt.W * qt.W;
            double sqx = qt.X * qt.X;
            double sqy = qt.Y * qt.Y;
            double sqz = qt.Z * qt.Z;

            vector.X = (float)Math.Asin(2f * (qt.X * qt.Z - qt.W * qt.Y));                             // Pitch 
            vector.Y = (float)Math.Atan2(2f * qt.X * qt.W + 2f * qt.Y * qt.Z, 1 - 2f * (sqz + sqw));     // Yaw 
            vector.Z = (float)Math.Atan2(2f * qt.X * qt.Y + 2f * qt.Z * qt.W, 1 - 2f * (sqy + sqz)); // Roll

            return vector;
        }

        public static Quaternion QuaternionFromEuler(float X, float Y, float Z)
        {
            float rollOver2 = Z * 0.5f;
            float sinRollOver2 = (float)Math.Sin((double)rollOver2);
            float cosRollOver2 = (float)Math.Cos((double)rollOver2);
            float pitchOver2 = X * 0.5f;
            float sinPitchOver2 = (float)Math.Sin((double)pitchOver2);
            float cosPitchOver2 = (float)Math.Cos((double)pitchOver2);
            float yawOver2 = Y * 0.5f;
            float sinYawOver2 = (float)Math.Sin((double)yawOver2);
            float cosYawOver2 = (float)Math.Cos((double)yawOver2);
            Quaternion result;
            result.X = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.Y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;
            result.Z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.W = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            return result;
        }

        public static Quaternion QuaternionFromEuler(this Vector3 vec)
        {
            return QuaternionFromEuler(vec.X, vec.Y, vec.Z);
        }

        public static Quaternion QuaternionLookRotation(Vector3 forward, Vector3 up)
        {
            forward.Normalize();

            Vector3 vector = Vector3.Normalize(forward);
            Vector3 vector2 = Vector3.Normalize(Vector3.Cross(up, vector));
            Vector3 vector3 = Vector3.Cross(vector, vector2);
            var m00 = vector2.X;
            var m01 = vector2.Y;
            var m02 = vector2.Z;
            var m10 = vector3.X;
            var m11 = vector3.Y;
            var m12 = vector3.Z;
            var m20 = vector.X;
            var m21 = vector.Y;
            var m22 = vector.Z;


            float num8 = (m00 + m11) + m22;
            var quaternion = new Quaternion();
            if (num8 > 0f)
            {
                var num = (float)Math.Sqrt(num8 + 1f);
                quaternion.W = num * 0.5f;
                num = 0.5f / num;
                quaternion.X = (m12 - m21) * num;
                quaternion.Y = (m20 - m02) * num;
                quaternion.Z = (m01 - m10) * num;
                return quaternion;
            }
            if ((m00 >= m11) && (m00 >= m22))
            {
                var num7 = (float)Math.Sqrt(((1f + m00) - m11) - m22);
                var num4 = 0.5f / num7;
                quaternion.X = 0.5f * num7;
                quaternion.Y = (m01 + m10) * num4;
                quaternion.Z = (m02 + m20) * num4;
                quaternion.W = (m12 - m21) * num4;
                return quaternion;
            }
            if (m11 > m22)
            {
                var num6 = (float)Math.Sqrt(((1f + m11) - m00) - m22);
                var num3 = 0.5f / num6;
                quaternion.X = (m10 + m01) * num3;
                quaternion.Y = 0.5f * num6;
                quaternion.Z = (m21 + m12) * num3;
                quaternion.W = (m20 - m02) * num3;
                return quaternion;
            }
            var num5 = (float)Math.Sqrt(((1f + m22) - m00) - m11);
            var num2 = 0.5f / num5;
            quaternion.X = (m20 + m02) * num2;
            quaternion.Y = (m21 + m12) * num2;
            quaternion.Z = 0.5f * num5;
            quaternion.W = (m01 - m10) * num2;
            return quaternion;
        }

        public static Quaternion QuaternionLookRotation(Vector3 forward)
        {
            Vector3 up = Vector3.WorldUp;

            return QuaternionLookRotation(forward, up);
        }

        // Cheaper than Slerp
        public static Quaternion QuaternionNLerp(Quaternion start, Quaternion end, float ammount)
        {
            return QuaternionFromEuler(Vector3.Lerp(start.ToEulerAngles(), end.ToEulerAngles(), MathR.Clamp01(ammount)).Normalized);
        }

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = (float)(-2.0 * (double)t * (double)t * (double)t + 3.0 * (double)t * (double)t);
            return (float)((double)to * (double)t + (double)from * (1.0 - (double)t));
        }

        public static float Clamp01(float value)
        {
            if ((double)value < 0.0)
                return 0.0f;
            if ((double)value > 1.0)
                return 1f;
            return value;
        }

        public static float Lerp(float value1, float value2, float ammount)
        {
            return value1 + (value2 - value1) * MathR.Clamp01(ammount);
        }

        public static float InverseLerp(float from, float to, float value)
        {
            if (from < to)
            {
                if (value < from)
                    return 0.0f;
                else if (value > to)
                    return 1.0f;
            }
            else
            {
                if (value < to)
                    return 1.0f;
                else if (value > from)
                    return 0.0f;
            }
            return (value - from) / (to - from);
        }

        public static int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public static Vector3 Vector3SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num1 = 2f / smoothTime;
            float num2 = num1 * deltaTime;
            float num3 = (float)(1.0 / (1.0 + (double)num2 + 0.479999989271164 * (double)num2 * (double)num2 + 0.234999999403954 * (double)num2 * (double)num2 * (double)num2));
            Vector3 vector = current - target;
            Vector3 vector3_1 = target;
            float maxLength = maxSpeed * smoothTime;
            Vector3 vector3_2 = Vector3ClampMagnitude(vector, maxLength);
            target = current - vector3_2;
            Vector3 vector3_3 = (currentVelocity + num1 * vector3_2) * deltaTime;
            currentVelocity = (currentVelocity - num1 * vector3_3) * num3;
            Vector3 vector3_4 = target + (vector3_2 + vector3_3) * num3;
            if ((double)Vector3.Dot(vector3_1 - current, vector3_4 - vector3_1) > 0.0)
            {
                vector3_4 = vector3_1;
                currentVelocity = (vector3_4 - vector3_1) / deltaTime;
            }
            return vector3_4;
        }

        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num1 = 2f / smoothTime;
            float num2 = num1 * deltaTime;
            float num3 = (float)(1.0 / (1.0 + (double)num2 + 0.479999989271164 * (double)num2 * (double)num2 + 0.234999999403954 * (double)num2 * (double)num2 * (double)num2));
            float num4 = current - target;
            float num5 = target;
            float max = maxSpeed * smoothTime;
            float num6 = Clamp(num4, -max, max);
            target = current - num6;
            float num7 = (currentVelocity + num1 * num6) * deltaTime;
            currentVelocity = (currentVelocity - num1 * num7) * num3;
            float num8 = target + (num6 + num7) * num3;
            if ((double)num5 - (double)current > 0.0 == (double)num8 > (double)num5)
            {
                num8 = num5;
                currentVelocity = (num8 - num5) / deltaTime;
            }
            return num8;
        }

        public static float Max(float a, float b)
        {
            if ((double)a > (double)b)
                return a;
            return b;
        }

        public static float Min(float a, float b)
        {
            if ((double)a < (double)b)
                return a;
            return b;
        }

        public static Vector3 Vector3ClampMagnitude(Vector3 vector, float maxLength)
        {
            if ((double)Vector3SqrMagnitude(vector) > (double)maxLength * (double)maxLength)
                return vector.Normalized * maxLength;
            return vector;
        }

        public static float Vector3SqrMagnitude(Vector3 a)
        {
            return (float)((double)a.X * (double)a.X + (double)a.Y * (double)a.Y + (double)a.Z * (double)a.Z);
        }

        /// <summary>
        /// Evaluates a rotation needed to be applied to an object positioned at sourcePoint to face destPoint
        /// </summary>
        /// <param name="sourcePoint">Coordinates of source point</param>
        /// <param name="destPoint">Coordinates of destionation point</param>
        /// <returns></returns>
        public static Quaternion LookAt(Vector3 sourcePoint, Vector3 destPoint)
        {
            Vector3 forwardVector = Vector3.Normalize(destPoint - sourcePoint);

            float dot = Vector3.Dot(Vector3.RelativeFront, forwardVector);

            if (Math.Abs(dot - (-1.0f)) < 0.000001f)
            {
                return new Quaternion(Vector3.WorldUp.X, Vector3.WorldUp.Y, Vector3.WorldUp.Z, 3.1415926535897932f);
            }
            if (Math.Abs(dot - (1.0f)) < 0.000001f)
            {
                return Quaternion.Identity;
            }

            float rotAngle = (float)Math.Acos(dot);
            Vector3 rotAxis = Vector3.Cross(Vector3.RelativeFront, forwardVector);
            rotAxis = Vector3.Normalize(rotAxis);
            return CreateFromAxisAngle(rotAxis, rotAngle);
        }

        // just in case you need that function also
        public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
        {
            float halfAngle = angle * .5f;
            float s = (float)System.Math.Sin(halfAngle);
            Quaternion q = Quaternion.Identity;
            q.X = axis.X * s;
            q.Y = axis.Y * s;
            q.Z = axis.Z * s;
            q.W = (float)System.Math.Cos(halfAngle);
            return q;
        }
    }

}
