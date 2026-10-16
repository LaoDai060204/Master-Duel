using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed { get; private set; }
        public int DestroyCalls { get; private set; }
        public static void Destroy(Object value)
        {
            if (ReferenceEquals(value, null)) return;
            value.DestroyCalls++;
            value.Destroyed = true;
        }
        public static bool operator ==(Object left, Object right)
        {
            bool leftNull = ReferenceEquals(left, null) || left.Destroyed;
            bool rightNull = ReferenceEquals(right, null) || right.Destroyed;
            return leftNull || rightNull ? leftNull == rightNull : ReferenceEquals(left, right);
        }
        public static bool operator !=(Object left, Object right) => !(left == right);
        public override bool Equals(object value) => ReferenceEquals(this, value);
        public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
    }
    public sealed class Texture2D : Object
    {
        public readonly int width;
        public readonly int height;
        public readonly string Name;
        public Texture2D(string name, int width = 64, int height = 64)
        {
            Name = name;
            this.width = width;
            this.height = height;
        }
        public byte[] EncodeToPNG()
        {
            if (Destroyed) throw new InvalidOperationException("Encoding a destroyed texture.");
            return System.Text.Encoding.UTF8.GetBytes(Name);
        }
    }
    public static class Application
    {
        public static bool isPlaying = true;
    }
    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => JsonConvert.DeserializeObject<T>(json);
    }
    public static class Debug
    {
        public static readonly List<string> Warnings = new();
        public static void Log(object value) { }
        public static void LogError(object value) { }
        public static void LogWarning(object value) => Warnings.Add(value.ToString());
    }
}

namespace UnityEngine.Networking
{
    public sealed class DownloadHandler
    {
        public string text;
    }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { Success, ConnectionError, ProtocolError }
        public static Action<UnityWebRequest> OnSend;
        public static readonly List<UnityWebRequest> Sent = new();
        public readonly string Url;
        public readonly string Body;
        public readonly Dictionary<string, string> Headers = new();
        public Result result = Result.Success;
        public DownloadHandler downloadHandler = new();
        public string error = "HTTP error";
        public bool Disposed;
        private UnityWebRequest(string url, string body = null) { Url = url; Body = body; }
        public static UnityWebRequest Get(string url) => new(url);
        public static UnityWebRequest Post(string url, string body, string contentType) => new(url, body);
        public static UnityWebRequest PostWwwForm(string url, string body) => new(url, body);
        public void SetRequestHeader(string name, string value) => Headers[name] = value;
        public RequestOperation SendWebRequest()
        {
            Sent.Add(this);
            OnSend(this);
            return new RequestOperation();
        }
        public void Dispose() => Disposed = true;
    }
    public sealed class RequestOperation
    {
        public bool isDone => true;
        public TaskAwaiter GetAwaiter() => Task.CompletedTask.GetAwaiter();
    }
}

namespace UnityWebSocket
{
    public sealed class OpenEventArgs : EventArgs { }
    public sealed class CloseEventArgs : EventArgs { public int StatusCode; public string Reason; }
    public sealed class ErrorEventArgs : EventArgs { public string Message; }
    public sealed class MessageEventArgs : EventArgs { public string Data; }
    public sealed class WebSocket
    {
        public WebSocket(string address) { }
#pragma warning disable CS0067
        public event EventHandler<OpenEventArgs> OnOpen;
        public event EventHandler<CloseEventArgs> OnClose;
        public event EventHandler<MessageEventArgs> OnMessage;
        public event EventHandler<ErrorEventArgs> OnError;
#pragma warning restore CS0067
        public void ConnectAsync() { }
        public void CloseAsync() { }
    }
}

namespace MDPro3
{
    public class Program
    {
        public const string pngExpansion = ".png";
        public static readonly Program instance = new();
        public readonly OnlineStub online = new();
    }
    public class OnlineStub
    {
        public void ClearWatchList() { }
        public void SetWatchRooms(List<Net.MyCardRoom> rooms) { }
        public void CreateWatchRoom(Net.MyCardRoom room) { }
        public void UpdateWatchRoom(Net.MyCardRoom room) { }
        public void DeleteWatchRoom(string room) { }
    }
    public static class TextureManager
    {
        public static Func<string, Task<UnityEngine.Texture2D>> Load;
        public static Task<UnityEngine.Texture2D> LoadPicFromFileAsync(string path) => Load(path);
        public static UnityEngine.Texture2D ResizeTexture2D(UnityEngine.Texture2D source, int width, int height)
        {
            var texture = new UnityEngine.Texture2D(source.Name + " resized", width, height);
            UnityEngine.Object.Destroy(source);
            return texture;
        }
    }
    public static class Tools
    {
        public static Func<string, Task<UnityEngine.Texture2D>> Download;
        public static Task<UnityEngine.Texture2D> DownloadImageAsync(string url) => Download(url);
    }
    public static class InterString
    {
        public static string Get(string text) => text;
    }
}

namespace MDPro3.Net
{
    public static class OnlineDeck
    {
        public static Func<Task> Fetch = () => Task.CompletedTask;
        public static Task GetAllDecks() => Fetch();
    }
}
