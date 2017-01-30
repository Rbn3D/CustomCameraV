using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using GTA.Math;
using System.Windows.Forms;

public class CustomCameraV : Script
{
    private Camera mainCamera;
    private bool init = true;
    private bool camSet = false;
    private Camera backupCamera;

    private Vector3 camPos;
    private Vector3 currentPos;
    private Quaternion currentRotation;
    private Vector3 lookTargetOffset;
    private Vector3 camTargetOffset;
    private float smoothFixedVsVelocity = 0f;
    private float tempSmoothVsVl = 0f;

    private float currentVehicleHeight;
    private float currentVehicleLongitude;

    private float targetFPS = 30f;
    private bool customCamEnabled = true;

    // How fast lerp between rear and velocity cam position when neccesary
    private float fixedVsVelocitySpeed = 1.5f;
    private float generalFixedvsSmoothMovement;
    private Vector3 smoothVelocity;
    private Vector3 smoothVelocitySmDamp = new Vector3();

    private bool showDebugStats = false;
    private bool isCycleOrByke;

    private float smoothIsRearGear = 0f;

    public float fov = 75f;
    public float distanceOffset = 2.4f;
    public float heightOffset = 0.28f;

    public float lookFrontOffset = -0.35f;

    // When vehicle is on ground and driving, influence of fixed camera vs camera looking at velocity (0 to 1)
    public float velocityInfluence = 0.5f;
    // influence of fixed camera vs camera looking at velocity (same as above) but at low speed (0 to 1)
    public float velocityInfluenceLowSpeed = 0.7f;

    // When we reach this speed, car speed is considered high (for camera effects)
    public float lowSpeedLimit = 33f;

    // If car speed is below this value, then the camera will default to looking forwards.
    public float nearlySttopedSpeed = 2.35f;

    // How closely the camera follows the car's position. The lower the value, the more the camera will lag behind.
    public float cameraStickiness = 17.0f;

    // How closely the camera matches the car's rotation. The lower the value, the smoother the camera rotations, but too much results in not being able to see where you're going.
    public float cameraRotationSpeed = 1.0f;
    // How closely the camera matches the car's rotation (for low speed)
    public float cameraRotationSpeedLowSpeed = 0.4f;

    // pre smooth position and rotation before interpolate (0 to 1)
    public float generalMovementSmoothness = 0.9818f;

    //// Should camera be centered after the vehicle even if stopped?
    //public bool autocenterOnNearlyStopped = false;

    public float stoppedSpeed = 0.1f;

    // UI stats
    UIText vehSpeedUI;
    UIText smoothVelocityUI;
    UIText currentGearUI;
    private Vector3 wantedPosVelocity = new Vector3();

    public CustomCameraV()
    {
        this.Tick += OnTick;
        this.KeyUp += onKeyUp;
        this.KeyDown += onKeyDown;
        this.Aborted += onAborted;

        lookTargetOffset = new Vector3(0, lookFrontOffset, heightOffset);
        camTargetOffset = new Vector3(0f, 0f, heightOffset);
    }

    private void onAborted(object sender, EventArgs e)
    {
        ExitCustomCameraView();
    }

    public void OnTick(object sender, EventArgs e)
    {
        if(init && customCamEnabled)
        {
            backupCamera = World.RenderingCamera;
            init = false;
        }

        var player = Game.Player.Character;
        if (player.IsInVehicle() && customCamEnabled)
        {
            Vehicle veh = player.CurrentVehicle;

            if(isVehicleSuitableForCustomCamera(veh))
            {
                if (!camSet)
                {
                    SetupCamera(player, veh);
                    UpdateCamera(player, veh);
                    setupDebugStats();
                }
                else
                {
                    UpdateCamera(player, veh);
                    if (showDebugStats)
                    {
                        drawDebugStats(veh);
                    }
                }
            }

        }
        else if(camSet)
        {
            ExitCustomCameraView();
        }

        if (camSet && !customCamEnabled)
            ExitCustomCameraView();
    }

