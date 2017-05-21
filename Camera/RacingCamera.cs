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
    class RacingCamera : CustomCamera
    {
        public Quaternion smoothQuat = Quaternion.Identity;

        public float camDistance = 5.8f;
        public float heightOffset = 1.5f;
        public float extraCamHeight = 0.1f;
        public float rotationSpeed = 5f;

        public RacingCamera(CustomCameraV script, Tweener tweener) : base(script, tweener)
        {
            // one time initialization
        }

        public override string getCameraName()
        {
            return "Racing";
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

            smoothQuat = Quaternion.Lerp(smoothQuat, veh.Quaternion, MathR.Clamp01(rotationSpeed * Time.getDeltaTime()));

            targetCamera.Position = posCenter + camExtraHeightV3 + (smoothQuat * Vector3.RelativeBack * camDistance);
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
    }
}
