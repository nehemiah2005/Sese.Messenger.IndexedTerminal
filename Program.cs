using System;
using System.Diagnostics;
using System.IO;
using Sese.Messenger.IndexedTerminal;

string dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "chat_data");
using var store = new IndexedMessageStore(dataDirectory);

string currentUser = "Nehemiah";

Console.WriteLine("============================================================");
Console.WriteLine("  Sese.Messenger.IndexedTerminal (sese-chat)");
Console.WriteLine($"  Storage: {dataDirectory}");
Console.WriteLine($"  Loaded Messages: {store.MessageCount}");
Console.WriteLine("============================================================");
PrintHelp();

while (true)
{
    Console.Write($"\n[{currentUser}] > ");
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
    {
        continue;
    }

    string trimmed = input.Trim();

    if (trimmed.Equals("/exit", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Closing file streams and exiting sese-chat...");
        break;
    }

    if (trimmed.Equals("/help", StringComparison.OrdinalIgnoreCase))
    {
        PrintHelp();
        continue;
    }

    if (trimmed.StartsWith("/user ", StringComparison.OrdinalIgnoreCase))
    {
        string newName = trimmed[6..].Trim();
        if (!string.IsNullOrWhiteSpace(newName))
        {
            currentUser = newName;
            Console.WriteLine($"Active sender changed to: {currentUser}");
        }
        continue;
    }

    if (trimmed.StartsWith("/jump", StringComparison.OrdinalIgnoreCase))
    {
        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int targetId))
        {
            Console.WriteLine("Usage: /jump <messageId>");
            continue;
        }

        var sw = Stopwatch.StartNew();
        MessageRecord? found = store.JumpToMessage(targetId);
        sw.Stop();

        if (found is null)
        {
            Console.WriteLine($"Message #{targetId} not found (valid range: 1 to {store.MessageCount}).");
        }
        else
        {
            Console.WriteLine($"[O(1) Indexed Jump in {sw.Elapsed.TotalMicroseconds:F2} µs ({sw.ElapsedTicks} ticks)]");
            Console.WriteLine($"  {found}");
        }
        continue;
    }

    if (trimmed.StartsWith("/recent", StringComparison.OrdinalIgnoreCase))
    {
        int count = 10;
        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out int parsedCount) && parsedCount > 0)
        {
            count = parsedCount;
        }

        var recent = store.GetRecentMessages(count);
        if (recent.Count == 0)
        {
            Console.WriteLine("No messages stored yet.");
        }
        else
        {
            Console.WriteLine($"Showing last {recent.Count} message(s):");
            foreach (var msg in recent)
            {
                Console.WriteLine($"  {msg}");
            }
        }
        continue;
    }

    if (trimmed.StartsWith("/seed", StringComparison.OrdinalIgnoreCase))
    {
        int count = 1000;
        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out int parsedCount) && parsedCount > 0)
        {
            count = parsedCount;
        }

        var sw = Stopwatch.StartNew();
        store.SeedMessages(count);
        sw.Stop();

        Console.WriteLine($"Seeded {count} messages in {sw.ElapsedMilliseconds} ms. Total messages: {store.MessageCount}.");
        continue;
    }

    if (trimmed.StartsWith("/benchmark", StringComparison.OrdinalIgnoreCase))
    {
        if (store.MessageCount < 1000)
        {
            int needed = 1000 - store.MessageCount;
            Console.WriteLine($"Auto-seeding {needed} messages to meet the 1,000-message minimum...");
            store.SeedMessages(needed);
        }

        int targetId = store.MessageCount; // Worst-case target at the end of the file
        string[] parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out int customId) && customId >= 1 && customId <= store.MessageCount)
        {
            targetId = customId;
        }

        // Warm-up call
        _ = store.JumpToMessage(1);

        // 1. Measure O(N) Sequential Scan
        var seqWatch = Stopwatch.StartNew();
        var (seqRecord, linesScanned) = store.SequentialScanMessage(targetId);
        seqWatch.Stop();

        // 2. Measure O(1) Binary Indexed Jump
        var jumpWatch = Stopwatch.StartNew();
        var jumpRecord = store.JumpToMessage(targetId);
        jumpWatch.Stop();

        double speedup = jumpWatch.ElapsedTicks > 0
            ? (double)seqWatch.ElapsedTicks / jumpWatch.ElapsedTicks
            : 0;

        Console.WriteLine("\n--- BENCHMARK RESULTS ---");
        Console.WriteLine($"Total Stored Messages : {store.MessageCount:N0}");
        Console.WriteLine($"Target Message ID     : #{targetId:N0}");
        Console.WriteLine($"Sequential Scan (O(N)): {seqWatch.Elapsed.TotalMilliseconds:F4} ms ({seqWatch.ElapsedTicks:N0} ticks) | Lines Scanned: {linesScanned:N0}");
        Console.WriteLine($"Indexed Jump    (O(1)): {jumpWatch.Elapsed.TotalMilliseconds:F4} ms ({jumpWatch.ElapsedTicks:N0} ticks) | Direct Seek");
        Console.WriteLine($"Speedup Factor        : {speedup:F2}x faster");
        Console.WriteLine($"Verified Match        : {seqRecord == jumpRecord}");
        continue;
    }

    if (trimmed.Equals("/clear", StringComparison.OrdinalIgnoreCase))
    {
        store.ClearAll();
        Console.WriteLine("Cleared messages.txt and offset-index.dat.");
        continue;
    }

    // Default behavior: append typed text as a new chat message
    MessageRecord saved = store.AppendMessage(currentUser, trimmed);
    Console.WriteLine($"Saved: {saved}");
}

static void PrintHelp()
{
    Console.WriteLine("Commands:");
    Console.WriteLine("  <text>              Send and index a new message");
    Console.WriteLine("  /jump <messageId>   O(1) seek directly to a message via offset-index.dat");
    Console.WriteLine("  /recent [n]         Show the last n messages (default: 10)");
    Console.WriteLine("  /user <name>        Change sender display name");
    Console.WriteLine("  /seed [n]           Generate n sample messages (default: 1000)");
    Console.WriteLine("  /benchmark [id]     Compare sequential scan vs. indexed jump (>=1,000 msgs)");
    Console.WriteLine("  /clear              Reset message and index files");
    Console.WriteLine("  /exit               Exit the terminal");
}