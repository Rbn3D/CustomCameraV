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
        public bool isCycleOrByke;

        public bool showDebugStats = false;

        public Vector2 oldMousePosition = Vector2.Zero;
        public bool isMouseMoving = false;
        public bool isMouseLooking;
        public Tween mouseLookTimer;
        public float smoothIsMouseLooking = 0f;

        public float currentVehicleHeight;
        public float currentVehicleLongitudeOffset;

        public Vehicle currentTrailer = null;
        public Vector3 smoothVelocityTrailer = new Vector3();
        public Vector3 smoothVelocityTrailerSmDamp = new Vector3();
        public Vector3 smoothVelocityAverage = new Vector3();

        public bool isTowOrTrailerTruck = false;

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

        public float towedVehicleLongitude = 1f;
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

            // Towed vehicles / trailer callback
            tweener.Timer(2f, 2f).Repeat().OnComplete(new Action(updateTowedVehicleOrTrailerLongitude));

            availableCams = new CustomCamera[2];

            availableCams[0] = new LegacyCamera(this, tweener);
            availableCams[1] = new SmoothCamera(this, tweener);
            //availableCams[0] = new LegacyCamera(this, tweener);

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
            if (player.IsInVehicle() && customCamEnabled && !Game.Player.IsAiming && !Game.IsControlPressed(2, GTA.Control.VehicleLookBehind))
            {
                veh = player.CurrentVehicle;
                var NewVehHash = veh.GetHashCode();

                if(oldVehHash != NewVehHash)
                {
                    UpdateVehicleProperties();
                }

                oldVehHash = NewVehHash;

                if (isSuitableForCam)
                {

                    updateMouseAndGamepadInput(veh);

                    updateCameraCommon();

                    if (!camSet)
                    {
                        // Just entered on vehicle
                        Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE, 0);

                        SetupCameras(player, veh);
                        SetupCurrentCamera();
                        setupDebugStats(veh);

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
            //dbgPanel.watchedVariables.Add("MouseMoving", () => { return isMouseMoving; });
            //dbgPanel.watchedVariables.Add("MouseLooking", () => { return isMouseLooking; });
            dbgPanel.watchedVariables.Add("MouseLookingSmooth", () => { return smoothIsMouseLooking; });
            //dbgPanel.watchedVariables.Add("generalMovementSpeed", () => { return generalMovementSpeed; });
            //dbgPanel.watchedVariables.Add("Delta (custom impl)", () => { return getDeltaTime(); });
            //dbgPanel.watchedVariables.Add("TIMESTEP", () => { return Function.Call<float>(Hash.TIMESTEP); });
            //dbgPanel.watchedVariables.Add("Interval", () => { return Interval; });
            //dbgPanel.watchedVariables.Add("veh.Position", () => { return veh.Position; });
            //dbgPanel.watchedVariables.Add("veh.Speed / maxHighSpeed", () => { return veh.Speed / maxHighSpeed; });
            //dbgPanel.watchedVariables.Add("isMouseCameraOnly", () => { return isMouseCameraOnly; });
            //dbgPanel.watchedVariables.Add("isRearCameraOnly", () => { return isRearCameraOnly; });
            //dbgPanel.watchedVariables.Add("veh.Rotation", () => { return veh.Rotation; });
            //dbgPanel.watchedVariables.Add("alignFactor", () => { return alignmentSpeed * (1 - smoothIsInAir) * (1 - smoothIsRearGear); });
            //dbgPanel.watchedVariables.Add("vehHeight", () => { return veh.Model.GetDimensions().Z; });
            //dbgPanel.watchedVariables.Add("vehHeightFn", () => { return getVehicleHeight(veh); });
            //dbgPanel.watchedVariables.Add("towedVehLong", () => { return towedVehicleLongitude; });
            //dbgPanel.watchedVariables.Add("isIndustrial", () => { return isTowOrTrailerTruck; });
            //dbgPanel.watchedVariables.Add("vehClass", () => { return veh.ClassType; });
            //dbgPanel.watchedVariables.Add("vehDisplayName", () => { return veh.DisplayName; });

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

        private void UpdateVehicleProperties()
        {
            currentVehicleHeight = getVehicleHeight(veh) * 0.8333f;
            currentVehicleLongitudeOffset = getVehicleLongitudeOffset(veh);
            isCycleOrByke = veh.ClassType == VehicleClass.Cycles || veh.ClassType == VehicleClass.Motorcycles;
            isSuitableForCam = isVehicleSuitableForCustomCamera(veh);

            isTowOrTrailerTruck = veh.ClassType == VehicleClass.Commercial || veh.HasTowArm || veh.HasBone("attach_female");
            if(!ReferenceEquals(CurrentCamera, null))
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

                //tweener.Tween<CustomCameraV>(this, new { smoothIsMouseLooking = 1f }, 0.01f, 0f).Ease(Ease.SineInOut);
                smoothIsMouseLooking = 1f;

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
            //    tweener.Tween<CustomCameraV>(this, new { smoothIsMouseLooking = 0f }, 0.25f, 0f).Ease(Ease.SineInOut);
            //}

            if (!isMouseLooking && smoothIsMouseLooking <= 0.000001f)
            {
                // When we are not looking around with mouse, sync gamplaycam heading with mod camera, so radar always is shown in the right rotation
                GameplayCamera.RelativeHeading = gameCamera.Rotation.Z - veh.Rotation.Z;
            }
        }

        private void endFreeLookingSmooth()
        {
            isMouseLooking = false;
            mouseLookTimer.Cancel();
            mouseLookTimer = null;
            tweener.Tween<CustomCameraV>(this, new { smoothIsMouseLooking = 0f }, 0.25f, 0f).Ease(Ease.SineInOut);
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
            smoothIsInAir = MathR.Lerp(smoothIsInAir, veh.IsInAir ? 1f : 0f, 2f * getDeltaTime());
            smoothIsRearGear = MathR.Lerp(smoothIsRearGear, veh.CurrentGear == 0 ? 1f : 0f, 1.3f * getDeltaTime());
            //speedCoeff = MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f);
            //pointAt = veh.Position + fullHeightOffset + (veh.ForwardVector * computeLookFrontOffset(veh, speedCoeff, smoothIsInAir));
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

            if(height > 1.6f)
            {
                distanceAdd = (height - 1.6f);
            }

            if (veh.HasBone("bumper_r"))
            {
                var pos = veh.Position;
                var rearBumperPos = veh.GetBoneCoord("bumper_r");

                return Vector3.Distance2D(pos, rearBumperPos) + distanceAdd;
            }

            if (veh.HasBone("spoiler"))
            {
                var pos = veh.Position;
                var spoilerPos = veh.GetBoneCoord("spoiler");

                return Vector3.Distance2D(pos, spoilerPos) + distanceAdd;
            }

            if (veh.HasBone("neon_b"))
            {
                var pos = veh.Position;
                var rarNeonPos = veh.GetBoneCoord("neon_b");

                return Vector3.Distance2D(pos, rarNeonPos) + 0.1f + distanceAdd;
            }

            if (veh.HasBone("boot"))
            {
                var pos = veh.Position;
                var bootPos = veh.GetBoneCoord("boot");

                return Vector3.Distance2D(pos, bootPos) + 2.0f + distanceAdd;
            }

            if (veh.HasBone("windscreen_r"))
            {
                var pos = veh.Position;
                var rearGlassPos = veh.GetBoneCoord("windscreen_r");

                return Vector3.Distance2D(pos, rearGlassPos) + 1.0f + distanceAdd;
            }

            var longitude =  veh.Model.GetDimensions().Y;

            if (veh.Model.IsBicycle || veh.Model.IsBike)
            {
                longitude += 1.4f;
            } else
            {
                longitude += 3.6f;
            }

            return (longitude * 0.5f) + distanceAdd;
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