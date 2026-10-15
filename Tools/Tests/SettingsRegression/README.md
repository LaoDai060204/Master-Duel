Run `dotnet run --project Tools/Tests/SettingsRegression` with .NET SDK 8 and Python 3.

The harness compiles the real Settings source, decoding a temporary copy from its
original GB18030 encoding. It uses Newtonsoft.Json shipped with the SDK and
minimal application adapters. It verifies first launch, malformed and null JSON,
wrong types, null fields, custom values and deliberate empty-array overrides.
The original code passed 3/11 checks; the fixed code passes 11/11.

These checks exercise JSON handling; the exact Unity package version and game
integration still require Editor validation.
