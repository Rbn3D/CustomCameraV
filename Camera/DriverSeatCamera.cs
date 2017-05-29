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
        private bool isCycleOrBike = false;

        public DriverSeatCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        {

        }

        public override void setupCamera()
        {
            base.setupCamera();

            Game.Player.Character.Alpha = 0;
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
