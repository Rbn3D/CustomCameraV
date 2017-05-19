using System;
using GTA;
using CustomCameraVScript;

namespace CustomCameraVScript
{
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
