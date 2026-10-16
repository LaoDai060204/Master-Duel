// Minimal deterministic stand-ins for Boot's dependencies. These checks exercise
// the real Boot.cs iterators, file operations and shipped SharpZipLib; they do not replace Unity tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace UnityEngine
{
    public class MonoBehaviour
    {
        public Coroutine StartCoroutine(IEnumerator iterator) => CoroutineScheduler.Start(iterator);
    }

    public sealed class Coroutine
    {
        internal readonly IEnumerator Iterator;
        internal Coroutine WaitingFor;
        internal bool Done;
        internal Coroutine(IEnumerator iterator) { Iterator = iterator; }
    }

    public static class CoroutineScheduler
    {
        private static readonly List<Coroutine> running = new List<Coroutine>();
        public static Coroutine Start(IEnumerator iterator)
        {
            var coroutine = new Coroutine(iterator);
            running.Add(coroutine);
            Advance(coroutine);
            return coroutine;
        }
        private static void Advance(Coroutine coroutine)
        {
            if (coroutine.Done || (coroutine.WaitingFor != null && !coroutine.WaitingFor.Done))
                return;
            try
            {
                coroutine.Done = !coroutine.Iterator.MoveNext();
                coroutine.WaitingFor = coroutine.Done ? null : coroutine.Iterator.Current as Coroutine;
            }
            catch (Exception error)
            {
                // Unity logs and stops a failed child; its parent can resume.
                coroutine.Done = true;
                Debug.LogException(error, null);
            }
        }
        public static void Complete(Coroutine coroutine, int maxFrames)
        {
            for (int frame = 0; frame < maxFrames && !coroutine.Done; frame++)
                foreach (Coroutine item in running.ToArray())
                    Advance(item);
            if (!coroutine.Done)
                throw new Exception("Coroutine did not complete within the frame budget.");
        }
        public static void Reset() => running.Clear();
    }

    public static class Application
    {
        public static int targetFrameRate;
        public static string persistentDataPath;
        public static string streamingAssetsPath;
        public static string version;
    }
    public static class Debug
    {
        public static readonly List<Exception> Exceptions = new List<Exception>();
        public static void LogException(Exception error, object context) => Exceptions.Add(error);
    }
    public static class Time { public static float deltaTime; }
    public static class Mathf { public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f); }
    public sealed class WWW
    {
        public readonly byte[] bytes;
        public bool isDone => true;
        public float progress => 1f;
        public WWW(string path) { bytes = File.Exists(path) ? File.ReadAllBytes(path) : null; }
    }
}

namespace UnityEngine.UI
{
    public sealed class Slider { public float value; }
    public sealed class Text { public string text; }
}

namespace UnityEngine.Networking
{
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ConnectionError, ProtocolError }
        public static int Disposed;
        private readonly string path;
        public int timeout;
        public Result result;
        public string error;
        public float downloadProgress => 1f;
        public DownloadHandler downloadHandler = new DownloadHandler();
        private UnityWebRequest(string uri) { path = new Uri(uri).LocalPath; }
        public static UnityWebRequest Get(string path) => new UnityWebRequest(path);
        public DownloadOperation SendWebRequest()
        {
            try
            {
                downloadHandler.data = File.ReadAllBytes(path);
                result = Result.Success;
            }
            catch (IOException exception)
            {
                result = Result.ConnectionError;
                error = exception.Message;
            }
            return new DownloadOperation();
        }
        public void Dispose() => Disposed++;
    }
    public sealed class DownloadHandler { public byte[] data; }
    public sealed class DownloadOperation { public bool isDone => true; }
}

namespace UnityEngine.ResourceManagement.ResourceProviders { }
namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { None, Succeeded, Failed }
}
namespace UnityEngine.AddressableAssets
{
    using UnityEngine.ResourceManagement.AsyncOperations;
    public sealed class CompletedHandle<T>
    {
        public bool IsDone => true;
        public float PercentComplete => 1f;
        public T Result;
        public AsyncOperationStatus Status;
        public Exception OperationException;
    }
    public static class Addressables
    {
        public static string FailureStage;
        public static int ItemLoads;
        public static int SceneLoads;
        public static int Releases;
        public static void Reset()
        {
            FailureStage = null;
            ItemLoads = SceneLoads = Releases = 0;
        }
        private static CompletedHandle<T> Finish<T>(string stage, T result) => new CompletedHandle<T>
        {
            Result = FailureStage == stage ? default : result,
            Status = FailureStage == stage ? AsyncOperationStatus.Failed : AsyncOperationStatus.Succeeded,
            OperationException = FailureStage == stage ? new IOException(stage + " unavailable") : null
        };
        public static CompletedHandle<object> InitializeAsync(bool autoReleaseHandle = true) => Finish("initialize", new object());
        public static CompletedHandle<T> LoadAssetAsync<T>(string name)
        {
            ItemLoads++;
            return Finish("items", (T)(object)new MDPro3.Items());
        }
        public static CompletedHandle<object> LoadSceneAsync(string name)
        {
            SceneLoads++;
            return Finish("scene", new object());
        }
        public static void Release<T>(CompletedHandle<T> handle) => Releases++;
    }
}

namespace MDPro3
{
    public sealed class Items { }
    public static class Program
    {
        public const string dataPath = "Data/";
        public const string configPath = "Data/config.conf";
        public const string slash = "/";
        public static Items items;
    }
    public static class InterString
    {
        public static void Initialize() { }
        public static string Get(string value, string argument = null) => value;
    }
    public static class Config
    {
        private static readonly Dictionary<string, string> values = new Dictionary<string, string>();
        private static string path;
        public static void Reset() { values.Clear(); path = null; }
        public static void Initialize(string file)
        {
            path = file;
            values.Clear();
            if (!File.Exists(path))
                File.WriteAllText(path, "Language->en-US\n");
            foreach (string line in File.ReadAllLines(path))
            {
                string[] parts = line.Split("->", 2);
                if (parts.Length == 2)
                    values[parts[0]] = parts[1];
            }
        }
        public static string Get(string name, string fallback)
        {
            if (!values.TryGetValue(name, out string value))
            {
                values[name] = value = fallback;
                Save();
            }
            return value;
        }
        public static void Set(string name, string value) => values[name] = value;
        public static void Save()
        {
            using var writer = new StreamWriter(path);
            foreach (var item in values)
                writer.WriteLine(item.Key + "->" + item.Value);
        }
    }
}
