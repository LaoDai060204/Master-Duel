Runs the production `MyCard.cs` (staged from its original GB18030 encoding) with
in-memory HTTP and Unity adapters. No accounts, credentials, or network calls are
used. The SDK's Newtonsoft.Json assembly provides the actual JSON serializer.

```bash
dotnet run --project Tools/Tests/MyCardRegression/MyCardRegression.csproj
```

Checks login payload escaping, non-JSON HTTP failures, propagation of deck-load
errors, avatar URL escaping, cache hits and collisions, lost disk entries, disk
write failures, concurrent texture ownership, resizing, and play-mode shutdown.
Texture data in these tests is simulated; Editor import and rendering still need
validation in a licensed Unity 6000.6.3f1 installation.
