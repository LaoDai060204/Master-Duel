using System.Collections;
using System.Reflection;
using MDPro3;
using MDPro3.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

var originalDirectory = Environment.CurrentDirectory;
var testRoot = Directory.CreateTempSubdirectory("master-duel-mycard-").FullName;
var tests = new List<(string Name, Func<Task> Run)>
{
    ("Login serializes quotes, slashes, control characters and Unicode without changing credentials", async () =>
    {
        const string account = "Duelist\"\\\n雪";
        const string password = "a\"b\\c\r\n\t!";
        UnityWebRequest.OnSend = request =>
        {
            var json = JObject.Parse(request.Body);
            Equal(account, (string)json["account"]);
            Equal(password, (string)json["password"]);
            Equal(2, json.Properties().Count());
            request.downloadHandler.text = "{\"user\":{\"username\":\"Duelist\"},\"token\":\"test\"}";
        };
        var result = await MyCard.Login(account, password);
        Equal("Duelist", result.user.username);
        Equal(true, UnityWebRequest.Sent.Single().Disposed);
    }),
    ("Login observes deck-fetch failures instead of silently reporting successful initialization", async () =>
    {
        UnityWebRequest.OnSend = request => request.downloadHandler.text = "{\"user\":{\"username\":\"Duelist\"}}";
        OnlineDeck.Fetch = () => Task.FromException(new IOException("Deck fetch failed"));
        try { await MyCard.Login("test", "test"); }
        catch (IOException exception) when (exception.Message == "Deck fetch failed") { return; }
        throw new Exception("Expected deck-fetch error to reach the caller.");
    }),
    ("Avatar downloads are reused from memory without reloading the disk cache", async () =>
    {
        var texture = new Texture2D("Alice");
        SetupAvatar("https://avatars.test/alice.png", texture);
        Same(texture, await MyCard.GetAvatarAsync("Alice"));
        TextureManager.Load = _ => throw new Exception("Unexpected disk reload.");
        Tools.Download = _ => throw new Exception("Unexpected second download.");
        Same(texture, await MyCard.GetAvatarAsync("Alice"));
        Equal(1, UnityWebRequest.Sent.Count);
    }),
    ("Avatar URLs with equal basenames retain separate memory and disk entries", async () =>
    {
        var first = new Texture2D("Alice");
        var second = new Texture2D("Bob");
        SetupAvatar("https://avatars.test/alice/avatar.png?v=1", first);
        Same(first, await MyCard.GetAvatarAsync("Alice"));
        SetupAvatar("https://avatars.test/bob/avatar.png?v=1", second);
        Same(second, await MyCard.GetAvatarAsync("Bob"));
        Same(first, await MyCard.GetAvatarAsync("Alice"));
        Same(second, await MyCard.GetAvatarAsync("Bob"));
        var files = Directory.GetFiles("Picture/MyCardAvatars");
        Equal(2, files.Length);
        Equal(true, files.All(file => Path.GetFileNameWithoutExtension(file).Length == 64));
    }),
    ("A shared avatar URL reuses one texture for both users", async () =>
    {
        var texture = new Texture2D("shared");
        SetupAvatar("https://avatars.test/shared.png", texture);
        Same(texture, await MyCard.GetAvatarAsync("Alice"));
        Tools.Download = _ => throw new Exception("Shared avatar was downloaded twice.");
        Same(texture, await MyCard.GetAvatarAsync("Bob"));
        Equal(2, UnityWebRequest.Sent.Count);
    }),
    ("Missing disk cache after texture destruction downloads a replacement", async () =>
    {
        var first = new Texture2D("old");
        SetupAvatar("https://avatars.test/alice.png", first);
        await MyCard.GetAvatarAsync("Alice");
        UnityEngine.Object.Destroy(first);
        foreach (var file in Directory.GetFiles("Picture/MyCardAvatars")) File.Delete(file);
        var replacement = new Texture2D("replacement");
        Tools.Download = _ => Task.FromResult(replacement);
        Same(replacement, await MyCard.GetAvatarAsync("Alice"));
        Equal(2, UnityWebRequest.Sent.Count);
        Same(replacement, await MyCard.GetAvatarAsync("Alice"));
    }),
    ("Disk cache reload restores the in-memory entry", async () =>
    {
        var first = new Texture2D("first");
        SetupAvatar("https://avatars.test/alice.png", first);
        await MyCard.GetAvatarAsync("Alice");
        UnityEngine.Object.Destroy(first);
        var restored = new Texture2D("restored");
        TextureManager.Load = _ => Task.FromResult(restored);
        Tools.Download = _ => throw new Exception("Unexpected network download.");
        Same(restored, await MyCard.GetAvatarAsync("Alice"));
        Same(restored, await MyCard.GetAvatarAsync("Alice"));
        Equal(1, UnityWebRequest.Sent.Count);
    }),
    ("Disk write failure preserves the in-memory avatar and reports the cache problem", async () =>
    {
        Directory.CreateDirectory("Picture");
        File.WriteAllText("Picture/MyCardAvatars", "block cache directory");
        var texture = new Texture2D("uncached");
        SetupAvatar("https://avatars.test/avatar.png", texture);
        Same(texture, await MyCard.GetAvatarAsync("Alice"));
        Same(texture, await MyCard.GetAvatarAsync("Alice"));
        Equal(1, Debug.Warnings.Count);
        Equal(1, UnityWebRequest.Sent.Count);
    }),
    ("Concurrent requests retain one texture and destroy the unused duplicate", async () =>
    {
        SetupAvatar("https://avatars.test/alice.png", null);
        var firstDownload = new TaskCompletionSource<Texture2D>();
        var secondDownload = new TaskCompletionSource<Texture2D>();
        int requests = 0;
        Tools.Download = _ => ++requests == 1 ? firstDownload.Task : secondDownload.Task;
        var firstTask = MyCard.GetAvatarAsync("Alice");
        var secondTask = MyCard.GetAvatarAsync("Alice");
        var first = new Texture2D("first");
        var second = new Texture2D("second");
        firstDownload.SetResult(first);
        Same(first, await firstTask);
        secondDownload.SetResult(second);
        Same(first, await secondTask);
        Equal(true, second.Destroyed);
        Equal(1, second.DestroyCalls);
        Equal(false, first.Destroyed);
    }),
    ("Oversized tall avatar is resized and its source destroyed only once", async () =>
    {
        var source = new Texture2D("tall", 128, 1024);
        SetupAvatar("https://avatars.test/avatar.png", source);
        var result = await MyCard.GetAvatarAsync("Alice");
        Equal(256, result.width);
        Equal(256, result.height);
        Equal(1, source.DestroyCalls);
        Equal(false, result.Destroyed);
    }),
    ("Usernames are escaped as one URL path segment", async () =>
    {
        SetupAvatar("https://avatars.test/avatar.png", new Texture2D("escaped"));
        await MyCard.GetAvatarAsync("a/b #?雪");
        Equal("https://sapi.moecube.com:444/accounts/users/a%2Fb%20%23%3F%E9%9B%AA.json", UnityWebRequest.Sent.Single().Url);
    }),
    ("Blank username and absent avatar return no image without downloading", async () =>
    {
        Equal<Texture2D>(null, await MyCard.GetAvatarAsync(null));
        Equal<Texture2D>(null, await MyCard.GetAvatarAsync(" "));
        UnityWebRequest.OnSend = request => request.downloadHandler.text = "{\"user\":{}}";
        Tools.Download = _ => throw new Exception("Unexpected download for missing avatar.");
        Equal<Texture2D>(null, await MyCard.GetAvatarAsync("Alice"));
        Equal(1, UnityWebRequest.Sent.Count);
    }),
    ("Late download after play mode ends destroys its unused texture", async () =>
    {
        var source = new Texture2D("late");
        SetupAvatar("https://avatars.test/avatar.png", source);
        Tools.Download = _ => { Application.isPlaying = false; return Task.FromResult(source); };
        Equal<Texture2D>(null, await MyCard.GetAvatarAsync("Alice"));
        Equal(1, source.DestroyCalls);
    })
};

