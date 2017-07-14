using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CustomCameraVScript;
using Glide;
using GTA;
using GTA.UI;

namespace CustomCameraVScript
{
    public static class UIex
    {
        //public static List<Notification> notifications = new List<Notification>();

        public static Tweener tweener = null;

        public static void Notify(string text, float time = 2.0f)
        {
            var notification = GTA.UI.Screen.ShowNotification(text);

            tweener.Timer(0f, time).OnComplete(() => { notification.Hide(); });
        }
    }
}
