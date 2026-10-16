Run `dotnet run --project Tools/Tests/SourceAudit` with .NET SDK 8.

The audit parses every C# file under Assets using C# 9 and representative Unity
6000.6 Editor/Linux, Windows and Android preprocessor profiles. It reads UTF-8
and legacy GB18030 without modifying sources. Roslyn comes from the installed
SDK; no package downloads are required.

This detects syntax errors in those branches. It does not resolve assembly
references, verify serialized assets or replace Unity import/player builds.
