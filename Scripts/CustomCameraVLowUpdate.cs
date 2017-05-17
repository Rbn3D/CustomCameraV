using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCameraVScript;
using GTA;

namespace CustomCameraVScript
{
    class CustomCameraVLowUpdate : Script
    {
        bool customCamInitialized = false;

        public CustomCameraVLowUpdate()
        {
            // Run only one time at a second (to optimize things that aren't required at every frame)
            this.Interval = 1000;
            this.Tick += OnTick;
        }

        private void OnTick(object sender, EventArgs e)
        {
            if(!customCamInitialized)
            {
                if (ReferenceEquals(CustomCameraV.Instance, null))
                    return;
                else
                    customCamInitialized = true;
            }
            

            var customCam = CustomCameraV.Instance;

            if(customCam.veh != null)
                customCam.updateTowedVehicleOrTrailerLongitude();
        }
    }
}
