using System.Text.Json;

namespace UnityEngine
{
    public static class Application { public static bool isPlaying = true; }
    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
        public static void LogFormat(string format, params object[] values) { }
    }
    public static class JsonUtility
    {
        static readonly JsonSerializerOptions Options = new() { IncludeFields = true };
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
        public static string ToJson(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);
    }
}

namespace UnityEngine.Networking
{
    public sealed class DownloadHandler : IDisposable
    {
        public string text;
        public void Dispose() { }
    }
    public sealed class UnityWebRequest : IDisposable
    {
        public enum Result { InProgress, Success, ConnectionError, ProtocolError, DataProcessingError }
        public sealed class Operation { public volatile bool isDone = true; }
        public record Reply(Result Result, string Text, Action<UnityWebRequest> OnSend = null);
        public static Queue<Reply> Replies = new();
        public static List<UnityWebRequest> Sent = new();
        public readonly string url;
        public readonly string body;
        public Result result;
        public string error => result == Result.Success ? null : "Simulated request failure";
        public DownloadHandler downloadHandler = new();
        public Operation operation;
        UnityWebRequest(string url, string body = null) { this.url = url; this.body = body; }
        public static UnityWebRequest Get(string url) => new(url);
        public static UnityWebRequest Post(string url, string body, string type) => new(url, body);
        public static UnityWebRequest PostWwwForm(string url, string body) => new(url, body);
        public void SetRequestHeader(string key, string value) { }
        public Operation SendWebRequest()
        {
            if (Replies.Count == 0) throw new InvalidOperationException("Unexpected network request: " + url);
            var reply = Replies.Dequeue();
            Sent.Add(this);
            operation = new Operation();
            result = reply.Result;
            downloadHandler.text = reply.Text;
            reply.OnSend?.Invoke(this);
            return operation;
        }
        public void Dispose() { }
    }
}

namespace MDPro3.Utility
{
    public static class TaskUtility
    {
        public static async Task WaitUntil(Func<bool> predicate)
        {
            while (!predicate()) await Task.Delay(1);
        }
    }
}
namespace MDPro3
{
    public static class Program { public static string deckPath; public const string ydkExpansion = ".ydk"; }
    public static class MessageManager
    {
        public static List<string> Messages = new();
        public static void Cast(string text) => Messages.Add(text);
    }
    public static class InterString
    {
        public static string Get(string text, params string[] values) => text;
    }
    public static class Tools { public static bool StringIsLowerAlphaNumeric(string value) => true; }
}
namespace MDPro3.Net
{
    public static class MyCard { public static Account account; }
    public sealed class Account { public User user = new(); public string token = "fake-local-test-token"; }
    public sealed class User { public int id = 42; public string username = "local-test"; }
}
namespace MDPro3.YGOSharp.OCGWrapper.Enums
{
    public enum CardType { Token }
}
namespace MDPro3.YGOSharp
{
    public sealed class Banlist { public int GetQuantity(int code) => 3; }
    public sealed class Card
    {
        public int Id, Alias, Ot;
        public bool HasType(OCGWrapper.Enums.CardType value) => false;
    }
    public static class CardsManager { public static Card Get(int code) => new() { Id = code }; }
}
