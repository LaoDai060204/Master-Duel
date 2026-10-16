using MDPro3;
using UnityEngine;
using UnityEngine.Networking;
using GameProgram = MDPro3.Program;

internal static class Regression
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static Task<T> Complete<T>(Task<T> task) => task.WaitAsync(TimeSpan.FromSeconds(2));

    public static async Task Main()
    {
        var directory = Path.Combine(Path.GetTempPath(), "master-duel-textures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var previousDirectory = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = directory;
            GameProgram.artPath = "art/";
            GameProgram.altArtPath = "alt/";
            var fallback = TextureManager.container.unknownCard.texture;

            Check(ReferenceEquals(await Complete(TextureLoader.LoadCardAsync(0)), fallback), "Missing card returns fallback.");
            Check(ReferenceEquals(await Complete(TextureLoader.LoadCardAsync(0)), fallback), "Repeated missing-card load must complete.");
            TextureLoader.DeleteCard(0);
            TextureLoader.DeleteCard(0);
            Check(!fallback.Destroyed, "Deleting cached missing cards must preserve shared fallback.");

            GameProgram.instance.cardRenderer.Succeed = false;
            Check(ReferenceEquals(await Complete(TextureLoader.LoadCardAsync(1)), fallback), "Failed rendering returns fallback.");
            Check(ReferenceEquals(await Complete(TextureLoader.LoadCardAsync(1)), fallback), "Repeated failed-render load must complete.");
            TextureLoader.DeleteCache();
            Check(!fallback.Destroyed, "Clearing failed-render cache must preserve shared fallback.");

            GameProgram.instance.cardRenderer.Throw = true;
            try { await Complete(TextureLoader.LoadCardAsync(2)); throw new Exception("Render error should propagate."); }
            catch (InvalidOperationException e) when (e.Message == "fixture render failure") { checks++; }
            GameProgram.instance.cardRenderer.Throw = false;
            Check(ReferenceEquals(await Complete(TextureLoader.LoadCardAsync(2)), fallback), "Render failure can be retried without hanging.");

            File.WriteAllText("blocks-directory", "fixture");
            GameProgram.artPath = "blocks-directory";
            try { await Complete(TextureLoader.LoadArtAsync(3, false)); throw new Exception("Filesystem error should propagate."); }
            catch (IOException) { checks++; }
            GameProgram.artPath = "art/";
            Check(ReferenceEquals(await Complete(TextureLoader.LoadArtAsync(3, false)), TextureManager.container.unknownArt.texture), "Failed art initialization can be retried.");

            File.WriteAllText("a # picture.png", "fixture");
            await Complete(TextureLoader.LoadPicFromFileAsync("a # picture.png"));
            Check(UnityWebRequest.LastUrl == new Uri(Path.GetFullPath("a # picture.png")).AbsoluteUri, "Local file paths must use an escaped file URI.");

            UnityWebRequest.ThrowOnSend = true;
            for (int i = 0; i < TextureLoader.MAX_LOADING_THREADS; i++)
            {
                try { await Complete(TextureLoader.LoadPicFromFileAsync("a # picture.png")); throw new Exception("Send error should propagate."); }
                catch (IOException) { checks++; }
            }
            UnityWebRequest.ThrowOnSend = false;
            Check(await Complete(TextureLoader.LoadPicFromFileAsync("a # picture.png")) != null, "Exceptions must release every load slot.");

            UnityWebRequest.Fail = true;
            Check(await Complete(TextureLoader.LoadPicFromFileAsync("a # picture.png")) == null, "Failed HTTP request returns no texture.");
            UnityWebRequest.Fail = false;

            UnityWebRequest.Hold = true;
            var stopped = Enumerable.Range(0, TextureLoader.MAX_LOADING_THREADS + 1)
                .Select(_ => TextureLoader.LoadPicFromFileAsync("a # picture.png")).ToArray();
            Check(stopped.Count(task => !task.IsCompleted) == TextureLoader.MAX_LOADING_THREADS + 1, "Exercise saturated loading slots.");
            Application.Stop();
            await Task.WhenAll(stopped).WaitAsync(TimeSpan.FromSeconds(2));
            Check(stopped.All(task => task.Result == null), "Exit cancels queued work and finishes active work.");
            UnityWebRequest.CompleteAll();
            Application.Start();
            Check(await Complete(TextureLoader.LoadPicFromFileAsync("a # picture.png")) != null, "Exit cleanup must release all slots.");
            Check(UnityWebRequest.Disposed == UnityWebRequest.Sent, "Successful, failed, and interrupted requests are disposed.");

            TextureLoader.DeleteCache();
            GameProgram.instance.cardRenderer.Succeed = true;
            File.WriteAllText("alt/4.jpg", "fixture");
            UnityWebRequest.Hold = true;
            int renders = GameProgram.instance.cardRenderer.Calls;
            var first = TextureLoader.LoadCardAsync(4);
            var second = TextureLoader.LoadCardAsync(4);
            UnityWebRequest.CompleteAll();
            var firstTexture = await Complete(first);
            var secondTexture = await Complete(second);
            Check(ReferenceEquals(firstTexture, secondTexture), "Concurrent callers share the completed card texture.");
            Check(GameProgram.instance.cardRenderer.Calls == renders + 1, "Concurrent callers render once.");
            TextureLoader.DeleteCard(4);
            Check(!firstTexture.Destroyed, "First release keeps another caller's texture alive.");
            TextureLoader.DeleteCard(4);
            Check(firstTexture.Destroyed, "Final release destroys the owned texture.");

            int created = Texture2D.Created;
            await Complete(TextureLoader.LoadArtAsync(5, false));
            Check(Texture2D.Created == created, "An absent archive image must not allocate throwaway textures.");
            var archive = new FixtureZip();
            archive.Entries.Add("ART/6.PNG", new FixtureEntry());
            ZipHelper.zips.Add(archive);
            Check(!ReferenceEquals(await Complete(TextureLoader.LoadArtAsync(6, false)), TextureManager.container.unknownArt.texture), "Archive lookup handles original entry casing.");

            TextureLoader.DeleteCache();
            File.WriteAllText("alt/7.jpg", "fixture");
            UnityWebRequest.Hold = true;
            renders = GameProgram.instance.cardRenderer.Calls;
            var releasedOwner = TextureLoader.LoadCardAsync(7);
            var releasedRequest = UnityWebRequest.Pending.Single();
            Check(!releasedOwner.IsCompleted, "Exercise release while its sole card owner is still loading art.");
            TextureLoader.DeleteCard(7);
            UnityWebRequest.CompleteAll();
            Check(await Complete(releasedOwner) == null, "A released pending card must not return an unused texture.");
            Check(releasedRequest.DownloadedTexture != null && releasedRequest.DownloadedTexture.Destroyed,
                "A released pending owner must destroy the art arriving after its release.");
            Check(GameProgram.instance.cardRenderer.Calls == renders, "A released pending owner must not render an unneeded card.");
            var reacquired = await Complete(TextureLoader.LoadCardAsync(7));
            Check(reacquired != null && !reacquired.Destroyed, "The released pending entry must permit a fresh acquisition.");
            TextureLoader.DeleteCard(7);
            Check(reacquired.Destroyed, "A fresh acquisition after a pending release retains normal disposal.");

            File.WriteAllText("alt/8.jpg", "fixture");
            UnityWebRequest.Hold = true;
            var clearedOwner = TextureLoader.LoadCardAsync(8);
            var clearedWaiter = TextureLoader.LoadCardAsync(8);
            var clearedRequest = UnityWebRequest.Pending.Single();
            TextureLoader.DeleteCache();
            Check(await Complete(clearedWaiter) == null, "Cache reset wakes pending shared waiters before their owner completes.");
            Check(!clearedOwner.IsCompleted, "A reset must also cover an owner whose request remains pending.");
            UnityWebRequest.CompleteAll();
            Check(await Complete(clearedOwner) == null, "A cache reset prevents the old pending owner from returning its result.");
            Check(clearedRequest.DownloadedTexture != null && clearedRequest.DownloadedTexture.Destroyed,
                "A cache reset must destroy the art produced by its old pending owner.");

            File.WriteAllText("alt/9.jpg", "fixture");
            UnityWebRequest.Hold = true;
            var oldCard = TextureLoader.LoadCardAsync(9);
            var oldRequest = UnityWebRequest.Pending.Single();
            TextureLoader.DeleteCache();
            var replacementArtTask = TextureLoader.LoadArtAsync(9, false);
            var replacementRequest = UnityWebRequest.Pending.Single(request => !ReferenceEquals(request, oldRequest));
            var replacementCardTask = TextureLoader.LoadCardAsync(9);
            UnityWebRequest.Complete(replacementRequest);
            var replacementArt = await Complete(replacementArtTask);
            var replacementCard = await Complete(replacementCardTask);
            Check(replacementArt != null && !replacementArt.Destroyed && replacementCard != null && !replacementCard.Destroyed,
                "A new generation can complete art and card acquisitions while the old request remains pending.");
            UnityWebRequest.Complete(oldRequest);
            Check(await Complete(oldCard) == null, "The replaced pending card must not return its stale generation.");
            Check(oldRequest.DownloadedTexture != null && oldRequest.DownloadedTexture.Destroyed,
                "The replaced pending owner must destroy its own late art result.");
            Check(!replacementArt.Destroyed && !replacementCard.Destroyed,
                "Old owner cleanup must preserve both replacement art and card references.");
            Check(ReferenceEquals(await Complete(TextureLoader.LoadCardAsync(9)), replacementCard),
                "Old owner cleanup must retain the replacement card cache entry.");
            Check(ReferenceEquals(await Complete(TextureLoader.LoadArtAsync(9, false)), replacementArt),
                "Old art cleanup must retain the replacement art cache entry.");
            TextureLoader.DeleteCard(9);
            Check(!replacementCard.Destroyed, "The replacement card retains its second owner's reference.");
            TextureLoader.DeleteCard(9);
            Check(replacementCard.Destroyed, "The replacement card is destroyed after its final release.");
            UnityWebRequest.CompleteAll();
            TextureLoader.DeleteCache();
            Check(replacementArt.Destroyed, "A later reset disposes retained replacement art normally.");
            Console.WriteLine($"Texture regression: {checks} checks passed.");
        }
        finally
        {
            TextureLoader.DeleteCache();
            Environment.CurrentDirectory = previousDirectory;
            Directory.Delete(directory, true);
        }
    }
}
