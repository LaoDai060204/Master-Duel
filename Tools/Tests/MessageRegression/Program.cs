using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MDPro3;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;

internal static class RegressionTests
{
    private static int Main()
    {
        int failures = 0;
        foreach (Action test in new Action[]
        {
            CastBeforeProgramWaitsForPrefab,
            LoadingPrefabFlushesWithoutAnotherCast,
            FailedLoadKeepsPendingUntilSuccessfulRetry,
            QueuedOverflowEventuallyDrainsInOrder,
            NewMessagesWhileFullAreNotLost,
            SubstringMessageBeforeProgramIsQueued,
            SubsystemRegistrationClearsPreviousPlayState,
            ReplacedManagerReleasesEveryStaleLoad,
            ReinitializingSameManagerRejectsOlderLoads,
            DestroyedManagerReleasesLateCompletions,
            DestroyingManagerReleasesLoadedPrefabs,
            FailedAndNullPrefabsReleaseTheirHandles
        })
        {
            Reset();
            try
            {
                test();
                Console.WriteLine("PASS " + test.Method.Name);
            }
            catch (Exception error)
            {
                failures++;
                Console.Error.WriteLine("FAIL " + test.Method.Name + ": " + error.GetBaseException().Message);
            }
        }
        return failures == 0 ? 0 : 1;
    }

    private static void Invoke(string name, MessageManager manager = null) => typeof(MessageManager)
        .GetMethod(name, BindingFlags.NonPublic | (manager == null ? BindingFlags.Static : BindingFlags.Instance))
        .Invoke(manager, null);
    private static void Reset()
    {
        Invoke("ResetStatics");
        GameObject.Instances.Clear();
        Addressables.Requests.Clear();
        Debug.Errors.Clear();
        DOTween.Reset();
        CameraManager.Blur = 0;
        MDPro3.Program.instance = null;
    }
    private static MessageManager Initialize()
    {
        var manager = new MessageManager();
        // Program.Awake installs its instance before InitializeAllManagers.
        MDPro3.Program.instance = new MDPro3.Program { message_ = manager };
        manager.Initialize();
        return manager;
    }
    private static LoadRequest CastRequest(int index = 0) => Addressables.Requests.Where(item => item.Key == "MessageCast").ElementAt(index);
    private static void Require(bool value, string reason)
    {
        if (!value) throw new Exception(reason);
    }
    private static void RequireMessages(params string[] expected) => Require(
        GameObject.Instances.Select(item => item.Text).SequenceEqual(expected),
        "Expected messages " + string.Join(", ", expected) + "; got " + string.Join(", ", GameObject.Instances.Select(item => item.Text)));

    private static void CastBeforeProgramWaitsForPrefab()
    {
        MessageManager.Cast("before Program");
        Require(MDPro3.Program.instance == null, "The enqueue path must not require Program initialization.");
        Require(GameObject.Instances.Count == 0, "A missing manager must defer rendering.");
        Initialize();
        Require(GameObject.Instances.Count == 0, "An unfinished prefab load must defer rendering.");
        Addressables.Finish(CastRequest());
        RequireMessages("before Program");
    }

    private static void LoadingPrefabFlushesWithoutAnotherCast()
    {
        Initialize();
        MessageManager.Cast("one");
        MessageManager.Cast("two");
        Addressables.Finish(CastRequest());
        RequireMessages("one", "two");
        DOTween.CompleteAll();
        Require(CameraManager.Blur == 0, "Completed messages must release their blur contributions.");
    }

    private static void FailedLoadKeepsPendingUntilSuccessfulRetry()
    {
        var manager = Initialize();
        MessageManager.Cast("pending");
        Addressables.Finish(CastRequest(), false);
        Require(CastRequest().Releases == 1, "A failed prefab load must be released.");
        Require(GameObject.Instances.Count == 0, "A failed load must not render a missing prefab.");
        manager.Initialize();
        Addressables.Finish(CastRequest(1));
        RequireMessages("pending");
    }

    private static void QueuedOverflowEventuallyDrainsInOrder()
    {
        string[] messages = Enumerable.Range(0, 40).Select(index => "queued-" + index).ToArray();
        foreach (string message in messages) MessageManager.Cast(message);
        Initialize();
        Addressables.Finish(CastRequest());
        Require(GameObject.Instances.Count == 11, "The existing visible-message limit must be preserved.");
        DOTween.CompleteAll();
        RequireMessages(messages);
        Require(GameObject.Instances.All(item => item.Destroyed), "Every displayed message must finish.");
        Require(CameraManager.Blur == 0, "Draining queued messages must balance blur contributions.");
    }

