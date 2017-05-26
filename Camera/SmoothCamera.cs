using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCameraVScript;
using Glide;
using GTA;
using GTA.Math;

namespace CustomCameraVScript
{
    class SmoothCamera : CustomCamera
    {
        public Quaternion smoothQuat = Quaternion.Identity;

        public float camDistance = 5.8f;
        public float heightOffset = 1.75f;
        public float extraCamHeight = 0.1f;
        public float rotationSpeed = 4.75f;

        public bool useVariableRotSpeed = true;

        public float minRotSpeed = 2f;
        public float maxRotSpeed = 5f;

        public float maxRotSpeedAngle = 90f;
        public float currentRootSpeed = 2f;
        private Quaternion velQuat = Quaternion.Identity;

        public SmoothCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        {
            // one time initialization
        }

        public override string getCameraName()
        {
            return "Smooth";
        }

        public override void loadCameraSettings()
        {
            // no-op for now
        }

        // player in vehicle and using this cam (initialize)
        public override void setupCamera()
        {
            smoothQuat = veh.Quaternion;
    }

        public override void updateCamera()
        {
            var heightOffsetV3 = Vector3.WorldUp * heightOffset;
            var camExtraHeightV3 = Vector3.WorldUp * extraCamHeight;

            var posCenter = veh.Position + heightOffsetV3;

            float rotSpeed = rotationSpeed;
            if(useVariableRotSpeed)
            {
                var desiredRootSpeed = MathR.SmoothStep(minRotSpeed, maxRotSpeed, MathR.Clamp01(AngleInDeg(veh.ForwardVector, veh.Velocity) / 90));
                currentRootSpeed = MathR.Lerp(currentRootSpeed, desiredRootSpeed, 2f * Time.getDeltaTime());
            }

            //smoothQuat = MathR.QuatNlerp(smoothQuat, veh.Quaternion, MathR.Clamp01(rotationSpeed * Time.getDeltaTime()));
            smoothQuat = Quaternion.SlerpUnclamped(smoothQuat, veh.Quaternion, rotationSpeed * Time.getDeltaTime());

            var finalQuat = smoothQuat;

            if (script.smoothIsInAir > 0.001f)
            {
                if (veh.Speed > 0.05f)
                {
                    //velQuat = MathR.QuaternionLookRotation(veh.Velocity);
                    velQuat = MathR.LookAt(veh.Position, veh.Position + (veh.Velocity.Normalized * 2f));
                }

                finalQuat = Quaternion.Lerp(smoothQuat, velQuat, script.smoothIsInAir);
            }

            targetCamera.Position = posCenter + camExtraHeightV3 + (finalQuat * Vector3.RelativeBack * camDistance);
            targetCamera.PointAt(posCenter);
        }

        public override void haltCamera()
        {
            // player switched to another camera, so stop tweens, timers and other stuff about this camera
        }

        public override void dispose()
        {
            // mod/game shutdown (no-op for now)
        }

        public float AngleInRad(Vector3 vec1, Vector3 vec2)
        {
            return MathR.Atan2(vec2.Y - vec1.Y, vec2.X - vec1.X);
        }

        //This returns the angle in degrees
        public float AngleInDeg(Vector3 vec1, Vector3 vec2)
        {
            return AngleInRad(vec1, vec2) * 180 / MathR.PI;
        }
    }
}
