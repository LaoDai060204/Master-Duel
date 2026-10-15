Run from the repository root with .NET 8 and Python 3:

```sh
dotnet run --project Tools/Tests/StartupRegression/StartupRegression.csproj
```

The project stages a UTF-8 copy of the original GB18030 `Boot.cs` under ignored
`obj/`; it never changes the source file. It uses no external packages.

Three checks exercise the real startup logic with temporary files and a small
coroutine scheduler: rejecting an update APK preserves fresh configuration and
existing data, and ZIP extraction yields between entries while the installer
waits for completion. Unity and ZIP dependencies use minimal stand-ins. These
checks supplement an Android/Unity smoke test; they do not validate Addressables,
Unity's scheduler, or the shipped ZIP library.
