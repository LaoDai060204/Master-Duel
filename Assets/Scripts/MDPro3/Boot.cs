using System;
using System.IO;
using UnityEngine;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using MDPro3.Utility;

namespace MDPro3
{
    public class Boot : MonoBehaviour
    {
        public Slider progressBar;
        public Text text;

        string title;
        string dots;
        float time;
        bool extracting;
        bool startupFailed;
        int totalNum;
        int nowNum;

        List<string> zips = new List<string>();

        void Start()
        {
            Application.targetFrameRate = 0;
            StartCoroutine(RunStartup(InitializeStartup()));
        }

        IEnumerator InitializeStartup()
        {
#if !UNITY_EDITOR && UNITY_ANDROID
            Environment.CurrentDirectory = Application.persistentDataPath;
            Directory.SetCurrentDirectory(Application.persistentDataPath);
            if (VersionCheck())
            {
                BetterStreamingAssets.Initialize();
                var paths = BetterStreamingAssets.GetFiles("\\", "*.zip");
                foreach (var zip in paths)
                    zips.Add(Path.GetFileName(zip).Replace(".zip", ""));

                yield return CheckFile();
            }
#else
            InitializeLanguage();
            yield return LoadMainSceneAsync();
#endif
        }

        // Drive nested iterators in one coroutine so an extraction failure cannot
        // resume the installer and mark an incomplete archive as installed.
        IEnumerator RunStartup(IEnumerator operation)
        {
            var operations = new Stack<IEnumerator>();
            operations.Push(operation);
            try
            {
                while (operations.Count > 0)
                {
                    object yielded = null;
                    bool moved = false;
                    Exception failure = null;
                    try
                    {
                        var current = operations.Peek();
                        moved = current.MoveNext();
                        if (moved)
                            yielded = current.Current;
                    }
                    catch (Exception exception)
                    {
                        failure = exception;
                    }
                    if (failure != null)
                    {
                        startupFailed = true;
                        extracting = false;
                        totalNum = 0;
                        title = "Startup failed: " + failure.Message;
                        text.text = title;
                        Debug.LogException(failure, this);
                        yield break;
                    }
                    if (!moved)
                    {
                        (operations.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (yielded is IEnumerator nested)
                        operations.Push(nested);
                    else
                        yield return yielded;
                }
            }
            finally
            {
                while (operations.Count > 0)
                    (operations.Pop() as IDisposable)?.Dispose();
            }
        }

        void Update()
        {
            if (startupFailed)
                return;
            time += Time.deltaTime;
            if (time > 0.33f)
            {
                time = 0;
                dots += ".";
                if (dots == "....")
                    dots = "";
            }
            if (extracting && totalNum != 0)
            {
                float progress = (float)nowNum / totalNum;
                progressBar.value = progress;
            }
            if (totalNum == 0)
                text.text = title + dots;
            else
                text.text = title + "(" + nowNum + Program.slash + totalNum + ")";
        }

        bool InitializeLanguage()
        {
            if (!Directory.Exists(Program.dataPath))
            {
                Directory.CreateDirectory(Program.dataPath);
                Config.Initialize(Program.configPath);
                return true;
            }
            else
            {
                Config.Initialize(Program.configPath);
                if(Config.Get("Version", "Version") == "Version")
                    return true;
                InterString.Initialize();
                return false;
            }
        }

        IEnumerator CheckFile()
        {
            //V1.2.0 Delete Folder MonsterCutin
            if(Application.version == "1.2.0.0")
            {
                if(Config.Get("Android-V1.2.0.0_install", "0") == "0")
                {
                    //Program.ClearDirectoryRecursively(new DirectoryInfo("Android/MonsterCutin"));
                    //Program.ClearDirectoryRecursively(new DirectoryInfo("Android/MasterDuel/Mate"));
                    if (Directory.Exists("Android/MonsterCutin"))
                        Directory.Delete("Android/MonsterCutin", true);
                    if (Directory.Exists("Android/MasterDuel/Mate"))
                        Directory.Delete("Android/MasterDuel/Mate", true);
                }
            }

            foreach (string zip in zips)
            {
                if (Config.Get(zip + "_install", "0") == "0")
                {
                    yield return Check(zip);
                    Config.Set(zip + "_install", "1");
                    Config.Save();
                    GC.Collect();
                }
            }
            yield return null;
            yield return LoadMainSceneAsync();
        }

        IEnumerator Check(string type)
        {
            title = InterString.Get("正在读取[?]", type + ".zip");
            nowNum = 0;
            totalNum = 0;

            string filePath = Application.streamingAssetsPath + Program.slash + type + ".zip";
            // Android supplies a jar:file URI; desktop test/streaming paths need
            // an absolute file URI (including escaping spaces and non-ASCII names).
            if (!filePath.Contains("://"))
                filePath = new Uri(Path.GetFullPath(filePath)).AbsoluteUri;
            using var request = UnityWebRequest.Get(filePath);
            request.timeout = 120;
            var download = request.SendWebRequest();
            while (!download.isDone)
            {
                progressBar.value = Mathf.Clamp01(request.downloadProgress);
                yield return null;
            }
            if (request.result != UnityWebRequest.Result.Success)
                throw new IOException("Cannot read " + type + ".zip: " + request.error);

            title = InterString.Get("正在解压[?]", type + ".zip");
            string outPath = type.Contains("_") ? type.Split('_')[0] : "";
            yield return ExtractZipFile(request.downloadHandler.data, outPath);
        }

        IEnumerator LoadMainSceneAsync()
        {
            nowNum = 0;
            totalNum = 0;
            progressBar.value = 0;

            title = InterString.Get("正在初始化");
            var ini = Addressables.InitializeAsync(false);
            try
            {
                while (!ini.IsDone)
                {
                    progressBar.value = ini.PercentComplete;
                    yield return null;
                }
                if (ini.Status != AsyncOperationStatus.Succeeded)
                    throw new InvalidOperationException("Addressables initialization failed.", ini.OperationException);
            }
            finally
            {
                Addressables.Release(ini);
            }

            title = InterString.Get("正在读取数据");
            var handle = Addressables.LoadAssetAsync<Items>("Items");
            while (!handle.IsDone)
            {
                progressBar.value = handle.PercentComplete;
                yield return null;
            }
            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                var failure = handle.OperationException;
                Addressables.Release(handle);
                throw new InvalidOperationException("Cannot load the Items asset. Check Addressables content.", failure);
            }
            Program.items = handle.Result;

            Config.Initialize(Program.configPath);
            Config.Set("Version", Application.version[..5]);
            Config.Save();

            title = InterString.Get("正在进入游戏");
            var load = Addressables.LoadSceneAsync("SceneMain");
            while (!load.IsDone)
            {
                yield return null;
                progressBar.value = load.PercentComplete;
            }
            if (load.Status != AsyncOperationStatus.Succeeded)
            {
                var failure = load.OperationException;
                Addressables.Release(load);
                Program.items = null;
                Addressables.Release(handle);
                throw new InvalidOperationException("Cannot load SceneMain. Check Addressables content.", failure);
            }
        }

        public static void FastExtractZipFile(string file, string dir, string password = "")
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            FastZip zip = new FastZip();
            zip.Password = password;
            zip.ExtractZip(file, dir, "");
        }

