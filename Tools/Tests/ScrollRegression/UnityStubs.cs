// Minimal API doubles for control flow tests; these do not replace Unity runtime tests.
namespace UnityEngine
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class RequireComponent : System.Attribute { public RequireComponent(System.Type type) { } }
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class AddComponentMenu : System.Attribute { public AddComponentMenu(string name) { } }
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class Header : System.Attribute { public Header(string name) { } }
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class SerializeField : System.Attribute { }
    public class Component
    {
        public Transform transform => this as Transform;
        public T GetComponent<T>() where T : class => this as T;
        public bool TryGetComponent<T>(out T value) where T : class { value = GetComponent<T>(); return value != null; }
    }
    public class MonoBehaviour : Component { }
    public class Transform : Component { public Transform parent; }
    public class RectTransform : Transform
    {
        public Vector2 anchoredPosition;
        public Vector2 pivot;
        public Rect rect;
    }
    public class GameObject
    {
        public Transform transform = new RectTransform();
        public T GetComponent<T>() where T : class => transform as T;
    }
    public struct Vector2 { public float x, y; }
    public struct Rect { public float width, height; }
    public class RectOffset { public int top, bottom; }
    public enum CursorLockMode { None, Locked }
    public static class Cursor { public static CursorLockMode lockState; }
    public static class Time { public static float unscaledDeltaTime = 1f; }
    // Values relevant to the tests follow Unity's KeyCode values.
    public enum KeyCode
    {
        None = 0, Return = 13, Escape = 27, Space = 32, Alpha0 = 48, Alpha1 = 49,
        A = 97, Keypad3 = 259, KeypadEnter = 271, LeftArrow = 276, F5 = 286,
        RightControl = 305, LeftControl = 306, RightCommand = 309, LeftCommand = 310,
        LeftApple = LeftCommand, RightApple = RightCommand, AltGr = 313, Print = 316, Menu = 319,
        Mouse0 = 323, Mouse1 = 324, Mouse2 = 325, Mouse3 = 326, Mouse4 = 327
    }
    public static class Input
    {
        public static KeyCode? Pressed;
        public static int Calls;
        public static bool GetKeyDown(KeyCode key)
        {
            Calls++;
#if !ENABLE_LEGACY_INPUT_MANAGER
            throw new System.InvalidOperationException("Legacy input is disabled.");
#else
            return Pressed == key;
#endif
        }
    }
}
namespace UnityEngine.UI
{
    public class ScrollRect : UnityEngine.Component
    {
        public UnityEngine.RectTransform content;
        public float verticalNormalizedPosition, horizontalNormalizedPosition;
    }
    public class LayoutGroup : UnityEngine.Component { public UnityEngine.RectOffset padding = new(); }
}
namespace UnityEngine.EventSystems
{
    public class EventSystem
    {
        public static EventSystem current;
        public UnityEngine.GameObject currentSelectedGameObject;
    }
}
namespace UnityEngine.InputSystem
{
    public enum Key
    {
        None, Enter, Escape, Space, Digit0, Digit1, A, Numpad3, NumpadEnter, LeftArrow, F5,
        RightCtrl, LeftCtrl, RightMeta, LeftMeta, RightAlt, PrintScreen, ContextMenu
    }
    public class ButtonControl { public bool wasPressedThisFrame; }
    public class Keyboard
    {
        public static Keyboard current;
        private readonly System.Collections.Generic.Dictionary<Key, ButtonControl> keys = new();
        public ButtonControl this[Key key]
        {
            get { if (!keys.ContainsKey(key)) keys[key] = new(); return keys[key]; }
        }
    }
    public class Mouse
    {
        public static Mouse current;
        public ButtonControl leftButton = new(), rightButton = new(), middleButton = new(), backButton = new(), forwardButton = new();
    }
}
namespace MDPro3.UI { public class UIScrollToSelectionMark : UnityEngine.MonoBehaviour { } }
namespace PropertyOverrider { public static class PropertyOverrider { public static bool NeedMobileLayout() => false; } }
