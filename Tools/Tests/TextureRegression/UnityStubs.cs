// Boundary doubles for texture-loader control flow; no rendering or HTTP is simulated as validation.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static void Destroy(Object value) { if (value != null) value.Destroyed = true; }
        public static void DestroyImmediate(Object value) => Destroy(value);
    }
    public class MonoBehaviour : Object { }
    public static class Application
    {
        private static CancellationTokenSource exit = new();
        public static bool isPlaying { get; private set; } = true;
        public static CancellationToken exitCancellationToken => exit.Token;
        public static void Stop() { isPlaying = false; exit.Cancel(); }
        public static void Start() { exit.Dispose(); exit = new(); isPlaying = true; }
    }
    public enum TextureFormat { RGB24 }
    public struct Color { }
    public struct Rect { public Rect(float x, float y, float width, float height) { } }
    public class Texture2D : Object
    {
        public static int Created;
        public int width, height;
        public string name;
        public Texture2D(int width, int height) { this.width = width; this.height = height; Created++; }
        public Texture2D(int width, int height, TextureFormat format, bool mipmap) : this(width, height) { }
        public bool LoadImage(byte[] bytes) { width = 100; height = 100; return bytes.Length > 0; }
        public void ReadPixels(Rect rect, int x, int y) { }
        public void Apply() { }
        public void SetPixels(Color[] pixels) { }
        public Color GetPixel(int x, int y) => new();
    }
    public class RenderTexture { public static RenderTexture active = new(); public int width = 100, height = 100; }
    public class Material { public void SetTexture(string name, Texture2D texture) { } public void SetFloat(string name, float value) { } }
    public static class Mathf { public static int CeilToInt(float value) => (int)Math.Ceiling(value); }
    public static class Debug { public static void LogWarningFormat(string format, params object[] args) { } }
}
namespace UnityEngine.UI
{
    public class RawImage : UnityEngine.Object { public UnityEngine.Material material = new(); public UnityEngine.Texture2D texture; }
}
namespace UnityEngine.Networking
{
    public class UnityWebRequestAsyncOperation { public bool isDone; }
    public class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ConnectionError }
        public static bool Hold, ThrowOnSend, Fail;
        public static int Sent, Disposed;
        public static string LastUrl;
        public static readonly List<UnityWebRequest> Pending = new();
        public string error => "fixture request failure";
        public Result result => Fail ? Result.ConnectionError : Result.Success;
        public readonly UnityWebRequestAsyncOperation Operation = new();
        public UnityEngine.Texture2D DownloadedTexture;
        public UnityWebRequestAsyncOperation SendWebRequest()
        {
            Interlocked.Increment(ref Sent);
            if (ThrowOnSend) throw new IOException("fixture send failure");
            Operation.isDone = !Hold;
            if (Hold) lock (Pending) Pending.Add(this);
            return Operation;
        }
        public void Dispose() => Interlocked.Increment(ref Disposed);
        public static void Complete(UnityWebRequest request)
        {
            lock (Pending)
            {
                if (!Pending.Remove(request)) throw new InvalidOperationException("Request is not pending.");
                request.Operation.isDone = true;
            }
        }
        public static void CompleteAll()
        {
            lock (Pending) { foreach (var request in Pending) request.Operation.isDone = true; Pending.Clear(); }
            Hold = false;
        }
    }
    public static class UnityWebRequestTexture
    {
        public static UnityWebRequest GetTexture(string url) { UnityWebRequest.LastUrl = url; return new(); }
    }
    public static class DownloadHandlerTexture
    {
        public static UnityEngine.Texture2D GetContent(UnityWebRequest request) => request.DownloadedTexture = new(100, 100);
    }
}
namespace MDPro3.YGOSharp.OCGWrapper.Enums { [Flags] public enum CardType { Monster = 1, Pendulum = 2 } }
namespace MDPro3.YGOSharp
{
    public class Card
    {
        public int Id;
        public bool HasType(OCGWrapper.Enums.CardType type) => false;
    }
    public static class CardsManager { public static Card Get(int code, bool flag = false) => new() { Id = code }; }
}
namespace MDPro3
{
    public class CardRenderer
    {
        public bool Succeed = true, Throw;
        public int Calls;
        public bool RenderCard(int code, UnityEngine.Texture2D art)
        {
            Interlocked.Increment(ref Calls);
            if (Throw) throw new InvalidOperationException("fixture render failure");
            return Succeed;
        }
    }
    public class Program
    {
        public const string slash = "/", jpgExpansion = ".jpg", pngExpansion = ".png";
        public static string artPath, altArtPath;
        public static Program instance = new();
        public CardRenderer cardRenderer = new();
    }
    public class Sprite { public UnityEngine.Texture2D texture = new(1, 1); }
    public class TextureContainer
    {
        public Sprite unknownArt = new(), unknownCard = new();
        public UnityEngine.Texture2D GetCardUnloadTexture(YGOSharp.Card card) => unknownCard.texture;
    }
    public static class TextureManager
    {
        public static TextureContainer container = new();
        public static UnityEngine.Material GetCardMaterial(int code) => new();
    }
    public static class ZipHelper { public static List<FixtureZip> zips = new(); }
    public class FixtureZip
    {
        public string Name = "fixture.zip";
        public Dictionary<string, FixtureEntry> Entries = new();
        public ICollection<string> EntryFileNames => Entries.Keys;
        public FixtureEntry this[string name] => Entries[name];
    }
    public class FixtureEntry
    {
        public byte[] Content = new byte[] { 1 };
        public void Extract(Stream stream) => stream.Write(Content);
    }
}
namespace zFramework.Internal { }
namespace DG.Tweening
{
    public static class MaterialTween
    {
        public static void DOFloat(this UnityEngine.Material material, float value, string property, float duration) { }
    }
}
