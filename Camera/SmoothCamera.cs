using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCameraVScript;
using Glide;
using GTA;
using GTA.Math;
using GTA.Native;

namespace CustomCameraVScript
{
    class SmoothCamera : ThirdPersonCamera
    {
        public float rotationSpeed = 4.75f;

        public bool useVariableRotSpeed = true;
        public float minRotSpeed = 2f;
        public float maxRotSpeed = 5f;
        public float maxRotSpeedAngle = 90f;

        public bool avoidCameraBouncinessCar = true;

        private float currentRootSpeed = 2f;
        private Quaternion velocityQuat = Quaternion.Identity;
        private Quaternion smoothVelQuat = Quaternion.Identity;
        private Quaternion finalQuat;
        private float surfaceNormalDistance;
        private Vector3 cachedRaycastDir;
        private int frameCounter = 0;
        private Quaternion smoothQuat = Quaternion.Identity;

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
            cachedRaycastDir = veh.ForwardVector;
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
                var desiredRootSpeed = MathR.SmoothStep(minRotSpeed, maxRotSpeed, MathR.Clamp01(AngleInDeg(veh.ForwardVector, veh.Velocity) /* / 90 */ * 0.0111111111111111f));
                currentRootSpeed = MathR.Lerp(currentRootSpeed, desiredRootSpeed, 2f * Time.getDeltaTime());
            }

            if (veh.Speed > 1f)
            {
                //velQuat = MathR.LookRotation(veh.Velocity, Vector3.WorldUp);
                velocityQuat = MathR.XLookRotation(smoothVelocity);
            }

            Quaternion vehQuat;

            if(isCycleOrByke)
            {
                // Bykes / cycles (avoid unwanted cam rotations during wheelies/stoppies)
                if (veh.Speed < 3f)
                {
                    vehQuat = getSurfaceNormalRotation(); // Possibly unperformant (using it only for wheelie/stoppie al low speeds)
                }
                else
                    vehQuat = velocityQuat;
            }
            else // cars
            {
                if(avoidCameraBouncinessCar && veh.IsOnAllWheels)
                {
                    //vehQuat = getRotationFromAverageWheelPosCar(); // Avoid camera movements caused by car suspension movement (wheels are on the floor)
                    vehQuat = getSurfaceNormalRotation(); // Avoid camera movements caused by car suspension movement (wheels are on the floor)
                }
                else
                {
                    vehQuat = veh.Quaternion;
                }    
            }

            //smoothQuat = MathR.QuatNlerp(smoothQuat, veh.Quaternion, MathR.Clamp01(rotationSpeed * Time.getDeltaTime()));
            smoothQuat = Quaternion.SlerpUnclamped(smoothQuat, vehQuat, rotationSpeed * Time.getDeltaTime());

            smoothVelQuat = Quaternion.Lerp(smoothVelQuat, velocityQuat, 2f * Time.getDeltaTime());

            finalQuat = Quaternion.Lerp(smoothQuat, velocityQuat, script.smoothIsInAir);

            float finalDistMult = 1f;
            if (veh.Speed > 0.15f)
            {
                // TODO Optimize?
                finalDistMult = Vector3.Angle(finalQuat * Vector3.RelativeFront, veh.Velocity) /* / 180f */ * 0.0055555555555556f;
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

        private Quaternion getRotationFromAverageWheelPosBike()
        {
            // Front
            var fPos = veh.GetBonePosition("wheel_lf");

            // Rear
            var rPos = veh.GetBonePosition("wheel_lr");

            var dir = fPos - rPos;

            return MathR.XLookRotation(dir);
        }

        private Quaternion getRotationFromAverageWheelPosCar()
        {
            // Front Left
            var flPos = veh.GetBonePosition("wheel_lf");

            // Front Right
            var frPos = veh.GetBonePosition("wheel_rf");

            var frontAverage = (flPos + frPos) * 0.5f;

            // Rear Left
            var rlPos = veh.GetBonePosition("wheel_lr");

            // Rear Right
            var rrPos = veh.GetBonePosition("wheel_rr");

            var rearAverage = (rlPos + rrPos) * 0.5f;

            var dir = frontAverage - rearAverage;

            return MathR.XLookRotation(dir);
        }

        public override void UpdateVehicleProperties()
        {
            base.UpdateVehicleProperties();

            surfaceNormalDistance = (veh.Model.GetDimensions().Y * 0.5f) - 0.12f;
        }

        private Quaternion getSurfaceNormalRotation()
        {
            var raycast = World.Raycast(veh.Position, Vector3.WorldDown, 2f, IntersectOptions.Map, veh);

            if (raycast.DitHit)
            {
                //cachedRaycastDir = MathR.OrthoNormalize(raycast.SurfaceNormal, veh.ForwardVector);
                cachedRaycastDir = Vector3.Cross(raycast.SurfaceNormal, veh.RightVector).Normalized;
            }

            return MathR.XLookRotation(cachedRaycastDir);
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
            vars.Add("veh.HeightAboveGround", () => { return veh.HeightAboveGround; });
            vars.Add("CamFarClip", () => { return targetCamera.FarClip; });
            vars.Add("CamNearClip", () => { return targetCamera.NearClip; });

            return vars;
        }
    }
}
