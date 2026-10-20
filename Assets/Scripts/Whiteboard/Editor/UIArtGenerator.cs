using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Whiteboard.EditorTools
{
    /// <summary>Sprites used by the whiteboard UI.</summary>
    public sealed class UIArt
    {
        public Sprite Panel, Button, ButtonSmall, Tooltip, Slider, Shadow, Circle, Ring;
        public Sprite Pen, Eraser, Bucket, Undo, Redo, Grid, Trash, StrokeErase, AreaErase, Plus;
    }

    /// <summary>
    /// Renders the UI sprites as PNG assets and configures their importers
    /// (uncompressed, mipmapped icons, 9-slice borders), so they stay sharp at any UI scale.
    /// </summary>
    public static class UIArtGenerator
    {
        public const int ShadowBlur = 12;
        public const int PanelRadius = 14;

        // Icons are drawn on a 24-unit grid and rendered at 4x, so at the 24px display size
        // the GPU samples an exact mip level; on 2x-4x displays it uses the full texture.
        const int IconTexture = 96;
        const float IconGrid = 24f;
        const float IconStroke = 2f;

        public static UIArt Generate(string folder)
        {
            EnsureFolder(folder);
            return new UIArt
            {
                Panel = Rounded(folder, "panel", PanelRadius),
                Button = Rounded(folder, "button", 10),
                ButtonSmall = Rounded(folder, "button_small", 8),
                Tooltip = Rounded(folder, "tooltip", 6),
                Slider = Rounded(folder, "slider_track", 3),
                Shadow = Shadow(folder),
                Circle = Radial(folder, "circle", (d, r) => 0.5f - (d - r)),
                Ring = Radial(folder, "ring", (d, r) => 0.5f - (Mathf.Abs(d - (r - 5f)) - 5f)),

                Pen = Icon(folder, "pen", s =>
                {
                    var c0 = new Vector2(5.5f, 18.5f);
                    var c1 = new Vector2(18f, 6f);
                    var dir = (c1 - c0).normalized;
                    var side = new Vector2(-dir.y, dir.x) * 2.6f;
                    var tip = c0 - dir * 3.4f;
                    s.Polyline(true, tip, c0 + side, c1 + side, c1 - side, c0 - side);
                    var band = c1 - dir * 3.4f;
                    s.Line(band + side, band - side);
                }),
                Eraser = Icon(folder, "eraser", s =>
                {
                    s.Polyline(true,
                        new Vector2(7f, 21f), new Vector2(2.7f, 16.7f), new Vector2(2.7f, 13.3f), new Vector2(12.3f, 3.7f),
                        new Vector2(15.7f, 3.7f), new Vector2(21.3f, 9.3f), new Vector2(21.3f, 12.7f), new Vector2(13f, 21f));
                    s.Line(new Vector2(22f, 21f), new Vector2(7f, 21f));
                    s.Line(new Vector2(5f, 11f), new Vector2(14f, 20f));
                }),
                Bucket = Icon(folder, "bucket", s =>
                {
                    s.Polyline(true,
                        new Vector2(19f, 11f), new Vector2(11f, 3f), new Vector2(2.4f, 11.6f), new Vector2(2.4f, 14.4f),
                        new Vector2(7.6f, 19.6f), new Vector2(10.4f, 19.6f));
                    s.Line(new Vector2(5f, 2f), new Vector2(10f, 7f));
                    s.Line(new Vector2(2f, 13f), new Vector2(17f, 13f));
                    s.Arc(new Vector2(20f, 20f), 2f, 0f, 180f);
                    s.Polyline(false, new Vector2(18f, 20f), new Vector2(20f, 16f), new Vector2(22f, 20f));
                }),
                Undo = Icon(folder, "undo", DrawUndo),
                Redo = Icon(folder, "redo", s =>
                {
                    s.MirrorX = true;
                    DrawUndo(s);
                }),
                Grid = Icon(folder, "grid", s =>
                {
                    s.RoundedRect(3f, 3f, 21f, 21f, 2.5f);
                    s.Line(new Vector2(9f, 3f), new Vector2(9f, 21f));
                    s.Line(new Vector2(15f, 3f), new Vector2(15f, 21f));
                    s.Line(new Vector2(3f, 9f), new Vector2(21f, 9f));
                    s.Line(new Vector2(3f, 15f), new Vector2(21f, 15f));
                }),
                Trash = Icon(folder, "trash", s =>
                {
                    s.Line(new Vector2(3f, 6f), new Vector2(21f, 6f));
                    s.Line(new Vector2(19f, 6f), new Vector2(19f, 20f));
                    s.Arc(new Vector2(17f, 20f), 2f, 0f, 90f);
                    s.Line(new Vector2(17f, 22f), new Vector2(7f, 22f));
                    s.Arc(new Vector2(7f, 20f), 2f, 90f, 180f);
                    s.Line(new Vector2(5f, 20f), new Vector2(5f, 6f));
                    s.Line(new Vector2(8f, 6f), new Vector2(8f, 4f));
                    s.Arc(new Vector2(10f, 4f), 2f, 180f, 270f);
                    s.Line(new Vector2(10f, 2f), new Vector2(14f, 2f));
                    s.Arc(new Vector2(14f, 4f), 2f, 270f, 360f);
                    s.Line(new Vector2(16f, 4f), new Vector2(16f, 6f));
                    s.Line(new Vector2(10f, 11f), new Vector2(10f, 17f));
                    s.Line(new Vector2(14f, 11f), new Vector2(14f, 17f));
                }),
                StrokeErase = Icon(folder, "stroke_erase", s =>
                {
                    var points = new Vector2[25];
                    for (int i = 0; i < points.Length; i++)
                    {
                        float t = i / (points.Length - 1f);
                        points[i] = new Vector2(3f + 18f * t, 12f - 5f * Mathf.Sin(t * Mathf.PI * 2f));
                    }
                    s.Polyline(false, points);
                }),
                AreaErase = Icon(folder, "area_erase", s =>
                {
                    for (int k = 0; k < 8; k++) s.Arc(new Vector2(12f, 12f), 8.5f, k * 45f, k * 45f + 25f);
                    s.Dot(new Vector2(12f, 12f), 1.6f);
                }),
                Plus = Icon(folder, "plus", s =>
                {
                    s.Line(new Vector2(12f, 5f), new Vector2(12f, 19f));
                    s.Line(new Vector2(5f, 12f), new Vector2(19f, 12f));
                }),
            };
        }

        static void DrawUndo(Shape s)
        {
            s.Polyline(false, new Vector2(9f, 14f), new Vector2(4f, 9f), new Vector2(9f, 4f));
            s.Line(new Vector2(4f, 9f), new Vector2(14.5f, 9f));
            s.Arc(new Vector2(14.5f, 14.5f), 5.5f, -90f, 90f);
            s.Line(new Vector2(14.5f, 20f), new Vector2(11f, 20f));
        }

        #region Sprite renderers

        static Sprite Icon(string folder, string name, Action<Shape> draw)
        {
            var shape = new Shape();
            draw(shape);
            var px = shape.Render(IconTexture);
            return Save(folder, "icon_" + name, IconTexture, IconTexture, px, 100f, Vector4.zero, mipmaps: true);
        }

        /// <summary>9-sliced rounded rectangle at 2x resolution (pixels per unit 200).</summary>
        static Sprite Rounded(string folder, string name, int radius)
        {
            const int scale = 2;
            int r = radius * scale;
            int size = r * 2 + 4;
            float half = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBoxDistance(new Vector2(x + 0.5f - half, y + 0.5f - half), half - 1f, half - 1f, r);
                px[y * size + x] = new Color32(255, 255, 255, ToByte(0.5f - d));
            }
            float border = r + 2;
            return Save(folder, name, size, size, px, 100f * scale, new Vector4(border, border, border, border), mipmaps: false);
        }

        static Sprite Shadow(string folder)
        {
            int size = 2 * (PanelRadius + ShadowBlur) + 4;
            float half = size * 0.5f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = RoundedBoxDistance(new Vector2(x + 0.5f - half, y + 0.5f - half),
                    half - ShadowBlur - 2f, half - ShadowBlur - 2f, PanelRadius);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-ShadowBlur * 0.5f, ShadowBlur, d));
                px[y * size + x] = new Color32(255, 255, 255, ToByte(a * a));
            }
            float border = PanelRadius + ShadowBlur + 2;
            return Save(folder, "panel_shadow", size, size, px, 100f, new Vector4(border, border, border, border), mipmaps: false);
        }

        static Sprite Radial(string folder, string name, Func<float, float, float> alpha)
        {
            const int size = 128;
            float half = size * 0.5f;
            float radius = half - 2f;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - half, y + 0.5f - half).magnitude;
                px[y * size + x] = new Color32(255, 255, 255, ToByte(alpha(d, radius)));
            }
            return Save(folder, name, size, size, px, 100f, Vector4.zero, mipmaps: true);
        }

        #endregion

        #region Asset IO

        static Sprite Save(string folder, string name, int width, int height, Color32[] pixels,
            float pixelsPerUnit, Vector4 border, bool mipmaps)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            string path = folder + "/" + name + ".png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.spriteBorder = border;
            importer.mipmapEnabled = mipmaps;
            importer.filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }

        static float RoundedBoxDistance(Vector2 p, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(p.x) - halfWidth + radius;
            float qy = Mathf.Abs(p.y) - halfHeight + radius;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        static byte ToByte(float value01) => (byte)(Mathf.Clamp01(value01) * 255f + 0.5f);

        #endregion

        /// <summary>
        /// Line icon described on a 24×24 grid with y pointing down (SVG convention),
        /// rendered as anti-aliased white strokes so it can be tinted with Image.color.
        /// </summary>
        sealed class Shape
        {
            readonly List<(Vector2 a, Vector2 b)> segments = new List<(Vector2, Vector2)>();
            readonly List<(Vector2 center, float radius)> dots = new List<(Vector2, float)>();

            public bool MirrorX;

            Vector2 Map(Vector2 p) => MirrorX ? new Vector2(IconGrid - p.x, p.y) : p;

            public void Line(Vector2 a, Vector2 b) => segments.Add((Map(a), Map(b)));

            public void Polyline(bool closed, params Vector2[] points)
            {
                for (int i = 1; i < points.Length; i++) Line(points[i - 1], points[i]);
                if (closed) Line(points[points.Length - 1], points[0]);
            }

            /// <summary>Angles in degrees; with y down, 90° points down.</summary>
            public void Arc(Vector2 center, float radius, float fromDeg, float toDeg)
            {
                int steps = Mathf.Max(6, Mathf.CeilToInt(Mathf.Abs(toDeg - fromDeg) / 5f));
                var previous = center + Polar(radius, fromDeg);
                for (int i = 1; i <= steps; i++)
                {
                    var next = center + Polar(radius, Mathf.Lerp(fromDeg, toDeg, i / (float)steps));
                    Line(previous, next);
                    previous = next;
                }
            }

            public void RoundedRect(float x0, float y0, float x1, float y1, float r)
            {
                Line(new Vector2(x0 + r, y0), new Vector2(x1 - r, y0));
                Line(new Vector2(x1, y0 + r), new Vector2(x1, y1 - r));
                Line(new Vector2(x1 - r, y1), new Vector2(x0 + r, y1));
                Line(new Vector2(x0, y1 - r), new Vector2(x0, y0 + r));
                Arc(new Vector2(x1 - r, y0 + r), r, 270f, 360f);
                Arc(new Vector2(x1 - r, y1 - r), r, 0f, 90f);
                Arc(new Vector2(x0 + r, y1 - r), r, 90f, 180f);
                Arc(new Vector2(x0 + r, y0 + r), r, 180f, 270f);
            }

            public void Dot(Vector2 center, float radius) => dots.Add((Map(center), radius));

            static Vector2 Polar(float radius, float degrees)
            {
                float rad = degrees * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
            }

            public Color32[] Render(int size)
            {
                float pixelsPerUnit = size / IconGrid;
                float halfStroke = IconStroke * 0.5f;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Texture rows go up, icon grid goes down.
                    var p = new Vector2((x + 0.5f) / pixelsPerUnit, IconGrid - (y + 0.5f) / pixelsPerUnit);
                    float alpha = 0f;
                    foreach (var (a, b) in segments)
                        alpha = Mathf.Max(alpha, (halfStroke - DistanceToSegment(p, a, b)) * pixelsPerUnit + 0.5f);
                    foreach (var (center, radius) in dots)
                        alpha = Mathf.Max(alpha, (radius - Vector2.Distance(p, center)) * pixelsPerUnit + 0.5f);
                    px[y * size + x] = new Color32(255, 255, 255, ToByte(alpha));
                }
                return px;
            }

            static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
            {
                var ab = b - a;
                float lengthSq = ab.sqrMagnitude;
                float t = lengthSq > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
                return Vector2.Distance(p, a + ab * t);
            }
        }
    }
}
