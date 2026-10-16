using System.Globalization;
using MDPro3.Utility;

var tests = new (string Name, Action Run)[]
{
    ("Read all launch options", () =>
    {
        var options = CommandLineOptions.Parse(new[] { "Master-Duel.exe", "-n", "Duelist", "-h", "localhost", "-p", "7911",
            "-w", "secret", "-d", "Deck", "-r", "Replay", "-s", "Puzzle", "-j" });
        Require(options.Nickname == "Duelist" && options.Host == "localhost" && options.Port == "7911"
            && options.Password == "secret" && options.Deck == "Deck" && options.Replay == "Replay"
            && options.Puzzle == "Puzzle" && options.Join);
    }),
    ("Join and deck options are independent of their order", () =>
    {
        foreach (var args in new[] { new[] { "-j", "-d", "My Deck" }, new[] { "-d", "My Deck", "-j" } })
        {
            var options = CommandLineOptions.Parse(args);
            Require(options.Join && options.Deck == "My Deck");
        }
    }),
    ("An option-shaped value is consumed once", () =>
    {
        var options = CommandLineOptions.Parse(new[] { "-n", "-h", "localhost", "-w", "-j" });
        Require(options.Nickname == "-h" && options.Host == null && options.Password == "-j" && !options.Join);
    }),
    ("Missing final values leave options unset", () =>
    {
        foreach (var option in new[] { "-n", "-h", "-p", "-w", "-d", "-r", "-s" })
        {
            var options = CommandLineOptions.Parse(new[] { option });
            Require(options.Nickname == null && options.Host == null && options.Port == null
                && options.Password == null && options.Deck == null && options.Replay == null && options.Puzzle == null);
        }
    }),
    ("Case matching remains independent of process culture", () =>
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var options = CommandLineOptions.Parse(new[] { "-J", "-N", "Duelist" });
            Require(options.Join && options.Nickname == "Duelist");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }),
    ("Repeated options use the last value", () =>
    {
        var options = CommandLineOptions.Parse(new[] { "-d", "Old", "-d", "New" });
        Require(options.Deck == "New");
    }),
    ("Unknown Unity arguments do not become player options", () =>
    {
        var options = CommandLineOptions.Parse(new[] { "-projectPath", "/some/project", "-batchmode", "-logFile", "log.txt" });
        Require(!options.Join && options.Deck == null && options.Nickname == null);
    }),
    ("Joining without a deck leaves the configured deck untouched", () =>
    {
        var options = CommandLineOptions.Parse(new[] { "-j" });
        Require(options.Join && options.Deck == null);
    }),
    ("Archive entries resolve inside the chosen destination", () =>
    {
        string root = Path.GetFullPath("Output");
        Require(ArchivePath.Resolve(root, "nested\\card.txt") == Path.Combine(root, "nested", "card.txt"));
        Require(ArchivePath.Resolve(root, "nested/../card.txt") == Path.Combine(root, "card.txt"));
    }),
    ("Archive paths reject rooted and sibling destinations", () =>
    {
        foreach (string entry in new[] { "../escape.txt", "..\\escape.txt", "/absolute.txt", "C:/absolute.txt",
            "C:\\absolute.txt", "\\\\server\\share", "../Output2/sibling.txt", "", ".", "nested/.." })
        {
            bool rejected = false;
            try { ArchivePath.Resolve("Output", entry); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected);
        }
    })
};
int failures = 0;
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine("PASS " + test.Name);
    }
    catch (Exception error)
    {
        failures++;
        Console.Error.WriteLine("FAIL " + test.Name + ": " + error.Message);
    }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} launch/path checks passed.");
return failures == 0 ? 0 : 1;

void Require(bool condition)
{
    if (!condition) throw new InvalidOperationException("Unexpected parsed options or destination path.");
}
