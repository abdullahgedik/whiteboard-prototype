using System.Collections.Generic;
using UnityEngine;

namespace Whiteboard
{
    public enum StrokeKind
    {
        Paint,
        Erase
    }

    /// <summary>
    /// A recorded drawing operation. The visible canvas is always the result of
    /// rasterizing every operation in order, which is what lets whole strokes be
    /// removed (stroke eraser, undo) after other things were drawn on top.
    /// </summary>
    public abstract class CanvasOp
    {
        public readonly int Id;

        /// <summary>Every pixel this operation can change lies inside these bounds.</summary>
        public PixelRect Bounds { get; protected set; } = PixelRect.Empty;

        protected CanvasOp(int id)
        {
            Id = id;
        }
    }

    public sealed class StrokeOp : CanvasOp
    {
        public readonly StrokeKind Kind;
        public readonly Color32 Color;
        public readonly float Radius;
        public readonly List<Vector2> Points = new List<Vector2>();

        public StrokeOp(int id, StrokeKind kind, Color32 color, float radius) : base(id)
        {
            Kind = kind;
            Color = color;
            Radius = radius;
        }

        /// <summary>Call once the points are final.</summary>
        public void UpdateBounds()
        {
            float reach = Radius + 1f;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var p in Points)
            {
                if (p.x < xMin) xMin = p.x;
                if (p.y < yMin) yMin = p.y;
                if (p.x > xMax) xMax = p.x;
                if (p.y > yMax) yMax = p.y;
            }
            Bounds = new PixelRect(
                Mathf.FloorToInt(xMin - reach), Mathf.FloorToInt(yMin - reach),
                Mathf.CeilToInt(xMax + reach), Mathf.CeilToInt(yMax + reach));
        }
    }

    /// <summary>Inclusive integer pixel rectangle.</summary>
    public readonly struct PixelRect
    {
        public readonly int XMin, YMin, XMax, YMax;

        public static readonly PixelRect Empty = new PixelRect(int.MaxValue, int.MaxValue, int.MinValue, int.MinValue);

        public PixelRect(int xMin, int yMin, int xMax, int yMax)
        {
            XMin = xMin;
            YMin = yMin;
            XMax = xMax;
            YMax = yMax;
        }

        public bool IsEmpty => XMin > XMax || YMin > YMax;

        public bool Overlaps(PixelRect other) =>
            !IsEmpty && !other.IsEmpty
            && XMin <= other.XMax && other.XMin <= XMax
            && YMin <= other.YMax && other.YMin <= YMax;

        public PixelRect Union(PixelRect other)
        {
            if (IsEmpty) return other;
            if (other.IsEmpty) return this;
            return new PixelRect(
                Mathf.Min(XMin, other.XMin), Mathf.Min(YMin, other.YMin),
                Mathf.Max(XMax, other.XMax), Mathf.Max(YMax, other.YMax));
        }

        public PixelRect ClampTo(int width, int height) => new PixelRect(
            Mathf.Max(XMin, 0), Mathf.Max(YMin, 0), Mathf.Min(XMax, width - 1), Mathf.Min(YMax, height - 1));
    }

    /// <summary>Horizontal span of pixels [X0, X1] on row Y.</summary>
    public readonly struct PixelRun
    {
        public readonly int Y;
        public readonly int X0;
        public readonly int X1;

        public PixelRun(int y, int x0, int x1)
        {
            Y = y;
            X0 = x0;
            X1 = x1;
        }
    }

    /// <summary>
    /// A bucket fill. The region is stored (not the seed point) so the fill keeps
    /// its shape even if the strokes that bounded it are erased later.
    /// </summary>
    public sealed class FillOp : CanvasOp
    {
        public readonly Color32 Color;

        /// <summary>Pixels replaced by the fill color.</summary>
        public readonly PixelRun[] Interior;

        /// <summary>One pixel ring around the region; the fill is blended underneath
        /// these so anti-aliased stroke edges don't leave a white halo.</summary>
        public readonly PixelRun[] Edge;

        public FillOp(int id, Color32 color, PixelRun[] interior, PixelRun[] edge) : base(id)
        {
            Color = color;
            Interior = interior;
            Edge = edge;
            var bounds = PixelRect.Empty;
            foreach (var run in interior) bounds = bounds.Union(new PixelRect(run.X0, run.Y, run.X1, run.Y));
            foreach (var run in edge) bounds = bounds.Union(new PixelRect(run.X0, run.Y, run.X1, run.Y));
            Bounds = bounds;
        }
    }
}
