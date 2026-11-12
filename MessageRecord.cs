using System;

namespace Sese.Messenger.IndexedTerminal;

public sealed record MessageRecord(
    int MessageId,
    DateTime TimestampUtc,
    string Sender,
    string Content)
{
    public string ToStorageLine()
    {
        string sanitizedSender = Sender.Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        string sanitizedContent = Content.Replace("\r", " ").Replace("\n", " ");
        return $"{MessageId}|{TimestampUtc:O}|{sanitizedSender}|{sanitizedContent}";
    }

    public static MessageRecord Parse(string line)
    {
        string[] parts = line.Split('|', 4);
        if (parts.Length < 4)
        {
            throw new FormatException($"Corrupted message record: {line}");
        }

        return new MessageRecord(
            MessageId: int.Parse(parts[0]),
            TimestampUtc: DateTime.Parse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind),
            Sender: parts[2],
            Content: parts[3]);
    }

    public override string ToString()
    {
        return $"[#{MessageId}] ({TimestampUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}) {Sender}: {Content}";
    }
}
