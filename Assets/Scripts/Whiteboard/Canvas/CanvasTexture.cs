using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Whiteboard
{
    /// <summary>
    /// GPU side of the canvas. Tracks which pixels changed since the last upload and, when
    /// that area is small, sends only that region (through a staging texture and
    /// Graphics.CopyTexture) instead of re-uploading the whole canvas every frame.
    /// </summary>
    public sealed class CanvasTexture : IDisposable
    {
        // Regions larger than this fraction of the canvas are uploaded in full.
        const float PartialUploadMaxFraction = 0.33f;

        readonly bool regionCopySupported = (SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) != 0;
        Texture2D staging;
        int width, height;
        bool fullDirty;
        int dirtyXMin, dirtyYMin, dirtyXMax, dirtyYMax;
        bool hasDirty;

        public Texture2D Texture { get; private set; }

        public void Resize(int newWidth, int newHeight)
        {
            width = newWidth;
            height = newHeight;
            if (Texture != null) Object.Destroy(Texture);
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Whiteboard Canvas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            MarkAllDirty();
        }

        public void MarkAllDirty() => fullDirty = true;

        /// <summary>Marks an inclusive pixel rectangle as changed.</summary>
        public void MarkDirty(int xMin, int yMin, int xMax, int yMax)
        {
            if (fullDirty) return;
            if (!hasDirty)
            {
                dirtyXMin = xMin;
                dirtyYMin = yMin;
                dirtyXMax = xMax;
                dirtyYMax = yMax;
                hasDirty = true;
                return;
            }
            if (xMin < dirtyXMin) dirtyXMin = xMin;
            if (yMin < dirtyYMin) dirtyYMin = yMin;
            if (xMax > dirtyXMax) dirtyXMax = xMax;
            if (yMax > dirtyYMax) dirtyYMax = yMax;
        }

        public void Upload(Color32[] pixels)
        {
            if (!fullDirty && !hasDirty) return;

            int w = dirtyXMax - dirtyXMin + 1;
            int h = dirtyYMax - dirtyYMin + 1;
            bool partial = !fullDirty && regionCopySupported
                && (long)w * h <= (long)(width * (long)height * PartialUploadMaxFraction);

            if (partial) UploadRegion(pixels, dirtyXMin, dirtyYMin, w, h);
            else
            {
                Texture.SetPixelData(pixels, 0);
                Texture.Apply(false);
            }
            fullDirty = false;
            hasDirty = false;
        }

        void UploadRegion(Color32[] pixels, int x, int y, int w, int h)
        {
            if (staging == null || staging.width < w || staging.height < h)
            {
                if (staging != null) Object.Destroy(staging);
                staging = new Texture2D(
                    Mathf.NextPowerOfTwo(Mathf.Max(w, 64)),
                    Mathf.NextPowerOfTwo(Mathf.Max(h, 64)),
                    TextureFormat.RGBA32, false)
                {
                    name = "Whiteboard Canvas Staging",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
            }

            NativeArray<Color32> data = staging.GetPixelData<Color32>(0);
            int stride = staging.width;
            for (int row = 0; row < h; row++)
                NativeArray<Color32>.Copy(pixels, (y + row) * width + x, data, row * stride, w);
            staging.Apply(false);
            Graphics.CopyTexture(staging, 0, 0, 0, 0, w, h, Texture, 0, 0, x, y);
        }

        public void Dispose()
        {
            if (Texture != null) Object.Destroy(Texture);
            if (staging != null) Object.Destroy(staging);
            Texture = null;
            staging = null;
        }
    }
}
