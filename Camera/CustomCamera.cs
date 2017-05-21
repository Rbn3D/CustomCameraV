using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomCameraVScript;
using GTA;
using Glide;

namespace CustomCameraVScript
{
    public abstract class CustomCamera
    {
        public CustomCameraV script = null;
        public Tweener tweener = null;
        public Camera targetCamera = null;

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

        public abstract void setupCamera();
        public abstract void updateCamera();
        public abstract void haltCamera();

        public abstract void dispose();

        public virtual void UpdateVehicleProperties()
        {
            
        }
    }
}
