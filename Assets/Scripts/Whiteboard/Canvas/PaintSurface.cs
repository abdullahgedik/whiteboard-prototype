using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Whiteboard
{
    /// <summary>
    /// CPU raster canvas backed by a list of drawing operations.
    /// Pixels are straight-alpha RGBA in screen space (origin bottom-left), so a
    /// screen position maps directly to a pixel. Alongside the colors, every pixel
    /// remembers which operation drew it on top; the stroke eraser uses that to find
    /// what is under the cursor.
    /// </summary>
    public sealed class PaintSurface : IDisposable
    {
        const float MinPointSpacing = 1f;
        const byte OwnershipThreshold = 128;

        readonly List<CanvasOp> ops = new List<CanvasOp>();
        readonly Stack<HistoryEntry> undoStack = new Stack<HistoryEntry>();
        readonly Stack<HistoryEntry> redoStack = new Stack<HistoryEntry>();
        readonly HashSet<int> hitIds = new HashSet<int>();
        readonly List<int> touched = new List<int>();
        readonly Stack<int> fillStack = new Stack<int>();

        Color32[] pixels;
        int[] owners;
        // Per-stroke scratch: max coverage reached so far and the pixel before the stroke
        // touched it. Keeps overlapping segments of one stroke from darkening the edges.
        byte[] coverage;
        Color32[] basePixels;
        byte[] regionMask;

        int nextId = 1;
        StrokeOp activeStroke;
        RemoveOpsEntry activeRemoval;
        bool dirty;

        public int Width { get; private set; }
        public int Height { get; private set; }
        public Texture2D Texture { get; private set; }

        public bool IsBusy => activeStroke != null || activeRemoval != null;
        public bool CanUndo => undoStack.Count > 0 && !IsBusy;
        public bool CanRedo => redoStack.Count > 0 && !IsBusy;
        public bool CanClear => ops.Count > 0 && !IsBusy;

        public event Action HistoryChanged;

        public PaintSurface(int width, int height)
        {
            Allocate(width, height);
            dirty = true;
        }

        public void Dispose()
        {
            if (Texture != null) Object.Destroy(Texture);
            Texture = null;
        }

        public void Resize(int width, int height)
        {
            if (width == Width && height == Height) return;
            EndStroke();
            EndObjectErase();
            Allocate(width, height);
            Replay();
        }

        /// <summary>Pushes pending pixel changes to the GPU. Call once per frame.</summary>
        public void Upload()
        {
            if (!dirty) return;
            Texture.SetPixelData(pixels, 0);
            Texture.Apply(false);
            dirty = false;
        }

        #region Strokes (pen and area eraser)

        public void BeginStroke(StrokeKind kind, Color32 color, float radius, Vector2 position)
        {
            EndStroke();
            activeStroke = new StrokeOp(nextId++, kind, color, Mathf.Max(0.5f, radius));
            activeStroke.Points.Add(position);
            StampSegment(activeStroke, position, position);
            HistoryChanged?.Invoke();
        }

        public void ExtendStroke(Vector2 position)
        {
            if (activeStroke == null) return;
            var points = activeStroke.Points;
            var last = points[points.Count - 1];
            if ((position - last).sqrMagnitude < MinPointSpacing * MinPointSpacing) return;
            points.Add(position);
            StampSegment(activeStroke, last, position);
        }

        public void EndStroke()
        {
            if (activeStroke == null) return;
            ResetStrokeScratch();
            var stroke = activeStroke;
            activeStroke = null;
            ops.Add(stroke);
            Push(new AddOpEntry(stroke));
        }

        void StampSegment(StrokeOp stroke, Vector2 a, Vector2 b)
        {
            float radius = stroke.Radius;
            float reach = radius + 1f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach));
            int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach));
            if (minX > maxX || minY > maxY) return;

            float abX = b.x - a.x, abY = b.y - a.y;
            float abLengthSq = abX * abX + abY * abY;
            bool erase = stroke.Kind == StrokeKind.Erase;

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                int row = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f;
                    float t = abLengthSq > 0f ? Mathf.Clamp01(((px - a.x) * abX + (py - a.y) * abY) / abLengthSq) : 0f;
                    float dx = px - (a.x + abX * t);
                    float dy = py - (a.y + abY * t);
                    float cov = radius + 0.5f - Mathf.Sqrt(dx * dx + dy * dy);
                    if (cov <= 0f) continue;

                    byte cov8 = cov >= 1f ? (byte)255 : (byte)(cov * 255f + 0.5f);
                    int i = row + x;
                    byte previous = coverage[i];
                    if (cov8 <= previous) continue;
                    if (previous == 0)
                    {
                        basePixels[i] = pixels[i];
                        touched.Add(i);
                    }
                    coverage[i] = cov8;

                    float c = cov8 / 255f;
                    if (erase)
                    {
                        var p = basePixels[i];
                        p.a = (byte)(p.a * (1f - c) + 0.5f);
                        pixels[i] = p;
                        if (p.a < OwnershipThreshold) owners[i] = 0;
                    }
                    else
                    {
                        pixels[i] = Blend(basePixels[i], stroke.Color, c);
                        if (cov8 >= OwnershipThreshold) owners[i] = stroke.Id;
                    }
                }
            }
            dirty = true;
        }

        void ResetStrokeScratch()
        {
            foreach (int i in touched) coverage[i] = 0;
            touched.Clear();
        }

        #endregion

        #region Stroke eraser

        public void BeginObjectErase()
        {
            EndObjectErase();
            activeRemoval = new RemoveOpsEntry();
            HistoryChanged?.Invoke();
        }

        /// <summary>Removes every stroke or fill visible under the capsule a→b.</summary>
        public void EraseObjects(Vector2 a, Vector2 b, float radius)
        {
            if (activeRemoval == null) return;

            hitIds.Clear();
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius));
            int maxX = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius));
            int maxY = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius));
            float abX = b.x - a.x, abY = b.y - a.y;
            float abLengthSq = abX * abX + abY * abY;
            float radiusSq = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                float py = y + 0.5f;
                int row = y * Width;
                for (int x = minX; x <= maxX; x++)
                {
                    int id = owners[row + x];
                    if (id == 0) continue;
                    float px = x + 0.5f;
                    float t = abLengthSq > 0f ? Mathf.Clamp01(((px - a.x) * abX + (py - a.y) * abY) / abLengthSq) : 0f;
                    float dx = px - (a.x + abX * t);
                    float dy = py - (a.y + abY * t);
                    if (dx * dx + dy * dy <= radiusSq) hitIds.Add(id);
                }
            }
            if (hitIds.Count == 0) return;

            for (int i = ops.Count - 1; i >= 0; i--)
            {
                if (!hitIds.Contains(ops[i].Id)) continue;
                activeRemoval.Add(i, ops[i]);
                ops.RemoveAt(i);
            }
            Replay();
        }

        public void EndObjectErase()
        {
            if (activeRemoval == null) return;
            var removal = activeRemoval;
            activeRemoval = null;
            if (removal.Count > 0) Push(removal);
            else HistoryChanged?.Invoke();
        }

        #endregion

        #region Bucket fill

        /// <summary>
        /// Flood fills the area connected to <paramref name="position"/> whose visible color
        /// is within <paramref name="tolerance"/> (0-255 per channel) of the clicked pixel.
        /// </summary>
        public bool Fill(Vector2 position, Color32 color, int tolerance)
        {
            if (IsBusy) return false;
            int sx = Mathf.FloorToInt(position.x), sy = Mathf.FloorToInt(position.y);
            if (sx < 0 || sy < 0 || sx >= Width || sy >= Height) return false;

            int seed = sy * Width + sx;
            if (SameColor(pixels[seed], color)) return false;
            var target = Flatten(pixels[seed]);

            var interior = new List<PixelRun>();
            fillStack.Clear();
            fillStack.Push(seed);
            while (fillStack.Count > 0)
            {
                int i = fillStack.Pop();
                if (regionMask[i] != 0 || !Matches(i, target, tolerance)) continue;

                int y = i / Width;
                int row = y * Width;
                int x0 = i - row, x1 = x0;
                while (x0 > 0 && regionMask[row + x0 - 1] == 0 && Matches(row + x0 - 1, target, tolerance)) x0--;
                while (x1 < Width - 1 && regionMask[row + x1 + 1] == 0 && Matches(row + x1 + 1, target, tolerance)) x1++;

                for (int x = x0; x <= x1; x++) regionMask[row + x] = 1;
                interior.Add(new PixelRun(y, x0, x1));

                if (y > 0) QueueSpans(row - Width, x0, x1, target, tolerance);
                if (y < Height - 1) QueueSpans(row + Width, x0, x1, target, tolerance);
            }

            // Grow the region by one pixel (8-connected) to get the edge ring.
            var edgeIndices = new List<int>();
            foreach (var run in interior)
            {
                int yMin = Mathf.Max(0, run.Y - 1), yMax = Mathf.Min(Height - 1, run.Y + 1);
                int xMin = Mathf.Max(0, run.X0 - 1), xMax = Mathf.Min(Width - 1, run.X1 + 1);
                for (int y = yMin; y <= yMax; y++)
                {
                    int row = y * Width;
                    for (int x = xMin; x <= xMax; x++)
                    {
                        int i = row + x;
                        if (regionMask[i] != 0) continue;
                        regionMask[i] = 2;
                        edgeIndices.Add(i);
                    }
                }
            }

            foreach (var run in interior)
                Array.Clear(regionMask, run.Y * Width + run.X0, run.X1 - run.X0 + 1);
            foreach (int i in edgeIndices) regionMask[i] = 0;

            edgeIndices.Sort();
            var fill = new FillOp(nextId++, color, interior.ToArray(), ToRuns(edgeIndices));
            RasterizeFill(fill);
            ops.Add(fill);
            Push(new AddOpEntry(fill));
            return true;
        }

        void QueueSpans(int row, int x0, int x1, Color32 target, int tolerance)
        {
            bool inSpan = false;
            for (int x = x0; x <= x1; x++)
            {
                int i = row + x;
                bool open = regionMask[i] == 0 && Matches(i, target, tolerance);
                if (open && !inSpan) fillStack.Push(i);
                inSpan = open;
            }
        }

        bool Matches(int i, Color32 target, int tolerance)
        {
            var c = Flatten(pixels[i]);
            return Mathf.Abs(c.r - target.r) <= tolerance
                && Mathf.Abs(c.g - target.g) <= tolerance
                && Mathf.Abs(c.b - target.b) <= tolerance;
        }

        PixelRun[] ToRuns(List<int> sortedIndices)
        {
            var runs = new List<PixelRun>();
            int count = sortedIndices.Count;
            int start = 0;
            for (int k = 1; k <= count; k++)
            {
                bool breaks = k == count
                    || sortedIndices[k] != sortedIndices[k - 1] + 1
                    || sortedIndices[k] / Width != sortedIndices[start] / Width;
                if (!breaks) continue;
                int first = sortedIndices[start];
                int y = first / Width;
                runs.Add(new PixelRun(y, first - y * Width, sortedIndices[k - 1] - y * Width));
                start = k;
            }
            return runs.ToArray();
        }

        void RasterizeFill(FillOp fill)
        {
            foreach (var run in fill.Interior)
            {
                if (run.Y >= Height || run.X0 >= Width) continue;
                int row = run.Y * Width;
                int x1 = Mathf.Min(run.X1, Width - 1);
                for (int x = run.X0; x <= x1; x++)
                {
                    pixels[row + x] = fill.Color;
                    owners[row + x] = fill.Id;
                }
            }
            foreach (var run in fill.Edge)
            {
                if (run.Y >= Height || run.X0 >= Width) continue;
                int row = run.Y * Width;
                int x1 = Mathf.Min(run.X1, Width - 1);
                for (int x = run.X0; x <= x1; x++)
                {
                    int i = row + x;
                    var top = pixels[i];
                    pixels[i] = Blend(fill.Color, top, 1f);
                    if (top.a < OwnershipThreshold) owners[i] = fill.Id;
                }
            }
            dirty = true;
        }

        #endregion

        #region History

        public void Undo()
        {
            if (!CanUndo) return;
            var entry = undoStack.Pop();
            entry.Undo(ops);
            redoStack.Push(entry);
            Replay();
            HistoryChanged?.Invoke();
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var entry = redoStack.Pop();
            entry.Redo(ops);
            undoStack.Push(entry);
            Replay();
            HistoryChanged?.Invoke();
        }

        public void Clear()
        {
            if (IsBusy || ops.Count == 0) return;
            var entry = new ClearEntry(ops);
            entry.Redo(ops);
            Push(entry);
            Replay();
        }

        void Push(HistoryEntry entry)
        {
            undoStack.Push(entry);
            redoStack.Clear();
            HistoryChanged?.Invoke();
        }

        abstract class HistoryEntry
        {
            public abstract void Undo(List<CanvasOp> ops);
            public abstract void Redo(List<CanvasOp> ops);
        }

        sealed class AddOpEntry : HistoryEntry
        {
            readonly CanvasOp op;
            public AddOpEntry(CanvasOp op) => this.op = op;
            public override void Undo(List<CanvasOp> ops) => ops.Remove(op);
            public override void Redo(List<CanvasOp> ops) => ops.Add(op);
        }

        sealed class RemoveOpsEntry : HistoryEntry
        {
            // In removal order; indices are valid at the moment each op was removed.
            readonly List<(int index, CanvasOp op)> removed = new List<(int, CanvasOp)>();
            public int Count => removed.Count;
            public void Add(int index, CanvasOp op) => removed.Add((index, op));

            public override void Undo(List<CanvasOp> ops)
            {
                for (int k = removed.Count - 1; k >= 0; k--) ops.Insert(removed[k].index, removed[k].op);
            }

            public override void Redo(List<CanvasOp> ops)
            {
                foreach (var (index, _) in removed) ops.RemoveAt(index);
            }
        }

        sealed class ClearEntry : HistoryEntry
        {
            readonly List<CanvasOp> snapshot;
            public ClearEntry(List<CanvasOp> ops) => snapshot = new List<CanvasOp>(ops);

            public override void Undo(List<CanvasOp> ops)
            {
                ops.Clear();
                ops.AddRange(snapshot);
            }

            public override void Redo(List<CanvasOp> ops) => ops.Clear();
        }

        #endregion

        #region Raster helpers

        void Allocate(int width, int height)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            int length = Width * Height;
            pixels = new Color32[length];
            owners = new int[length];
            coverage = new byte[length];
            basePixels = new Color32[length];
            regionMask = new byte[length];
            touched.Clear();

            if (Texture != null) Object.Destroy(Texture);
            Texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Whiteboard Canvas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            dirty = true;
        }

        void Replay()
        {
            Array.Clear(pixels, 0, pixels.Length);
            Array.Clear(owners, 0, owners.Length);
            foreach (var op in ops)
            {
                switch (op)
                {
                    case StrokeOp stroke:
                        var points = stroke.Points;
                        StampSegment(stroke, points[0], points[0]);
                        for (int i = 1; i < points.Count; i++) StampSegment(stroke, points[i - 1], points[i]);
                        ResetStrokeScratch();
                        break;
                    case FillOp fill:
                        RasterizeFill(fill);
                        break;
                }
            }
            dirty = true;
        }

        /// <summary>Straight-alpha "source over destination" with source opacity scaled by coverage.</summary>
        static Color32 Blend(Color32 dst, Color32 src, float coverage)
        {
            float sa = src.a / 255f * coverage;
            if (sa <= 0f) return dst;
            float da = dst.a / 255f;
            float k = da * (1f - sa);
            float oa = sa + k;
            if (oa <= 0f) return default;
            float inv = 1f / oa;
            return new Color32(
                (byte)((src.r * sa + dst.r * k) * inv + 0.5f),
                (byte)((src.g * sa + dst.g * k) * inv + 0.5f),
                (byte)((src.b * sa + dst.b * k) * inv + 0.5f),
                (byte)(oa * 255f + 0.5f));
        }

        /// <summary>The color a pixel appears as on the white board.</summary>
        static Color32 Flatten(Color32 c)
        {
            if (c.a == 255) return c;
            int a = c.a, ia = 255 - a;
            return new Color32(
                (byte)((c.r * a + 255 * ia) / 255),
                (byte)((c.g * a + 255 * ia) / 255),
                (byte)((c.b * a + 255 * ia) / 255),
                255);
        }

        static bool SameColor(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        #endregion
    }
}
