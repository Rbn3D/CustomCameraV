using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCameraVScript;
using Glide;
using GTA.Native;
using GTA;
using GTA.Math;

namespace CustomCameraVScript
{
    public abstract class ThirdPersonCamera : CustomCamera
    {
        public bool isTowOrTrailerTruck = false;
        public float towedVehicleLongitude = 1f;
        public Vehicle currentTrailer = null;

        public float currentVehicleHeight;
        public float currentVehicleLongitudeOffset;
        public bool isCycleOrByke;

        public bool increaseDistanceAtHighSpeed = true;
        public float maxHighSpeedDistanceIncrement = 1.9f;
        public float maxHighSpeed = 80f;

        public float currentDistanceIncrement = 0f;
        public float fullLongitudeOffset;
        public Vector3 fullHeightOffset;

        public float distanceOffset = 1.75f;

        public float fov = 75f;
        public float heightOffset = 0.3f;
        public float extraCamHeight = 0.10f;

        public bool accelerationAffectsCamDistance = true;
        public bool useEasingForCamDistance = true;
        public float accelerationCamDistanceMultiplier = 2.38f;
        private float lastVelocityMagnitude = 0f;
        public Vector3 smoothVelocity = new Vector3();
        //public Vector3 smoothVelocitySmDamp = new Vector3();

        public ThirdPersonCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        {
        }

        public override void onLowUpdateCheck()
        {
            base.onLowUpdateCheck();

            updateTowedVehicleOrTrailerLongitude();
        }

        public override void UpdateVehicleProperties()
        {
            base.UpdateVehicleProperties();

            currentVehicleHeight = getVehicleHeight(veh) * 0.8333f;
            currentVehicleLongitudeOffset = getVehicleLongitudeOffset(veh);
            isCycleOrByke = veh.ClassType == VehicleClass.Cycles || veh.ClassType == VehicleClass.Motorcycles;

            fullHeightOffset = (Vector3.WorldUp * (heightOffset + currentVehicleHeight));

            isTowOrTrailerTruck = veh.ClassType == VehicleClass.Commercial || veh.HasTowArm || veh.HasBone("attach_female");

            smoothVelocity = veh.Velocity;
        }

        public float getVehicleHeight(Vehicle veh)
        {
            var height = veh.Model.GetDimensions().Z;

            if (Function.Call<bool>(Hash.IS_BIG_VEHICLE, veh.Handle))
            {
                height += 0.9f;
            }

            return height;
        }

        public float getVehicleLongitudeOffset(Vehicle veh)
        {
            var height = getVehicleHeight(veh);

            var distanceAdd = 0f;

            if (height > 1.6f)
            {
                distanceAdd = (height - 1.6f);
            }

            if (veh.HasBone("bumper_r"))
            {
                var pos = veh.Position;
                var rearBumperPos = veh.GetBoneCoord("bumper_r");

                return Vector3.Distance(pos, rearBumperPos) + distanceAdd;
            }

            if (veh.HasBone("spoiler"))
            {
                var pos = veh.Position;
                var spoilerPos = veh.GetBoneCoord("spoiler");

                return Vector3.Distance(pos, spoilerPos) + distanceAdd;
            }

            if (veh.HasBone("neon_b"))
            {
                var pos = veh.Position;
                var rarNeonPos = veh.GetBoneCoord("neon_b");

                return Vector3.Distance(pos, rarNeonPos) + 0.1f + distanceAdd;
            }

            if (veh.HasBone("boot"))
            {
                var pos = veh.Position;
                var bootPos = veh.GetBoneCoord("boot");

                return Vector3.Distance(pos, bootPos) + 2.0f + distanceAdd;
            }

            if (veh.HasBone("windscreen_r"))
            {
                var pos = veh.Position;
                var rearGlassPos = veh.GetBoneCoord("windscreen_r");

                return Vector3.Distance(pos, rearGlassPos) + 1.0f + distanceAdd;
            }

            var longitude = veh.Model.GetDimensions().Y;

            if (veh.Model.IsBicycle || veh.Model.IsBike)
            {
                longitude += 1.4f;
            }
            else
            {
                longitude += 3.6f;
            }

            return (longitude * 0.5f) + distanceAdd;
        }

        public override void updateCamera()
        {
            smoothVelocity = Vector3.Lerp(smoothVelocity, veh.Velocity, 20f * Time.getDeltaTime());
        }

        public void updateTowedVehicleOrTrailerLongitude()
        {
            if (!isTowOrTrailerTruck || ReferenceEquals(veh, null))
            {
                towedVehicleLongitude = 1f;
                return;
            }


            if (!ReferenceEquals(veh.TowedVehicle, null))
                towedVehicleLongitude = veh.TowedVehicle.Model.GetDimensions().Y + 1.0f;

            if (Function.Call<bool>(Hash.IS_VEHICLE_ATTACHED_TO_TRAILER, veh))
            {
                currentTrailer = GetTrailer(veh);
                towedVehicleLongitude = currentTrailer.Model.GetDimensions().Y + 1.0f;
            }
            else
            {
                currentTrailer = null;
            }
        }

        private Vehicle GetTrailer(Vehicle veh)
        {
            OutputArgument outputArgument = new OutputArgument();
            if (Function.Call<bool>(Hash.GET_VEHICLE_TRAILER_VEHICLE, veh, outputArgument))
            {
                return outputArgument.GetResult<Vehicle>();
            }
            else
            {
                return null;
            }
        }

        public float getVehicleAcceleration()
        {
            var mag = veh.Velocity.Magnitude();
            var ret = (mag - lastVelocityMagnitude) * Time.getDeltaTime();

            lastVelocityMagnitude = mag;

            return ret;
        }
    }
}
