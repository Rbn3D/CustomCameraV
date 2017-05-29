using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using GTA.Math;
using System.Windows.Forms;
using Glide;
using System.Threading;
using System.Globalization;
using CustomCameraVScript;

namespace CustomCameraVScript
{
    public class CustomCameraV : Script
    {
        public Vehicle veh = null;

        public Camera gameCamera;
        public bool init = true;
        public bool camSet = false;

        public bool customCamEnabled = true;

        public bool showDebugStats = false;

        public Vector2 oldMousePosition = Vector2.Zero;
        public bool isMouseMoving = false;
        public bool isMouseLooking;
        public Tween mouseLookTimer;
        public float smoothIsFreeLooking = 0f;

        public DebugPanel dbgPanel;
        public Tweener tweener = new Tweener();
        public int oldVehHash = -1;
        public bool isSuitableForCam = false;

        private bool firstVeh = true;

        public Keys toggleEnabledKey = Keys.NumPad1;
        public Keys toggleDebugKey = Keys.NumPad2;

        // Notify user about mod enabled of first vehicle enter?
        public bool notifyModEnabled = true;

        // TODO: Delete these two
        public bool isRearCameraOnly = false;
        public bool isMouseCameraOnly = false;

        public float smoothIsRearGear;
        public float smoothIsInAir = 0f;

        private CustomCamera[] availableCams;
        private CustomCamera _currentCam;

        public int currentCameraIndex = 0;

        public CustomCamera CurrentCamera
        {
            get { return _currentCam; }
            set {
                if(!ReferenceEquals(_currentCam, null))
                {
                    _currentCam.haltCamera();
                }
                _currentCam = value;
                if(!ReferenceEquals(veh, null))
                {
                    _currentCam.targetCamera = gameCamera;
                    _currentCam.setupCamera();
                    _currentCam.UpdateVehicleProperties();
                }
            }
        }

        public CustomCameraV()
        {
            // instance = this;

            this.Tick += OnTick;
            this.KeyUp += onKeyUp;
            this.KeyDown += onKeyDown;
            this.Aborted += onAborted;

            // Always use invariant culture (dot decimal separator)
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            this.LoadSettings();

            availableCams = new CustomCamera[3];

            availableCams[0] = new SmoothCamera(this, tweener);
            availableCams[1] = new LegacyCamera(this, tweener);          
            availableCams[2] = new DriverSeatCamera(this, tweener);

            _currentCam = availableCams[currentCameraIndex];
        }

        private void LoadSettings()
        {
            //// general
            //customCamEnabled = Settings.GetValue<bool>("general", "enabled", customCamEnabled);
            //distanceOffset = Settings.GetValue<float>("general", "distanceOffset", distanceOffset);
            //heightOffset = Settings.GetValue<float>("general", "heightOffset", heightOffset);
            //fov = Settings.GetValue<float>("general", "fov", fov);
            //increaseDistanceAtHighSpeed = Settings.GetValue<bool>("general", "increaseDistanceAtHighSpeed", increaseDistanceAtHighSpeed);
            //accelerationAffectsCamDistance = Settings.GetValue<bool>("general", "accelerationAffectsCamDistance", accelerationAffectsCamDistance);
            //useEasingForCamDistance = Settings.GetValue<bool>("general", "useEasingForCamDistance", useEasingForCamDistance);

            //// advanced
            //lookFrontOffset = Settings.GetValue<float>("advanced", "lookFrontOffset", lookFrontOffset);
            //lookFrontOffsetLowSpeed = Settings.GetValue<float>("advanced", "lookFrontOffsetLowSpeed", lookFrontOffsetLowSpeed);
            //cameraRotationSpeed = Settings.GetValue<float>("advanced", "cameraRotationSpeed", cameraRotationSpeed);
            //cameraRotationSpeedLowSpeed = Settings.GetValue<float>("advanced", "cameraRotationSpeedLowSpeed", cameraRotationSpeedLowSpeed);
            //generalMovementSpeed = Settings.GetValue<float>("advanced", "generalMovementSpeed", generalMovementSpeed);
            //extraCamHeight = Settings.GetValue<float>("advanced", "extraCamHeight", extraCamHeight);

            ////key-mappings
            toggleEnabledKey = Settings.GetValue<Keys>("keymappings", "toggleEnabledKey", toggleEnabledKey);
            toggleDebugKey = Settings.GetValue<Keys>("keymappings", "toggleDebugKey", toggleDebugKey);

            //// Sanitize
            //generalMovementSpeed = MathR.Clamp(generalMovementSpeed, 0.1f, 10f);
        }

        private void onAborted(object sender, EventArgs e)
        {
            ExitCustomCameraView();
        }

