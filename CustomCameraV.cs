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

    private float currentVehicleHeight;
    private float currentVehicleLongitude;

    public float fov = 65f;
    public float distanceOffset = 1f;

    public float camHeightOffset = 0.2f;
    public float lookFrontOffset = -0.35f;

    private float targetFPS = 30f;
    private bool customCamEnabled = true;

    // How fast lerp between rear and velocity cam position when neccesary
    private float fixedVsVelocitySpeed = 0.8f;

    // When vehicle is on ground and driving, influence of fixed camera vs camera looking at velocity
    public float velocityInfluence = 0.2f;

    // If car speed is below this value, then the camera will default to looking forwards.
    public float rotationThreshold = 1.9f;

    // How closely the camera follows the car's position. The lower the value, the more the camera will lag behind.
    public float cameraStickiness = 15.0f;

    // How closely the camera matches the car's rotation. The lower the value, the smoother the camera rotations, but too much results in not being able to see where you're going.
    public float cameraRotationSpeed = 4.75f;

    public CustomCameraV()
    {
        this.Tick += OnTick;
        this.KeyUp += onKeyUp;
        this.KeyDown += onKeyDown;
        this.Aborted += onAborted;

        lookTargetOffset = new Vector3(0, lookFrontOffset, camHeightOffset);
        camTargetOffset = new Vector3(0f, 0f, camHeightOffset);
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
                }
                else
                {
                    UpdateCamera(player, veh);
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

    private bool isVehicleSuitableForCustomCamera(Vehicle veh)
    {
        return veh.ClassType != VehicleClass.Trains && veh.ClassType != VehicleClass.Planes && veh.ClassType != VehicleClass.Helicopters && veh.ClassType != VehicleClass.Boats;
    }

    private void SetupCamera(Ped player, Vehicle veh)
    {
        camSet = true;
        currentVehicleHeight = getVehicleHeight(veh);
        currentVehicleLongitude = getVehicleLongitude(veh);

        mainCamera = World.CreateCamera(veh.Position, veh.Rotation, fov);
        currentPos = veh.Position - (veh.ForwardVector * (currentVehicleLongitude + distanceOffset)) + (Vector3.WorldUp * (camHeightOffset + currentVehicleHeight));
        currentRotation = veh.Quaternion;
    }

    private void UpdateCamera(Ped player, Vehicle veh)
    {
        currentRotation = Extensions.QuaternionFromEuler(mainCamera.Rotation);

        //smooth out delta time to avoid cam stuttering
        targetFPS = Lerp(targetFPS, Game.FPS, 0.8f);
        float delta = 1f / targetFPS;

        var boneIndex = Function.Call<int>(Hash._GET_ENTITY_BONE_INDEX, veh.Handle, "chassis_dummy");
        var bonePos = Function.Call<Vector3>(Hash._GET_ENTITY_BONE_COORDS, veh.Handle, boneIndex);
        camPos = Function.Call<Vector3>(Hash.GET_OFFSET_FROM_ENTITY_GIVEN_WORLD_COORDS, veh.Handle, bonePos.X, bonePos.Y, bonePos.Z);

        Quaternion look;
        var wantedPos = veh.Position;

        // should camera be attached to vehicle's back or back his velocity
        var fixedVsVelocity = getFixedVsVelocityFactor(veh);

        // Compute camera position rear the vechicle
        var wantedPosFixed = wantedPos - veh.Quaternion * Vector3.RelativeFront * (distanceOffset + currentVehicleLongitude );

        // Compute camera postition rear the direction of the vehcle
        var wantedPosVelocity = wantedPos + Extensions.QuaternionLookRotation(veh.Velocity.Normalized) * Vector3.RelativeBottom * (distanceOffset + currentVehicleLongitude);

        // Smooth factor between two above cam positions
        smoothFixedVsVelocity = Lerp(smoothFixedVsVelocity, fixedVsVelocity, fixedVsVelocitySpeed * delta);

        // Compute final camera position
        wantedPos = Vector3.Lerp(wantedPosFixed, wantedPosVelocity, smoothFixedVsVelocity);

        mainCamera.PointAt(veh.Position + (Vector3.WorldUp * (camHeightOffset + currentVehicleHeight)) + (veh.ForwardVector * lookFrontOffset));
        look = Quaternion.Euler(mainCamera.Rotation);
        currentPos = Vector3.Lerp(currentPos, wantedPos, cameraStickiness * delta);

        // make camera rotation same accross speed
        var speedDelta = ((veh.Speed / 100f) + 1);
        // Rotate the camera towards the velocity vector.
        look = Quaternion.Slerp(currentRotation, look, cameraRotationSpeed * speedDelta * delta);

        mainCamera.Position = currentPos + (Vector3.WorldUp * (camHeightOffset + currentVehicleHeight));
        mainCamera.Rotation = look.ToEulerAngles();

        World.RenderingCamera = mainCamera;
    }

    private float getFixedVsVelocityFactor(Vehicle veh)
    {
        // If the car isn't moving, default to looking forwards
        if (veh.Velocity.Magnitude() < rotationThreshold)
        {
            return 0f;
        }
        else
        {
            // for bikes, always look at velocity
            if (veh.ClassType == VehicleClass.Cycles || veh.ClassType == VehicleClass.Motorcycles)
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
                return velocityInfluence;
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

        // bikes and motorbikes has too near camera, so bump distance by 1.52 game units
        if(veh.Model.IsBicycle || veh.Model.IsBike)
        {
            longitude += 1.52f;
        }

        return longitude;
    }


    private void onKeyDown(object sender, KeyEventArgs e)
    {

    }

    private void onKeyUp(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.NumPad1)
        {
            customCamEnabled = !customCamEnabled;
        }
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

        Script.Wait(100);
        Function.Call(Hash.DESTROY_ALL_CAMS, true);
        World.RenderingCamera = null;
        World.RenderingCamera = backupCamera;
        Function.Call(Hash.DESTROY_ALL_CAMS, true);

        camSet = false;
    }

    protected override void Dispose(bool A_0)
    {
        World.RenderingCamera = null;
        base.Dispose(A_0);
    }

    public float Lerp(float value1, float value2, float amount)
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
}

public static class Extensions
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

}