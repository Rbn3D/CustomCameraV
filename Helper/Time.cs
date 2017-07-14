using System;
using CustomCameraVScript;
using GTA.Native;

namespace CustomCameraVScript
{
    public static class Time
    {
        public static float getDeltaTime()
        {
            return Function.Call<float>(GTA.Hash.TIMESTEP);
        }
    }
}
