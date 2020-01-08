using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomCameraVScript;
using Glide;
using GTA.Math;
using GTA.Native;
using GTA;

namespace CustomCameraVScript
{
    class DriverSeatCamera : CustomCamera
    {
        public bool useRollRotation = true;

        private bool isCycleOrBike = false;

        public const float defualtFarClip = 800f;
        public const float defualtNearClip = 0.15f;

        public const float driverSeatFarClip = 750f;
        public const float driverSeatNearClip = 0.10f;

        public DriverSeatCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        {

        }

        public override void setupCamera()
        {
            base.setupCamera();

            Game.Player.Character.Alpha = 0;

            targetCamera.FarClip = driverSeatFarClip;
            targetCamera.NearClip = driverSeatNearClip;
        }

        public override void updateCamera()
        {
            Vector3 camPos;
            if(!isCycleOrBike)
                camPos = veh.GetBoneCoord("seat_dside_f") + (veh.UpVector * 0.69f);
            else
                camPos = veh.GetBoneCoord("seat_f") + (veh.UpVector * 0.4f) + (veh.ForwardVector * 0.45f);

            targetCamera.Position = camPos;
            var lookAt = camPos + veh.ForwardVector;

            if (!script.isMouseLooking)
                targetCamera.PointAt(lookAt);
            else
            {
                var lookAtFreeLooking = camPos + getFreelookDirectionVector();
                targetCamera.PointAt(Vector3.Lerp(lookAt, lookAtFreeLooking, script.smoothIsFreeLooking));
            }
        }

        public override void haltCamera()
        {
            base.haltCamera();

            Game.Player.Character.Alpha = 255;

            targetCamera.FarClip = defualtFarClip;
            targetCamera.NearClip = defualtNearClip;
        }

        public override string getCameraName()
        {
            return "Driver seat (First Person)";
        }

        public override void loadCameraSettings()
        {
            
        }

        public override void UpdateVehicleProperties()
        {
            isCycleOrBike = veh.Model.IsBicycle || veh.Model.IsBike;
        }

        public override void dispose()
        {

        }


    }
}