    private void setupDebugStats()
    {
        vehSpeedUI = new UIText("Veh Speed: -", new System.Drawing.Point(10, 10), 0.3f);
        smoothVelocityUI = new UIText("Smooth Velocity: -", new System.Drawing.Point(10, 25), 0.3f);
        currentGearUI = new UIText("Current gear: -", new System.Drawing.Point(10, 40), 0.3f);
    }

    private void drawDebugStats(Vehicle veh)
    {
        vehSpeedUI.Caption = "Veh Speed: " + veh.Speed.ToString();
        vehSpeedUI.Draw();

        smoothVelocityUI.Caption = "Smooth Fixed vs Velocity: " + smoothFixedVsVelocity.ToString();
        smoothVelocityUI.Draw();

        currentGearUI.Caption = "Current gear: " + veh.CurrentGear;
        currentGearUI.Draw();
    }

    private bool isVehicleSuitableForCustomCamera(Vehicle veh)
    {
        return veh.ClassType != VehicleClass.Trains && veh.ClassType != VehicleClass.Planes && veh.ClassType != VehicleClass.Helicopters && veh.ClassType != VehicleClass.Boats;
    }

    private void SetupCamera(Ped player, Vehicle veh)
    {
        camSet = true;
        currentVehicleHeight = getVehicleHeight(veh) / 1.2f;
        currentVehicleLongitude = getVehicleLongitude(veh);

        mainCamera = World.CreateCamera(veh.Position, veh.Rotation, fov);
        currentPos = veh.Position - (veh.ForwardVector * (currentVehicleLongitude + distanceOffset)) + (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
        currentRotation = veh.Quaternion;
        generalFixedvsSmoothMovement = 1f - generalMovementSmoothness;
        smoothVelocity = veh.Velocity.Normalized;
        wantedPosVelocity = veh.Rotation;
        World.RenderingCamera = mainCamera;
        isCycleOrByke = veh.ClassType == VehicleClass.Cycles || veh.ClassType == VehicleClass.Motorcycles;
    }

    private void UpdateCamera(Ped player, Vehicle veh)
    {
        float realDelta = 1f / Game.FPS;

        //smooth out delta time to avoid cam stuttering
        targetFPS = Mathr.Lerp(targetFPS, Game.FPS, 0.8f);
        float smoothDelta = 1f / targetFPS;

        // smooth out current rotation and position
        currentRotation = Mathr.QuaternionNLerp(currentRotation, Mathr.QuaternionFromEuler(mainCamera.Rotation), generalFixedvsSmoothMovement);
        currentPos = Vector3.Lerp(currentPos, mainCamera.Position - (Vector3.WorldUp * (heightOffset + currentVehicleHeight)), generalFixedvsSmoothMovement);

        var boneIndex = Function.Call<int>(Hash._GET_ENTITY_BONE_INDEX, veh.Handle, "chassis_dummy");
        var bonePos = Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, veh.Handle, boneIndex);
        camPos = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS, veh.Handle, bonePos.X, bonePos.Y, bonePos.Z);

        Quaternion look;
        var wantedPos = veh.Position;

        var towedVehicleLongitude = 0f;
        if (veh.TowedVehicle != null)
            towedVehicleLongitude = veh.TowedVehicle.Model.GetDimensions().Y;

        // should camera be attached to vehicle's back or back his velocity
        var fixedVsVelocity = getFixedVsVelocityFactor(veh);

        // Compute camera position rear the vechicle
        var wantedPosFixed = wantedPos - veh.Quaternion * Vector3.RelativeFront * ((distanceOffset + currentVehicleLongitude / 2f) + towedVehicleLongitude);

        // smooth out velocity
        smoothVelocity = Mathr.Vector3SmoothDamp(smoothVelocity, veh.Velocity.Normalized, ref smoothVelocitySmDamp, generalFixedvsSmoothMovement, 9999999f, realDelta);
        // Compute camera postition rear the direction of the vehcle
        if(veh.Speed >= stoppedSpeed)
            wantedPosVelocity = wantedPos + Mathr.QuaternionLookRotation(smoothVelocity) * Vector3.RelativeBottom * (distanceOffset + currentVehicleLongitude / 2f);

        // Smooth factor between two above cam positions
        smoothFixedVsVelocity = Mathr.Lerp(smoothFixedVsVelocity, fixedVsVelocity, fixedVsVelocitySpeed * realDelta);