        public void OnTick(object sender, EventArgs e)
        {
            if (init && customCamEnabled)
            {
                init = false;
            }

            var player = Game.Player.Character;

            if (player.IsInVehicle() && customCamEnabled)
            {
                Game.DisableControlThisFrame(2, GTA.Control.NextCamera);
            }

            if (player.IsInVehicle() && customCamEnabled && !Game.Player.IsAiming && !Game.IsControlPressed(2, GTA.Control.VehicleLookBehind))
            {            
                veh = player.CurrentVehicle;
                var NewVehHash = veh.GetHashCode();

                if(oldVehHash != NewVehHash)
                {
                    UpdateCameraVehicleProperties();
                }

                oldVehHash = NewVehHash;

                if (isSuitableForCam)
                {

                    updateMouseAndGamepadInput(veh);

                    updateCameraCommon();

                    if (!camSet)
                    {
                        setupDebugStats(veh);
                        Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE, 1);
                        SetupCameras(player, veh);
                        SetupCurrentCamera();

                        if (firstVeh && notifyModEnabled)
                        {
                            UI.Notify("CustomCameraV Enabled (Press "+ toggleEnabledKey.ToString() +" to disable)");
                            firstVeh = false;
                        }
                    }
                    else
                    {
                        if (showDebugStats)
                        {
                            drawDebugStats(veh);
                        }
                    }
                    // Game.DisableControlThisFrame(2, GTA.Control.NextCamera);

                    updateCurrentCamera();

                    if(Game.IsControlJustPressed(2, GTA.Control.NextCamera))
                    {
                        switchToNextCustomCamera();
                    }
                }
            }
            else if(camSet)
            {
                ExitCustomCameraView();
            }

            if (camSet && !customCamEnabled)
                ExitCustomCameraView();

            //tweener.Update(realDelta);
            tweener.Update(getDeltaTime());
        }

        private void switchToNextCustomCamera()
        {
            var nextIndex = currentCameraIndex + 1;
            nextIndex = nextIndex % availableCams.Length;

            // CurrentCamera property will take care of callbacks for old/new cameras
            CurrentCamera = availableCams[nextIndex];
            currentCameraIndex = nextIndex;
        }

        private void updateCurrentCamera()
        {
            CurrentCamera.updateCamera();
        }

        private void SetupCurrentCamera()
        {
            CurrentCamera.targetCamera = gameCamera;
            CurrentCamera.setupCamera();
            CurrentCamera.UpdateVehicleProperties();
        }

        private void setupDebugStats(Vehicle veh)
        {
            dbgPanel = new DebugPanel();

            dbgPanel.header = "CustomCameraV debug ( press " + toggleDebugKey.ToString() + " to hide )";

            dbgPanel.watchedVariables.Add("Veh Speed", () => { return veh.Speed; });
            dbgPanel.watchedVariables.Add("Veh vel. mag", () => { return MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f); });
            //dbgPanel.watchedVariables.Add("Smooth fixed vs velocity", () => { return smoothFixedVsVelocity; });
            //dbgPanel.watchedVariables.Add("Smooth is in air", () => { return smoothIsInAir; });
            //dbgPanel.watchedVariables.Add("Current gear", () => { return veh.CurrentGear; });
            //dbgPanel.watchedVariables.Add("LookFrontOffset", () => { return computeLookFrontOffset(veh, MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f), smoothIsInAir); });
            //dbgPanel.watchedVariables.Add("Height", () => { return currentVehicleHeight; });
            //dbgPanel.watchedVariables.Add("Longitude", () => { return currentVehicleLongitudeOffset; });
            //dbgPanel.watchedVariables.Add("MouseX", () => { return InputR.MouseX; });
            //dbgPanel.watchedVariables.Add("MouseY", () => { return InputR.MouseY; });
            dbgPanel.watchedVariables.Add("MouseMoving", () => { return isMouseMoving; });
            dbgPanel.watchedVariables.Add("MouseLooking", () => { return isMouseLooking; });
            dbgPanel.watchedVariables.Add("MouseLookingSmooth", () => { return smoothIsFreeLooking; });
            dbgPanel.watchedVariables.Add("vehDisplayName", () => { return veh.DisplayName; });

            dbgPanel.watchedVariables.Add("vehRotX", () => { return veh.Rotation.X; });
            dbgPanel.watchedVariables.Add("vehRotY", () => { return veh.Rotation.Y; });
            dbgPanel.watchedVariables.Add("vehRotZ", () => { return veh.Rotation.Z; });


            dbgPanel.watchedVariables.Add("vehVelocityX", () => { return veh.Velocity.X; });
            dbgPanel.watchedVariables.Add("vehVelocityY", () => { return veh.Velocity.Y; });
            dbgPanel.watchedVariables.Add("vehVelocityZ", () => { return veh.Velocity.Z; });

