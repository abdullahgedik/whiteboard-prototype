using System;
using UnityEngine;

namespace Whiteboard
{
    /// <summary>Built-in colors plus user slots that persist between sessions.</summary>
    public sealed class Palette
    {
        public const int CustomSlotCount = 12;
        const string PrefsKey = "whiteboard.customColors";

        public static readonly Color32[] Defaults =
        {
            new Color32(0x1E, 0x1E, 0x1E, 0xFF), // black
            new Color32(0x8A, 0x8F, 0x98, 0xFF), // gray
            new Color32(0xFF, 0xFF, 0xFF, 0xFF), // white
            new Color32(0x6D, 0x4C, 0x41, 0xFF), // brown
            new Color32(0xE5, 0x39, 0x35, 0xFF), // red
            new Color32(0xFB, 0x8C, 0x00, 0xFF), // orange
            new Color32(0xFD, 0xD8, 0x35, 0xFF), // yellow
            new Color32(0x43, 0xA0, 0x47, 0xFF), // green
            new Color32(0x00, 0xAC, 0xC1, 0xFF), // teal
            new Color32(0x1E, 0x88, 0xE5, 0xFF), // blue
            new Color32(0x8E, 0x24, 0xAA, 0xFF), // purple
            new Color32(0xEC, 0x40, 0x7A, 0xFF), // pink
        };

        readonly Color32?[] custom = new Color32?[CustomSlotCount];

        public event Action Changed;

        public Color32? GetCustom(int slot) => custom[slot];

        public void SetCustom(int slot, Color32 color)
        {
            custom[slot] = color;
            Save();
        }

        public void ClearCustom(int slot)
        {
            custom[slot] = null;
            Save();
        }

        /// <summary>Stores the color in the first free slot; when all slots are used the
        /// oldest (first) one is dropped.</summary>
        public void AddCustom(Color32 color)
        {
            foreach (var existing in custom)
                if (existing.HasValue && Same(existing.Value, color)) return;

            int slot = Array.FindIndex(custom, c => !c.HasValue);
            if (slot < 0)
            {
                Array.Copy(custom, 1, custom, 0, CustomSlotCount - 1);
                slot = CustomSlotCount - 1;
            }
            SetCustom(slot, color);
        }

        public static Palette Load()
        {
            var palette = new Palette();
            string[] entries = PlayerPrefs.GetString(PrefsKey, string.Empty).Split(';');
            for (int i = 0; i < CustomSlotCount && i < entries.Length; i++)
            {
                if (entries[i].Length > 0 && ColorUtility.TryParseHtmlString("#" + entries[i], out var c))
                    palette.custom[i] = (Color32)c;
            }
            return palette;
        }

        void Save()
        {
            var entries = new string[CustomSlotCount];
            for (int i = 0; i < CustomSlotCount; i++)
                entries[i] = custom[i].HasValue ? ColorUtility.ToHtmlStringRGB(custom[i].Value) : string.Empty;
            PlayerPrefs.SetString(PrefsKey, string.Join(";", entries));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
    }
}