        smoothIsRearGear = Mathr.Lerp(smoothIsRearGear, Mathr.Clamp01(veh.CurrentGear), generalMovementSmoothness);

        if(!isCycleOrByke)
        {
            tempSmoothVsVl = Mathr.Lerp(tempSmoothVsVl, Mathr.Clamp(veh.Speed / 2.3f, 0.025f, 1f), (fixedVsVelocitySpeed / 20f));
            smoothFixedVsVelocity = Mathr.Lerp(0f, smoothFixedVsVelocity, tempSmoothVsVl);
        }

        // Compute final camera position
        wantedPos = Vector3.Lerp(wantedPosFixed, wantedPosVelocity, smoothFixedVsVelocity);

        mainCamera.PointAt(veh.Position + (Vector3.WorldUp * (heightOffset + currentVehicleHeight)) + (veh.ForwardVector * lookFrontOffset));
        look = Quaternion.Euler(mainCamera.Rotation);
        currentPos = Vector3.Lerp(currentPos, wantedPos, cameraStickiness * smoothDelta);

        //var speedDelta = ((veh.Speed / 200f) + 0.5f);

        // Rotate the camera towards the velocity vector.
        var finalCamRotationSpeed = Mathr.Lerp(cameraRotationSpeedLowSpeed, cameraRotationSpeed, Mathr.Clamp01(veh.Speed / lowSpeedLimit));
        look = Mathr.QuaternionNLerp(currentRotation, look, finalCamRotationSpeed * realDelta);

        mainCamera.Position = currentPos + (Vector3.WorldUp * (heightOffset + currentVehicleHeight));
        mainCamera.Rotation = look.ToEulerAngles();

        mainCamera.IsActive = true;
        World.RenderingCamera = mainCamera;
    }

    private float getFixedVsVelocityFactor(Vehicle veh)
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
                return Mathr.Lerp(velocityInfluenceLowSpeed, velocityInfluence, Mathr.Clamp(veh.Speed / lowSpeedLimit, 0f, 1f));
            }
        }
    }

    public float getVehicleHeight(Vehicle veh)
    {
        return veh.Model.GetDimensions().Z;
    }

    public float getVehicleLongitude(Vehicle veh)
    {
        var longitude =  veh.Model.GetDimensions().Y;

        // bikes and motorbikes has too near camera, so bump distance by 0.5 game units
        if (veh.Model.IsBicycle || veh.Model.IsBike)
        {
            longitude += 0.5f;
        }

        return longitude;
    }


    private void onKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.NumPad2)
        {
            showDebugStats = !showDebugStats;
        }

        if (e.KeyCode == Keys.NumPad1)
        {
            customCamEnabled = !customCamEnabled;
        }
    }

    private void onKeyUp(object sender, KeyEventArgs e)
    {

    }

    private void ExitCustomCameraView()
    {
        if (Game.Player.Character.IsInVehicle())
        {
            int mode = Function.Call<int>(Hash.GET_FOLLOW_VEHICLE_CAM_VIEW_MODE);
            Function.Call(Hash.SET_FOLLOW_VEHICLE_CAM_VIEW_MODE, mode);
        }

        else if (Game.Player.Character.IsOnFoot)
        {
            int mode = Function.Call<int>(Hash.GET_FOLLOW_PED_CAM_VIEW_MODE);
            Function.Call(Hash.SET_FOLLOW_PED_CAM_VIEW_MODE, mode);
        }

        //Script.Wait(10);
        //Function.Call(Hash.DESTROY_ALL_CAMS, true);
        mainCamera.IsActive = false;
        mainCamera.Destroy();
        mainCamera = null;
        World.RenderingCamera = null;
        World.RenderingCamera = backupCamera;
        backupCamera.IsActive = true;
        Function.Call(Hash.DESTROY_ALL_CAMS, true);

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
        return QuaternionFromEuler(Vector3.Lerp(start.ToEulerAngles(), end.ToEulerAngles(), ammount).Normalized);
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

    public static float Lerp(float value1, float value2, float amount)
    {
        return value1 + (value2 - value1) * amount;
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
}