Run the regression harness from the repository root with .NET 8:

```sh
dotnet run --project Tools/Tests/ScrollRegression
dotnet run --project Tools/Tests/ScrollRegression -p:InputBackend=Legacy
dotnet run --project Tools/Tests/ScrollRegression -p:InputBackend=Both
dotnet run --project Tools/Tests/ScrollRegression -p:InputBackend=None
```

The harness compiles the actual `UIScrollToSelection.cs` source. It checks missing
EventSystem/content references, recovery after content assignment, cancellation
keys and mouse buttons, and backend selection without downloading packages.
Minimal Unity API doubles make these control flow tests portable; an Editor
import and Play Mode check are still required to validate Unity integration.
