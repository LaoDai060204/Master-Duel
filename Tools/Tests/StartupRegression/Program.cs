using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using MDPro3;
using UnityEngine;

internal static class RegressionTests
{
    private static int Main()
    {
        int failures = 0;
        foreach (Action test in new Action[]
        {
            RejectFreshUpdateWithoutDeletingConfiguration,
            RejectFreshUpdateWithoutDeletingExistingData,
            WaitForArchiveExtractionWithoutAdvancingItTwice
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
        Coroutine root = boot.StartCoroutine((IEnumerator)Invoke(boot, "CheckFile"));

        Require(Directory.GetFiles(".", "entry-*.txt").Length == 1,
            "Starting extraction should write one entry before its first frame yield.");
        Require(Config.Get("payload_install", "0") == "0", "An incomplete extraction must not be marked installed.");

        CoroutineScheduler.Complete(root, 30);
        for (int i = 0; i < 3; i++)
            Require(File.ReadAllText("entry-" + i + ".txt") == "payload-" + i, "Every entry must be extracted completely.");
        Require(Config.Get("payload_install", "0") == "1", "A completed extraction should be marked installed.");
    }
}
