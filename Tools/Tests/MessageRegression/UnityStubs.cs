// Deterministic adapters for the real MessageManager.cs, not replacements for
// Unity import, real Addressables, DOTween timing, or scene integration tests.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UnityEngine
{
    public class Object
    {
        internal bool Destroyed;
        public static bool operator ==(Object left, Object right) =>
            (ReferenceEquals(left, null) || left.Destroyed)
                ? ReferenceEquals(right, null) || right.Destroyed
                : ReferenceEquals(left, right);
        public static bool operator !=(Object left, Object right) => !(left == right);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
        public static GameObject Instantiate(GameObject original)
        {
            if (original == null) throw new InvalidOperationException("Cannot clone a missing prefab.");
            var clone = new GameObject();
            GameObject.Instances.Add(clone);
            return clone;
        }
        public static void Destroy(Object value, float delay = 0f)
        {
            if (!ReferenceEquals(value, null) && delay == 0f) value.Destroyed = true;
        }
    }
    public class MonoBehaviour : Object
    {
        public readonly Transform transform = new Transform();
        public object StartCoroutine(IEnumerator routine) => routine;
    }
    public sealed class GameObject : Object
    {
        public static readonly List<GameObject> Instances = new List<GameObject>();
        public readonly RectTransform transform = new RectTransform();
        private readonly UI.RawImage image = new UI.RawImage();
        public T GetComponent<T>() where T : class
        {
            if (typeof(T) == typeof(RectTransform)) return transform as T;
            if (typeof(T) == typeof(UI.RawImage)) return image as T;
            throw new InvalidOperationException("Unexpected component " + typeof(T));
        }
        public string Text => transform.GetChild(0).GetComponent<TMPro.TextMeshProUGUI>().text;
    }
    public class Transform : Object
    {
        private Transform child;
        private readonly TMPro.TextMeshProUGUI text = new TMPro.TextMeshProUGUI();
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public Transform GetChild(int index) => child ??= new Transform();
        public T GetComponent<T>() where T : class => text as T;
    }
    public sealed class RectTransform : Transform { public Vector2 anchoredPosition; }
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }
    public enum RuntimeInitializeLoadType { SubsystemRegistration }
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType type) { }
    }
    public static class Debug
    {
        public static readonly List<string> Errors = new List<string>();
        public static void LogError(object error, Object context) => Errors.Add(error.ToString());
    }
    public sealed class Texture { }
    public sealed class Material { }
}
namespace UnityEngine.UI
{
    public sealed class RawImage
    {
        public UnityEngine.Material material;
        public UnityEngine.Texture texture;
    }
}
namespace TMPro { public sealed class TextMeshProUGUI { public string text; } }
namespace KonamiCommonIAB { }

namespace DG.Tweening
{
    public sealed class Tween
    {
        internal float Due;
        internal Action Completion;
        public Tween OnComplete(Action callback) { Completion = callback; return this; }
    }
    public static class DOTween
    {
        private static readonly List<Tween> pending = new List<Tween>();
        private static float clock;
        public static Tween To(Action<float> setter, float from, float to, float duration)
        {
            var tween = new Tween { Due = clock + duration };
            pending.Add(tween);
            return tween;
        }
        public static void CompleteAll()
        {
            int budget = 10000;
            while (pending.Count > 0)
            {
                if (--budget == 0) throw new InvalidOperationException("Tween queue did not drain.");
                pending.Sort((left, right) => left.Due.CompareTo(right.Due));
                Tween tween = pending[0];
                pending.RemoveAt(0);
                clock = tween.Due;
                tween.Completion?.Invoke();
            }
        }
        public static void Reset() { pending.Clear(); clock = 0f; }
    }
    public static class ShortcutExtensions
    {
        public static Tween DOAnchorPosX(this UnityEngine.RectTransform rect, float target, float duration)
        {
            rect.anchoredPosition.x = target;
            return new Tween();
        }
        public static Tween DOAnchorPosY(this UnityEngine.RectTransform rect, float target, float duration)
        {
            rect.anchoredPosition.y = target;
            return new Tween();
        }
    }
}

namespace UnityEngine.ResourceManagement.AsyncOperations
{
    public enum AsyncOperationStatus { None, Succeeded, Failed }
    public sealed class Operation<T>
    {
        public T Result;
        public AsyncOperationStatus Status;
        public Exception Error;
        public Action<AsyncOperationHandle<T>> Completion;
        public int Releases;
    }
    public struct AsyncOperationHandle<T>
    {
        internal Operation<T> Operation;
        public T Result => Operation.Result;
        public AsyncOperationStatus Status => Operation.Status;
        public Exception OperationException => Operation.Error;
        public event Action<AsyncOperationHandle<T>> Completed
        {
            add { Operation.Completion += value; }
            remove { Operation.Completion -= value; }
        }
    }
}
namespace UnityEngine.AddressableAssets
{
    using UnityEngine.ResourceManagement.AsyncOperations;
    public sealed class LoadRequest
    {
        public string Key;
        public AsyncOperationHandle<GameObject> Handle;
        public bool Complete;
        public int Releases => Handle.Operation.Releases;
    }
    public static class Addressables
    {
        public static readonly List<LoadRequest> Requests = new List<LoadRequest>();
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(string key)
        {
            if (typeof(T) != typeof(GameObject)) throw new InvalidOperationException("Unexpected asset type.");
            var handle = new AsyncOperationHandle<GameObject> { Operation = new Operation<GameObject>() };
            Requests.Add(new LoadRequest { Key = key, Handle = handle });
            return (AsyncOperationHandle<T>)(object)handle;
        }
        public static void Finish(LoadRequest request, bool success = true, bool nullResult = false)
        {
            if (request.Complete) throw new InvalidOperationException("Operation already finished.");
            request.Complete = true;
            var operation = request.Handle.Operation;
            operation.Status = success ? AsyncOperationStatus.Succeeded : AsyncOperationStatus.Failed;
            operation.Result = success && !nullResult ? new GameObject() : null;
            operation.Error = success ? null : new InvalidOperationException("Missing prefab.");
            operation.Completion?.Invoke(request.Handle);
        }
        public static void Release<T>(AsyncOperationHandle<T> handle)
        {
            if (++handle.Operation.Releases > 1) throw new InvalidOperationException("Handle released twice.");
        }
    }
}
namespace MDPro3
{
    public static class CameraManager
    {
        public static int Blur;
        // The production camera methods require Program's scene references.
        public static void UIBlurPlus()
        {
            if (Program.instance == null) throw new InvalidOperationException("Program has not started.");
            Blur++;
        }
        public static void UIBlurMinus()
        {
            if (Program.instance == null) throw new InvalidOperationException("Program has not started.");
            Blur--;
        }
    }
    public sealed class Program
    {
        public static Program instance;
        public MessageManager message_;
        public readonly Ocgcore ocgcore = new Ocgcore();
    }
    public sealed class Ocgcore { public readonly List<UnityEngine.GameObject> allGameObjects = new List<UnityEngine.GameObject>(); }
    public static class TextureManager
    {
        public static Task<UnityEngine.Texture> LoadCardAsync(int code, bool value) => Task.FromResult(new UnityEngine.Texture());
        public static UnityEngine.Material GetCardMaterial(int code) => new UnityEngine.Material();
    }
}
