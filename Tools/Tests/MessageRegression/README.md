# Message startup and lifecycle regression checks

Run from the repository root:

```sh
DOTNET_CLI_HOME=/tmp/master-duel-dotnet-home /workspace/.tools/dotnet/dotnet run --project Tools/Tests/MessageRegression/MessageRegression.csproj
```

The project compiles the actual `MessageManager.cs` and base `Manager.cs` with
small deterministic adapters for Unity, Addressables, and DOTween. The checks
cover messages arriving before `Program` exists, immediate flush when a prefab
finishes loading, failure/retry, FIFO overflow drain, repeat-play static reset,
and Addressables ownership across replacement, reinitialization, and destruction.

These checks exercise manager control flow and resource ownership. They do not
replace Unity Editor import, scene integration, visual timing, or a licensed game
build; those still require a valid Unity license and the external game assets.
