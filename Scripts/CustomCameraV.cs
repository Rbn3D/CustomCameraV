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

        //private static CustomCameraV instance;

        //// Explicit static constructor to tell C# compiler
        //// not to mark type as beforefieldinit
        //static CustomCameraV()
        //{
        //}

        ////private CustomCameraV()
        ////{
        ////}

        //public static CustomCameraV Instance
        //{
        //    get
        //    {
        //        return instance;
        //    }
        //}

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
        private float towedVehicleLongitude = 1f;
        private float currentDistanceIncrement = 0f;
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

        public float alignmentSpeed = 0.25f;
        private Vehicle currentTrailer = null;
        private Vector3 smoothVelocityTrailer = new Vector3();
        private Vector3 smoothVelocityTrailerSmDamp = new Vector3();
        private Vector3 smoothVelocityAverage = new Vector3();

        public bool isTowOrTrailerTruck = false;

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
            generalMovementSpeed = MathR.Clamp(generalMovementSpeed, 0.1f, 10f);
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

                        mainCamera.Position = Vector3.Lerp(rearCameraTransform.position, cameraMouseLooking.position, MathR.Clamp01(smoothIsMouseLooking));
                        mainCamera.Rotation = MathR.QuaternionNLerp(rearCameraTransform.quaternion, cameraMouseLooking.quaternion, smoothIsMouseLooking).ToEulerAngles();
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
            dbgPanel.watchedVariables.Add("Veh vel. mag", () => { return MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f); });
            dbgPanel.watchedVariables.Add("Smooth fixed vs velocity", () => { return smoothFixedVsVelocity; });
            dbgPanel.watchedVariables.Add("Smooth is in air", () => { return smoothIsInAir; });
            dbgPanel.watchedVariables.Add("Current gear", () => { return veh.CurrentGear; });
            dbgPanel.watchedVariables.Add("LookFrontOffset", () => { return computeLookFrontOffset(veh, MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f), smoothIsInAir); });
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
            dbgPanel.watchedVariables.Add("towedVehLong", () => { return towedVehicleLongitude; });
            dbgPanel.watchedVariables.Add("isIndustrial", () => { return isTowOrTrailerTruck; });
            dbgPanel.watchedVariables.Add("vehClass", () => { return veh.ClassType; });
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
            currentVehicleHeight = getVehicleHeight(veh) * 0.8333f;
            currentVehicleLongitudeOffset = getVehicleLongitudeOffset(veh);
            isCycleOrByke = veh.ClassType == VehicleClass.Cycles || veh.ClassType == VehicleClass.Motorcycles;
            isSuitableForCam = isVehicleSuitableForCustomCamera(veh);

            fullHeightOffset = (Vector3.WorldUp * (heightOffset + currentVehicleHeight));

            isTowOrTrailerTruck = veh.ClassType == VehicleClass.Commercial || veh.HasTowArm;
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
            smoothIsInAir = MathR.Lerp(smoothIsInAir, veh.IsInAir ? 1f : 0f, 1.3f * getDeltaTime());
            smoothIsRearGear = MathR.Lerp(smoothIsRearGear, veh.CurrentGear == 0 ? 1f : 0f, 1.3f * getDeltaTime());
            speedCoeff = MathR.Max(veh.Speed, veh.Velocity.Magnitude() * 0.045454f);
            pointAt = veh.Position + fullHeightOffset + (veh.ForwardVector * computeLookFrontOffset(veh, speedCoeff, smoothIsInAir));

            finalDummyOffset = MathR.Lerp(vehDummyOffset, vehDummyOffsetHighSpeed, speedCoeff / (maxHighSpeed * 1.6f));

            // no offset if car is in the air
            finalDummyOffset = MathR.Lerp(finalDummyOffset, 0f, smoothIsInAir);

            // now towed vehicle/trailer stuff are checked only one time per second (for performance). See Scripts/CustomCameraVLowUpdate.cs

            fullLongitudeOffset = (distanceOffset + currentVehicleLongitudeOffset) /* + vehDummyOffset*/ + towedVehicleLongitude;

            currentDistanceIncrement = 0f;

            if (increaseDistanceAtHighSpeed)
            {
                var factor = veh.Speed / maxHighSpeed;
                currentDistanceIncrement = MathR.Lerp(0f, maxHighSpeedDistanceIncrement, Easing.EaseOut(factor, useEasingForCamDistance ? EasingType.Cubic : EasingType.Linear));
            }

            if (accelerationAffectsCamDistance)
            {
                var factor = getVehicleAcceleration(veh) / (maxHighSpeed * 10f);
                currentDistanceIncrement += MathR.Lerp(0f, accelerationCamDistanceMultiplier, Easing.EaseOut(factor, useEasingForCamDistance ? EasingType.Quadratic : EasingType.Linear));
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
            if (!isTowOrTrailerTruck || ReferenceEquals(veh, null))
                return;

            towedVehicleLongitude = 0f;

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

        private Transform UpdateCameraRear(Ped player, Vehicle veh)
        {
            // smooth out current rotation and position
            currentRotation = MathR.QuaternionNLerp(currentRotation, rearCamCurrentTransform.quaternion, (responsivenessMultiplier * generalMovementSpeed) * getDeltaTime());
            currentPos = Vector3.Lerp(currentPos, rearCamCurrentTransform.position, MathR.Clamp01((responsivenessMultiplier * generalMovementSpeed) * getDeltaTime()));

            Quaternion look;

            var wantedPos = veh.Position + (veh.ForwardVector * finalDummyOffset);

            // If veh has towed vehicle, increase camera distance by towed vehicle longitude

            // should camera be attached to vehicle's back or back his velocity
            var fixedVsVelocity = getFixedVsVelocityFactor(veh, speedCoeff);

            Quaternion vehQuat = veh.Quaternion;
            smoothVelocity = MathR.Vector3SmoothDamp(smoothVelocity, veh.Velocity.Normalized, ref smoothVelocitySmDamp, generalMovementSpeed, 9999999f, responsivenessMultiplier * getDeltaTime());

            if (!ReferenceEquals(currentTrailer, null))
            {
                vehQuat = Quaternion.Lerp(veh.Quaternion, currentTrailer.Quaternion, 0.5f);
                smoothVelocityTrailer = MathR.Vector3SmoothDamp(smoothVelocityTrailer, currentTrailer.Velocity.Normalized, ref smoothVelocityTrailerSmDamp, generalMovementSpeed, 9999999f, responsivenessMultiplier * getDeltaTime());
                smoothVelocityAverage = Vector3.Lerp(smoothVelocity, smoothVelocityTrailer, 0.5f);
            }

            // Compute camera position rear the vechicle
            var wantedPosFixed = wantedPos - veh.Quaternion * Vector3.RelativeFront * fullLongitudeOffset;

            // smooth out velocity

            // Compute camera postition rear the direction of the vehcle
            if(speedCoeff >= stoppedSpeed)
                wantedPosVelocity = wantedPos + MathR.QuaternionLookRotation(ReferenceEquals(currentTrailer, null) ? smoothVelocity : smoothVelocityAverage) * Vector3.RelativeBottom * fullLongitudeOffset;

            // Smooth factor between two above cam positions
            smoothFixedVsVelocity = MathR.Lerp(smoothFixedVsVelocity, fixedVsVelocity, (fixedVsVelocitySpeed) * getDeltaTime());

            if(!isCycleOrByke)
            {
                tempSmoothVsVl = MathR.Lerp(tempSmoothVsVl, MathR.Clamp(speedCoeff * 0.4347826f, 0.025f, 1f), (fixedVsVelocitySpeed * 0.05f));
                smoothFixedVsVelocity = MathR.Lerp(0f, smoothFixedVsVelocity, tempSmoothVsVl);
            }

            wantedPos = Vector3.Lerp(wantedPosFixed, wantedPosVelocity, MathR.Clamp01(smoothFixedVsVelocity));

            //currentPos = Vector3.Lerp(currentPos, wantedPos, Mathr.Clamp01((responsivenessMultiplier * cameraStickiness) * getDeltaTime()));
            currentPos = Vector3.Lerp(currentPos, wantedPos, MathR.Clamp01((responsivenessMultiplier * cameraStickiness) * getDeltaTime()));

            //rearCamCurrentTransform.position = currentPos;
            mainCamera.PointAt(pointAt);
            look = Quaternion.Euler(mainCamera.Rotation);

            // Rotate the camera towards the velocity vector.
            var finalCamRotationSpeed = MathR.Lerp(cameraRotationSpeedLowSpeed, cameraRotationSpeed, ((speedCoeff / lowSpeedLimit) * 1.32f) * getDeltaTime() * 51f);
            look = MathR.QuaternionNLerp(currentRotation, look, (1.8f * finalCamRotationSpeed) * getDeltaTime());
            

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

            var factor = MathR.InverseLerp(lookLowSpeedStart, lookLowSpeedEnd, speedCoeff);

            var res = MathR.Lerp(lookFrontOffsetLowSpeed, lookFrontOffset, factor);

            // No offset while in air
            res = MathR.Lerp(res, 0f, this.smoothIsInAir);

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
                    return MathR.Lerp(velocityInfluenceLowSpeed, velocityInfluence, MathR.Clamp(speedCoeff / lowSpeedLimit, 0f, 1f));
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