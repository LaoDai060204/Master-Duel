// Minimal deterministic stand-ins for Boot's dependencies. These checks exercise
// the real Boot.cs iterators and file operations; they do not replace Unity tests.
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
            coroutine.Done = !coroutine.Iterator.MoveNext();
            coroutine.WaitingFor = coroutine.Done ? null : coroutine.Iterator.Current as Coroutine;
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
    public static class Time { public static float deltaTime; }
    public static class Mathf { public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f); }
    public sealed class WWW
    {
        public readonly byte[] bytes;
        public bool isDone => true;
        public float progress => 1f;
        public WWW(string path) { bytes = File.ReadAllBytes(path); }
    }
}

namespace UnityEngine.UI
{
    public sealed class Slider { public float value; }
    public sealed class Text { public string text; }
}

namespace UnityEngine.ResourceManagement.ResourceProviders { }

namespace UnityEngine.AddressableAssets
{
    public sealed class CompletedHandle<T>
    {
        public bool IsDone => true;
        public float PercentComplete => 1f;
        public T Result => default;
    }
    public static class Addressables
    {
        public static CompletedHandle<object> InitializeAsync() => new CompletedHandle<object>();
        public static CompletedHandle<T> LoadAssetAsync<T>(string name) => new CompletedHandle<T>();
        public static CompletedHandle<object> LoadSceneAsync(string name) => new CompletedHandle<object>();
    }
}

namespace ICSharpCode.SharpZipLib.Core
{
    public static class StreamUtils
    {
        public static void Copy(Stream source, Stream destination, byte[] buffer) => source.CopyTo(destination, buffer.Length);
    }
}

namespace ICSharpCode.SharpZipLib.Zip
{
    public sealed class ZipEntry
    {
        internal readonly ZipArchiveEntry Entry;
        internal ZipEntry(ZipArchiveEntry entry) { Entry = entry; }
        public bool IsFile => !Entry.FullName.EndsWith("/");
        public string Name => Entry.FullName;
    }
    public sealed class ZipFile : IEnumerable
    {
        private readonly ZipArchive archive;
        public ZipFile(Stream stream) { archive = new ZipArchive(stream, ZipArchiveMode.Read, true); }
        public bool IsStreamOwner { get; set; }
        public Stream GetInputStream(ZipEntry entry) => entry.Entry.Open();
        public IEnumerator GetEnumerator()
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
                yield return new ZipEntry(entry);
        }
        public void Close() => archive.Dispose();
    }
    public sealed class FastZip
    {
        public string Password { get; set; }
        public void ExtractZip(string path, string directory, string filter) => System.IO.Compression.ZipFile.ExtractToDirectory(path, directory);
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
