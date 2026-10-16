Run `dotnet run --project Tools/Tests/SettingsRegression` with .NET SDK 8 and Python 3.

The harness compiles the real Settings source, decoding a temporary copy from its
original GB18030 encoding. It uses Newtonsoft.Json shipped with the SDK and
minimal application adapters. It verifies first launch, malformed and null JSON,
wrong types, null fields, custom values and deliberate empty-array overrides.
The initial JSON-hardening cases pass 11/11. Three added cases cover preservation of unknown fields/formatting, case-insensitive property names, and reading without rewriting: the prior migration code failed all three; the simplified loader passes 14/14 overall.

These checks exercise JSON handling; the exact Unity package version and game
integration still require Editor validation.
