using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using MDPro3;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;

internal static class RegressionTests
{
    private static int Main()
    {
        int failures = 0;
        foreach (Action test in new Action[]
        {
            RejectFreshUpdateWithoutDeletingConfiguration,
            RejectFreshUpdateWithoutDeletingExistingData,
            WaitForArchiveExtractionWithoutAdvancingItTwice,
            ArchiveRootDirectoryEntryIsAccepted,
            FailedInitializationDoesNotLoadItemsOrScene,
            FailedItemsDoesNotEnterMainSceneOrSaveVersion,
            FailedSceneReportsTheFailedStage,
            SuccessfulStartupLoadsItemsAndScene,
            MissingArchiveIsNotMarkedInstalled,
            CorruptArchiveIsNotMarkedInstalled,
            RejectTraversalBeforeWritingAnyEntries,
            RejectWindowsTraversalOnEveryPlatform,
            MissingLegacyDirectoriesDoNotStopStartup
        })
        {
            string originalDirectory = Environment.CurrentDirectory;
            string testDirectory = Path.Combine(Path.GetTempPath(), "mdpro3-startup-" + Guid.NewGuid());
            Directory.CreateDirectory(testDirectory);
            try
            {
                Environment.CurrentDirectory = testDirectory;
                Config.Reset();
                CoroutineScheduler.Reset();
                Addressables.Reset();
                Debug.Exceptions.Clear();
                UnityWebRequest.Disposed = 0;
                MDPro3.Program.items = null;
                Application.version = "1.2.5";
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }
            catch (Exception exception)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + exception.GetBaseException().Message);
            }
            finally
            {
                CoroutineScheduler.Reset();
                Environment.CurrentDirectory = originalDirectory;
                Directory.Delete(testDirectory, true);
            }
        }
        return failures == 0 ? 0 : 1;
    }

    private static Boot CreateBoot() => new Boot
    {
        progressBar = new UnityEngine.UI.Slider(),
        text = new UnityEngine.UI.Text()
    };

    private static object Invoke(Boot boot, string name) => typeof(Boot)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(boot, null);

    private static Coroutine Start(Boot boot, string operation)
    {
        var iterator = (IEnumerator)Invoke(boot, operation);
        var driver = typeof(Boot).GetMethod("RunStartup", BindingFlags.NonPublic | BindingFlags.Instance);
        // The fallback allows running the same cases against the pre-fix source.
        if (driver != null)
            iterator = (IEnumerator)driver.Invoke(boot, new object[] { iterator });
        return boot.StartCoroutine(iterator);
    }

    private static void PrepareConfiguration()
    {
        Directory.CreateDirectory(MDPro3.Program.dataPath);
        Config.Initialize(MDPro3.Program.configPath);
    }

    private static Boot RunMain()
    {
        PrepareConfiguration();
        var boot = CreateBoot();
        CoroutineScheduler.Complete(Start(boot, "LoadMainSceneAsync"), 30);
        return boot;
    }

    private static Boot RunArchive(string name, Action<ZipArchive> write = null)
    {
        PrepareConfiguration();
        Application.streamingAssetsPath = Path.GetFullPath("Streaming Assets");
        Directory.CreateDirectory(Application.streamingAssetsPath);
        if (write != null)
            using (var archive = ZipFile.Open(Path.Combine(Application.streamingAssetsPath, name + ".zip"), ZipArchiveMode.Create))
                write(archive);
        var boot = CreateBoot();
        var zips = (List<string>)typeof(Boot).GetField("zips", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boot);
        zips.Add(name);
        CoroutineScheduler.Complete(Start(boot, "CheckFile"), 30);
        return boot;
    }

    private static void WriteEntry(ZipArchive archive, string path, string text)
    {
        using var writer = new StreamWriter(archive.CreateEntry(path).Open());
        writer.Write(text);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }

    private static void RejectFreshUpdateWithoutDeletingConfiguration()
    {
        Require(!(bool)Invoke(CreateBoot(), "VersionCheck"), "An update APK must require the base installation.");
        Require(File.Exists(MDPro3.Program.configPath), "Rejecting an update must preserve configuration.");
    }

    private static void RejectFreshUpdateWithoutDeletingExistingData()
    {
        Directory.CreateDirectory(MDPro3.Program.dataPath);
        string sentinel = Path.Combine(MDPro3.Program.dataPath, "existing-data.txt");
        File.WriteAllText(sentinel, "keep this data");
        Require(!(bool)Invoke(CreateBoot(), "VersionCheck"), "The update should be rejected without an installed version.");
        Require(File.ReadAllText(sentinel) == "keep this data", "Rejecting an update must preserve existing data.");
    }

    private static void WaitForArchiveExtractionWithoutAdvancingItTwice()
    {
        Directory.CreateDirectory(MDPro3.Program.dataPath);
        Config.Initialize(MDPro3.Program.configPath);
        Application.streamingAssetsPath = Path.GetFullPath("StreamingAssets");
        Directory.CreateDirectory(Application.streamingAssetsPath);
        using (var archive = ZipFile.Open(Path.Combine(Application.streamingAssetsPath, "payload.zip"), ZipArchiveMode.Create))
        {
            for (int i = 0; i < 3; i++)
            {
                using var writer = new StreamWriter(archive.CreateEntry("entry-" + i + ".txt").Open());
                writer.Write("payload-" + i);
            }
        }

        Boot boot = CreateBoot();
        var zips = (List<string>)typeof(Boot).GetField("zips", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boot);
        zips.Add("payload");
        Coroutine root = Start(boot, "CheckFile");

        Require(Directory.GetFiles(".", "entry-*.txt").Length == 1,
            "Starting extraction should write one entry before its first frame yield.");
        Require(Config.Get("payload_install", "0") == "0", "An incomplete extraction must not be marked installed.");

        CoroutineScheduler.Complete(root, 30);
        for (int i = 0; i < 3; i++)
            Require(File.ReadAllText("entry-" + i + ".txt") == "payload-" + i, "Every entry must be extracted completely.");
        Require(Config.Get("payload_install", "0") == "1", "A completed extraction should be marked installed.");
        Require(UnityWebRequest.Disposed == 1, "The archive request should be disposed after extraction.");
        Require(!(bool)typeof(Boot).GetField("extracting", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boot),
            "Extraction progress must stop updating after extraction completes.");
    }

    private static void FailedInitializationDoesNotLoadItemsOrScene()
    {
        Addressables.FailureStage = "initialize";
        var boot = RunMain();
        Require(Addressables.ItemLoads == 0 && Addressables.SceneLoads == 0,
            "A failed catalog must stop dependent loads.");
        Require(boot.text.text.Contains("initialization failed"), "The startup UI should report catalog failure.");
        Require(Addressables.Releases == 1, "Initialization handles must be released even on failure.");
    }

    private static void ArchiveRootDirectoryEntryIsAccepted()
    {
        var boot = RunArchive("payload", archive =>
        {
            archive.CreateEntry("./");
            WriteEntry(archive, "./payload.txt", "complete");
        });
        Require(Config.Get("payload_install", "0") == "1", "A harmless root directory entry must not reject the archive.");
        Require(File.ReadAllText("payload.txt") == "complete", "Entries below ./ must be extracted normally.");
        Require(Debug.Exceptions.Count == 0, "A valid archive must not report a startup failure.");
    }

    private static void FailedItemsDoesNotEnterMainSceneOrSaveVersion()
    {
        Addressables.FailureStage = "items";
        var boot = RunMain();
        Require(MDPro3.Program.items == null && Addressables.SceneLoads == 0,
            "A missing Items asset must stop scene startup.");
        Require(Config.Get("Version", "missing") == "missing", "A failed asset load must not save a successful version.");
        Require(boot.text.text.Contains("Items"), "The startup UI should identify the missing asset.");
        Require(Addressables.Releases == 2, "Failed Items and initialization handles should both be released.");
    }

    private static void FailedSceneReportsTheFailedStage()
    {
        Addressables.FailureStage = "scene";
        var boot = RunMain();
        Require(boot.text.text.Contains("SceneMain"), "Scene failure should be visible instead of leaving a loading screen.");
        Require(Addressables.Releases == 3 && MDPro3.Program.items == null,
            "Failed scene startup should release its handles and the unused Items asset.");
    }

    private static void SuccessfulStartupLoadsItemsAndScene()
    {
        RunMain();
        Require(MDPro3.Program.items != null && Addressables.SceneLoads == 1, "Successful startup must still enter the game.");
        Require(Config.Get("Version", "missing") == "1.2.5", "A successful asset load should record the installation version.");
        Require(Addressables.Releases == 1 && Debug.Exceptions.Count == 0, "Only the transient initialization handle should be released.");
    }

    private static void MissingArchiveIsNotMarkedInstalled()
    {
        var boot = RunArchive("missing");
        Require(Config.Get("missing_install", "0") == "0", "An unreadable archive must remain pending installation.");
        Require(Addressables.SceneLoads == 0 && boot.text.text.Contains("Cannot read missing.zip"),
            "Archive read failures must stop startup with a useful error.");
        Require(UnityWebRequest.Disposed == 1, "Failed requests must be disposed.");
    }

    private static void CorruptArchiveIsNotMarkedInstalled()
    {
        PrepareConfiguration();
        Application.streamingAssetsPath = Path.GetFullPath("Streaming Assets");
        Directory.CreateDirectory(Application.streamingAssetsPath);
        File.WriteAllText(Path.Combine(Application.streamingAssetsPath, "corrupt.zip"), "not a ZIP");
        var boot = RunArchive("corrupt");
        Require(Config.Get("corrupt_install", "0") == "0" && Addressables.SceneLoads == 0,
            "Extraction failure must stop the parent installer before it saves a completion marker.");
        Require(boot.text.text.StartsWith("Startup failed:"), "ZIP failures must be visible in the startup UI.");
        Require(UnityWebRequest.Disposed == 1, "The failed extraction must dispose its request.");
    }

    private static void RejectTraversalBeforeWritingAnyEntries()
    {
        var boot = RunArchive("Output_payload", archive =>
        {
            WriteEntry(archive, "safe.txt", "should not be written");
            WriteEntry(archive, "../escaped.txt", "outside destination");
        });
        Require(!File.Exists("escaped.txt") && !File.Exists("Output/safe.txt"),
            "Every path must be checked before writing any archive entries.");
        Require(Config.Get("Output_payload_install", "0") == "0" && boot.text.text.Contains("escapes"),
            "Traversal failures must leave installation pending and explain the problem.");
    }

    private static void RejectWindowsTraversalOnEveryPlatform()
    {
        RunArchive("Output_payload", archive => WriteEntry(archive, "..\\escaped.txt", "outside destination"));
        Require(Config.Get("Output_payload_install", "0") == "0" && Addressables.SceneLoads == 0,
            "Backslash traversal must also be rejected on non-Windows hosts.");
    }

    private static void MissingLegacyDirectoriesDoNotStopStartup()
    {
        PrepareConfiguration();
        Application.version = "1.2.0.0";
        var boot = CreateBoot();
        CoroutineScheduler.Complete(Start(boot, "CheckFile"), 30);
        Require(Debug.Exceptions.Count == 0 && Addressables.SceneLoads == 1,
            "Legacy cleanup must tolerate installations without those optional directories.");
    }
}
