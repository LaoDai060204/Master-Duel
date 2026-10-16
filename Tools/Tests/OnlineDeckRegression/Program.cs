using System.Reflection;
using System.Text.Json;
using MDPro3.Net;
using MDPro3.YGOSharp;
using UnityEngine;
using UnityEngine.Networking;
using DeckClient = MDPro3.Net.OnlineDeck;
using ClientProgram = MDPro3.Program;

var checks = 0;
var failures = 0;
var root = Path.Combine(Path.GetTempPath(), "master-duel-online-deck-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
ClientProgram.deckPath = root + Path.DirectorySeparatorChar;

void Assert(bool success, string message)
{
    if (!success) throw new Exception(message);
    checks++;
}
async Task Check(string name, Func<Task> action)
{
    UnityWebRequest.Replies.Clear();
    UnityWebRequest.Sent.Clear();
    MDPro3.MessageManager.Messages.Clear();
    Application.isPlaying = true;
    MyCard.account = new Account();
    DeckClient.decks = Array.Empty<DeckClient.OnlineDeckData>();
    foreach (var file in Directory.GetFiles(root)) File.Delete(file);
    try { await action(); Console.WriteLine("PASS " + name); }
    catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
}
void Reply(string text, UnityWebRequest.Result result = UnityWebRequest.Result.Success,
    Action<UnityWebRequest> onSend = null) => UnityWebRequest.Replies.Enqueue(new(result, text, onSend));
Deck Deck()
{
    var deck = new Deck { deckId = "original-id", userId = "original-user" };
    deck.Main.Add(12345);
    deck.Pickup.AddRange(new[] { 101, 202, 303 });
    return deck;
}
string PathFor(string name) => Path.Combine(root, name + ".ydk");
DeckClient.OnlineDeckData Cloud(string name = "Test") => new()
{
    deckId = "cloud-id", deckName = name, deckYdk = "Original remote deck",
    deckUpdateDate = "2026-10-15T00:00:00Z"
};
void Ids(params string[] ids) => Reply(JsonSerializer.Serialize(new { code = 0, data = ids }));
void Refresh() => Reply("{\"code\":0,\"data\":[]}");
Task<bool> Upload(Deck deck, string name = "Test") => DeckClient.UploadDecks(new() { deck }, new() { name });

try
{
    await Check("all three cover cards survive serialization", () =>
    {
        var post = new DeckClient.PostDeck(Deck(), "id", "Test", "ydk");
        Assert(post.deckCoverCard1 == 101 && post.deckCoverCard2 == 202 && post.deckCoverCard3 == 303,
            "Cover card slots must retain separate cards.");
        return Task.CompletedTask;
    });
    await Check("failed upload preserves original file, IDs, and cache", async () =>
    {
        var deck = Deck();
        Assert(deck.Save("Test", DateTime.Now, false), "Fixture must save.");
        var original = File.ReadAllBytes(PathFor("Test"));
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Ids("new-id");
        Reply("offline", UnityWebRequest.Result.ConnectionError, _ =>
        {
            Assert(File.Exists(PathFor("Test")), "Original file must still exist while upload runs.");
            Assert(deck.deckId == "original-id", "Uploading must not commit IDs before HTTP success.");
        });
        Assert(!await Upload(deck), "HTTP failure must return false.");
        Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(original), "Original bytes must survive failure.");
        Assert(Directory.GetFiles(root).Length == 1, "Failed upload must not create a renamed deck.");
        Assert(deck.deckId == "original-id" && deck.userId == "original-user", "Failed upload must preserve IDs.");
        Assert(!cloud.isDelete, "Failed upload must preserve cached state.");
    });
    foreach (var response in new[]
    {
        "{\"code\":403,\"data\":1}", "{\"code\":0,\"data\":0}", "null", "{broken"
    })
        await Check("upload rejects non-confirming response: " + response, async () =>
        {
            var deck = Deck();
            deck.Save("Test", DateTime.Now, false);
            var original = File.ReadAllBytes(PathFor("Test"));
            Ids("new-id"); Reply(response); Refresh();
            Assert(!await Upload(deck), "Unconfirmed application response must fail.");
            Assert(deck.deckId == "original-id", "Unconfirmed response must preserve source IDs.");
            Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(original), "Unconfirmed response must preserve local bytes.");
        });
    foreach (var response in new[]
    {
        "{\"code\":403,\"data\":[\"new-id\"]}", "{\"code\":0,\"data\":[]}",
        "{\"code\":0,\"data\":[\"\"]}", "null", "{broken"
    })
        await Check("upload rejects invalid ID allocation: " + response, async () =>
        {
            var deck = Deck();
            Reply(response);
            Assert(!await Upload(deck), "Invalid allocation must return false without sending the batch.");
            Assert(UnityWebRequest.Sent.Count == 1, "No upload may follow invalid allocation.");
            Assert(deck.deckId == "original-id", "Invalid allocation must preserve IDs.");
        });
    await Check("successful upload commits metadata after server confirmation", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        Ids("new-id"); Reply("{\"code\":0,\"data\":1}"); Refresh();
        Assert(await Upload(deck), "Confirmed upload must succeed.");
        var saved = new Deck(PathFor("Test"));
        Assert(deck.deckId == "new-id" && saved.deckId == "new-id", "Both live and saved deck must get the confirmed ID.");
        Assert(saved.userId == "42", "Saved deck must get the signed-in user.");
        using var body = JsonDocument.Parse(UnityWebRequest.Sent[1].body);
        Assert(body.RootElement.GetProperty("decks")[0].GetProperty("deckYdk").GetString().Contains("##new-id"),
            "Uploaded YDK and JSON ID must agree.");
    });
    await Check("successful collision rename preserves any existing target", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        DeckClient.decks = new[] { Cloud() };
        Ids("new-id");
        Reply("{\"code\":0,\"data\":1}", onSend: request =>
        {
            using var body = JsonDocument.Parse(request.body);
            var name = body.RootElement.GetProperty("decks")[0].GetProperty("deckName").GetString();
            Assert(name != "Test", "A cloud name collision must pick a new local name.");
            Assert(File.Exists(PathFor("Test")), "Original file must remain until server success.");
        });
        Refresh();
        Assert(await Upload(deck), "Confirmed rename must succeed.");
        Assert(!File.Exists(PathFor("Test")), "Old file is retired only after the new file is committed.");
        Assert(Directory.GetFiles(root).Length == 1, "Rename must not leave duplicate local decks.");
    });
    await Check("failed sync keeps cloud cache and source identity", async () =>
    {
        var deck = Deck();
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Reply("offline", UnityWebRequest.Result.ConnectionError, _ =>
        {
            Assert(cloud.deckYdk == "Original remote deck", "Cache must not change while HTTP runs.");
            Assert(deck.deckId == "original-id", "Source identity must not change before HTTP success.");
        });
        Assert(!await DeckClient.SyncDeck("cloud-id", "Renamed", deck), "Failed sync must return false.");
        Assert(cloud.deckName == "Test" && cloud.deckYdk == "Original remote deck", "Failed sync must retain cloud snapshot.");
        Assert(deck.deckId == "original-id" && deck.userId == "original-user", "Failed sync must retain original identity.");
    });
    foreach (var response in new[]
    {
        "{\"code\":403,\"data\":true}", "{\"code\":0,\"data\":false}", "null", "{broken"
    })
        await Check("sync rejects non-confirming response: " + response, async () =>
        {
            var deck = Deck();
            var cloud = Cloud();
            DeckClient.decks = new[] { cloud };
            Reply(response);
            Assert(!await DeckClient.SyncDeck("cloud-id", "Renamed", deck), "Unconfirmed sync must return false.");
            Assert(cloud.deckName == "Test" && deck.deckId == "original-id", "Unconfirmed sync must preserve local/cache data.");
        });
    await Check("successful sync commits cache, cover slots, and saved metadata", async () =>
    {
        var deck = Deck();
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Reply("{\"code\":0,\"data\":true}");
        Assert(await DeckClient.SyncDeck("cloud-id", "Renamed", deck), "Confirmed sync must succeed.");
        Assert(cloud.deckName == "Renamed" && cloud.deckYdk.Contains("##cloud-id"), "Cache must reflect confirmed content and identity.");
        Assert(new Deck(PathFor("Renamed")).deckId == "cloud-id", "Saved deck must reflect confirmed identity.");
        Assert(cloud.deckCoverCard1 == 101 && cloud.deckCoverCard2 == 202 && cloud.deckCoverCard3 == 303, "Cache cover slots must refresh.");
    });
    await Check("sync acknowledgement retains a newer file and live edit", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        DeckClient.decks = new[] { Cloud() };
        byte[] newerBytes = null;
        Reply("{\"code\":0,\"data\":true}", onSend: _ =>
        {
            deck.Main.Add(98765);
            deck.Save("Test", DateTime.Now, false);
            newerBytes = File.ReadAllBytes(PathFor("Test"));
        });
        Assert(!await DeckClient.SyncDeck("cloud-id", "Test", deck), "An older response must decline the local commit.");
        Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "Newer saved cards must survive an older acknowledgement.");
        Assert(deck.deckId == "original-id" && deck.userId == "original-user", "Skipped commits must preserve the current live identity.");
    });
    await Check("upload acknowledgement retains a separately replaced source file", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        byte[] newerBytes = null;
        Ids("new-id");
        Reply("{\"code\":0,\"data\":1}", onSend: _ =>
        {
            var newer = Deck();
            newer.Main.Add(98765);
            newer.Save("Test", DateTime.Now, false);
            newerBytes = File.ReadAllBytes(PathFor("Test"));
        });
        Refresh();
        Assert(!await Upload(deck), "An upload must decline to replace a newer source file.");
        Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "A separately saved newer deck must retain its bytes.");
        Assert(deck.deckId == "original-id", "A skipped commit must not assign an obsolete upload ID.");
    });
    await Check("upload rename retains a source edited while the upload runs", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        DeckClient.decks = new[] { Cloud() };
        byte[] newerBytes = null;
        Ids("new-id");
        Reply("{\"code\":0,\"data\":1}", onSend: _ =>
        {
            var newer = Deck();
            newer.Main.Add(98765);
            newer.Save("Test", DateTime.Now, false);
            newerBytes = File.ReadAllBytes(PathFor("Test"));
        });
        Refresh();
        Assert(!await Upload(deck), "An older rename must decline its local commit.");
        Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "A newer source must never be deleted by an old rename.");
        Assert(Directory.GetFiles(root).Length == 1, "A stale rename must not create an obsolete duplicate.");
    });
    await Check("upload rename retains a destination created while the upload runs", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        var original = File.ReadAllBytes(PathFor("Test"));
        DeckClient.decks = new[] { Cloud() };
        string target = null;
        Ids("new-id");
        Reply("{\"code\":0,\"data\":1}", onSend: request =>
        {
            using var body = JsonDocument.Parse(request.body);
            target = PathFor(body.RootElement.GetProperty("decks")[0].GetProperty("deckName").GetString());
            File.WriteAllText(target, "A separate deck created during upload");
        });
        Refresh();
        Assert(!await Upload(deck), "A new destination must not be overwritten by an older upload.");
        Assert(File.ReadAllText(target) == "A separate deck created during upload", "Concurrent destination bytes must survive.");
        Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(original), "An uncommitted rename must keep the original.");
    });
    await Check("two upload responses arriving out of order retain the newer identity", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        UnityWebRequest earlierRequest = null;
        Ids("earlier-id");
        Reply("{\"code\":0,\"data\":1}", onSend: request =>
        {
            request.operation.isDone = false;
            earlierRequest = request;
        });
        var earlier = Upload(deck);
        try
        {
            Assert(!earlier.IsCompleted, "The first response must remain in flight.");
            deck.Main.Add(98765);
            deck.Save("Test", DateTime.Now, false);
            Ids("newer-id"); Reply("{\"code\":0,\"data\":1}"); Refresh();
            Assert(await Upload(deck), "The newer upload should commit first.");
            var newerBytes = File.ReadAllBytes(PathFor("Test"));
            Refresh();
            earlierRequest.operation.isDone = true;
            Assert(!await earlier, "The older upload must decline a stale local commit.");
            Assert(deck.deckId == "newer-id" && new Deck(PathFor("Test")).deckId == "newer-id", "Older responses must not restore older IDs.");
            Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "The newer committed bytes must survive response reordering.");
        }
        finally
        {
            if (earlierRequest != null) earlierRequest.operation.isDone = true;
            await earlier;
        }
    });
    await Check("two sync responses arriving out of order retain newer cards and cache", async () =>
    {
        var deck = Deck();
        deck.Save("Test", DateTime.Now, false);
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        UnityWebRequest earlierRequest = null;
        Reply("{\"code\":0,\"data\":true}", onSend: request =>
        {
            request.operation.isDone = false;
            earlierRequest = request;
        });
        var earlier = DeckClient.SyncDeck("cloud-id", "Test", deck);
        try
        {
            Assert(!earlier.IsCompleted, "The earlier response must remain in flight.");
            deck.Main.Add(98765);
            deck.Save("Test", DateTime.Now, false);
            Reply("{\"code\":0,\"data\":true}");
            Assert(await DeckClient.SyncDeck("cloud-id", "Test", deck), "The newer sync should commit first.");
            var newerBytes = File.ReadAllBytes(PathFor("Test"));
            var newerCache = cloud.deckYdk;
            earlierRequest.operation.isDone = true;
            Assert(!await earlier, "An older response must decline to commit stale cards.");
            Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "Reverse-order responses must retain the newest file.");
            Assert(cloud.deckYdk == newerCache && cloud.deckYdk.Contains("98765"), "The confirmed cache must also retain newer cards.");
        }
        finally
        {
            if (earlierRequest != null) earlierRequest.operation.isDone = true;
            await earlier;
        }
    });
    await Check("failed deletion leaves cached deck visible", async () =>
    {
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Reply("offline", UnityWebRequest.Result.ConnectionError, _ =>
            Assert(!cloud.isDelete, "Cached deletion must wait for server acknowledgement."));
        Assert(!await DeckClient.DeleteDecks(new() { "cloud-id" }), "Failed deletion must return false.");
        Assert(!cloud.isDelete, "Failed deletion must preserve cached visibility.");
    });
    await Check("rejected deletion leaves cached deck visible", async () =>
    {
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Reply("{\"code\":403,\"data\":1}"); Refresh();
        Assert(!await DeckClient.DeleteDecks(new() { "cloud-id" }), "Rejected deletion must return false.");
        Assert(!cloud.isDelete, "Rejected deletion must preserve cache.");
    });
    await Check("confirmed deletion deduplicates IDs and commits even if refresh fails", async () =>
    {
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Reply("{\"code\":0,\"data\":1}");
        Reply("offline", UnityWebRequest.Result.ConnectionError);
        Assert(await DeckClient.DeleteDecks(new() { "cloud-id", "cloud-id" }), "A confirmed deletion must succeed despite refresh failure.");
        Assert(cloud.isDelete, "Confirmed deletion must be reflected without a refresh.");
        using var body = JsonDocument.Parse(UnityWebRequest.Sent[0].body);
        Assert(body.RootElement.GetProperty("decks").GetArrayLength() == 1, "The batch must contain each ID once.");
    });
    await Check("invalid deck-list response cannot replace a valid cache", async () =>
    {
        var cloud = Cloud();
        DeckClient.decks = new[] { cloud };
        Reply("{\"code\":403,\"data\":null}");
        Assert(await DeckClient.GetAllDecks() == null, "Rejected list response must fail.");
        Assert(DeckClient.decks?.Length == 1 && ReferenceEquals(DeckClient.decks[0], cloud), "Rejected response must preserve the cache.");
    });
    await Check("logged-out mutation requests fail without network", async () =>
    {
        MyCard.account = null;
        Assert(!await Upload(Deck()), "Logged-out upload must fail safely.");
        Assert(!await DeckClient.SyncDeck("cloud-id", "Test", Deck()), "Logged-out sync must fail safely.");
        Assert(!await DeckClient.DeleteDecks(new() { "cloud-id" }), "Logged-out delete must fail safely.");
        Assert(!await DeckClient.UpdatePublicState("cloud-id", true), "Logged-out visibility update must fail safely.");
        Assert(UnityWebRequest.Sent.Count == 0, "Logged-out mutations must not send network requests.");
    });
    await Check("atomic local save retains original on rename destination failure", () =>
    {
        var helper = typeof(DeckClient).GetMethod("SaveLocalDeck", BindingFlags.Public | BindingFlags.Static);
        Assert(helper != null, "Shared local-save helper must exist.");
        File.WriteAllText(PathFor("Original"), "original local bytes");
        Directory.CreateDirectory(PathFor("Blocked"));
        try
        {
            Assert(!(bool)helper.Invoke(null, new object[] { Deck(), "Blocked", DateTime.Now, "Original" }), "Unwritable destination must fail safely.");
            Assert(File.ReadAllText(PathFor("Original")) == "original local bytes", "Failed rename must keep the source file.");
            Assert(Directory.GetFiles(root).Length == 1, "Failed save must clean temporary files.");
        }
        finally { Directory.Delete(PathFor("Blocked")); }
        return Task.CompletedTask;
    });
    await Check("atomic local save updates content then retires renamed source", () =>
    {
        var helper = typeof(DeckClient).GetMethod("SaveLocalDeck", BindingFlags.Public | BindingFlags.Static);
        Assert(helper != null, "Shared local-save helper must exist.");
        File.WriteAllText(PathFor("Original"), "original local bytes");
        File.WriteAllText(PathFor("Renamed"), "older destination bytes");
        var time = DateTime.Now.AddMinutes(-10);
        Assert((bool)helper.Invoke(null, new object[] { Deck(), "Renamed", time, "Original" }), "Atomic replacement must succeed.");
        Assert(new Deck(PathFor("Renamed")).deckId == "original-id", "Replacement must contain the deck payload.");
        Assert(!File.Exists(PathFor("Original")), "Source must be retired after successful replacement.");
        Assert(Math.Abs((File.GetLastWriteTime(PathFor("Renamed")) - time).TotalSeconds) < 1, "Server timestamp must survive atomic move.");
        return Task.CompletedTask;
    });
    await Check("remote deck names cannot escape the deck folder", () =>
    {
        var helper = typeof(DeckClient).GetMethod("SaveLocalDeck", BindingFlags.Public | BindingFlags.Static);
        Assert(helper != null, "Shared local-save helper must exist.");
        foreach (var name in new[] { "../escaped", "..\\escaped", "/absolute", "", "a/b" })
            Assert(!(bool)helper.Invoke(null, new object[] { Deck(), name, DateTime.Now, null }), "Unsafe deck names must be rejected: " + name);
        Assert(Directory.GetFiles(root).Length == 0, "Unsafe names must not write files.");
        return Task.CompletedTask;
    });
    await Check("case-only rename keeps the committed file and retires a distinct source", () =>
    {
        File.WriteAllText(PathFor("Original"), "old source");
        Assert(DeckClient.SaveLocalDeck(Deck(), "original", DateTime.Now, "Original"), "Case-only rename should save successfully.");
        Assert(File.Exists(PathFor("original")), "A case-insensitive alias must never delete the committed file.");
        Assert(Directory.GetFiles(root).Length == 1, "Case-sensitive volumes must not keep a duplicate old spelling.");
        Assert(new Deck(PathFor("original")).deckId == "original-id", "The committed deck payload must survive cleanup.");
        return Task.CompletedTask;
    });
    await Check("unchanged owned deck downloads and renames atomically", () =>
    {
        var local = Deck();
        local.deckId = "cloud-id";
        local.userId = "42";
        local.Save("Original", DateTime.Now, false);
        Assert(DeckClient.TryCaptureLocalDeck("Original", out var source), "The source must be captured before reconciliation waits.");
        Assert(DeckClient.TryCaptureLocalDeck("Renamed", out var destination), "The missing destination must also be captured.");
        var cloud = Cloud("Renamed");
        var remote = Deck();
        remote.Main.Add(98765);
        cloud.deckYdk = remote.GetYDK();
        Assert(DeckClient.SaveDownloadedDeck(cloud, destination, "Original", source), "An unchanged source should accept the remote update.");
        Assert(!File.Exists(PathFor("Original")) && File.Exists(PathFor("Renamed")), "The successful rename must retire the old file.");
        var saved = new Deck(PathFor("Renamed"));
        Assert(saved.Main.Contains(98765) && saved.deckId == "cloud-id" && saved.userId == "42", "Downloaded cards and ownership must be committed together.");
        return Task.CompletedTask;
    });
    await Check("a new cloud download uses its captured missing destination", () =>
    {
        Assert(DeckClient.TryCaptureLocalDeck("Test", out var destination), "A missing destination must be capturable.");
        var cloud = Cloud();
        cloud.deckYdk = Deck().GetYDK();
        Assert(DeckClient.SaveDownloadedDeck(cloud, destination), "An unchanged missing destination should accept a download.");
        Assert(new Deck(PathFor("Test")).deckId == "cloud-id", "The downloaded file must retain its cloud identity.");
        return Task.CompletedTask;
    });
    await Check("a download after an upload wait retains a newer source edit", async () =>
    {
        var local = Deck();
        local.deckId = "cloud-id";
        local.userId = "42";
        local.Save("Original", DateTime.Now, false);
        Assert(DeckClient.TryCaptureLocalDeck("Original", out var source), "The source must be captured before the upload wait.");
        Assert(DeckClient.TryCaptureLocalDeck("Renamed", out var destination), "The destination must be captured before the upload wait.");
        var upload = Deck();
        upload.Save("Upload", DateTime.Now, false);
        UnityWebRequest delayedRequest = null;
        Ids("uploaded-id");
        Reply("{\"code\":0,\"data\":1}", onSend: request =>
        {
            request.operation.isDone = false;
            delayedRequest = request;
        });
        var pendingUpload = Upload(upload, "Upload");
        try
        {
            Assert(!pendingUpload.IsCompleted, "The unrelated upload must still be in flight.");
            local.Main.Add(98765);
            local.Save("Original", DateTime.Now, false);
            var newerBytes = File.ReadAllBytes(PathFor("Original"));
            Refresh();
            delayedRequest.operation.isDone = true;
            Assert(await pendingUpload, "The unrelated upload should complete normally.");
            var cloud = Cloud("Renamed");
            cloud.deckYdk = Deck().GetYDK();
            Assert(!DeckClient.SaveDownloadedDeck(cloud, destination, "Original", source), "An old download decision must reject a newer source.");
            Assert(File.ReadAllBytes(PathFor("Original")).SequenceEqual(newerBytes), "The newer source must not be overwritten or deleted.");
            Assert(!File.Exists(PathFor("Renamed")), "A rejected download must not create its obsolete rename.");
            Assert(!source.IsCurrent(), "The same initial snapshot must reject stale candidates for a later upload.");
        }
        finally
        {
            if (delayedRequest != null) delayedRequest.operation.isDone = true;
            await pendingUpload;
        }
    });
    await Check("a new cloud download retains a destination created during another upload", async () =>
    {
        Assert(DeckClient.TryCaptureLocalDeck("Test", out var destination), "The missing destination must be captured before waiting.");
        var upload = Deck();
        upload.Save("Upload", DateTime.Now, false);
        UnityWebRequest delayedRequest = null;
        Ids("uploaded-id");
        Reply("{\"code\":0,\"data\":1}", onSend: request =>
        {
            request.operation.isDone = false;
            delayedRequest = request;
        });
        var pendingUpload = Upload(upload, "Upload");
        try
        {
            var newer = Deck();
            newer.Main.Add(98765);
            newer.Save("Test", DateTime.Now, false);
            var newerBytes = File.ReadAllBytes(PathFor("Test"));
            Refresh(); delayedRequest.operation.isDone = true;
            Assert(await pendingUpload, "The unrelated upload should finish normally.");
            var cloud = Cloud();
            cloud.deckYdk = Deck().GetYDK();
            Assert(!DeckClient.SaveDownloadedDeck(cloud, destination), "The earlier missing-file decision must reject a new destination.");
            Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "The newly created local deck must be retained.");
        }
        finally
        {
            if (delayedRequest != null) delayedRequest.operation.isDone = true;
            await pendingUpload;
        }
    });
    await Check("cloud deletion requires matching bytes and cloud ownership", async () =>
    {
        var local = Deck();
        local.deckId = "cloud-id";
        local.userId = "42";
        local.Save("Test", DateTime.Now, false);
        Assert(DeckClient.TryCaptureLocalDeck("Test", out var snapshot), "The existing deck must be captured.");
        Assert(!DeckClient.DeleteLocalDeck("Test", "another-cloud-id", snapshot), "A different cloud ID must not delete this file.");
        var upload = Deck();
        upload.Save("Upload", DateTime.Now, false);
        UnityWebRequest delayedRequest = null;
        Ids("uploaded-id");
        Reply("{\"code\":0,\"data\":1}", onSend: request =>
        {
            request.operation.isDone = false;
            delayedRequest = request;
        });
        var pendingUpload = Upload(upload, "Upload");
        try
        {
            local.Main.Add(98765);
            local.Save("Test", DateTime.Now, false);
            var newerBytes = File.ReadAllBytes(PathFor("Test"));
            Refresh(); delayedRequest.operation.isDone = true;
            Assert(await pendingUpload, "The unrelated upload should finish normally.");
            Assert(!DeckClient.DeleteLocalDeck("Test", "cloud-id", snapshot), "A stale deletion decision must retain a newer local edit.");
            Assert(File.ReadAllBytes(PathFor("Test")).SequenceEqual(newerBytes), "The newer local bytes must survive cloud deletion.");
            Assert(DeckClient.TryCaptureLocalDeck("Test", out snapshot), "The updated file should be capturable.");
            MyCard.account.user.id = 84;
            Assert(!DeckClient.DeleteLocalDeck("Test", "cloud-id", snapshot), "A different signed-in owner must not delete this file.");
            MyCard.account.user.id = 42;
            Assert(DeckClient.DeleteLocalDeck("Test", "cloud-id", snapshot), "An unchanged file with matching ownership should be deletable.");
            Assert(!File.Exists(PathFor("Test")), "A valid cloud tombstone should remove its matching local file.");
        }
        finally
        {
            if (delayedRequest != null) delayedRequest.operation.isDone = true;
            await pendingUpload;
        }
    });
}
finally { Directory.Delete(root, true); }

Console.WriteLine($"Online deck regression checks: {checks} passed assertions; {failures} failed scenarios.");
return failures == 0 ? 0 : 1;