foreach (bool tokenLogin in new[] { false, true })
foreach (string body in new[] { "", " ", "<html>Bad Gateway</html>", "{broken", "null", "[]", "{}", "{\"message\":\"\"}" })
{
    tests.Add(($"{(tokenLogin ? "TokenIn" : "Login")} handles non-API/empty error body {body}", async () =>
    {
        UnityWebRequest.OnSend = request => { request.result = UnityWebRequest.Result.ProtocolError; request.downloadHandler.text = body; };
        var response = tokenLogin ? await MyCard.TokenIn("test") : await MyCard.Login("test", "test");
        Equal("请检查网络连接", response.user.username);
        Equal<string>(null, response.token);
        Equal(true, UnityWebRequest.Sent.Single().Disposed);
    }));
}
foreach (bool tokenLogin in new[] { false, true })
{
    tests.Add(($"{(tokenLogin ? "TokenIn" : "Login")} preserves API error messages", async () =>
    {
        UnityWebRequest.OnSend = request => { request.result = UnityWebRequest.Result.ProtocolError; request.downloadHandler.text = "{\"message\":\"Account locked\"}"; };
        var response = tokenLogin ? await MyCard.TokenIn("test") : await MyCard.Login("test", "test");
        Equal("Account locked", response.user.username);
    }));
}

int failures = 0;
try
{
    for (int i = 0; i < tests.Count; i++)
    {
        Environment.CurrentDirectory = Directory.CreateDirectory(Path.Combine(testRoot, i.ToString())).FullName;
        Reset();
        try { await tests[i].Run(); Console.WriteLine("PASS " + tests[i].Name); }
        catch (Exception exception) { failures++; Console.Error.WriteLine("FAIL " + tests[i].Name + ": " + exception.Message); }
    }
}
finally
{
    Environment.CurrentDirectory = originalDirectory;
    Directory.Delete(testRoot, true);
}
Console.WriteLine($"MyCard regression: {tests.Count - failures}/{tests.Count} passed.");
return failures == 0 ? 0 : 1;

static void Reset()
{
    foreach (var name in new[] { "cachedAvatarAddress", "cachedAvatars" })
        ((IDictionary)typeof(MyCard).GetField(name, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)).Clear();
    MyCard.account = null;
    Application.isPlaying = true;
    Debug.Warnings.Clear();
    UnityWebRequest.Sent.Clear();
    UnityWebRequest.OnSend = _ => throw new Exception("Unexpected HTTP request.");
    OnlineDeck.Fetch = () => Task.CompletedTask;
    Tools.Download = _ => throw new Exception("Unexpected image download.");
    TextureManager.Load = _ => Task.FromResult<Texture2D>(null);
}
static void SetupAvatar(string url, Texture2D texture)
{
    UnityWebRequest.OnSend = request => request.downloadHandler.text = new JObject(new JProperty("user", new JObject(new JProperty("avatar", url)))).ToString();
    Tools.Download = _ => Task.FromResult(texture);
}
static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"Expected {expected}, received {actual}.");
}
static void Same(object expected, object actual)
{
    if (!ReferenceEquals(expected, actual)) throw new Exception("Texture identity changed.");
}