        IEnumerator ExtractZipFile(byte[] data, string outFolder)
        {
            using var stream = new MemoryStream(data);
            using var archive = new ZipFile(stream);
            // Validate every entry before writing any payload, including entries
            // with Windows separators when this code is running on Linux.
            foreach (ZipEntry entry in archive)
                ArchivePath.Resolve(outFolder, entry.Name);

            totalNum = (int)archive.Count;
            nowNum = 0;
            extracting = true;
            var buffer = new byte[4096];
            try
            {
                foreach (ZipEntry entry in archive)
                {
                    nowNum++;
                    if (!entry.IsFile)
                        continue;
                    string path = ArchivePath.Resolve(outFolder, entry.Name);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (Stream source = archive.GetInputStream(entry))
                    using (FileStream destination = File.Create(path))
                        StreamUtils.Copy(source, destination, buffer);
                    yield return null;
                }
            }
            finally
            {
                extracting = false;
            }
        }

        bool VersionCheck()
        {
            var firstInstall = InitializeLanguage();

            var installVersion = Application.version;
            var installedVersion = Config.Get("Version", "Version");
            if (installedVersion == "Version")
                firstInstall = true;

            if(firstInstall)
            {
                if(installVersion.Length > 5 || !installVersion.EndsWith("0"))
                {
                    title = "不能直接安装更新包。Can not install update apk directly.";
                    return false;
                }
                else
                    return true;
            }
            else //firstInstall
            {
                if(installVersion == installedVersion)
                    return true;

                if (installVersion.Length > 5)
                {
                    if(installVersion.EndsWith("0"))
                    {
                        if (InstallNext(installedVersion, installVersion))
                            return true;
                        else if (installVersion[..5] == installedVersion)
                            return true;
                        else
                        {
                            title = InterString.Get("当前更新包需要的版本：「[?]」。", VersionPre(installVersion));
                            title += InterString.Get("已安装版本：「[?]」。", installedVersion);
                            return false;
                        }
                    }
                    else
                    {
                        if (installVersion.Substring(0, 5) == installedVersion)
                            return true;
                        else
                        {
                            title = InterString.Get("当前更新包需要的版本：「[?]」。", installVersion.Substring(0, 5));
                            title += InterString.Get("已安装版本：「[?]」。", installedVersion);
                            return false;
                        }
                    }
                }
                else
                {
                    if (installVersion.EndsWith("0"))
                        return true;
                    else
                    {
                        if(VersionPre(installVersion) == installedVersion.Substring(0, 5))
                            return true;
                        else
                        {
                            title = InterString.Get("当前更新包需要的版本：「[?]」。", VersionPre(installVersion));
                            title += InterString.Get("已安装版本：「[?]」。", installedVersion);
                            return false;
                        }
                    }
                }
            }
        }

        bool InstallNext(string installedVersion, string installVersion)
        {
            var installedInt = GetVersionInt(installedVersion);
            var installInt = GetVersionInt(installVersion);
            if(installInt -  installedInt == 1)
                return true;
            else
                return false;
        }

        string VersionPre(string version)
        {
            var versionInt = GetVersionInt(version);
            string returnValue = (versionInt - 1).ToString("D3");
            return returnValue.Substring(0, 1) + "." + returnValue.Substring(1, 1) + "." + returnValue.Substring(2, 1);
        }

        int GetVersionInt(string version)
        {
            string textInt = version.Substring(0, 1) + version.Substring(2, 1) + version.Substring(4, 1);
            return int.Parse(textInt);
        }
    }
}
