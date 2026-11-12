using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sese.Messenger.IndexedTerminal;

public sealed class IndexedMessageStore : IDisposable
{
    private const int IndexEntrySizeBytes = sizeof(int) + sizeof(long); // 4 + 8 = 12 bytes

    private readonly FileStream _messageStream;
    private readonly FileStream _indexStream;
    private readonly BinaryWriter _indexWriter;
    private readonly BinaryReader _indexReader;

    public string MessageFilePath { get; }
    public string IndexFilePath { get; }
    public int MessageCount { get; private set; }

    public IndexedMessageStore(string storageDirectory)
    {
        Directory.CreateDirectory(storageDirectory);

        MessageFilePath = Path.Combine(storageDirectory, "messages.txt");
        IndexFilePath = Path.Combine(storageDirectory, "offset-index.dat");

        _messageStream = new FileStream(
            MessageFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);

        _indexStream = new FileStream(
            IndexFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read);

        _indexWriter = new BinaryWriter(_indexStream, Encoding.UTF8, leaveOpen: true);
        _indexReader = new BinaryReader(_indexStream, Encoding.UTF8, leaveOpen: true);

        InitializeIndexState();
    }

    private void InitializeIndexState()
    {
        if (_indexStream.Length == 0)
        {
            MessageCount = 0;
            return;
        }

        if (_indexStream.Length % IndexEntrySizeBytes != 0)
        {
            throw new InvalidDataException("offset-index.dat is corrupted (invalid byte alignment).");
        }

        // Verify existing binary entries using BinaryReader
        _indexStream.Seek(0, SeekOrigin.Begin);
        int count = 0;
        while (_indexStream.Position < _indexStream.Length)
        {
            int storedId = _indexReader.ReadInt32();
            long storedOffset = _indexReader.ReadInt64();
            count++;

            if (storedId != count || storedOffset < 0)
            {
                throw new InvalidDataException($"Index mismatch at record {count}.");
            }
        }

        MessageCount = count;
    }

    /// <summary>
    /// Appends a message to messages.txt and writes (messageId -> byteOffset) to offset-index.dat via BinaryWriter.
    /// </summary>
    public MessageRecord AppendMessage(string sender, string content, bool flushImmediately = true)
    {
        int nextId = MessageCount + 1;
        var record = new MessageRecord(nextId, DateTime.UtcNow, sender, content);

        // Seek to end of plain-text stream and capture exact starting byte offset
        _messageStream.Seek(0, SeekOrigin.End);
        long byteOffset = _messageStream.Position;

        byte[] payload = Encoding.UTF8.GetBytes(record.ToStorageLine() + "\n");
        _messageStream.Write(payload, 0, payload.Length);

        // Write fixed-width (Int32 messageId, Int64 byteOffset) entry via BinaryWriter
        _indexStream.Seek(0, SeekOrigin.End);
        _indexWriter.Write(record.MessageId);
        _indexWriter.Write(byteOffset);

        if (flushImmediately)
        {
            _messageStream.Flush();
            _indexWriter.Flush();
            _indexStream.Flush();
        }

        MessageCount = nextId;
        return record;
    }

    /// <summary>
    /// O(1) retrieval: reads byte offset from offset-index.dat via BinaryReader and seeks directly in messages.txt.
    /// </summary>
    public MessageRecord? JumpToMessage(int messageId)
    {
        if (messageId < 1 || messageId > MessageCount)
        {
            return null;
        }

        long indexPosition = (long)(messageId - 1) * IndexEntrySizeBytes;
        _indexStream.Seek(indexPosition, SeekOrigin.Begin);

        int storedId = _indexReader.ReadInt32();
        long messageByteOffset = _indexReader.ReadInt64();

        if (storedId != messageId)
        {
            throw new InvalidDataException($"Expected MessageId {messageId} in index, found {storedId}.");
        }

        _messageStream.Seek(messageByteOffset, SeekOrigin.Begin);
        string? rawLine = ReadUtf8Line(_messageStream);

        return rawLine is null ? null : MessageRecord.Parse(rawLine);
    }

    /// <summary>
    /// O(N) sequential scan baseline used for benchmarking against JumpToMessage.
    /// </summary>
    public (MessageRecord? Record, int LinesScanned) SequentialScanMessage(int targetMessageId)
    {
        _messageStream.Seek(0, SeekOrigin.Begin);
        int scanned = 0;

        while (true)
        {
            string? line = ReadUtf8Line(_messageStream);
            if (line is null)
            {
                break;
            }

            scanned++;
            if (line.Length == 0)
            {
                continue;
            }

            MessageRecord record = MessageRecord.Parse(line);
            if (record.MessageId == targetMessageId)
            {
                return (record, scanned);
            }
        }

        return (null, scanned);
    }

    /// <summary>
    /// Seeds bulk messages for testing and benchmarking (1,000+ messages).
    /// </summary>
    public void SeedMessages(int count, string sender = "SystemSeed")
    {
        for (int i = 1; i <= count; i++)
        {
            AppendMessage(
                sender,
                $"Benchmark log payload #{MessageCount + 1} - verifying binary offset jump performance.",
                flushImmediately: false);
        }

        _messageStream.Flush();
        _indexWriter.Flush();
        _indexStream.Flush();
    }

    public List<MessageRecord> GetRecentMessages(int count)
    {
        var results = new List<MessageRecord>();
        if (MessageCount == 0)
        {
            return results;
        }

        int startId = Math.Max(1, MessageCount - count + 1);
        for (int id = startId; id <= MessageCount; id++)
        {
            MessageRecord? record = JumpToMessage(id);
            if (record is not null)
            {
                results.Add(record);
            }
        }

        return results;
    }

    public void ClearAll()
    {
        _messageStream.SetLength(0);
        _messageStream.Flush();

        _indexStream.SetLength(0);
        _indexWriter.Flush();
        _indexStream.Flush();

        MessageCount = 0;
    }

    private static string? ReadUtf8Line(FileStream stream)
    {
        int firstByte = stream.ReadByte();
        if (firstByte == -1)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        int current = firstByte;

        while (current != -1 && current != '\n')
        {
            if (current != '\r')
            {
                buffer.WriteByte((byte)current);
            }
            current = stream.ReadByte();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public void Dispose()
    {
        _indexWriter.Dispose();
        _indexReader.Dispose();
        _indexStream.Dispose();
        _messageStream.Dispose();
    }
}