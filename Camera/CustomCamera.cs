using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GTA;
using Glide;
using CustomCameraVScript;
using GTA.Math;

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
            lowUpdateTimer = tweener.Timer(lowUpdateCheckTime, lowUpdateCheckTime).Repeat().OnComplete(new Action(onLowUpdateCheck));
        }

        public virtual void onLowUpdateCheck()
        {

        }

        public abstract void updateCamera();

        public virtual void haltCamera()
        {
            lowUpdateTimer.Cancel();
            lowUpdateTimer = null;
        }

        public abstract void dispose();

        public virtual void UpdateVehicleProperties()
        {
            
        }
    }
}
