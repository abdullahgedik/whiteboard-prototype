using UnityEngine;
using UnityEngine.UI;

namespace Whiteboard
{
    /// <summary>Colors used for state-driven visuals (active tool, hover, disabled).</summary>
    public static class Theme
    {
        public static readonly Color Panel = Color.white;
        public static readonly Color Icon = new Color(0.20f, 0.21f, 0.24f);
        public static readonly Color Accent = new Color(0.12f, 0.45f, 0.96f);
        public static readonly Color AccentSoft = new Color(0.89f, 0.93f, 1f);
        public static readonly Color AccentSoftHover = new Color(0.84f, 0.90f, 1f);
        public static readonly Color Hover = new Color(0.94f, 0.95f, 0.97f);
        public static readonly Color Pressed = new Color(0.88f, 0.89f, 0.92f);
        public static readonly Color Transparent = new Color(1f, 1f, 1f, 0f);
        public static readonly Color Divider = new Color(0.87f, 0.88f, 0.90f);
        public static readonly Color SwatchBorder = new Color(0f, 0f, 0f, 0.14f);
        public static readonly Color EmptySlotBorder = new Color(0f, 0f, 0f, 0.25f);
        public static readonly Color TextMuted = new Color(0.46f, 0.48f, 0.53f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.16f);
        public static readonly Color TooltipBackground = new Color(0.13f, 0.14f, 0.16f, 0.95f);

        public static void SetButtonColors(Selectable selectable, Color normal, Color hover, Color pressed)
        {
            var colors = selectable.colors;
            colors.normalColor = normal;
            colors.highlightedColor = hover;
            colors.pressedColor = pressed;
            colors.selectedColor = normal;
            colors.disabledColor = normal;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            selectable.colors = colors;
        }
    }

    public static class TextureUtil
    {
        public static Texture2D Create(int width, int height, Color32[] pixels, FilterMode filter, TextureWrapMode wrap)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = filter,
                wrapMode = wrap
            };
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        public static byte ToByte(float value01) => (byte)(Mathf.Clamp01(value01) * 255f + 0.5f);
    }
}
