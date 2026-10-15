/// Credit zero3growlithe
/// sourced from: http://forum.unity3d.com/threads/scripts-useful-4-6-scripts-collection.264161/page-2#post-2011648

/*USAGE:
Simply place the script on the ScrollRect that contains the selectable children we'll be scroling to
and drag'n'drop the RectTransform of the options "container" that we'll be scrolling.*/

using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace MDPro3.UI
{
    [RequireComponent(typeof(ScrollRect))]
    [AddComponentMenu("UI/Extensions/UIScrollToSelection")]
    public class UIScrollToSelection : MonoBehaviour
    {

        //*** ATTRIBUTES ***//
        [Header("[ Settings ]")]
        [SerializeField]
        private ScrollType scrollDirection = ScrollType.BOTH;
        [SerializeField]
        private float scrollSpeed = 10f;

        [Header("[ Input ]")]
        [SerializeField]
        private bool cancelScrollOnInput = false;
        [SerializeField]
        private List<KeyCode> cancelScrollKeycodes = new List<KeyCode>();

        //*** PROPERTIES ***//
        // REFERENCES
        protected RectTransform LayoutListGroup
        {
            get { return TargetScrollRect != null ? TargetScrollRect.content : null; }
        }

        // SETTINGS
        protected ScrollType ScrollDirection
        {
            get { return scrollDirection; }
        }
        protected float ScrollSpeed
        {
            get { return scrollSpeed; }
        }

        // INPUT
        protected bool CancelScrollOnInput
        {
            get { return cancelScrollOnInput; }
        }
        protected List<KeyCode> CancelScrollKeycodes
        {
            get { return cancelScrollKeycodes; }
        }

        // CACHED REFERENCES
        protected RectTransform ScrollWindow { get; set; }
        protected ScrollRect TargetScrollRect { get; set; }
        protected LayoutGroup LayoutGroup { get; set; }

        // SCROLLING
        protected EventSystem CurrentEventSystem
        {
            get { return EventSystem.current; }
        }
        protected GameObject LastCheckedGameObject { get; set; }
        protected GameObject CurrentSelectedGameObject
        {
            get { return CurrentEventSystem != null ? CurrentEventSystem.currentSelectedGameObject : null; }
        }
        protected RectTransform CurrentTargetRectTransform { get; set; }
        protected RectTransform CurrentTargetChildrenRectTransform { get; set; }
        protected bool IsManualScrollingAvailable { get; set; }

        //*** METHODS - PUBLIC ***//


        //*** METHODS - PROTECTED ***//
        protected virtual void Awake()
        {
            TargetScrollRect = GetComponent<ScrollRect>();
            ScrollWindow = GetComponent<RectTransform>();
            if(LayoutListGroup != null)
                LayoutGroup = LayoutListGroup.GetComponent<LayoutGroup>();
        }

        protected virtual void Start()
        {

        }

        protected virtual void Update()
        {
            UpdateReferences();
            CheckIfScrollingShouldBeLocked();
            ScrollRectToLevelSelection();
        }

        //*** METHODS - PRIVATE ***//
        private void UpdateReferences()
        {
            if (LayoutListGroup == null)
            {
                CurrentTargetRectTransform = null;
                CurrentTargetChildrenRectTransform = null;
                LastCheckedGameObject = null;
                return;
            }

            // update current selected rect transform
            if (CurrentSelectedGameObject != LastCheckedGameObject)
            {
                CurrentTargetRectTransform = (CurrentSelectedGameObject != null) ?
                    CurrentSelectedGameObject.GetComponent<RectTransform>() :
                    null;

                CurrentTargetChildrenRectTransform = null;

                if (CurrentTargetRectTransform != null)
                    if (CurrentSelectedGameObject.transform.parent != null)
                        if (CurrentSelectedGameObject.transform.parent.TryGetComponent<UIScrollToSelectionMark>(out _))
                        {
                            CurrentTargetRectTransform = CurrentSelectedGameObject.transform.parent.GetComponent<RectTransform>();
                            CurrentTargetChildrenRectTransform = CurrentSelectedGameObject.GetComponent<RectTransform>();
                        }

                // unlock automatic scrolling
                if (CurrentTargetRectTransform != null &&
                    CurrentTargetRectTransform.parent == LayoutListGroup.transform)
                {
                    IsManualScrollingAvailable = false;
                }
            }

            LastCheckedGameObject = CurrentSelectedGameObject;
        }

        private void CheckIfScrollingShouldBeLocked()
        {
            if (CancelScrollOnInput == false || IsManualScrollingAvailable == true)
            {
                return;
            }

            for (int i = 0; i < CancelScrollKeycodes.Count; i++)
            {
                if (WasCancelKeyPressed(CancelScrollKeycodes[i]))
                {
                    IsManualScrollingAvailable = true;

                    break;
                }
            }
        }

        private static bool WasCancelKeyPressed(KeyCode keyCode)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(keyCode);
#elif ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            switch (keyCode)
            {
                case KeyCode.Mouse0: return mouse != null && mouse.leftButton.wasPressedThisFrame;
                case KeyCode.Mouse1: return mouse != null && mouse.rightButton.wasPressedThisFrame;
                case KeyCode.Mouse2: return mouse != null && mouse.middleButton.wasPressedThisFrame;
                case KeyCode.Mouse3: return mouse != null && mouse.backButton.wasPressedThisFrame;
                case KeyCode.Mouse4: return mouse != null && mouse.forwardButton.wasPressedThisFrame;
            }

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            // Keep the existing serialized KeyCode list when using the new Input System.
            var keyName = keyCode.ToString();
            if (keyName.StartsWith("Alpha", System.StringComparison.Ordinal))
                keyName = "Digit" + keyName.Substring(5);
            else if (keyName.StartsWith("Keypad", System.StringComparison.Ordinal))
                keyName = "Numpad" + keyName.Substring(6);

            switch (keyCode)
            {
                case KeyCode.Return: keyName = "Enter"; break;
                case KeyCode.LeftControl: keyName = "LeftCtrl"; break;
                case KeyCode.RightControl: keyName = "RightCtrl"; break;
                case KeyCode.LeftCommand: keyName = "LeftMeta"; break;
                case KeyCode.RightCommand: keyName = "RightMeta"; break;
                case KeyCode.AltGr: keyName = "RightAlt"; break;
                case KeyCode.Print: keyName = "PrintScreen"; break;
                case KeyCode.Menu: keyName = "ContextMenu"; break;
            }

            return System.Enum.TryParse(keyName, true, out Key key) &&
                key != Key.None && System.Enum.IsDefined(typeof(Key), key) &&
                keyboard[key].wasPressedThisFrame;
#else
            return false;
#endif
        }

        private void ScrollRectToLevelSelection()
        {
            // check main references
            bool referencesAreIncorrect = (TargetScrollRect == null || LayoutListGroup == null || ScrollWindow == null);

            if (referencesAreIncorrect == true || IsManualScrollingAvailable == true || Cursor.lockState == CursorLockMode.None)
            {
                return;
            }

            RectTransform selection = CurrentTargetRectTransform;

            // check if scrolling is possible
            if (selection == null || selection.transform.parent != LayoutListGroup.transform)
            {
                return;
            }

            // depending on selected scroll direction move the scroll rect to selection
            switch (ScrollDirection)
            {
                case ScrollType.VERTICAL:
                    UpdateVerticalScrollPosition(selection);
                    break;
                case ScrollType.HORIZONTAL:
                    UpdateHorizontalScrollPosition(selection);
                    break;
                case ScrollType.BOTH:
                    UpdateVerticalScrollPosition(selection);
                    UpdateHorizontalScrollPosition(selection);
                    break;
            }
        }

        private void UpdateVerticalScrollPosition(RectTransform selection)
        {
            // move the current scroll rect to correct position
            float selectionPosition = -selection.anchoredPosition.y - (selection.rect.height * (1 - selection.pivot.y));

            var childRect = CurrentTargetChildrenRectTransform;
            if (childRect != null)
            {
                selectionPosition += -childRect.anchoredPosition.y - (childRect.rect.height * (1 - childRect.pivot.y));
            }

            float elementHeight = selection.rect.height;
            if (childRect != null)
            {
                elementHeight = childRect.rect.height;
            }

            float maskHeight = ScrollWindow.rect.height;
            float listAnchorPosition = LayoutListGroup.anchoredPosition.y;

            // get the element offset value depending on the cursor move direction
            float offlimitsValue = GetVerticalScrollOffset(selectionPosition, listAnchorPosition, elementHeight, maskHeight);

            // move the target scroll rect
            TargetScrollRect.verticalNormalizedPosition +=
                (offlimitsValue / LayoutListGroup.rect.height) * Time.unscaledDeltaTime * scrollSpeed;
        }

        private void UpdateHorizontalScrollPosition(RectTransform selection)
        {
            // move the current scroll rect to correct position
            float selectionPosition = -selection.anchoredPosition.x - (selection.rect.width * (1 - selection.pivot.x));

            float elementWidth = selection.rect.width;
            float maskWidth = ScrollWindow.rect.width;
            float listAnchorPosition = -LayoutListGroup.anchoredPosition.x;

            // get the element offset value depending on the cursor move direction
            float offlimitsValue = -GetScrollOffset(selectionPosition, listAnchorPosition, elementWidth, maskWidth);

            // move the target scroll rect
            TargetScrollRect.horizontalNormalizedPosition +=
                (offlimitsValue / LayoutListGroup.rect.width) * Time.unscaledDeltaTime * scrollSpeed;
        }

        private float GetScrollOffset(float position, float listAnchorPosition, float targetLength, float maskLength)
        {
            if (position < listAnchorPosition + (targetLength / 2))
            {
                return (listAnchorPosition + maskLength) - (position - targetLength);
            }
            else if (position + targetLength > listAnchorPosition + maskLength)
            {
                return (listAnchorPosition + maskLength) - (position + targetLength);
            }
            return 0;
        }

        private float GetVerticalScrollOffset(float position, float listAnchorPosition, float targetLength, float maskLength)
        {
            if (position < listAnchorPosition + (LayoutGroup == null ? PropertyOverrider.PropertyOverrider.NeedMobileLayout() ? 148f : 134f : LayoutGroup.padding.top))
            {
                return listAnchorPosition - position + (LayoutGroup == null ? PropertyOverrider.PropertyOverrider.NeedMobileLayout() ? 148f : 134f : LayoutGroup.padding.top);
            }
            else if (position + targetLength > listAnchorPosition + maskLength - (LayoutGroup == null ? PropertyOverrider.PropertyOverrider.NeedMobileLayout() ? 64f : 54f : LayoutGroup.padding.bottom))
            {
                return (listAnchorPosition + maskLength - (LayoutGroup == null ? PropertyOverrider.PropertyOverrider.NeedMobileLayout() ? 64f : 54f : LayoutGroup.padding.bottom)) - (position + targetLength);
            }
            return 0;
        }

        //*** ENUMS ***//
        public enum ScrollType
        {
            VERTICAL,
            HORIZONTAL,
            BOTH
        }
    }
}
