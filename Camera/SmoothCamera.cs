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
    class SmoothCamera : ThirdPersonCamera
    {
        public Quaternion smoothQuat = Quaternion.Identity;

        //public float camDistance = 5.8f;
        public float rotationSpeed = 4.75f;

        public bool useVariableRotSpeed = true;

        public float minRotSpeed = 2f;
        public float maxRotSpeed = 5f;

        public float maxRotSpeedAngle = 90f;
        public float currentRootSpeed = 2f;
        private Quaternion velQuat = Quaternion.Identity;
        private Quaternion smoothVelQuat = Quaternion.Identity;
        private Quaternion finalQuat;

        public SmoothCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        {
            // one time initialization
            maxHighSpeedDistanceIncrement = 1f;
        }

        public override string getCameraName()
        {
            return "Smooth (Third person)";
        }

        public override void loadCameraSettings()
        {
            // no-op for now
        }

        // player in vehicle and using this cam (initialize)
        public override void setupCamera()
        {
            base.setupCamera();

            smoothQuat = veh.Quaternion;
    }

        public override void updateCamera()
        {
            base.updateCamera();

            updateCameraCommon();

            var camExtraHeightV3 = Vector3.WorldUp * extraCamHeight;

            var posCenter = veh.Position + fullHeightOffset;

            float rotSpeed = rotationSpeed;
            if(useVariableRotSpeed)
            {
                var desiredRootSpeed = MathR.SmoothStep(minRotSpeed, maxRotSpeed, MathR.Clamp01(AngleInDeg(veh.ForwardVector, veh.Velocity) / 90));
                currentRootSpeed = MathR.Lerp(currentRootSpeed, desiredRootSpeed, 2f * Time.getDeltaTime());
            }

            //smoothQuat = MathR.QuatNlerp(smoothQuat, veh.Quaternion, MathR.Clamp01(rotationSpeed * Time.getDeltaTime()));
            smoothQuat = Quaternion.SlerpUnclamped(smoothQuat, veh.Quaternion, rotationSpeed * Time.getDeltaTime());

            if (veh.Speed > 0.15f)
            {
                //velQuat = MathR.LookRotation(veh.Velocity, Vector3.WorldUp);
                velQuat = MathR.LookAt(Vector3.Zero, smoothVelocity);
            }
            smoothVelQuat = Quaternion.Lerp(smoothVelQuat, velQuat, 2f * Time.getDeltaTime());

            finalQuat = Quaternion.Lerp(smoothQuat, velQuat, script.smoothIsInAir);

            float finalDistMult = 1f;
            if (veh.Speed > 0.15f)
            {
                // TODO Optimize?
                finalDistMult = Vector3.Angle(finalQuat * Vector3.RelativeFront, veh.Velocity) / 180f;
                finalDistMult = MathR.Lerp(1f, -1f, finalDistMult);
            }

            if(script.isMouseLooking)
            {
                finalQuat = Quaternion.Lerp(finalQuat, getFreelookQuaternion(), script.smoothIsFreeLooking);
            }

            targetCamera.Position = posCenter + camExtraHeightV3 + (finalQuat * Vector3.RelativeBack * (fullLongitudeOffset + ( currentDistanceIncrement * finalDistMult )));

            var pointAt = posCenter - finalQuat * Vector3.RelativeBack * (currentDistanceIncrement + 2f);

            targetCamera.PointAt(pointAt);
        }

        public void updateCameraCommon()
        {
            fullLongitudeOffset = distanceOffset + currentVehicleLongitudeOffset + towedVehicleLongitude;

            currentDistanceIncrement = 0f;

            if (increaseDistanceAtHighSpeed)
            {
                var factor = veh.Speed / maxHighSpeed;
                currentDistanceIncrement = MathR.Lerp(0f, maxHighSpeedDistanceIncrement, Easing.EaseOut(factor, useEasingForCamDistance ? EasingType.Cubic : EasingType.Linear));
            }

            if (accelerationAffectsCamDistance)
            {
                var factor = getVehicleAcceleration() / (maxHighSpeed * 10f);
                currentDistanceIncrement += MathR.Lerp(0f, accelerationCamDistanceMultiplier, Easing.EaseOut(factor, useEasingForCamDistance ? EasingType.Quadratic : EasingType.Linear));
            }
        }

        public override void haltCamera()
        {
            base.haltCamera();
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

        public override Dictionary<string, DebugPanel.watchDelegate> getDebugVars()
        {
            var vars = new Dictionary<string, DebugPanel.watchDelegate>();

            vars.Add("angleBetween", () => { return Quaternion.AngleBetween(finalQuat, smoothVelQuat); });
            vars.Add("V3AngleBetween", () => { return Vector3.Angle(finalQuat * Vector3.RelativeFront, veh.Velocity); });
            vars.Add("dot", () => { return Quaternion.Dot(finalQuat, smoothVelQuat); });

            return vars;
        }
    }
}
