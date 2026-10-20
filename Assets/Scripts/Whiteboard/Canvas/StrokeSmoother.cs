using System.Collections.Generic;
using UnityEngine;

namespace Whiteboard
{
    /// <summary>
    /// Turns raw pointer samples into a smooth, densely sampled polyline.
    /// 1. Jitter filter: exponential smoothing of the input position.
    /// 2. Spacing: filtered samples closer than <see cref="MinSpacing"/> are skipped, so
    ///    integer mouse positions don't produce staircases.
    /// 3. Centripetal Catmull-Rom through the kept samples, subdivided every few pixels,
    ///    so fast strokes become curves instead of straight segments.
    /// A curve segment needs the following sample for its end tangent, so output trails
    /// the pointer by one sample; <see cref="Finish"/> flushes the rest.
    /// </summary>
    public sealed class StrokeSmoother
    {
        const float MinSpacing = 2.5f;
        const float SampleStep = 2f;
        const int MaxSamplesPerSegment = 64;

        readonly List<Vector2> keys = new List<Vector2>();
        Vector2 filtered;
        Vector2 lastRaw;
        float smoothing;
        int emittedSegments;

        /// <param name="smoothingAmount">0 = follow the pointer exactly, 0.9 = heavy smoothing.</param>
        public void Begin(Vector2 start, float smoothingAmount)
        {
            keys.Clear();
            keys.Add(start);
            filtered = lastRaw = start;
            smoothing = Mathf.Clamp(smoothingAmount, 0f, 0.9f);
            emittedSegments = 0;
        }

        /// <summary>Adds a pointer sample and appends any newly finished curve points to <paramref name="output"/>.</summary>
        public void Add(Vector2 raw, List<Vector2> output)
        {
            lastRaw = raw;
            filtered = Vector2.Lerp(filtered, raw, 1f - smoothing);
            if ((filtered - keys[keys.Count - 1]).sqrMagnitude < MinSpacing * MinSpacing) return;
            keys.Add(filtered);
            EmitReadySegments(output, final: false);
        }

        /// <summary>Ends the stroke at the last real pointer position and flushes the remaining curve.</summary>
        public void Finish(List<Vector2> output)
        {
            if ((lastRaw - keys[keys.Count - 1]).sqrMagnitude > 0.25f) keys.Add(lastRaw);
            EmitReadySegments(output, final: true);
        }

        void EmitReadySegments(List<Vector2> output, bool final)
        {
            // Segment i runs keys[i] -> keys[i+1] and needs keys[i+2] unless it is the last one.
            int available = final ? keys.Count - 1 : keys.Count - 2;
            for (; emittedSegments < available; emittedSegments++)
            {
                int i = emittedSegments;
                var p1 = keys[i];
                var p2 = keys[i + 1];
                // Missing neighbours at the ends are mirrored so the end tangents stay natural.
                var p0 = i > 0 ? keys[i - 1] : p1 * 2f - p2;
                var p3 = i + 2 < keys.Count ? keys[i + 2] : p2 * 2f - p1;

                int samples = Mathf.Clamp(Mathf.CeilToInt(Vector2.Distance(p1, p2) / SampleStep), 1, MaxSamplesPerSegment);
                for (int s = 1; s <= samples; s++) output.Add(CentripetalCatmullRom(p0, p1, p2, p3, s / (float)samples));
            }
        }

        /// <summary>Point on the p1→p2 span at t ∈ [0,1] (Barry–Goldman form, alpha = 0.5),
        /// which avoids the loops and overshoot of uniform Catmull-Rom on uneven spacing.</summary>
        static Vector2 CentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            const float epsilon = 1e-4f;
            float t0 = 0f;
            float t1 = t0 + Mathf.Max(Mathf.Sqrt(Vector2.Distance(p0, p1)), epsilon);
            float t2 = t1 + Mathf.Max(Mathf.Sqrt(Vector2.Distance(p1, p2)), epsilon);
            float t3 = t2 + Mathf.Max(Mathf.Sqrt(Vector2.Distance(p2, p3)), epsilon);
            float u = Mathf.Lerp(t1, t2, t);

            var a1 = (t1 - u) / (t1 - t0) * p0 + (u - t0) / (t1 - t0) * p1;
            var a2 = (t2 - u) / (t2 - t1) * p1 + (u - t1) / (t2 - t1) * p2;
            var a3 = (t3 - u) / (t3 - t2) * p2 + (u - t2) / (t3 - t2) * p3;
            var b1 = (t2 - u) / (t2 - t0) * a1 + (u - t0) / (t2 - t0) * a2;
            var b2 = (t3 - u) / (t3 - t1) * a2 + (u - t1) / (t3 - t1) * a3;
            return (t2 - u) / (t2 - t1) * b1 + (u - t1) / (t2 - t1) * b2;
        }
    }
}
