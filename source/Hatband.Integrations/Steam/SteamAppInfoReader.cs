using System.Text;
using ValveKeyValue;

namespace Hatband.Integrations.Steam;

internal sealed class SteamAppInfoReader
{
    private const int MaximumStringTableEntries = 1_000_000;
    private const int MaximumStringTableBytes = 64 * 1024 * 1024;
    private const int MaximumStringBytes = 1024 * 1024;
    private const int MaximumAppInfoEntries = 1_000_000;
    private static readonly Encoding utf8Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private const uint MaximumAppInfoEntryBytes = 64 * 1024 * 1024;

    private readonly object cacheLock = new();
    private readonly Dictionary<string, CachedAppInfoIndex> appInfoIndexes = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public KVObject? ReadAppInfo(string path, uint requestedAppId, CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (!file.Exists)
        {
            return null;
        }

        lock (cacheLock)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (appInfoIndexes.TryGetValue(path, out var cachedIndex) &&
                cachedIndex.Length == file.Length &&
                cachedIndex.LastWriteTimeUtc == file.LastWriteTimeUtc)
            {
                return ReadAppInfo(path, cachedIndex, requestedAppId, cancellationToken);
            }

            var index = ReadAppInfoIndex(path, file.Length, file.LastWriteTimeUtc, cancellationToken);
            appInfoIndexes[path] = index;
            return ReadAppInfo(path, index, requestedAppId, cancellationToken);
        }
    }

    private static KVObject? ReadAppInfo(
        string path,
        CachedAppInfoIndex index,
        uint appId,
        CancellationToken cancellationToken)
    {
        if (!index.Entries.TryGetValue(appId, out var location))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var file = new FileInfo(path);
        if (!file.Exists || file.Length != index.Length || file.LastWriteTimeUtc != index.LastWriteTimeUtc)
        {
            throw new IOException("The Steam appinfo cache changed while launch metadata was being read.");
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        stream.Position = location.Offset;
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var data = ReadExactly(reader, location.Size);
        using var entryStream = new MemoryStream(data, writable: false);
        var options = new KVSerializerOptions();
        if (index.StringTable is not null)
        {
            options.StringTable = new StringTable(index.StringTable);
        }

        var serializer = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary);
        return serializer.Deserialize(entryStream, options).Root;
    }

    private static CachedAppInfoIndex ReadAppInfoIndex(
        string path,
        long expectedLength,
        DateTime expectedLastWriteTimeUtc,
        CancellationToken cancellationToken)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (stream.Length != expectedLength)
        {
            throw new IOException("The Steam appinfo cache changed while it was being indexed.");
        }

        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var magic = reader.ReadUInt32();
        var version = magic & 0xff;
        if ((magic >> 8) != 0x075644 || version is < 39 or > 41)
        {
            throw new InvalidDataException("The Steam appinfo cache format is not supported.");
        }

        EnsureRemaining(stream, sizeof(uint));
        _ = reader.ReadUInt32();

        string[]? stringTable = null;
        var entriesEnd = stream.Length;
        if (version >= 41)
        {
            var stringTableData = ReadStringTable(reader, stream, cancellationToken);
            stringTable = stringTableData.Values;
            entriesEnd = stringTableData.Offset;
        }

        var entries = new Dictionary<uint, AppInfoEntryLocation>();
        while (stream.Position < entriesEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EnsureRemaining(stream, sizeof(uint), entriesEnd);
            var appId = reader.ReadUInt32();
            if (appId == 0)
            {
                break;
            }

            if (entries.Count >= MaximumAppInfoEntries)
            {
                throw new InvalidDataException("The Steam appinfo cache contains too many app entries.");
            }

            EnsureRemaining(stream, sizeof(uint), entriesEnd);
            var entrySize = reader.ReadUInt32();
            if (entrySize > MaximumAppInfoEntryBytes)
            {
                throw new InvalidDataException("The Steam appinfo cache contains an oversized app entry.");
            }

            var entryEnd = checked(stream.Position + entrySize);
            if (entryEnd > entriesEnd || entryEnd > stream.Length)
            {
                throw new InvalidDataException("The Steam appinfo cache contains an invalid app entry.");
            }

            var metadataSize = version >= 40 ? 60u : 40u;
            if (entrySize < metadataSize)
            {
                throw new InvalidDataException("The Steam appinfo cache contains an incomplete app entry.");
            }

            SkipExactly(stream, metadataSize, entriesEnd);
            var dataSize = checked((int)(entryEnd - stream.Position));
            if (dataSize > 0)
            {
                entries[appId] = new AppInfoEntryLocation(stream.Position, dataSize);
            }

            stream.Position = entryEnd;
        }

        var file = new FileInfo(path);
        if (!file.Exists || file.Length != expectedLength || file.LastWriteTimeUtc != expectedLastWriteTimeUtc)
        {
            throw new IOException("The Steam appinfo cache changed while it was being indexed.");
        }

        return new CachedAppInfoIndex(expectedLength, expectedLastWriteTimeUtc, stringTable, entries);
    }

    private static (string[] Values, long Offset) ReadStringTable(
        BinaryReader reader,
        Stream stream,
        CancellationToken cancellationToken)
    {
        EnsureRemaining(stream, sizeof(long));
        var stringTableOffset = reader.ReadInt64();
        var dataOffset = stream.Position;
        if (stringTableOffset < dataOffset || stringTableOffset > stream.Length - sizeof(uint))
        {
            throw new InvalidDataException("The Steam appinfo cache contains an invalid string table offset.");
        }

        stream.Position = stringTableOffset;
        var stringCount = reader.ReadUInt32();
        if (stringCount > MaximumStringTableEntries)
        {
            throw new InvalidDataException("The Steam appinfo cache contains an invalid string table.");
        }

        var strings = new string[checked((int)stringCount)];
        var tableStart = stream.Position;
        using var bytes = new MemoryStream();
        for (var index = 0; index < strings.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            strings[index] = ReadNullTerminatedString(reader, bytes, MaximumStringBytes);
            if (stream.Position - tableStart > MaximumStringTableBytes)
            {
                throw new InvalidDataException("The Steam appinfo cache string table is too large.");
            }
        }

        stream.Position = dataOffset;
        return (strings, stringTableOffset);
    }

    private static string ReadNullTerminatedString(BinaryReader reader, MemoryStream bytes, int maximumBytes)
    {
        bytes.SetLength(0);
        bytes.Position = 0;
        while (true)
        {
            var value = reader.ReadByte();
            if (value == 0)
            {
                return utf8Encoding.GetString(bytes.GetBuffer(), 0, checked((int)bytes.Length));
            }

            if (bytes.Length >= maximumBytes)
            {
                throw new InvalidDataException("The Steam appinfo cache contains an oversized string.");
            }

            bytes.WriteByte(value);
        }
    }

    private static void EnsureRemaining(Stream stream, long byteCount, long? limit = null)
    {
        var end = Math.Min(limit ?? stream.Length, stream.Length);
        if (byteCount < 0 || stream.Position > end || end - stream.Position < byteCount)
        {
            throw new EndOfStreamException("The Steam appinfo cache ended unexpectedly.");
        }
    }

    private static void SkipExactly(Stream stream, uint byteCount, long limit)
    {
        EnsureRemaining(stream, byteCount, limit);
        stream.Position += byteCount;
    }

    private static byte[] ReadExactly(BinaryReader reader, int byteCount)
    {
        var data = reader.ReadBytes(byteCount);
        if (data.Length != byteCount)
        {
            throw new EndOfStreamException("The Steam appinfo cache ended unexpectedly.");
        }

        return data;
    }

    private sealed record AppInfoEntryLocation(long Offset, int Size);

    private sealed record CachedAppInfoIndex(
        long Length,
        DateTime LastWriteTimeUtc,
        string[]? StringTable,
        IReadOnlyDictionary<uint, AppInfoEntryLocation> Entries);
}