            dbgPanel.watchedVariables.Add("currentCameraIndex", () => { return currentCameraIndex; });
            dbgPanel.watchedVariables.Add("currentCameraName", () => { return _currentCam.getCameraName(); });
        }

        private void drawDebugStats(Vehicle veh)
        {
            dbgPanel.Draw();
        }

        private float getDeltaTime()
        {
            return Function.Call<float>(Hash.TIMESTEP);
        }

        private void UpdateCameraVehicleProperties()
        {
            isSuitableForCam = isVehicleSuitableForCustomCamera(veh);

            if (!ReferenceEquals(CurrentCamera, null))
            {
                CurrentCamera.UpdateVehicleProperties();
            }
        }

        private void updateMouseAndGamepadInput(Vehicle veh)
        {
            Vector2 currentMousePosition = new Vector2(InputR.MouseX, InputR.MouseY);

            if ((currentMousePosition.X == 0f && currentMousePosition.Y == 0f))
                return; // Fix for mouse look  to beign fired after return from pause menu or splash screen

            float xMovement = Math.Abs(oldMousePosition.X - currentMousePosition.X);
            float yMovement = Math.Abs(oldMousePosition.Y - currentMousePosition.Y);
            oldMousePosition = currentMousePosition;

            var isGamepadLooking = 
                Game.IsControlPressed(2, GTA.Control.LookLeftOnly)  ||
                Game.IsControlPressed(2, GTA.Control.LookRightOnly) ||
                Game.IsControlPressed(2, GTA.Control.LookUpOnly)    ||
                Game.IsControlPressed(2, GTA.Control.LookDownOnly)
                ;

            if (xMovement > 2.0f || yMovement > 2.0f || isGamepadLooking)
            {
                isMouseMoving = true;
                isMouseLooking = true;

                //tweener.Tween<CustomCameraV>(this, new { smoothIsFreeLooking = 1f }, 0.01f, 0f).Ease(Ease.SineInOut);
                smoothIsFreeLooking = 1f;

                if (mouseLookTimer != null)
                {
                    mouseLookTimer.Cancel();
                }
                // TODO
                mouseLookTimer = tweener.Timer(isGamepadLooking ? 0.1f : 1.55f).OnComplete(new Action(endFreeLookingSmooth));
            }
            else
            {
                isMouseMoving = false;
            }

            //if (mouseLookTimer != null && mouseLookTimer.Completion == 1f)
            //{
            //    isMouseLooking = false;
            //    mouseLookTimer.Cancel();
            //    mouseLookTimer = null;
            //    tweener.Tween<CustomCameraV>(this, new { smoothIsFreeLooking = 0f }, 0.25f, 0f).Ease(Ease.SineInOut);
            //}

            if (!isMouseLooking)
            {
                // When we are not looking around with mouse, sync gamplaycam heading with mod camera, so radar always shows the right rotation
                GameplayCamera.RelativeHeading = gameCamera.Rotation.Z - veh.Rotation.Z;
            }
        }

        private void endFreeLookingSmooth()
        {
            //mouseLookTimer.Cancel();
            mouseLookTimer = null;
            tweener.Tween<CustomCameraV>(this, new { smoothIsFreeLooking = 0f }, 0.25f, 0f).Ease(Ease.SineInOut).OnComplete(() => { isMouseLooking = false; });
        }


        private bool isVehicleSuitableForCustomCamera(Vehicle veh)
        {
            return veh.ClassType != VehicleClass.Trains && veh.ClassType != VehicleClass.Planes && veh.ClassType != VehicleClass.Helicopters && veh.ClassType != VehicleClass.Boats;
        }

        private void SetupCameras(Ped player, Vehicle veh)
        {
            camSet = true;

            gameCamera = World.CreateCamera(GameplayCamera.Position, GameplayCamera.Rotation, 75f);
            //mainCamera.Position -= (Vector3.WorldUp * (heightOffset + currentVehicleHeight));

            //currentPos = veh.Position - (veh.ForwardVector * (currentVehicleLongitude + distanceOffset)) + (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            //currentRotation = veh.Quaternion;

            gameCamera.IsActive = true;
            World.RenderingCamera = gameCamera;
        }

        private void updateCameraCommon()
        {
            smoothIsInAir = MathR.Lerp(smoothIsInAir, veh.IsInAir || veh.IsUpsideDown ? 1f : 0f, 2f * getDeltaTime());
            smoothIsRearGear = MathR.Lerp(smoothIsRearGear, veh.CurrentGear == 0 ? 1f : 0f, 1.3f * getDeltaTime());
            //speedCoeff = MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f);
            //pointAt = veh.Position + fullHeightOffset + (veh.ForwardVector * computeLookFrontOffset(veh, speedCoeff, smoothIsInAir));
        }


        private void onKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == toggleDebugKey)
            {
                showDebugStats = !showDebugStats;
            }

            if (e.KeyCode == toggleEnabledKey)
            {
                customCamEnabled = !customCamEnabled;
            }
        }

        private void onKeyUp(object sender, KeyEventArgs e)
        {

        }

        private void ExitCustomCameraView()
        {
            if (!ReferenceEquals(_currentCam, null))
                CurrentCamera.haltCamera();

            veh = null;
            World.RenderingCamera = null;
            camSet = false;
        }

        protected override void Dispose(bool A_0)
        {
            World.RenderingCamera = null;
            base.Dispose(A_0);
        }
    }
}