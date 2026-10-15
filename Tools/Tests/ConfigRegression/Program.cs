using System.Globalization;
using MDPro3;
using MDPro3.Utility;
using UnityEngine;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var testRoot = Directory.CreateTempSubdirectory("master-duel-config-tests-").FullName;
var configPath = "";
#if UNITY_ANDROID
const float platformUIScale = 1.5f;
#else
const float platformUIScale = 1f;
#endif

// Runs Config.cs itself, with only the Unity/runtime dependencies replaced.
var cases = new (string Name, Action Run)[]
{
    ("Load and save the existing configuration format", () =>
    {
        Initialize("Language->ja-JP\r\nName->Duelist\r\nVolume->725\r\n");
        Equal("ja-JP", Config.Get(Language.ConfigName, "en-US"));
        Equal("Duelist", Config.Get("Name", "Unknown"));
        Equal(0.725f, Config.GetFloat("Volume", 0.7f));
        Config.SetFloat("Volume", 0.625f);
        Config.Save();
        Equal("Language->ja-JP\r\nName->Duelist\r\nVolume->625\r\n", File.ReadAllText(configPath));
    }),
    ("New configuration does not inherit the previous file", () =>
    {
        Initialize("Language->ja-JP\r\nOldSetting->private\r\n");
        var newPath = Path.Combine(testRoot, Guid.NewGuid() + ".conf");
        Application.systemLanguage = SystemLanguage.English;
        Config.Initialize(newPath);
        Equal(false, Config.Have("OldSetting"));
        Equal(Language.English, Config.Get(Language.ConfigName, "missing"));
        Equal("Language->en-US\r\n", File.ReadAllText(newPath));
    }),
    ("Existing configuration also replaces the previous file", () =>
    {
        Initialize("OldSetting->private\r\n");
        Initialize("NewSetting->value\r\n");
        Equal(false, Config.Have("OldSetting"));
        Equal("value", Config.Get("NewSetting", "missing"));
    }),
    ("New configuration saves a single language entry across reloads", () =>
    {
        var newPath = Path.Combine(testRoot, Guid.NewGuid() + ".conf");
        Application.systemLanguage = SystemLanguage.English;
        Config.Initialize(newPath);
        Config.Save();
        Equal("Language->en-US\r\n", File.ReadAllText(newPath));
        Config.Initialize(newPath);
        Config.Save();
        Equal("Language->en-US\r\n", File.ReadAllText(newPath));
    }),
    ("First-run language follows the system language", () =>
    {
        Application.systemLanguage = SystemLanguage.ChineseTraditional;
        Config.Initialize(Path.Combine(testRoot, Guid.NewGuid() + ".conf"));
        Equal(Language.TraditionalChinese, Config.Get(Language.ConfigName, "missing"));
        Application.systemLanguage = SystemLanguage.English;
    }),
    ("Malformed stored float uses its caller's default", () =>
    {
        Initialize("Volume->broken\r\n");
        Equal(0.7f, Config.GetFloat("Volume", 0.7f));
    }),
    ("Overflowing stored float uses its caller's default", () =>
    {
        Initialize("Volume->999999999999999999999\r\n");
        Equal(0.7f, Config.GetFloat("Volume", 0.7f));
    }),
    ("Valid zero and signed stored floats are preserved", () =>
    {
        Initialize("Zero->0\r\nSigned->-1250\r\n");
        Equal(0f, Config.GetFloat("Zero", 0.7f));
        Equal(-1.25f, Config.GetFloat("Signed", 0.7f));
    }),
    ("Missing float stores and returns the default", () =>
    {
        Initialize("");
        Equal(0.7f, Config.GetFloat("Volume", 0.7f));
        Equal("Volume->700\r\n", File.ReadAllText(configPath));
    }),
    ("Malformed UI scale uses the platform default and still applies its cap", () =>
    {
        Initialize("UIScale->broken\r\n");
        Equal(platformUIScale, Config.GetUIScale());
        Equal(0.9f, Config.GetUIScale(0.9f));
    }),
    ("UI scale rejects non-finite and non-positive values", () =>
    {
        foreach (var value in new[] { "NaN", "Infinity", "-Infinity", "1E40", "0", "-1000" })
        {
            Initialize("UIScale->" + value + "\r\n");
            Equal(platformUIScale, Config.GetUIScale());
        }
    }),
    ("Valid UI scale remains fractional and capped", () =>
    {
        Initialize("UIScale->1250.5\r\n");
        Equal(1.2505f, Config.GetUIScale());
        Equal(1.1f, Config.GetUIScale(1.1f));
    }),
    ("Localized UI scale remains readable", () =>
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            Initialize("UIScale->1250,5\r\n");
            Equal(1.2505f, Config.GetUIScale());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }),
    ("Missing UI scale stores the platform default", () =>
    {
        Initialize("");
        Equal(platformUIScale, Config.GetUIScale());
        Equal("UIScale->" + (platformUIScale * 1000).ToString() + "\r\n", File.ReadAllText(configPath));
    })
};

var failures = 0;
try
{
    foreach (var test in cases)
    {
        try
        {
            test.Run();
            Console.WriteLine("PASS " + test.Name);
        }
        catch (Exception exception)
        {
            failures++;
            Console.WriteLine("FAIL " + test.Name + ": " + exception.Message);
        }
    }
}
finally
{
    Directory.Delete(testRoot, true);
}
Console.WriteLine($"{cases.Length - failures}/{cases.Length} configuration checks passed.");
return failures == 0 ? 0 : 1;

void Initialize(string content)
{
    configPath = Path.Combine(testRoot, Guid.NewGuid() + ".conf");
    File.WriteAllText(configPath, content);
    Config.Initialize(configPath);
}

void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
}

namespace UnityEngine
{
    public enum SystemLanguage { English, ChineseSimplified, ChineseTraditional, Spanish, Japanese, Korean }
    public static class Application
    {
        public static SystemLanguage systemLanguage = SystemLanguage.English;
    }
    public static class Debug
    {
        public static void Log(object message) { }
    }
}

namespace MDPro3
{
    public static class Program
    {
        public static bool noAccess;
    }
}

namespace MDPro3.Utility
{
    public static class Language
    {
        public const string ConfigName = "Language";
        public const string SimplifiedChinese = "zh-CN";
        public const string TraditionalChinese = "zh-TW";
        public const string English = "en-US";
        public const string Spanish = "es-ES";
        public const string Japanese = "ja-JP";
        public const string Korean = "ko-KR";
    }
}
