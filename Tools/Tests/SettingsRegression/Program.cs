using MDPro3;

var originalDirectory = Environment.CurrentDirectory;
var root = Directory.CreateTempSubdirectory("master-duel-settings-").FullName;
var cases = new (string Name, Action Run)[]
{
    ("First launch creates settings and its parent directory", () =>
    {
        Settings.Initialize();
        Require(File.Exists("Data/Settings.json"));
        Require(Settings.Data.SavedCardSize.Length == 2);
    }),
    ("JSON null recovers to defaults", () => Check("null", data => Require(data.SavedCardFormat == ".jpg"))),
    ("Empty settings recover to defaults", () => Check("", data => Require(data.FinalAttackBlueEyes.Length > 0))),
    ("Malformed JSON recovers without overwriting the source", () =>
    {
        Check("{", data => Require(data.SavedCardSize.Length == 2));
        Require(File.ReadAllText("Data/Settings.json") == "{");
    }),
    ("Array root recovers without serialization exception", () => Check("[]", data => Require(data.BatchMove))),
    ("Wrong field type recovers without serialization exception", () =>
        Check("{\"SavedCardSize\":\"broken\"}", data => Require(data.SavedCardSize.Length == 2))),
    ("Null array retains safe defaults", () =>
        Check("{\"FinalAttackBlueEyes\":null,\"SavedCardSize\":null}", data =>
        {
            Require(data.FinalAttackBlueEyes.Length > 0);
            Require(data.SavedCardSize.Length == 2);
        })),
    ("Null strings retain safe defaults", () =>
        Check("{\"SavedCardFormat\":null,\"DiySymbol\":null,\"MDPro3VersionUrl\":null}", data =>
        {
            Require(data.SavedCardFormat == ".jpg");
            Require(data.DiySymbol != null);
            Require(data.MDPro3VersionUrl.StartsWith("https://"));
        })),
    ("Custom values and explicit false settings are preserved", () =>
        Check("{\"MDPro3VersionUrl\":\"https://example.com/version\",\"BatchMove\":false,\"CardRenderPassword\":false,\"SavedCardSize\":[500,700]}", data =>
        {
            Require(data.MDPro3VersionUrl == "https://example.com/version");
            Require(!data.BatchMove && !data.CardRenderPassword);
            Require(data.SavedCardSize.SequenceEqual(new[] { 500, 700 }));
        })),
    ("Empty attack array remains a deliberate override", () =>
        Check("{\"FinalAttackBlueEyes\":[]}", data => Require(data.FinalAttackBlueEyes.Length == 0))),
    ("Missing fields get constructor defaults", () =>
        Check("{}", data => Require(data.FinalAttackRa.Length > 0 && data.SavedCardFormat == ".jpg"))),
    ("Partial settings preserve custom fields and formatting", () =>
    {
        const string json = "{\n  \"CustomExtension\": {\"active\":true},\n  \"BatchMove\": false\n}\n";
        Check(json, data => Require(!data.BatchMove && data.FinalAttackRa.Length > 0));
        Require(File.ReadAllText("Data/Settings.json") == json);
        Settings.Initialize();
        Require(File.ReadAllText("Data/Settings.json") == json);
    }),
    ("Case-insensitive property names retain configured values", () =>
        Check("{\"batchMove\":false,\"cardRenderPassword\":false}", data =>
            Require(!data.BatchMove && !data.CardRenderPassword))),
    ("Missing fields are loaded without rewriting an existing file", () =>
    {
        Check("{}", data => Require(data.SavedCardSize.Length == 2));
        Require(File.ReadAllText("Data/Settings.json") == "{}");
    })
};
var failures = 0;
try
{
    foreach (var test in cases)
    {
        var directory = Path.Combine(root, Guid.NewGuid().ToString());
        Directory.CreateDirectory(directory);
        Environment.CurrentDirectory = directory;
        try
        {
            test.Run();
            Console.WriteLine("PASS " + test.Name);
        }
        catch (Exception error)
        {
            failures++;
            Console.Error.WriteLine("FAIL " + test.Name + ": " + error.GetBaseException().Message);
        }
    }
}
finally
{
    Environment.CurrentDirectory = originalDirectory;
    Directory.Delete(root, true);
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} settings checks passed.");
return failures == 0 ? 0 : 1;

void Check(string json, Action<SettingData> verify)
{
    Directory.CreateDirectory("Data");
    File.WriteAllText("Data/Settings.json", json);
    Settings.Initialize();
    verify(Settings.Data);
}

void Require(bool condition)
{
    if (!condition) throw new InvalidOperationException("Unexpected settings state.");
}

namespace MDPro3
{
    public static class Program { public const string jpgExpansion = ".jpg"; }
    public static class MessageManager { public static void Cast(string message) { } }
}
namespace UnityEngine { }
