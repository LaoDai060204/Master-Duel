Run `dotnet run --project Tools/Tests/OnlineDeckRegression` with .NET SDK 8 and Python 3.

The harness links the real `OnlineDeck.cs` and YDK `Deck.cs`, uses temporary files,
and supplies deterministic HTTP/Unity boundary adapters. It covers request
serialization, server rejection, malformed replies, deferred metadata changes,
atomic local replacement, safe renames, file identity/ownership, and newer edits
while requests wait or finish in reverse order. Reconciliation guards are tested
using the same opaque file snapshots consumed by the Online UI coroutine.

The suite passes 39 scenarios / 148 assertions. No live accounts or network calls
are used. It does not validate the full Unity UI, a real server, or filesystem
transactions against unrelated processes that edit between a check and a write.
