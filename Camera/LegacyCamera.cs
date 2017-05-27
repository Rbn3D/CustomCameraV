using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Glide;
using GTA;
using GTA.Math;
using CustomCameraVScript;

namespace CustomCameraVScript
{
    class LegacyCamera : ThirdPersonCamera
    {
        private Vector3 currentPos;
        private Quaternion currentRotation;
        private float smoothFixedVsVelocity = 0f;
        private float tempSmoothVsVl = 0f;

        // How fast lerp between rear and velocity cam position when neccesary
        private float fixedVsVelocitySpeed = 2.5f;
        private Vector3 smoothVelocity = new Vector3();
        private Vector3 smoothVelocitySmDamp = new Vector3();

        //private float smoothIsNotRearGear = 1f;

        private Vector3 wantedPosVelocity = new Vector3();

        private Vector3 pointAt;

        private float speedCoeff = 0f;

        private Transform rearCamCurrentTransform;

        public float fov = 75f;
        public float heightOffset = 0.28f;
        public float extraCamHeight = 0.10f;

        public float lookFrontOffset = -0.25f;
        public float lookFrontOffsetLowSpeed = -0.4f;
        public float lookLowSpeedStart = 10f;
        public float lookLowSpeedEnd = 22f;

        // When vehicle is on ground and driving, influence of fixed camera vs camera looking at velocity (0 to 1)
        public float velocityInfluence = 0.5f;
        // influence of fixed camera vs camera looking at velocity (same as above) but at low speed (0 to 1)
        public float velocityInfluenceLowSpeed = 0.7f;

        // When we reach this speed, car speed is considered high (for camera effects)
        public float lowSpeedLimit = 33f;

        // If car speed is below this value, then the camera will default to looking forwards.
        public float nearlySttopedSpeed = 2.35f;

        // How closely the camera follows the car's position. The lower the value, the more the camera will lag behind.
        public float cameraStickiness = 17f;

        // How closely the camera matches the car's rotation. The lower the value, the smoother the camera rotations, but too much results in not being able to see where you're going.
        public float cameraRotationSpeed = 1.0f;
        // How closely the camera matches the car's rotation (for low speed)
        public float cameraRotationSpeedLowSpeed = 0.4f;

        // pre smooth position and rotation before interpolate (0 to 1)
        public float generalMovementSpeed = 0.1f;

        public float responsivenessMultiplier = 1.1f;

        public float stoppedSpeed = 0.1f;

        public float vehDummyOffset = 0.11f;
        public float vehDummyOffsetHighSpeed = -0.05f;

        public float finalDummyOffset = 0f;

        public LegacyCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        { 
            // one time initialization
        }

        public override string getCameraName()
        {
            return "Default";
        }

        public override void loadCameraSettings()
        {
            // no-op for now
        }

        public override void setupCamera()
        {
            // player in vehicle and using this cam (initialize)
            setupLegacyCamera();
        }

        public override void updateCamera()
        {
            Transform rearCameraTransform;
            Transform cameraMouseLooking;

            updateCameraCommon();

            //bool isRearCameraOnly = script.smoothIsMouseLooking < 0.005f;
            //bool isMouseCameraOnly = script.smoothIsMouseLooking > 0.995f;

            // @TODO Fix

            bool isRearCameraOnly = true;
            bool isMouseCameraOnly = false;

            if (isRearCameraOnly)
            {
                rearCameraTransform = UpdateCameraRear();

                targetCamera.Position = rearCameraTransform.position;
                targetCamera.Rotation = rearCameraTransform.rotation;
            }
            else if (isMouseCameraOnly)
            {
                cameraMouseLooking = UpdateCameraMouse();

                targetCamera.Position = cameraMouseLooking.position;
                //mainCamera.Rotation = cameraMouseLooking.rotation;

                // ugly fix, but works (still some stuttering while mouse looking)
                targetCamera.PointAt(pointAt);
            }
            else
            {
                rearCameraTransform = UpdateCameraRear();
                cameraMouseLooking = UpdateCameraMouse();

                targetCamera.Position = Vector3.Lerp(rearCameraTransform.position, cameraMouseLooking.position, MathR.Clamp01(script.smoothIsMouseLooking));
                targetCamera.Rotation = MathR.EulerNlerp(rearCameraTransform.quaternion, cameraMouseLooking.quaternion, script.smoothIsMouseLooking).ToEulerAngles();
            }
        }

        public override void haltCamera()
        {
            // player switched to another camera, so stop tweens, timers and other stuff about this camera
        }

