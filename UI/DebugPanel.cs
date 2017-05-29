using GTA;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;

namespace CustomCameraVScript
{
    public class DebugPanel
    {
        public delegate object watchDelegate();

        public Dictionary<string, watchDelegate> watchedVariables = new Dictionary<string, watchDelegate>();
        public Point startPoint = new Point(10, 10);
        public int distanceBetweenLines = 15;
        public float fontSize = 0.3f;

        public string header = "CustomCameraV debug";

        public void Draw()
        {
            int x = startPoint.X;
            int y = startPoint.Y;

            DrawInfo(header, x, y, fontSize);

            y += distanceBetweenLines;

            foreach (var entry in watchedVariables)
            {
                DrawInfo(entry.Key + (entry.Value == null ? "" : ": " + entry.Value.DynamicInvoke().ToString()), x, y, fontSize);

                y += distanceBetweenLines;
            }
        }

        protected void DrawInfo(string caption, int x, int y, float fontSize)
        {
            UIText text = new UIText(caption, new Point(x, y), fontSize);
            text.Draw();
        }

        public bool AddVar(string key, watchDelegate fn)
        {
            if (!watchedVariables.Keys.Contains(key))
                watchedVariables.Add(key, fn);
            else
                return false;

            return true;
        }

        public bool RemoveVar(string key)
        {
            if (watchedVariables.Keys.Contains(key))
                watchedVariables.Remove(key);
            else
                return false;

            return true;
        }

        public void AddRange(Dictionary<string, watchDelegate> range)
        {
            foreach (var pair in range)
            {
                AddVar(pair.Key, pair.Value);
            }
        }

        public void RemoveRange(Dictionary<string, watchDelegate> range)
        {
            foreach (var pair in range)
            {
                RemoveVar(pair.Key);
            }
        }
    }
}
