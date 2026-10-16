using DG.Tweening;
using KonamiCommonIAB;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace MDPro3
{
    public class MessageManager : Manager
    {
        public GameObject messageCast;
        public GameObject messageToast;
        public GameObject messageCard;

        static MessageManager instance;
        static readonly List<GameObject> items = new List<GameObject>();
        static readonly float transitionTime = 0.3f;
        static readonly float existTime = 3f;
        readonly List<AsyncOperationHandle<GameObject>> prefabHandles = new List<AsyncOperationHandle<GameObject>>();
        int loadGeneration;

        public static string messageFromSubString = string.Empty;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            items.Clear();
            cachedMessage.Clear();
            messageFromSubString = string.Empty;
        }

        public override void Initialize()
        {
            base.Initialize();
            instance = this;
            loadGeneration++;
            ReleasePrefabs();
            messageCast = messageToast = messageCard = null;
            LoadPrefab("MessageCast", prefab =>
            {
                messageCast = prefab;
                FlushCachedMessages();
            });
            LoadPrefab("MessageToast", prefab => messageToast = prefab);
            LoadPrefab("MessageCard", prefab => messageCard = prefab);
        }

        void LoadPrefab(string key, System.Action<GameObject> onLoaded)
        {
            int generation = loadGeneration;
            var handle = Addressables.LoadAssetAsync<GameObject>(key);
            handle.Completed += (result) =>
            {
                if (this == null || instance != this || generation != loadGeneration)
                {
                    Addressables.Release(result);
                    return;
                }
                if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
                {
                    Debug.LogError("Cannot load " + key + ". "
                        + result.OperationException, this);
                    Addressables.Release(result);
                    return;
                }
                prefabHandles.Add(result);
                onLoaded(result.Result);
            };
        }

        void ReleasePrefabs()
        {
            foreach (var handle in prefabHandles)
                Addressables.Release(handle);
            prefabHandles.Clear();
        }

        void OnDestroy()
        {
            loadGeneration++;
            ReleasePrefabs();
            if (instance == this)
            {
                instance = null;
                items.Clear();
            }
        }
        public override void PerFrameFunction()
        {
            base.PerFrameFunction();
            if(messageFromSubString != string.Empty)
            {
                Cast(messageFromSubString);
                messageFromSubString= string.Empty;
            }
        }

        public void CastCard(int code)
        {
            CameraManager.UIBlurPlus();
            var item = Instantiate(Program.instance.message_.messageCard);
            Program.instance.ocgcore.allGameObjects.Add(item);
            item.transform.SetParent(instance.transform, false);
            StartCoroutine(RefreshAsync(item, code));
        }

        IEnumerator RefreshAsync(GameObject item, int code)
        {
            var task = TextureManager.LoadCardAsync(code, false);
            while(!task.IsCompleted)
                yield return null;

            var mat = TextureManager.GetCardMaterial(code);
            item.GetComponent<RawImage>().material = mat;
            item.GetComponent<RawImage>().texture = task.Result;

            var rect = item.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(200, -160);
            rect.DOAnchorPosX(-50f, transitionTime);
            DOTween.To(v => { }, 0, 0, transitionTime + existTime).OnComplete(() =>
            {
                rect.DOAnchorPosX(200f, transitionTime);
            });
            DOTween.To(v => { }, 0, 0, existTime + transitionTime * 2).OnComplete(() =>
            {
                Destroy(item);
                CameraManager.UIBlurMinus();
            });
        }

        static readonly Queue<string> cachedMessage = new Queue<string>();

        public static void Toast(string message)
        {
            CameraManager.UIBlurPlus();
            var item = Instantiate(Program.instance.message_.messageToast);
            item.transform.SetParent(instance.transform, false);
            item.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = message;
            Destroy(item, 2f);
            DOTween.To(v => { }, 0f, 0f, 2f).OnComplete(() =>
            {
                Destroy(item);
                CameraManager.UIBlurMinus();
            });
        }

        public static void Cast(string message)
        {
            cachedMessage.Enqueue(message);
            FlushCachedMessages();
        }

        static void FlushCachedMessages()
        {
            if (instance == null || instance.messageCast == null)
                return;
            while (cachedMessage.Count > 0 && items.Count <= 10)
                ShowCast(cachedMessage.Dequeue());
        }

        static void ShowCast(string message)
        {
            CameraManager.UIBlurPlus();
            var item = Instantiate(instance.messageCast);
            item.transform.SetParent(instance.transform, false);
            item.transform.GetChild(0).GetComponent<TextMeshProUGUI>().text = message;
            RectTransform rect = item.GetComponent<RectTransform>();
            int id = items.Count;
            items.Add(item);
            rect.anchoredPosition = new Vector2(900, -160 - id * 120);
            rect.DOAnchorPosX(-10f, transitionTime);
            DOTween.To(v => { }, 0, 0, existTime + id).OnComplete(() =>
            {
                rect.DOAnchorPosX(900f, transitionTime);
            });
            DOTween.To(v => { }, 0, 0, existTime + transitionTime + id).OnComplete(() =>
            {
                items.Remove(item);
                Destroy(item);
                MoveUp();
                CameraManager.UIBlurMinus();
                FlushCachedMessages();
            });
        }

        static void MoveUp()
        {
            foreach (var item in items)
            {
                var rect = item.GetComponent<RectTransform>();
                rect.DOAnchorPosY(rect.anchoredPosition.y + 120f, transitionTime);
            }
        }
    }
}