        public override void dispose()
        {
            // mod/game shutdown (no-op for now)
        }

        private void setupLegacyCamera()
        {
            //mainCamera.Position -= (Vector3.WorldUp * (heightOffset + currentVehicleHeight));

            //currentPos = veh.Position - (veh.ForwardVector * (currentVehicleLongitude + distanceOffset)) + (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            //currentRotation = veh.Quaternion;
            //ResetMouseLookTween();
            //ResetSmoothValues();

            smoothVelocity = veh.Velocity.Normalized;
            wantedPosVelocity = veh.Rotation;

            rearCamCurrentTransform = new Transform(veh.Position + (veh.Quaternion * Vector3.RelativeBack * (fullLongitudeOffset + currentDistanceIncrement)), veh.Rotation);
        }

        private Transform UpdateCameraMouse()
        {
            Transform transform = new Transform();

            transform.position = GameplayCamera.Position + fullHeightOffset;

            // Fix stuttering (mantain camera distance fixed in local space)
            // For mouse look, there is still some stuttering at high speeds
            Transform fixedDistanceTr = new Transform(transform.position, Quaternion.Identity);
            fixedDistanceTr.PointAt(pointAt);

            fixedDistanceTr.position = veh.Position + (fixedDistanceTr.quaternion * Vector3.RelativeBack * (fullLongitudeOffset + currentDistanceIncrement));

            transform.position = fixedDistanceTr.position;

            return transform;
        }

        private void updateCameraCommon()
        {
            speedCoeff = MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f);
            pointAt = veh.Position + fullHeightOffset + (veh.ForwardVector * computeLookFrontOffset(speedCoeff, script.smoothIsInAir));

            fullLongitudeOffset = (distanceOffset + currentVehicleLongitudeOffset) /* + vehDummyOffset*/ + towedVehicleLongitude;

            finalDummyOffset = MathR.Lerp(vehDummyOffset, vehDummyOffsetHighSpeed, speedCoeff / (maxHighSpeed * 1.6f));

            // no offset if car is in the air
            finalDummyOffset = MathR.Lerp(finalDummyOffset, 0f, script.smoothIsInAir);

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

        private Transform UpdateCameraRear()
        {
            // smooth out current rotation and position
            currentRotation = MathR.EulerNlerp(currentRotation, rearCamCurrentTransform.quaternion, (responsivenessMultiplier * generalMovementSpeed) * Time.getDeltaTime());
            currentPos = Vector3.Lerp(currentPos, rearCamCurrentTransform.position, MathR.Clamp01((responsivenessMultiplier * generalMovementSpeed) * Time.getDeltaTime()));

            Quaternion look;

            var wantedPos = veh.Position + (veh.ForwardVector * finalDummyOffset);

            // If veh has towed vehicle, increase camera distance by towed vehicle longitude

            // should camera be attached to vehicle's back or back his velocity
            var fixedVsVelocity = getFixedVsVelocityFactor(speedCoeff);

            Quaternion vehQuat = veh.Quaternion;
            smoothVelocity = MathR.Vector3SmoothDamp(smoothVelocity, veh.Velocity.Normalized, ref smoothVelocitySmDamp, generalMovementSpeed, 9999999f, responsivenessMultiplier * Time.getDeltaTime());

            // Compute camera position rear the vechicle
            var wantedPosFixed = wantedPos - veh.Quaternion * Vector3.RelativeFront * fullLongitudeOffset;

            // smooth out velocity

            // Compute camera postition rear the direction of the vehcle
            if (speedCoeff >= stoppedSpeed)
                wantedPosVelocity = wantedPos + MathR.QuaternionLookRotation(smoothVelocity) * Vector3.RelativeBottom * fullLongitudeOffset;

            // Smooth factor between two above cam positions
            smoothFixedVsVelocity = MathR.Lerp(smoothFixedVsVelocity, fixedVsVelocity, (fixedVsVelocitySpeed) * Time.getDeltaTime());

            if (!isCycleOrByke)
            {
                tempSmoothVsVl = MathR.Lerp(tempSmoothVsVl, MathR.Clamp(speedCoeff * 0.4347826f, 0.025f, 1f), (fixedVsVelocitySpeed * 0.05f));
                smoothFixedVsVelocity = MathR.Lerp(0f, smoothFixedVsVelocity, tempSmoothVsVl);
            }

            wantedPos = Vector3.Lerp(wantedPosFixed, wantedPosVelocity, MathR.Clamp01(smoothFixedVsVelocity));

            //currentPos = Vector3.Lerp(currentPos, wantedPos, Mathr.Clamp01((responsivenessMultiplier * cameraStickiness) * Time.getDeltaTime()));
            currentPos = Vector3.Lerp(currentPos, wantedPos, MathR.Clamp01((responsivenessMultiplier * cameraStickiness) * Time.getDeltaTime()));

            //rearCamCurrentTransform.position = currentPos;
            targetCamera.PointAt(pointAt);
            look = Quaternion.Euler(targetCamera.Rotation);

            // Rotate the camera towards the velocity vector.
            var finalCamRotationSpeed = MathR.Lerp(cameraRotationSpeedLowSpeed, cameraRotationSpeed, ((speedCoeff / lowSpeedLimit) * 1.32f) * Time.getDeltaTime() * 51f);
            look = MathR.EulerNlerp(currentRotation, look, (1.8f * finalCamRotationSpeed) * Time.getDeltaTime());


            // Fix stuttering (mantain camera distance fixed in local space)
            Transform fixedDistanceTr = new Transform(currentPos + fullHeightOffset + (Vector3.WorldUp * extraCamHeight), Quaternion.Identity);
            fixedDistanceTr.PointAt(pointAt);

            Quaternion rotInitial = fixedDistanceTr.quaternion;

            fixedDistanceTr.position = veh.Position + fullHeightOffset + (Vector3.WorldUp * extraCamHeight) + (rotInitial * Vector3.RelativeBack * (fullLongitudeOffset + currentDistanceIncrement));

            //fixedDistanceTr.position = fixedDistanceTr.position + fullHeightOffset;

            var transform = new Transform();

            transform.position = fixedDistanceTr.position;
            //transform.position = currentPos + fullHeightOffset;
            transform.rotation = look.ToEulerAngles();
            transform.quaternion = look;

            rearCamCurrentTransform = transform;

            return transform;
        }

