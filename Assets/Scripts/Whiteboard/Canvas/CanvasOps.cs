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
        }
    }
}
