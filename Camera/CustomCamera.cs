using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTA;
using Glide;
using GTA.Math;
using CustomCameraVScript;

namespace CustomCameraVScript
{
    public abstract class CustomCamera
    {
        public CustomCameraV script = null;
        public Tweener tweener = null;
        public Camera targetCamera = null;

        public float lowUpdateCheckTime = 2f;
        public Tween lowUpdateTimer = null;

        public Vehicle veh
        {
            get
            {
                return script.veh;
            }
        }

        public CustomCamera(CustomCameraV script, Tweener tweener)
        {
            this.script = script;
            this.tweener = tweener;
        }

        public abstract string getCameraName();

        public abstract void loadCameraSettings();

        public virtual void setupCamera()
        {
            lowUpdateTimer = tweener.Timer(0f, lowUpdateCheckTime).Repeat().OnComplete(new Action(onLowUpdateCheck));

            setupDebugVars();
        }

        public virtual void onLowUpdateCheck()
        {

        }

        public abstract void updateCamera();

        public virtual void haltCamera()
        {
            lowUpdateTimer.Cancel();
            lowUpdateTimer = null;

            removeDebugVars();
        }



        public abstract void dispose();

        public virtual void UpdateVehicleProperties()
        {
            
        }

        public virtual Dictionary<string, DebugPanel.watchDelegate> getDebugVars()
        {
            return new Dictionary<string, DebugPanel.watchDelegate>();
        }

        private void setupDebugVars()
        {
            script.dbgPanel.AddRange(getDebugVars());
        }

        private void removeDebugVars()
        {
            script.dbgPanel.RemoveRange(getDebugVars());
        }

        public Quaternion getFreelookQuaternion()
        {
            return MathR.LookAt(Vector3.Zero, GameplayCamera.Direction);
        }

        public Vector3 getFreelookDirectionVector()
        {
            return GameplayCamera.Direction;
        }
    }
}
