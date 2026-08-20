using System.IO;
using System.Text.Json;

namespace Osdu.Client.ExampleApp.Services;

/// <summary>
/// File-backed record store that supports millions of rows without holding them all in memory.
/// Records are appended as one-JSON-object-per-line (JSONL) and read back by line offset.
/// </summary>
public sealed class RecordStore : IDisposable
{
    private readonly string _filePath;
    private readonly List<long> _lineOffsets = []; // byte offset of each record
    private FileStream? _writeStream;
    private StreamWriter? _writer;

    public int Count => _lineOffsets.Count;

    public RecordStore()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"osdu_records_{Guid.NewGuid():N}.jsonl");
        _writeStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536);
        _writer = new StreamWriter(_writeStream) { AutoFlush = false };
    }

    /// <summary>Appends records and returns the new total count.</summary>
    public int Append(IReadOnlyList<JsonElement> records)
    {
        if (_writer is null) throw new ObjectDisposedException(nameof(RecordStore));

        foreach (var record in records)
        {
            _lineOffsets.Add(_writeStream!.Position + _writer.BaseStream.Position);
            _writer.Flush();
            _lineOffsets[^1] = _writeStream.Position;
            string line = JsonSerializer.Serialize(record);
            _writer.WriteLine(line);
        }
        _writer.Flush();
        _writeStream!.Flush();

        return _lineOffsets.Count;
    }

    /// <summary>Reads a page of records from the file by offset. Only this slice is in memory.</summary>
    public List<JsonElement> GetPage(int startIndex, int count)
    {
        int actualCount = Math.Min(count, _lineOffsets.Count - startIndex);
        if (actualCount <= 0) return [];

        var results = new List<JsonElement>(actualCount);

        using var readStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 65536);
        using var reader = new StreamReader(readStream);

        for (int i = 0; i < actualCount; i++)
        {
            readStream.Position = _lineOffsets[startIndex + i];
            reader.DiscardBufferedData();
            string? line = reader.ReadLine();
            if (line is not null)
            {
                using var doc = JsonDocument.Parse(line);
                results.Add(doc.RootElement.Clone());
            }
        }

        return results;
    }

    /// <summary>Clears all records and resets the file.</summary>
    public void Clear()
    {
        _writer?.Dispose();
        _writeStream?.Dispose();
        _lineOffsets.Clear();

        _writeStream = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.Read, 65536);
        _writer = new StreamWriter(_writeStream) { AutoFlush = false };
    }

    public void Dispose()
    {
        _writer?.Dispose();
        _writeStream?.Dispose();
        try { File.Delete(_filePath); } catch { }
    }
}