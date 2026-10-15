using System.Reflection;
using MDPro3.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

var checks = 0;
void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    checks++;
}
bool CancelKey(KeyCode key) => (bool)typeof(UIScrollToSelection)
    .GetMethod("WasCancelKeyPressed", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { key });

// Scene unload and UI construction can temporarily leave these references absent.
var scroll = new TestScroll();
scroll.SetContent(new RectTransform());
EventSystem.current = null;
scroll.Tick();
Assert(scroll.Target == null, "An absent EventSystem must clear the target without throwing.");

var selection = new GameObject();
EventSystem.current = new EventSystem { currentSelectedGameObject = selection };
scroll.SetContent(null);
scroll.Tick();
Assert(scroll.Target == null, "Missing ScrollRect content must clear the target without throwing.");

var content = new RectTransform();
selection.transform.parent = content;
scroll.SetContent(content);
scroll.Tick();
Assert(ReferenceEquals(scroll.Target, selection.transform), "Selection tracking must recover after content is assigned.");
EventSystem.current = null;
scroll.Tick();
Assert(scroll.Target == null, "Removing the EventSystem must discard the previous target.");

#if ENABLE_LEGACY_INPUT_MANAGER
Input.Pressed = KeyCode.A;
Assert(CancelKey(KeyCode.A), "The legacy backend must observe its configured key.");
Assert(!CancelKey(KeyCode.Escape), "An unrelated key must not cancel scrolling.");
Assert(Input.Calls == 2, "When both backends are enabled, retain the legacy KeyCode behavior.");
#elif ENABLE_INPUT_SYSTEM
Assert(!CancelKey(KeyCode.A), "A disconnected keyboard must not throw or report a key.");
Assert(!CancelKey(KeyCode.Mouse0), "A disconnected mouse must not throw or report a button.");
Keyboard.current = new Keyboard();
foreach (var mapping in new (KeyCode Legacy, Key Modern)[]
{
    (KeyCode.A, Key.A), (KeyCode.Escape, Key.Escape), (KeyCode.Space, Key.Space),
    (KeyCode.Alpha0, Key.Digit0), (KeyCode.Alpha1, Key.Digit1),
    (KeyCode.Keypad3, Key.Numpad3), (KeyCode.KeypadEnter, Key.NumpadEnter),
    (KeyCode.Return, Key.Enter), (KeyCode.LeftArrow, Key.LeftArrow), (KeyCode.F5, Key.F5),
    (KeyCode.LeftControl, Key.LeftCtrl), (KeyCode.RightControl, Key.RightCtrl),
    (KeyCode.LeftCommand, Key.LeftMeta), (KeyCode.RightCommand, Key.RightMeta),
    (KeyCode.AltGr, Key.RightAlt), (KeyCode.Print, Key.PrintScreen), (KeyCode.Menu, Key.ContextMenu)
})
{
    Keyboard.current[mapping.Modern].wasPressedThisFrame = true;
    Assert(CancelKey(mapping.Legacy), $"{mapping.Legacy} must preserve the configured cancellation key.");
    Keyboard.current[mapping.Modern].wasPressedThisFrame = false;
    Assert(!CancelKey(mapping.Legacy), $"Releasing {mapping.Legacy} must stop reporting a press.");
}
Mouse.current = new Mouse();
foreach (var mapping in new (KeyCode Legacy, ButtonControl Button)[]
{
    (KeyCode.Mouse0, Mouse.current.leftButton), (KeyCode.Mouse1, Mouse.current.rightButton),
    (KeyCode.Mouse2, Mouse.current.middleButton), (KeyCode.Mouse3, Mouse.current.backButton),
    (KeyCode.Mouse4, Mouse.current.forwardButton)
})
{
    mapping.Button.wasPressedThisFrame = true;
    Assert(CancelKey(mapping.Legacy), $"{mapping.Legacy} must preserve mouse cancellation.");
    mapping.Button.wasPressedThisFrame = false;
}
Assert(!CancelKey(KeyCode.None), "An unassigned key must not cancel scrolling.");
Assert(!CancelKey((KeyCode)99999), "An unknown serialized key must be ignored safely.");
Assert(Input.Calls == 0, "The new Input System must never call disabled legacy input.");

// Exercise the configured component path, not only the input adapter.
Keyboard.current[Key.A].wasPressedThisFrame = true;
scroll.ConfigureCancellation(KeyCode.A);
EventSystem.current = new EventSystem { currentSelectedGameObject = selection };
scroll.Tick();
Assert(scroll.ManualScrolling, "A configured key press must lock automatic scrolling.");
Keyboard.current[Key.A].wasPressedThisFrame = false;
var nextSelection = new GameObject();
nextSelection.transform.parent = content;
EventSystem.current.currentSelectedGameObject = nextSelection;
scroll.Tick();
Assert(!scroll.ManualScrolling, "Changing selection must unlock automatic scrolling.");
#else
Assert(!CancelKey(KeyCode.A), "A build without an input backend must ignore cancellation safely.");
Assert(Input.Calls == 0, "A build without an input backend must not call legacy input.");
#endif

Console.WriteLine($"Scroll regression checks passed: {checks}");

sealed class TestScroll : UIScrollToSelection
{
    public RectTransform Target => CurrentTargetRectTransform;
    public bool ManualScrolling => IsManualScrollingAvailable;
    public void SetContent(RectTransform content)
    {
        TargetScrollRect = new ScrollRect { content = content };
        ScrollWindow = new RectTransform();
    }
    public void Tick() => base.Update();
    public void ConfigureCancellation(KeyCode key)
    {
        typeof(UIScrollToSelection).GetField("cancelScrollOnInput", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, true);
        typeof(UIScrollToSelection).GetField("cancelScrollKeycodes", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(this, new List<KeyCode> { key });
    }
}
