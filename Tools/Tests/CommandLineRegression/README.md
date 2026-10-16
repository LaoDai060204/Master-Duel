Run `dotnet run --project Tools/Tests/CommandLineRegression/CommandLineRegression.csproj`.

The checks compile the real, Unity-independent launch parser and archive path
resolver. They cover option order, consuming values exactly once, missing and
repeated arguments, culture-independent switches, and Windows/POSIX ZIP paths.
They do not validate Unity scene transitions or native ZIP extraction.