        private float computeLookFrontOffset(float speedCoeff, float smoothIsInAir)
        {
            var speed = speedCoeff;

            var factor = MathR.InverseLerp(lookLowSpeedStart, lookLowSpeedEnd, speedCoeff);

            var res = MathR.Lerp(lookFrontOffsetLowSpeed, lookFrontOffset, factor);

            // No offset while in air
            res = MathR.Lerp(res, 0f, script.smoothIsInAir);

            return res;
        }

        private float getFixedVsVelocityFactor(float speedCoeff)
        {
            // If the car isn't moving, default to looking forwards
            if (veh.Velocity.Magnitude() < nearlySttopedSpeed)
            {
                return 0f;
            }
            else
            {
                // for bikes, always look at velocity
                if (isCycleOrByke)
                {
                    return 1f;
                }

                // if veh is in air, look at is velocity
                if (veh.IsInAir || veh.CurrentGear == 0)
                {
                    return 1f;
                }
                else // if is on the ground. fit camera (mostly) behind vehicle (for better drift control)
                {
                    // Different factors for low/high speed
                    return MathR.Lerp(velocityInfluenceLowSpeed, velocityInfluence, MathR.Clamp(speedCoeff / lowSpeedLimit, 0f, 1f));
                }
            }
        }

        private void ResetSmoothValues()
        {
            smoothFixedVsVelocity = 0f;
            //smoothIsNotRearGear = 1f;
            smoothVelocity = Vector3.Zero;
            smoothVelocitySmDamp = Vector3.Zero;
            tempSmoothVsVl = 0.025f;

            currentPos = veh.Position - (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            wantedPosVelocity = veh.Position - (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            currentRotation = veh.Quaternion;
        }

        public override void UpdateVehicleProperties()
        {
            base.UpdateVehicleProperties();
            fullHeightOffset = (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
        }

        //private void endFreeLookingSmooth()
        //{
        //    isMouseLooking = false;
        //    mouseLookTimer.Cancel();
        //    mouseLookTimer = null;
        //    tweener.Tween<CustomCameraV>(this, new { smoothIsMouseLooking = 0f }, 0.25f, 0f).Ease(Ease.SineInOut);
        //}

        //private void ResetMouseLookTween()
        //{
        //    if (mouseLookTimer != null)
        //    {
        //        mouseLookTimer.Cancel();
        //        isMouseMoving = false;
        //        isMouseLooking = false;
        //        smoothIsMouseLooking = 0f;
        //    }
        //}
    }
}
