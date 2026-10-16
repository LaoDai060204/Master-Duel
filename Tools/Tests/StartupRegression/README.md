Run from the repository root with .NET 8 and Python 3:

```sh
dotnet run --project Tools/Tests/StartupRegression/StartupRegression.csproj
```

The project stages a UTF-8 copy of the original GB18030 `Boot.cs` under ignored
`obj/`; it never changes the source file. It uses the repository's shipped
SharpZipLib DLL and needs no package downloads.

Thirteen checks exercise the real startup iterators with temporary files, the real
ZIP implementation, and a small coroutine scheduler. They cover preserving data
when rejecting update APKs, waiting between extraction frames, failed reads and
corrupt archives, valid ./ archive directory entries, ZIP destination containment, obsolete Android cleanup, and
stopping dependent Addressables loads after initialization/asset/scene failures.

Unity requests and Addressables handles use deterministic stand-ins. The scheduler
models Unity stopping a failed child coroutine so the tests can detect an
incorrectly saved install marker. These checks supplement an Android/Unity smoke
test; they do not validate Unity's scheduler, package resolution, real remote
requests, or Addressables content builds.
