using System;
using System.Collections.Generic;

namespace Whiteboard
{
    /// <summary>
    /// Copies of the canvas taken roughly every <see cref="Interval"/> operations, so redraws
    /// start from the nearest snapshot instead of from an empty board.
    /// A snapshot with count N holds the pixels after ops[0..N-1] and stays valid as long as
    /// that prefix of the operation list doesn't change.
    /// </summary>
    public sealed class CanvasSnapshots
    {
        public const int Interval = 24;
        const long MemoryBudgetBytes = 128L * 1024 * 1024;

        sealed class Snapshot
        {
            public int Count;
            public UnityEngine.Color32[] Pixels;
            public int[] Owners;
        }

        readonly List<Snapshot> snapshots = new List<Snapshot>(); // sorted by Count
        Snapshot spare; // one recycled buffer pair, avoids reallocating ~8 bytes/pixel
        int pixelCount;
        int capacity;

        public void Configure(int pixels)
        {
            Clear();
            spare = null;
            pixelCount = pixels;
            long bytesPerSnapshot = (long)pixels * 8; // Color32 + int owner
            capacity = (int)Math.Min(32, MemoryBudgetBytes / Math.Max(1, bytesPerSnapshot));
        }

        public void Clear()
        {
            foreach (var s in snapshots) Release(s);
            snapshots.Clear();
        }

        /// <summary>Drops snapshots that depend on the operation at <paramref name="index"/> or later.</summary>
        public void InvalidateFrom(int index)
        {
            for (int i = snapshots.Count - 1; i >= 0 && snapshots[i].Count > index; i--)
            {
                Release(snapshots[i]);
                snapshots.RemoveAt(i);
            }
        }

        /// <summary>Captures the canvas (showing the first <paramref name="count"/> ops) when it is
        /// at least <see cref="Interval"/> ops past the newest snapshot.</summary>
        public void Offer(int count, UnityEngine.Color32[] pixels, int[] owners)
        {
            int newest = snapshots.Count > 0 ? snapshots[snapshots.Count - 1].Count : 0;
            if (capacity == 0 || count - newest < Interval) return;

            var snapshot = spare ?? new Snapshot
            {
                Pixels = new UnityEngine.Color32[pixelCount],
                Owners = new int[pixelCount]
            };
            spare = null;
            snapshot.Count = count;
            Array.Copy(pixels, snapshot.Pixels, pixelCount);
            Array.Copy(owners, snapshot.Owners, pixelCount);
            snapshots.Add(snapshot);

            if (snapshots.Count > capacity) EvictOne();
        }

        /// <summary>Like <see cref="Restore"/> but only for the inclusive rectangle; the rest of
        /// the canvas is left untouched.</summary>
        public int RestoreRegion(int maxCount, PixelRect rect, int width, UnityEngine.Color32[] pixels, int[] owners)
        {
            Snapshot source = null;
            for (int i = snapshots.Count - 1; i >= 0 && source == null; i--)
                if (snapshots[i].Count <= maxCount) source = snapshots[i];

            int length = rect.XMax - rect.XMin + 1;
            for (int y = rect.YMin; y <= rect.YMax; y++)
            {
                int start = y * width + rect.XMin;
                if (source != null)
                {
                    Array.Copy(source.Pixels, start, pixels, start, length);
                    Array.Copy(source.Owners, start, owners, start, length);
                }
                else
                {
                    Array.Clear(pixels, start, length);
                    Array.Clear(owners, start, length);
                }
            }
            return source?.Count ?? 0;
        }

        /// <summary>Restores the newest snapshot covering at most <paramref name="maxCount"/> ops,
        /// or clears the canvas when there is none. Returns how many ops the canvas now shows.</summary>
        public int Restore(int maxCount, UnityEngine.Color32[] pixels, int[] owners)
        {
            for (int i = snapshots.Count - 1; i >= 0; i--)
            {
                var s = snapshots[i];
                if (s.Count > maxCount) continue;
                Array.Copy(s.Pixels, pixels, pixelCount);
                Array.Copy(s.Owners, owners, pixelCount);
                return s.Count;
            }
            Array.Clear(pixels, 0, pixelCount);
            Array.Clear(owners, 0, pixelCount);
            return 0;
        }

        /// <summary>Removes the snapshot closest to its predecessor (never the newest), which keeps
        /// the remaining ones spread across the history.</summary>
        void EvictOne()
        {
            int victim = 0;
            int smallestGap = int.MaxValue;
            for (int i = 0; i < snapshots.Count - 1; i++)
            {
                int gap = snapshots[i].Count - (i > 0 ? snapshots[i - 1].Count : 0);
                if (gap >= smallestGap) continue;
                smallestGap = gap;
                victim = i;
            }
            Release(snapshots[victim]);
            snapshots.RemoveAt(victim);
        }

        void Release(Snapshot snapshot)
        {
            if (spare == null && snapshot.Pixels.Length == pixelCount) spare = snapshot;
        }
    }
}
