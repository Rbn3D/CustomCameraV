using GTA;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;


public class DebugPanel
{
    public delegate object watchDelegate();

    public Dictionary<string, watchDelegate> watchedVariables = new Dictionary<string, watchDelegate>();
    public Point startPoint = new Point(10, 10);
    public int distanceBetweenLines = 15;
    public float fontSize = 0.3f;

    public string header = "CustomCameraV debug ( press numpad2 to hide )";

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
}
