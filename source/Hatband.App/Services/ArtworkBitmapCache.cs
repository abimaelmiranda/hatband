using System.Collections.Generic;
using Avalonia.Media.Imaging;

namespace Hatband.App.Services;

/// <summary>Shares decoded artwork while bounding the memory retained for images not in use.</summary>
public sealed class ArtworkBitmapCache : IDisposable
{
    private const long MaximumRetainedBytes = 128L * 1024 * 1024;

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> leastRecentlyUsed = [];
    private long retainedBytes;
    private bool disposed;

    private sealed class Entry(string key, Bitmap bitmap, long byteSize)
    {
        public string Key { get; } = key;
        public Bitmap Bitmap { get; } = bitmap;
        public long ByteSize { get; } = byteSize;
        public int ReferenceCount { get; set; }
        public LinkedListNode<Entry>? Node { get; set; }
    }

    public ArtworkBitmapLease? TryAcquire(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!entries.TryGetValue(key, out var entry))
            {
                return null;
            }

            Acquire(entry);
            return new ArtworkBitmapLease(entry.Bitmap, () => Release(entry), () => AcquireLease(entry));
        }
    }

    public ArtworkBitmapLease AddOrAcquire(string key, Bitmap bitmap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(bitmap);

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (entries.TryGetValue(key, out var existing))
            {
                Acquire(existing);
                bitmap.Dispose();
                return new ArtworkBitmapLease(existing.Bitmap, () => Release(existing), () => AcquireLease(existing));
            }

            var byteSize = Math.Max(1L, (long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * 4);
            var entry = new Entry(key, bitmap, byteSize) { ReferenceCount = 1 };
            entry.Node = leastRecentlyUsed.AddFirst(entry);
            entries.Add(key, entry);
            retainedBytes += byteSize;
            TrimUnreferencedEntries();
            return new ArtworkBitmapLease(bitmap, () => Release(entry), () => AcquireLease(entry));
        }
    }

    private void Acquire(Entry entry)
    {
        entry.ReferenceCount++;
        if (entry.Node is not null)
        {
            leastRecentlyUsed.Remove(entry.Node);
            leastRecentlyUsed.AddFirst(entry.Node);
        }
    }

    private ArtworkBitmapLease AcquireLease(Entry entry)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!entries.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry))
            {
                throw new ObjectDisposedException(nameof(ArtworkBitmapLease));
            }

            Acquire(entry);
            return new ArtworkBitmapLease(entry.Bitmap, () => Release(entry), () => AcquireLease(entry));
        }
    }

    private void Release(Entry entry)
    {
        lock (gate)
        {
            if (!entries.TryGetValue(entry.Key, out var current) || !ReferenceEquals(current, entry))
            {
                return;
            }

            entry.ReferenceCount--;
            if (entry.ReferenceCount < 0)
            {
                throw new InvalidOperationException("An artwork bitmap lease was released more than once.");
            }

            TrimUnreferencedEntries();
        }
    }

    private void TrimUnreferencedEntries()
    {
        var node = leastRecentlyUsed.Last;
        while (retainedBytes > MaximumRetainedBytes && node is not null)
        {
            var previous = node.Previous;
            var entry = node.Value;
            if (entry.ReferenceCount == 0)
            {
                leastRecentlyUsed.Remove(node);
                entries.Remove(entry.Key);
                retainedBytes -= entry.ByteSize;
                entry.Bitmap.Dispose();
                entry.Node = null;
            }

            node = previous;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            foreach (var entry in entries.Values)
            {
                entry.Bitmap.Dispose();
            }

            entries.Clear();
            leastRecentlyUsed.Clear();
            retainedBytes = 0;
        }
    }
}

public sealed class ArtworkBitmapLease : IDisposable
{
    private Action? release;
    private readonly Func<ArtworkBitmapLease> acquire;

    internal ArtworkBitmapLease(Bitmap bitmap, Action release, Func<ArtworkBitmapLease> acquire)
    {
        Bitmap = bitmap;
        this.release = release;
        this.acquire = acquire;
    }

    public Bitmap Bitmap { get; }

    public ArtworkBitmapLease Clone() => acquire();

    public void Dispose()
    {
        Interlocked.Exchange(ref release, null)?.Invoke();
    }
}
