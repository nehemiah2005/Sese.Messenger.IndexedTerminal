# Sese.Messenger.IndexedTerminal

A file-based terminal messenger (`sese-chat`) built in C# that stores messages in a plain-text log (`messages.txt`) alongside a binary offset index (`offset-index.dat`) managed via `BinaryReader` and `BinaryWriter` on a separate `FileStream` for $O(1)$ message retrieval.

## Installation

Install globally as a .NET CLI tool from NuGet:

```bash
dotnet tool install --global Sese.Messenger.IndexedTerminal
```
Or add the package reference to a .NET project:

```bash
dotnet add package Sese.Messenger.IndexedTerminal
```

## Usage

Launch the interactive terminal messenger:

```bash
sese-chat
```

### Commands
- `<text>` — Appends a new message to `messages.txt` and logs its byte offset in `offset-index.dat`.
- `/jump <messageId>` — Uses the binary index to seek directly to `<messageId>` in $O(1)$ time without scanning the file.
- `/recent [n]` — Displays the last `n` messages (default: `10`).
- `/seed [n]` — Generates `n` sample messages (default: `1000`) for benchmarking.
- `/benchmark [id]` — Compares $O(N)$ sequential scan time against $O(1)$ indexed jump time.
- `/exit` — Closes all file streams and exits the application.

## Verification Benchmark ($\ge 1,000$ Messages)

| Dataset Size | Target Message ID | Sequential Scan ($O(N)$) | Indexed Jump ($O(1)$) | Speedup |
| :--- | :--- | :--- | :--- | :--- |
| 1,000 messages | `#1,000` | `3.6212 ms` (`36,212 ticks`) | `0.0143 ms` (`143 ticks`) | `253.23x faster` |
| 5,000 messages | `#5,000` | `17.3400 ms` (`173,400 ticks`) | `0.0235 ms` (`235 ticks`) | `737.87x faster` |

## Publication Workflow

```bash
dotnet pack -c Release
dotnet nuget push bin/Release/Sese.Messenger.IndexedTerminal.1.0.0.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```