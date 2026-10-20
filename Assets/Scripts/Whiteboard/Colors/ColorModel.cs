using System;
using UnityEngine;

namespace Whiteboard
{
    /// <summary>The active drawing color, kept in both RGB and HSV so the picker
    /// doesn't lose its hue when a gray, black or white is selected.</summary>
    public sealed class ColorModel
    {
        public Color32 Current { get; private set; }
        public float Hue { get; private set; }
        public float Saturation { get; private set; }
        public float Value { get; private set; }

        public event Action Changed;

        public ColorModel(Color32 initial)
        {
            Assign(initial);
        }

        public void SetColor(Color32 color)
        {
            Assign(color);
            Changed?.Invoke();
        }

        public void SetHsv(float hue, float saturation, float value)
        {
            Hue = Mathf.Clamp01(hue);
            Saturation = Mathf.Clamp01(saturation);
            Value = Mathf.Clamp01(value);
            Current = Color.HSVToRGB(Hue, Saturation, Value);
            Changed?.Invoke();
        }

        void Assign(Color32 color)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            if (v > 0f && s > 0f) Hue = h;
            if (v > 0f) Saturation = s;
            Value = v;
            Current = color;
        }
    }
}
