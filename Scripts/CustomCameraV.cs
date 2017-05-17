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

        private static CustomCameraV instance;

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static CustomCameraV()
        {
        }

        //private CustomCameraV()
        //{
        //}

        public static CustomCameraV Instance
        {
            get
            {
                return instance;
            }
        }

        public Vehicle veh = null;

        private Camera mainCamera;
        private bool init = true;
        private bool camSet = false;

        private Vector3 currentPos;
        private Quaternion currentRotation;
        private float smoothFixedVsVelocity = 0f;
        private float tempSmoothVsVl = 0f;

        private float currentVehicleHeight;
        private float currentVehicleLongitudeOffset;

        private bool customCamEnabled = true;

        // How fast lerp between rear and velocity cam position when neccesary
        private float fixedVsVelocitySpeed = 2.5f;
        private Vector3 smoothVelocity = new Vector3();
        private Vector3 smoothVelocitySmDamp = new Vector3();

        private bool showDebugStats = false;
        private bool isCycleOrByke;

        private Vector2 oldMousePosition = Vector2.Zero;
        private bool isMouseMoving = false;
        private bool isMouseLooking;
        private Tween mouseLookTimer;
        private float smoothIsMouseLooking = 0f;

        //private float smoothIsNotRearGear = 1f;

        private Vector3 wantedPosVelocity = new Vector3();

        private DebugPanel dbgPanel;
        private Tweener tweener = new Tweener();
        private int oldVehHash = -1;
        private bool isSuitableForCam = false;

        private bool firstVeh = true;
        private Vector3 pointAt;
        private float lastVelocityMagnitude = 0f;
        private float smoothIsInAir = 0f;

        private Vector3 fullHeightOffset;
        private float speedCoeff = 0f;
        private float finalDummyOffset = 0f;
        private bool isRearCameraOnly = false;
        private bool isMouseCameraOnly = false;
        private float fullLongitudeOffset;
        private float towedVehicleLongitude = 0f;
        private float currentDistanceIncrement;
        private float smoothIsRearGear;

        private Transform rearCamCurrentTransform;

        public float fov = 75f;
        public float distanceOffset = 2.4f;
        public float heightOffset = 0.28f;
        public float extraCamHeight = 0.10f;

        public float lookFrontOffset = -0.25f;
        public float lookFrontOffsetLowSpeed = -0.4f;
        public float lookLowSpeedStart = 10f;
        public float lookLowSpeedEnd = 22f;

        // When vehicle is on ground and driving, influence of fixed camera vs camera looking at velocity (0 to 1)
        public float velocityInfluence = 0.5f;
        // influence of fixed camera vs camera looking at velocity (same as above) but at low speed (0 to 1)
        public float velocityInfluenceLowSpeed = 0.7f;

        // When we reach this speed, car speed is considered high (for camera effects)
        public float lowSpeedLimit = 33f;

        // If car speed is below this value, then the camera will default to looking forwards.
        public float nearlySttopedSpeed = 2.35f;

        // How closely the camera follows the car's position. The lower the value, the more the camera will lag behind.
        public float cameraStickiness = 17f;

        // How closely the camera matches the car's rotation. The lower the value, the smoother the camera rotations, but too much results in not being able to see where you're going.
        public float cameraRotationSpeed = 1.0f;
        // How closely the camera matches the car's rotation (for low speed)
        public float cameraRotationSpeedLowSpeed = 0.4f;

        // pre smooth position and rotation before interpolate (0 to 1)
        public float generalMovementSpeed = 0.5f;

        public float responsivenessMultiplier = 1.1f;

        //// Should camera be centered after the vehicle even if stopped?
        //public bool autocenterOnNearlyStopped = false;

        public float stoppedSpeed = 0.1f;

        // Notify user about mod enabled of first vehicle enter?
        public bool notifyModEnabled = true;

        public bool increaseDistanceAtHighSpeed = true;
        public float maxHighSpeedDistanceIncrement = 1.9f;
        public float maxHighSpeed = 80f;

        public bool accelerationAffectsCamDistance = true;
        public float accelerationCamDistanceMultiplier = 2.38f;

        public bool useEasingForCamDistance = true;

        public Keys toggleEnabledKey = Keys.NumPad1;
        public Keys toggleDebugKey = Keys.NumPad2;

        public float vehDummyOffset = 0.11f;
        public float vehDummyOffsetHighSpeed = -0.05f;

        // Camera will tend to align with vehicle's back over time by this value (set to zero to disable)
        //public float alignmentSpeed = 0.0001f;
        //public float alignmentSpeed = 0.000092f;
        public float alignmentSpeed = 0.25f;
        private Vehicle currentTrailer = null;
        private Vector3 smoothVelocityTrailer = new Vector3();
        private Vector3 smoothVelocityTrailerSmDamp = new Vector3();
        private Vector3 smoothVelocityAverage = new Vector3();

        public CustomCameraV()
        {
            instance = this;

            this.Tick += OnTick;
            this.KeyUp += onKeyUp;
            this.KeyDown += onKeyDown;
            this.Aborted += onAborted;

            // Always use invariant culture (dot decimal separator)
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

            this.LoadSettings();
        }

        private void LoadSettings()
        {
            // general
            customCamEnabled = Settings.GetValue<bool>("general", "enabled", customCamEnabled);
            distanceOffset = Settings.GetValue<float>("general", "distanceOffset", distanceOffset);
            heightOffset = Settings.GetValue<float>("general", "heightOffset", heightOffset);
            fov = Settings.GetValue<float>("general", "fov", fov);
            increaseDistanceAtHighSpeed = Settings.GetValue<bool>("general", "increaseDistanceAtHighSpeed", increaseDistanceAtHighSpeed);
            accelerationAffectsCamDistance = Settings.GetValue<bool>("general", "accelerationAffectsCamDistance", accelerationAffectsCamDistance);
            useEasingForCamDistance = Settings.GetValue<bool>("general", "useEasingForCamDistance", useEasingForCamDistance);

            // advanced
            lookFrontOffset = Settings.GetValue<float>("advanced", "lookFrontOffset", lookFrontOffset);
            lookFrontOffsetLowSpeed = Settings.GetValue<float>("advanced", "lookFrontOffsetLowSpeed", lookFrontOffsetLowSpeed);
            cameraRotationSpeed = Settings.GetValue<float>("advanced", "cameraRotationSpeed", cameraRotationSpeed);
            cameraRotationSpeedLowSpeed = Settings.GetValue<float>("advanced", "cameraRotationSpeedLowSpeed", cameraRotationSpeedLowSpeed);
            generalMovementSpeed = Settings.GetValue<float>("advanced", "generalMovementSpeed", generalMovementSpeed);
            extraCamHeight = Settings.GetValue<float>("advanced", "extraCamHeight", extraCamHeight);

            //key-mappings
            toggleEnabledKey = Settings.GetValue<Keys>("keymappings", "toggleEnabledKey", toggleEnabledKey);
            toggleDebugKey = Settings.GetValue<Keys>("keymappings", "toggleDebugKey", toggleDebugKey);

            // Sanitize
            generalMovementSpeed = Mathr.Clamp(generalMovementSpeed, 0.1f, 10f);
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
                    UpdateVehicleProperties(veh);
                }

                oldVehHash = NewVehHash;

                if (isSuitableForCam)
                {
                    Transform rearCameraTransform;
                    Transform cameraMouseLooking;

                    updateMouseAndGamepadInput(veh);

                    updateCameraCommon(player, veh);

                    if (!camSet)
                    {
                        // Just entered on vehicle
                        Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE, 0);

                        ResetMouseLookTween();
                        ResetSmoothValues(veh);
                        SetupCamera(player, veh);
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
                    Game.DisableControlThisFrame(2, GTA.Control.NextCamera);

                    isRearCameraOnly = smoothIsMouseLooking < 0.005f;
                    isMouseCameraOnly = smoothIsMouseLooking > 0.995f;

                    if(isRearCameraOnly)
                    {
                        rearCameraTransform = UpdateCameraRear(player, veh);

                        mainCamera.Position = rearCameraTransform.position;
                        mainCamera.Rotation = rearCameraTransform.rotation;
                    }
                    else if(isMouseCameraOnly)
                    {
                        cameraMouseLooking = UpdateCameraMouse(player, veh);

                        mainCamera.Position = cameraMouseLooking.position;
                        //mainCamera.Rotation = cameraMouseLooking.rotation;

                        // ugly fix, but works (still some stuttering while mouse looking)
                        mainCamera.PointAt(pointAt);
                    }
                    else
                    {
                        rearCameraTransform = UpdateCameraRear(player, veh);
                        cameraMouseLooking = UpdateCameraMouse(player, veh);

                        mainCamera.Position = Vector3.Lerp(rearCameraTransform.position, cameraMouseLooking.position, Mathr.Clamp01(smoothIsMouseLooking));
                        mainCamera.Rotation = Mathr.QuaternionNLerp(rearCameraTransform.quaternion, cameraMouseLooking.quaternion, smoothIsMouseLooking).ToEulerAngles();
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

        private void setupDebugStats(Vehicle veh)
        {
            dbgPanel = new DebugPanel();

            dbgPanel.header = "CustomCameraV debug ( press " + toggleDebugKey.ToString() + " to hide )";

            dbgPanel.watchedVariables.Add("Veh Speed", () => { return veh.Speed; });
            dbgPanel.watchedVariables.Add("Veh vel. mag", () => { return Mathr.Max(veh.Speed, veh.Velocity.Magnitude() * 22f); });
            dbgPanel.watchedVariables.Add("Smooth fixed vs velocity", () => { return smoothFixedVsVelocity; });
            dbgPanel.watchedVariables.Add("Smooth is in air", () => { return smoothIsInAir; });
            dbgPanel.watchedVariables.Add("Current gear", () => { return veh.CurrentGear; });
            dbgPanel.watchedVariables.Add("LookFrontOffset", () => { return computeLookFrontOffset(veh, Mathr.Max(veh.Speed, veh.Velocity.Magnitude() / 22f), smoothIsInAir); });
            dbgPanel.watchedVariables.Add("Height", () => { return currentVehicleHeight; });
            dbgPanel.watchedVariables.Add("Longitude", () => { return currentVehicleLongitudeOffset; });
            dbgPanel.watchedVariables.Add("MouseX", () => { return InputR.MouseX; });
            dbgPanel.watchedVariables.Add("MouseY", () => { return InputR.MouseY; });
            dbgPanel.watchedVariables.Add("MouseMoving", () => { return isMouseMoving; });
            dbgPanel.watchedVariables.Add("MouseLooking", () => { return isMouseLooking; });
            dbgPanel.watchedVariables.Add("MouseLookingSmooth", () => { return smoothIsMouseLooking; });
            dbgPanel.watchedVariables.Add("generalMovementSpeed", () => { return generalMovementSpeed; });
            dbgPanel.watchedVariables.Add("Delta (custom impl)", () => { return getDeltaTime(); });
            dbgPanel.watchedVariables.Add("TIMESTEP", () => { return Function.Call<float>(Hash.TIMESTEP); });
            dbgPanel.watchedVariables.Add("Interval", () => { return Interval; });
            dbgPanel.watchedVariables.Add("veh.Position", () => { return veh.Position; });
            dbgPanel.watchedVariables.Add("veh.Speed / maxHighSpeed", () => { return veh.Speed / maxHighSpeed; });
            dbgPanel.watchedVariables.Add("isMouseCameraOnly", () => { return isMouseCameraOnly; });
            dbgPanel.watchedVariables.Add("isRearCameraOnly", () => { return isRearCameraOnly; });
            dbgPanel.watchedVariables.Add("veh.Rotation", () => { return veh.Rotation; });
            dbgPanel.watchedVariables.Add("alignFactor", () => { return alignmentSpeed * (1 - smoothIsInAir) * (1 - smoothIsRearGear); });
            dbgPanel.watchedVariables.Add("vehHeight", () => { return veh.Model.GetDimensions().Z; });
            dbgPanel.watchedVariables.Add("vehHeightFn", () => { return getVehicleHeight(veh); });
        }

        private void drawDebugStats(Vehicle veh)
        {
            dbgPanel.Draw();
        }

        private float getDeltaTime()
        {
            return Function.Call<float>(Hash.TIMESTEP);
        }

        private void ResetSmoothValues(Vehicle veh)
        {
            smoothFixedVsVelocity = 0f;
            //smoothIsNotRearGear = 1f;
            smoothVelocity = Vector3.Zero;
            smoothVelocitySmDamp = Vector3.Zero;
            tempSmoothVsVl = 0.025f;

            currentPos = veh.Position - (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            wantedPosVelocity = veh.Position - (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            currentRotation = veh.Quaternion;
        }

        private void ResetMouseLookTween()
        {
            if (mouseLookTimer != null)
            {
                mouseLookTimer.Cancel();
                isMouseMoving = false;
                isMouseLooking = false;
                smoothIsMouseLooking = 0f;
            }
        }

        private void UpdateVehicleProperties(Vehicle veh)
        {
            currentVehicleHeight = getVehicleHeight(veh) / 1.2f;
            currentVehicleLongitudeOffset = getVehicleLongitudeOffset(veh);
            isCycleOrByke = veh.ClassType == VehicleClass.Cycles || veh.ClassType == VehicleClass.Motorcycles;
            isSuitableForCam = isVehicleSuitableForCustomCamera(veh);

            fullHeightOffset = (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
        }

        private void updateMouseAndGamepadInput(Vehicle veh)
        {
            Vector2 currentMousePosition = new Vector2(InputR.MouseX, InputR.MouseY);

            if ((currentMousePosition.X == 0f && currentMousePosition.Y == 0f))
                return; // Fix mouse look always fired after return from pause menu or splash screen

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
                GameplayCamera.RelativeHeading = mainCamera.Rotation.Z - veh.Rotation.Z;
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

        private void SetupCamera(Ped player, Vehicle veh)
        {
            camSet = true;

            mainCamera = World.CreateCamera(GameplayCamera.Position, GameplayCamera.Rotation, fov);
            //mainCamera.Position -= (Vector3.WorldUp * (heightOffset + currentVehicleHeight));

            //currentPos = veh.Position - (veh.ForwardVector * (currentVehicleLongitude + distanceOffset)) + (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
            //currentRotation = veh.Quaternion;
            smoothVelocity = veh.Velocity.Normalized;
            wantedPosVelocity = veh.Rotation;

            mainCamera.IsActive = true;
            World.RenderingCamera = mainCamera;

            rearCamCurrentTransform = new Transform(veh.Position + (veh.Quaternion * Vector3.RelativeBack * (fullLongitudeOffset + currentDistanceIncrement)), veh.Rotation);
        }

        private Transform UpdateCameraMouse(Ped player, Vehicle veh)
        {
            Transform transform = new Transform();

            transform.position = GameplayCamera.Position + fullHeightOffset;

            // Fix stuttering (mantain camera distance fixed in local space)
            // For mouse look, there is still some stuttering at high speeds
            Transform fixedDistanceTr = new Transform(transform.position, Quaternion.Identity);
            fixedDistanceTr.PointAt(pointAt);

            fixedDistanceTr.position = veh.Position  + (fixedDistanceTr.quaternion * Vector3.RelativeBack * (fullLongitudeOffset + currentDistanceIncrement));

            transform.position = fixedDistanceTr.position;

            return transform;
        }

        private void updateCameraCommon(Ped player, Vehicle veh)
        {
            smoothIsInAir = Mathr.Lerp(smoothIsInAir, veh.IsInAir ? 1f : 0f, 1.3f * getDeltaTime());
            smoothIsRearGear = Mathr.Lerp(smoothIsRearGear, veh.CurrentGear == 0 ? 1f : 0f, 1.3f * getDeltaTime());
            speedCoeff = Mathr.Max(veh.Speed, veh.Velocity.Magnitude() / 22f);
            pointAt = veh.Position + fullHeightOffset + (veh.ForwardVector * computeLookFrontOffset(veh, speedCoeff, smoothIsInAir));

            finalDummyOffset = Mathr.Lerp(vehDummyOffset, vehDummyOffsetHighSpeed, speedCoeff / (maxHighSpeed * 1.6f));

            // no offset if car is in the air
            finalDummyOffset = Mathr.Lerp(finalDummyOffset, 0f, smoothIsInAir);

            // now towed vehicle/trailer stuff are checked only one time per second (for performance). See Scripts/CustomCameraVLowUpdate.cs

            fullLongitudeOffset = (distanceOffset + currentVehicleLongitudeOffset) /* + vehDummyOffset*/ + towedVehicleLongitude;

            currentDistanceIncrement = 0f;

            if (increaseDistanceAtHighSpeed)
            {
                var factor = veh.Speed / maxHighSpeed;
                currentDistanceIncrement = Mathr.Lerp(0f, maxHighSpeedDistanceIncrement, Easing.EaseOut(factor, useEasingForCamDistance ? EasingType.Cubic : EasingType.Linear));
            }

            if (accelerationAffectsCamDistance)
            {
                var factor = getVehicleAcceleration(veh) / (maxHighSpeed * 10f);
                currentDistanceIncrement += Mathr.Lerp(0f, accelerationCamDistanceMultiplier, Easing.EaseOut(factor, useEasingForCamDistance ? EasingType.Quadratic : EasingType.Linear));
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

        public void updateTowedVehicleOrTrailerLongitude()
        {
            towedVehicleLongitude = 0f;

            if (veh.TowedVehicle != null)
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

        private Transform UpdateCameraRear(Ped player, Vehicle veh)
        {
            // smooth out current rotation and position
            currentRotation = Mathr.QuaternionNLerp(currentRotation, rearCamCurrentTransform.quaternion, (responsivenessMultiplier * generalMovementSpeed) * getDeltaTime());
            currentPos = Vector3.Lerp(currentPos, rearCamCurrentTransform.position, Mathr.Clamp01((responsivenessMultiplier * generalMovementSpeed) * getDeltaTime()));

            Quaternion look;

            var wantedPos = veh.Position + (veh.ForwardVector * finalDummyOffset);

            // If veh has towed vehicle, increase camera distance by towed vehicle longitude

            // should camera be attached to vehicle's back or back his velocity
            var fixedVsVelocity = getFixedVsVelocityFactor(veh, speedCoeff);

            Quaternion vehQuat = veh.Quaternion;
            smoothVelocity = Mathr.Vector3SmoothDamp(smoothVelocity, veh.Velocity.Normalized, ref smoothVelocitySmDamp, generalMovementSpeed, 9999999f, responsivenessMultiplier * getDeltaTime());

            if (currentTrailer != null)
            {
                vehQuat = Quaternion.Lerp(veh.Quaternion, currentTrailer.Quaternion, 0.5f);
                smoothVelocityTrailer = Mathr.Vector3SmoothDamp(smoothVelocityTrailer, currentTrailer.Velocity.Normalized, ref smoothVelocityTrailerSmDamp, generalMovementSpeed, 9999999f, responsivenessMultiplier * getDeltaTime());
                smoothVelocityAverage = Vector3.Lerp(smoothVelocity, smoothVelocityTrailer, 0.5f);
            }

            // Compute camera position rear the vechicle
            var wantedPosFixed = wantedPos - veh.Quaternion * Vector3.RelativeFront * fullLongitudeOffset;

            // smooth out velocity

            // Compute camera postition rear the direction of the vehcle
            if(speedCoeff >= stoppedSpeed)
                wantedPosVelocity = wantedPos + Mathr.QuaternionLookRotation(currentTrailer == null ? smoothVelocity : smoothVelocityAverage) * Vector3.RelativeBottom * fullLongitudeOffset;

            // Smooth factor between two above cam positions
            smoothFixedVsVelocity = Mathr.Lerp(smoothFixedVsVelocity, fixedVsVelocity, (fixedVsVelocitySpeed) * getDeltaTime());

            if(!isCycleOrByke)
            {
                tempSmoothVsVl = Mathr.Lerp(tempSmoothVsVl, Mathr.Clamp(speedCoeff / 2.3f, 0.025f, 1f), (fixedVsVelocitySpeed / 20f));
                smoothFixedVsVelocity = Mathr.Lerp(0f, smoothFixedVsVelocity, tempSmoothVsVl);
            }

            wantedPos = Vector3.Lerp(wantedPosFixed, wantedPosVelocity, Mathr.Clamp01(smoothFixedVsVelocity));

            //currentPos = Vector3.Lerp(currentPos, wantedPos, Mathr.Clamp01((responsivenessMultiplier * cameraStickiness) * getDeltaTime()));
            currentPos = Vector3.Lerp(currentPos, wantedPos, Mathr.Clamp01((responsivenessMultiplier * cameraStickiness) * getDeltaTime()));

            //rearCamCurrentTransform.position = currentPos;
            mainCamera.PointAt(pointAt);
            look = Quaternion.Euler(mainCamera.Rotation);

            // Rotate the camera towards the velocity vector.
            var finalCamRotationSpeed = Mathr.Lerp(cameraRotationSpeedLowSpeed, cameraRotationSpeed, ((speedCoeff / lowSpeedLimit) * 1.32f) * getDeltaTime() * 51f);
            look = Mathr.QuaternionNLerp(currentRotation, look, (1.8f * finalCamRotationSpeed) * getDeltaTime());
            

            // Fix stuttering (mantain camera distance fixed in local space)
            Transform fixedDistanceTr = new Transform(currentPos + fullHeightOffset + (Vector3.WorldUp * extraCamHeight), Quaternion.Identity);
            fixedDistanceTr.PointAt(pointAt);

            Quaternion rotInitial = fixedDistanceTr.quaternion;

            fixedDistanceTr.position = veh.Position + fullHeightOffset + (Vector3.WorldUp * extraCamHeight) + (rotInitial * Vector3.RelativeBack * (fullLongitudeOffset + currentDistanceIncrement));

            //fixedDistanceTr.position = fixedDistanceTr.position + fullHeightOffset;

            var transform = new Transform();

            transform.position = fixedDistanceTr.position;
            //transform.position = currentPos + fullHeightOffset;
            transform.rotation = look.ToEulerAngles();
            transform.quaternion = look;

            rearCamCurrentTransform = transform;

            return transform;
        }

        private float getVehicleAcceleration(Vehicle veh)
        {
            var mag = smoothVelocity.Magnitude();
            var ret = (mag - lastVelocityMagnitude) * getDeltaTime();

            lastVelocityMagnitude = mag;

            return ret;
        }

        private float computeLookFrontOffset(Vehicle veh, float speedCoeff, float smoothIsInAir)
        {
            var speed = speedCoeff;

            var factor = Mathr.InverseLerp(lookLowSpeedStart, lookLowSpeedEnd, speedCoeff);

            var res = Mathr.Lerp(lookFrontOffsetLowSpeed, lookFrontOffset, factor);

            // No offset while in air
            res = Mathr.Lerp(res, 0f, this.smoothIsInAir);

            return res;
        }

        private float getFixedVsVelocityFactor(Vehicle veh, float speedCoeff)
        {
            // If the car isn't moving, default to looking forwards
            if (veh.Velocity.Magnitude() < nearlySttopedSpeed)
            {
                    return 0f;
            }
            else
            {
                // for bikes, always look at velocity
                if (isCycleOrByke)
                {
                    return 1f;
                }

                // if veh is in air, look at is velocity
                if (veh.IsInAir || veh.CurrentGear == 0)
                {
                    return 1f;
                }
                else // if is on the ground. fit camera (mostly) behind vehicle (for better drift control)
                {
                    // Different factors for low/high speed
                    return Mathr.Lerp(velocityInfluenceLowSpeed, velocityInfluence, Mathr.Clamp(speedCoeff / lowSpeedLimit, 0f, 1f));
                }
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
                distanceAdd = ((height - 1.6f) / 1.4f);
            }

            if(veh.HasBone("bumper_r"))
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

            if (veh.HasBone("windscreen_r"))
            {
                var pos = veh.Position;
                var rearGlassPos = veh.GetBoneCoord("windscreen_r");

                return Vector3.Distance2D(pos, rearGlassPos) + 0.6f + distanceAdd;
            }

            if (veh.HasBone("neon_b"))
            {
                var pos = veh.Position;
                var rarNeonPos = veh.GetBoneCoord("neon_b");

                return Vector3.Distance2D(pos, rarNeonPos) + 0.1f + distanceAdd;
            }

            var longitude =  veh.Model.GetDimensions().Y;

            // bikes and motorbikes has too near camera, so bump distance by 0.5 game units
            if (veh.Model.IsBicycle || veh.Model.IsBike)
            {
                longitude += 2.4f;
            } else
            {
                longitude += 3.6f;
            }

            return longitude / 2f + distanceAdd;
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

    public static class Mathr
    {
        public static float Magnitude(this Vector3 vector)
        {
            return (vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z);
        }

        public static Vector3 ToEulerAngles(this Quaternion qt)
        {
            var vector = new Vector3();

            double sqw = qt.W * qt.W;
            double sqx = qt.X * qt.X;
            double sqy = qt.Y * qt.Y;
            double sqz = qt.Z * qt.Z;

            vector.X = (float)Math.Asin(2f * (qt.X * qt.Z - qt.W * qt.Y));                             // Pitch 
            vector.Y = (float)Math.Atan2(2f * qt.X * qt.W + 2f * qt.Y * qt.Z, 1 - 2f * (sqz + sqw));     // Yaw 
            vector.Z = (float)Math.Atan2(2f * qt.X * qt.Y + 2f * qt.Z * qt.W, 1 - 2f * (sqy + sqz)); // Roll

            return vector;
        }

        public static Quaternion QuaternionFromEuler(float X, float Y, float Z)
        {
            float rollOver2 = Z * 0.5f;
            float sinRollOver2 = (float)Math.Sin((double)rollOver2);
            float cosRollOver2 = (float)Math.Cos((double)rollOver2);
            float pitchOver2 = X * 0.5f;
            float sinPitchOver2 = (float)Math.Sin((double)pitchOver2);
            float cosPitchOver2 = (float)Math.Cos((double)pitchOver2);
            float yawOver2 = Y * 0.5f;
            float sinYawOver2 = (float)Math.Sin((double)yawOver2);
            float cosYawOver2 = (float)Math.Cos((double)yawOver2);
            Quaternion result;
            result.X = cosYawOver2 * cosPitchOver2 * cosRollOver2 + sinYawOver2 * sinPitchOver2 * sinRollOver2;
            result.Y = cosYawOver2 * cosPitchOver2 * sinRollOver2 - sinYawOver2 * sinPitchOver2 * cosRollOver2;
            result.Z = cosYawOver2 * sinPitchOver2 * cosRollOver2 + sinYawOver2 * cosPitchOver2 * sinRollOver2;
            result.W = sinYawOver2 * cosPitchOver2 * cosRollOver2 - cosYawOver2 * sinPitchOver2 * sinRollOver2;
            return result;
        }

        public static Quaternion QuaternionFromEuler(this Vector3 vec)
        {
            return QuaternionFromEuler(vec.X, vec.Y, vec.Z);
        }

        public static Quaternion QuaternionLookRotation(Vector3 forward, Vector3 up)
        {
            forward.Normalize();

            Vector3 vector = Vector3.Normalize(forward);
            Vector3 vector2 = Vector3.Normalize(Vector3.Cross(up, vector));
            Vector3 vector3 = Vector3.Cross(vector, vector2);
            var m00 = vector2.X;
            var m01 = vector2.Y;
            var m02 = vector2.Z;
            var m10 = vector3.X;
            var m11 = vector3.Y;
            var m12 = vector3.Z;
            var m20 = vector.X;
            var m21 = vector.Y;
            var m22 = vector.Z;


            float num8 = (m00 + m11) + m22;
            var quaternion = new Quaternion();
            if (num8 > 0f)
            {
                var num = (float)Math.Sqrt(num8 + 1f);
                quaternion.W = num * 0.5f;
                num = 0.5f / num;
                quaternion.X = (m12 - m21) * num;
                quaternion.Y = (m20 - m02) * num;
                quaternion.Z = (m01 - m10) * num;
                return quaternion;
            }
            if ((m00 >= m11) && (m00 >= m22))
            {
                var num7 = (float)Math.Sqrt(((1f + m00) - m11) - m22);
                var num4 = 0.5f / num7;
                quaternion.X = 0.5f * num7;
                quaternion.Y = (m01 + m10) * num4;
                quaternion.Z = (m02 + m20) * num4;
                quaternion.W = (m12 - m21) * num4;
                return quaternion;
            }
            if (m11 > m22)
            {
                var num6 = (float)Math.Sqrt(((1f + m11) - m00) - m22);
                var num3 = 0.5f / num6;
                quaternion.X = (m10 + m01) * num3;
                quaternion.Y = 0.5f * num6;
                quaternion.Z = (m21 + m12) * num3;
                quaternion.W = (m20 - m02) * num3;
                return quaternion;
            }
            var num5 = (float)Math.Sqrt(((1f + m22) - m00) - m11);
            var num2 = 0.5f / num5;
            quaternion.X = (m20 + m02) * num2;
            quaternion.Y = (m21 + m12) * num2;
            quaternion.Z = 0.5f * num5;
            quaternion.W = (m01 - m10) * num2;
            return quaternion;
        }

        public static Quaternion QuaternionLookRotation(Vector3 forward)
        {
            Vector3 up = Vector3.WorldUp;

            return QuaternionLookRotation(forward, up);
        }

        // Cheaper than Slerp
        public static Quaternion QuaternionNLerp(Quaternion start, Quaternion end, float ammount)
        {
            return QuaternionFromEuler(Vector3.Lerp(start.ToEulerAngles(), end.ToEulerAngles(), Mathr.Clamp01(ammount)).Normalized);
        }

        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = (float)(-2.0 * (double)t * (double)t * (double)t + 3.0 * (double)t * (double)t);
            return (float)((double)to * (double)t + (double)from * (1.0 - (double)t));
        }

        public static float Clamp01(float value)
        {
            if ((double)value < 0.0)
                return 0.0f;
            if ((double)value > 1.0)
                return 1f;
            return value;
        }

        public static float Lerp(float value1, float value2, float ammount)
        {
            return value1 + (value2 - value1) * Mathr.Clamp01(ammount);
        }

        public static float InverseLerp(float from, float to, float value)
        {
            if (from < to)
            {
                if (value < from)
                    return 0.0f;
                else if (value > to)
                    return 1.0f;
            }
            else
            {
                if (value < to)
                    return 1.0f;
                else if (value > from)
                    return 0.0f;
            }
            return (value - from) / (to - from);
        }

        public static int Clamp(int value, int min, int max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public static float Clamp(float value, float min, float max)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        public static Vector3 Vector3SmoothDamp(Vector3 current, Vector3 target, ref Vector3 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num1 = 2f / smoothTime;
            float num2 = num1 * deltaTime;
            float num3 = (float)(1.0 / (1.0 + (double)num2 + 0.479999989271164 * (double)num2 * (double)num2 + 0.234999999403954 * (double)num2 * (double)num2 * (double)num2));
            Vector3 vector = current - target;
            Vector3 vector3_1 = target;
            float maxLength = maxSpeed * smoothTime;
            Vector3 vector3_2 = Vector3ClampMagnitude(vector, maxLength);
            target = current - vector3_2;
            Vector3 vector3_3 = (currentVelocity + num1 * vector3_2) * deltaTime;
            currentVelocity = (currentVelocity - num1 * vector3_3) * num3;
            Vector3 vector3_4 = target + (vector3_2 + vector3_3) * num3;
            if ((double)Vector3.Dot(vector3_1 - current, vector3_4 - vector3_1) > 0.0)
            {
                vector3_4 = vector3_1;
                currentVelocity = (vector3_4 - vector3_1) / deltaTime;
            }
            return vector3_4;
        }

        public static float SmoothDamp(float current, float target, ref float currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Max(0.0001f, smoothTime);
            float num1 = 2f / smoothTime;
            float num2 = num1 * deltaTime;
            float num3 = (float)(1.0 / (1.0 + (double)num2 + 0.479999989271164 * (double)num2 * (double)num2 + 0.234999999403954 * (double)num2 * (double)num2 * (double)num2));
            float num4 = current - target;
            float num5 = target;
            float max = maxSpeed * smoothTime;
            float num6 = Clamp(num4, -max, max);
            target = current - num6;
            float num7 = (currentVelocity + num1 * num6) * deltaTime;
            currentVelocity = (currentVelocity - num1 * num7) * num3;
            float num8 = target + (num6 + num7) * num3;
            if ((double)num5 - (double)current > 0.0 == (double)num8 > (double)num5)
            {
                num8 = num5;
                currentVelocity = (num8 - num5) / deltaTime;
            }
            return num8;
        }

        public static float Max(float a, float b)
        {
            if ((double)a > (double)b)
                return a;
            return b;
        }

        public static float Min(float a, float b)
        {
            if ((double)a < (double)b)
                return a;
            return b;
        }

        public static Vector3 Vector3ClampMagnitude(Vector3 vector, float maxLength)
        {
            if ((double)Vector3SqrMagnitude(vector) > (double)maxLength * (double)maxLength)
                return vector.Normalized * maxLength;
            return vector;
        }

        public static float Vector3SqrMagnitude(Vector3 a)
        {
            return (float)((double)a.X * (double)a.X + (double)a.Y * (double)a.Y + (double)a.Z * (double)a.Z);
        }

        /// <summary>
        /// Evaluates a rotation needed to be applied to an object positioned at sourcePoint to face destPoint
        /// </summary>
        /// <param name="sourcePoint">Coordinates of source point</param>
        /// <param name="destPoint">Coordinates of destionation point</param>
        /// <returns></returns>
        public static Quaternion LookAt(Vector3 sourcePoint, Vector3 destPoint)
        {
            Vector3 forwardVector = Vector3.Normalize(destPoint - sourcePoint);

            float dot = Vector3.Dot(Vector3.RelativeFront, forwardVector);

            if (Math.Abs(dot - (-1.0f)) < 0.000001f)
            {
                return new Quaternion(Vector3.WorldUp.X, Vector3.WorldUp.Y, Vector3.WorldUp.Z, 3.1415926535897932f);
            }
            if (Math.Abs(dot - (1.0f)) < 0.000001f)
            {
                return Quaternion.Identity;
            }

            float rotAngle = (float)Math.Acos(dot);
            Vector3 rotAxis = Vector3.Cross(Vector3.RelativeFront, forwardVector);
            rotAxis = Vector3.Normalize(rotAxis);
            return CreateFromAxisAngle(rotAxis, rotAngle);
        }

        // just in case you need that function also
        public static Quaternion CreateFromAxisAngle(Vector3 axis, float angle)
        {
            float halfAngle = angle * .5f;
            float s = (float)System.Math.Sin(halfAngle);
            Quaternion q = Quaternion.Identity;
            q.X = axis.X * s;
            q.Y = axis.Y * s;
            q.Z = axis.Z * s;
            q.W = (float)System.Math.Cos(halfAngle);
            return q;
        }
    }

    public static class InputR
    {
        public static int MouseX
        {
            get
            {
                return (int)(((GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_CONTROL_VALUE, 0, 239) - 127) / 127.0f) * UI.WIDTH);
            }
        }

        public static int MouseY
        {
            get
            {
                return (int)(((GTA.Native.Function.Call<int>(GTA.Native.Hash.GET_CONTROL_VALUE, 0, 240) - 127) / 127.0f) * UI.HEIGHT);
            }
        }
    }
}