    private static void NewMessagesWhileFullAreNotLost()
    {
        Initialize();
        Addressables.Finish(CastRequest());
        string[] messages = Enumerable.Range(0, 30).Select(index => "live-" + index).ToArray();
        foreach (string message in messages) MessageManager.Cast(message);
        Require(GameObject.Instances.Count == 11, "Overflow must queue until a visible slot opens.");
        DOTween.CompleteAll();
        RequireMessages(messages);
    }

    private static void SubstringMessageBeforeProgramIsQueued()
    {
        var manager = Initialize();
        MessageManager.messageFromSubString = "network callback";
        manager.PerFrameFunction();
        Require(MessageManager.messageFromSubString == string.Empty, "The shared message slot must clear after enqueueing.");
        Addressables.Finish(CastRequest());
        RequireMessages("network callback");
    }

    private static void SubsystemRegistrationClearsPreviousPlayState()
    {
        var oldManager = Initialize();
        Addressables.Finish(CastRequest());
        MessageManager.Cast("previous visible");
        for (int index = 0; index < 20; index++) MessageManager.Cast("previous pending");
        MessageManager.messageFromSubString = "previous slot";
        Invoke("OnDestroy", oldManager);
        Invoke("ResetStatics");
        DOTween.Reset();
        GameObject.Instances.Clear();
        Require(MessageManager.messageFromSubString == string.Empty, "A new play session must clear the shared message slot.");
        MessageManager.Cast("new session");
        Require(GameObject.Instances.Count == 0, "The previous manager must not receive new-session messages.");
        Initialize();
        Addressables.Finish(CastRequest(1));
        RequireMessages("new session");
    }

    private static void ReplacedManagerReleasesEveryStaleLoad()
    {
        var oldManager = Initialize();
        Initialize();
        foreach (LoadRequest request in Addressables.Requests.Take(3))
        {
            Addressables.Finish(request);
            Require(request.Releases == 1, "Every replaced manager's late load must be released.");
        }
        Require(oldManager.messageCast == null && oldManager.messageToast == null && oldManager.messageCard == null,
            "Late completions must not populate an old manager.");
        MessageManager.Cast("current only");
        Addressables.Finish(CastRequest(1));
        RequireMessages("current only");
    }

    private static void ReinitializingSameManagerRejectsOlderLoads()
    {
        var manager = Initialize();
        manager.Initialize();
        foreach (LoadRequest request in Addressables.Requests.Take(3))
        {
            Addressables.Finish(request);
            Require(request.Releases == 1, "A replaced load generation must release its handles.");
        }
        Require(manager.messageCast == null, "An older completion must not win a reinitialization race.");
        MessageManager.Cast("new generation");
        Addressables.Finish(CastRequest(1));
        RequireMessages("new generation");
    }

    private static void DestroyedManagerReleasesLateCompletions()
    {
        var manager = Initialize();
        Invoke("OnDestroy", manager);
        UnityEngine.Object.Destroy(manager);
        foreach (LoadRequest request in Addressables.Requests)
        {
            Addressables.Finish(request);
            Require(request.Releases == 1, "Late loads must release after their manager is destroyed.");
        }
        Require(manager.messageCast == null && manager.messageToast == null && manager.messageCard == null,
            "Destroyed managers must not accept loaded prefabs.");
    }

    private static void DestroyingManagerReleasesLoadedPrefabs()
    {
        var manager = Initialize();
        foreach (LoadRequest request in Addressables.Requests) Addressables.Finish(request);
        Require(Addressables.Requests.All(request => request.Releases == 0), "Live prefabs must retain their handles.");
        Invoke("OnDestroy", manager);
        Require(Addressables.Requests.All(request => request.Releases == 1), "Destroying a manager must release all loaded prefabs once.");
        Invoke("OnDestroy", manager);
        Require(Addressables.Requests.All(request => request.Releases == 1), "Cleanup must not double-release handles.");
    }

    private static void FailedAndNullPrefabsReleaseTheirHandles()
    {
        var manager = Initialize();
        Addressables.Finish(Addressables.Requests[0], true, true);
        Addressables.Finish(Addressables.Requests[1], false);
        Addressables.Finish(Addressables.Requests[2], false);
        Require(Addressables.Requests.All(request => request.Releases == 1), "Failed and null results must release all handles.");
        Require(Debug.Errors.Count == 3, "Each unusable prefab must report its failure.");
        Invoke("OnDestroy", manager);
        Require(Addressables.Requests.All(request => request.Releases == 1), "Cleanup must not release failed handles twice.");
    }
}
