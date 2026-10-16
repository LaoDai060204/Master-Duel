using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using MDPro3.YGOSharp;
using MDPro3.YGOSharp.OCGWrapper.Enums;
using MDPro3.Utility;
using System.Collections;
using zFramework.Internal;
using UnityEngine.UI;
using DG.Tweening;

namespace MDPro3
{
    public class TextureLoader : MonoBehaviour
    {
        public static TextureLoader Instance;

        private static readonly ConcurrentDictionary<int, TextureData> _cachedArts = new();
        private static readonly ConcurrentDictionary<int, TextureData> _cachedCards = new();

        public const int MAX_LOADING_THREADS = 16;
        private static readonly SemaphoreSlim _loadingSlots = new(MAX_LOADING_THREADS, MAX_LOADING_THREADS);

        private void Awake()
        {
            Instance = this;
        }

        public static async Task<Texture2D> LoadPicFromFileAsync(string path)
        {
            if (!Application.isPlaying || !File.Exists(path))
                return null;

            try
            {
                await _loadingSlots.WaitAsync(Application.exitCancellationToken);
            }
            catch (OperationCanceledException) when (!Application.isPlaying)
            {
                return null;
            }

            try
            {
                if (!Application.isPlaying)
                    return null;

                var fullPath = new Uri(Path.GetFullPath(path)).AbsoluteUri;
                using var request = UnityWebRequestTexture.GetTexture(fullPath);
                var send = request.SendWebRequest();
                await TaskUtility.WaitUntil(() => send.isDone);
                if (!Application.isPlaying)
                    return null;

                if (request.result == UnityWebRequest.Result.Success)
                    return DownloadHandlerTexture.GetContent(request);

                Debug.LogWarningFormat("Pic File [{0}] could not be loaded: {1}", path, request.error);
                return null;
            }
            finally
            {
                _loadingSlots.Release();
            }
        }

        private static TextureData AcquireTexture(ConcurrentDictionary<int, TextureData> cache, int code,
            bool keep, out bool ownsLoad)
        {
            var newData = new TextureData { notDelete = keep, referenceCount = 1 };
            var data = cache.GetOrAdd(code, newData);
            ownsLoad = ReferenceEquals(data, newData);
            if (!ownsLoad)
            {
                data.AddReference();
                data.notDelete |= keep;
            }
            return data;
        }

        private static bool CanUseTexture(TextureData data) => Application.isPlaying && !data.invalidated
            && (data.referenceCount > 0 || data.notDelete);

        private static Texture2D GetTexture(TextureData data, Texture2D fallback) => CanUseTexture(data)
            ? data.texture != null ? data.texture : fallback
            : null;

        private static void RemoveTexture(ConcurrentDictionary<int, TextureData> cache, int code, TextureData data)
        {
            // Remove only this acquisition; a cache reset may already have installed a new entry.
            ((ICollection<KeyValuePair<int, TextureData>>)cache).Remove(new KeyValuePair<int, TextureData>(code, data));
            data.invalidated = true;
            Destroy(data.texture);
            data.texture = null;
        }

        private static void FinishTexture(ConcurrentDictionary<int, TextureData> cache, int code, TextureData data)
        {
            data.loaded = true;
            if (!CanUseTexture(data))
                RemoveTexture(cache, code, data);
        }

        private static void ReleaseTexture(ConcurrentDictionary<int, TextureData> cache, int code, TextureData data)
        {
            data.Delete();
            // An unfinished owner still owns its result and will dispose it in FinishTexture.
            if (data.loaded && !CanUseTexture(data))
                RemoveTexture(cache, code, data);
        }

        public static async Task<Texture2D> LoadArtAsync(int code, bool cache)
        {
            await TaskUtility.WaitWhile(() => TextureManager.container == null);
            if (!Application.isPlaying)
                return null;

            var textureData = AcquireTexture(_cachedArts, code, cache, out var ownsLoad);
            return await LoadArtAsync(code, textureData, ownsLoad);
        }

        private static async Task<Texture2D> LoadArtAsync(int code, TextureData textureData, bool ownsLoad)
        {
            if (!ownsLoad)
            {
                await TaskUtility.WaitUntil(() => textureData.loaded || textureData.invalidated);
                return GetTexture(textureData, TextureManager.container.unknownArt.texture);
            }

            try
            {
                Directory.CreateDirectory(Program.artPath);
                Directory.CreateDirectory(Program.altArtPath);
                var path = Program.altArtPath + code;

                if (File.Exists(path + Program.jpgExpansion))
                    path += Program.jpgExpansion;
                else if (File.Exists(path + Program.pngExpansion))
                    path += Program.pngExpansion;
                else if (File.Exists(Program.artPath + code + Program.jpgExpansion))
                    path = Program.artPath + Program.slash + code + Program.jpgExpansion;
                else
                {
                    textureData.texture = await LoadArtFromZipArt(code);
                    if (textureData.texture == null && CanUseTexture(textureData))
                        textureData.texture = await LoadArtFromZipPics(code);
                    return GetTexture(textureData, TextureManager.container.unknownArt.texture);
                }

                textureData.texture = await LoadPicFromFileAsync(path);
                return GetTexture(textureData, TextureManager.container.unknownArt.texture);
            }
            catch
            {
                // Wake existing waiters, but allow later requests to retry a failed load.
                RemoveTexture(_cachedArts, code, textureData);
                throw;
            }
            finally
            {
                FinishTexture(_cachedArts, code, textureData);
            }
        }

        public static async Task<Texture2D> LoadCardAsync(int code, bool cache = false)
        {
            await TaskUtility.WaitWhile(() => TextureManager.container == null);
            if (!Application.isPlaying)
                return null;

            var textureData = AcquireTexture(_cachedCards, code, cache, out var ownsLoad);
            if (!ownsLoad)
            {
                await TaskUtility.WaitUntil(() => textureData.loaded || textureData.invalidated);
                return GetTexture(textureData, TextureManager.container.unknownCard.texture);
            }

            TextureData artData = null;
            try
            {
                var data = CardsManager.Get(code, true);
                if (data.Id == 0)
                    return TextureManager.container.unknownCard.texture;

                artData = AcquireTexture(_cachedArts, code, false, out var ownsArtLoad);
                var art = await LoadArtAsync(code, artData, ownsArtLoad);
                if (!CanUseTexture(textureData))
                    return null;
                if (!Program.instance.cardRenderer.RenderCard(code, art))
                    return TextureManager.container.unknownCard.texture;

                textureData.texture = new Texture2D(RenderTexture.active.width, RenderTexture.active.height, TextureFormat.RGB24, true);
                textureData.texture.ReadPixels(new Rect(0, 0, RenderTexture.active.width, RenderTexture.active.height), 0, 0);
                textureData.texture.Apply();
                textureData.texture.name = "Card_" + code;
                await TaskUtility.WaitOneFrame();
                return GetTexture(textureData, TextureManager.container.unknownCard.texture);
            }
            catch
            {
                RemoveTexture(_cachedCards, code, textureData);
                throw;
            }
            finally
            {
                FinishTexture(_cachedCards, code, textureData);
                if (artData != null)
                    ReleaseTexture(_cachedArts, code, artData);
            }
        }

        public static void DeleteCard(int code)
        {
            if (_cachedCards.TryGetValue(code, out var card))
                ReleaseTexture(_cachedCards, code, card);
        }

        public static void DeleteCache()
        {
            foreach (var art in _cachedArts)
                RemoveTexture(_cachedArts, art.Key, art.Value);
            foreach (var card in _cachedCards)
                RemoveTexture(_cachedCards, card.Key, card.Value);
        }

        private static Task<Texture2D> LoadArtFromZipArt(int code) => LoadArtFromZip(code, false);

        private static Task<Texture2D> LoadArtFromZipPics(int code) => LoadArtFromZip(code, true);

        private static async Task<Texture2D> LoadArtFromZip(int code, bool fullCard)
        {
            foreach (var zip in ZipHelper.zips)
            {
                if (zip.Name.EndsWith("script.zip", StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var file in zip.EntryFileNames)
                {
                    foreach (var extension in new[] { Program.pngExpansion, Program.jpgExpansion })
                    {
                        var picPath = $"{(fullCard ? "pics" : "art")}/{code}{extension}";
                        if (!string.Equals(file, picPath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        using var stream = new MemoryStream();
                        zip[file].Extract(stream);
                        await TaskUtility.WaitOneFrame();
                        if (!Application.isPlaying)
                            return null;

                        var texture = new Texture2D(2, 2);
                        try
                        {
                            if (!texture.LoadImage(stream.ToArray()))
                                return null;
                            if (!fullCard)
                            {
                                var art = texture;
                                texture = null;
                                return art;
                            }

                            var data = CardsManager.Get(code);
                            if (code >= 120000000 && code < 130000000)
                                return data.HasType(CardType.Monster)
                                    ? await GetArtFromRushDuelMonsterCard(texture)
                                    : await GetArtFromRushDuelSpellCard(texture);
                            return data.HasType(CardType.Pendulum)
                                ? await GetArtFromPendulumCard(texture)
                                : await GetArtFromCard(texture);
                        }
                        finally
                        {
                            Destroy(texture);
                        }
                    }
                }
            }
            return null;
        }

        public static IEnumerator LoadCardToRawImage(int code, RawImage rawImage)
        {
            rawImage.material = TextureManager.GetCardMaterial(code);
            rawImage.material
                .SetTexture("_LoadingTex", TextureManager.container
                .GetCardUnloadTexture(CardsManager.Get(code)));
            rawImage.material.SetFloat("_LoadingBlend", 1f);

            var task = LoadCardAsync(code, false);
            while(!task.IsCompleted)
                yield return null;

            if(rawImage != null)
            {
                rawImage.texture = task.Result;
                rawImage.material.DOFloat(0f, "_LoadingBlend", 0.1f);
            }
        }

        #region Crop Texture
        private static async Task<Texture2D> GetArtFromCard(Texture2D cardPic)
        {
            var startX = Mathf.CeilToInt(cardPic.width * 0.13f);
            var startY = Mathf.CeilToInt(cardPic.height * 0.3f);
            var width = Mathf.CeilToInt(cardPic.width * 0.87f);
            var height = Mathf.CeilToInt(cardPic.height * 0.81f);
            return await GetCroppingTex(cardPic, startX, startY, width, height);
        }
        private static async Task<Texture2D> GetArtFromPendulumCard(Texture2D cardPic)
        {
            var startX = Mathf.CeilToInt(cardPic.width * 0.067f);
            var startY = Mathf.CeilToInt(cardPic.height * 0.38f);
            var width = Mathf.CeilToInt(cardPic.width * 0.933f);
            var height = Mathf.CeilToInt(cardPic.height * 0.81f);
            return await GetCroppingTex(cardPic, startX, startY, width, height);
        }
        private static async Task<Texture2D> GetArtFromRushDuelMonsterCard(Texture2D cardPic)
        {
            var startX = Mathf.CeilToInt(cardPic.width * 0.067f);
            var startY = Mathf.CeilToInt(cardPic.height * 0.29f);
            var width = Mathf.CeilToInt(cardPic.width * 0.933f);
            var height = Mathf.CeilToInt(cardPic.height * 0.90f);
            return await GetCroppingTex(cardPic, startX, startY, width, height);
        }
        private static async Task<Texture2D> GetArtFromRushDuelSpellCard(Texture2D cardPic)
        {
            var startX = Mathf.CeilToInt(cardPic.width * 0.067f);
            var startY = Mathf.CeilToInt(cardPic.height * 0.29f);
            var width = Mathf.CeilToInt(cardPic.width * 0.933f);
            var height = Mathf.CeilToInt(cardPic.height * 0.90f);
            return await GetCroppingTex(cardPic, startX, startY, width, height);
        }
        private static async Task<Texture2D> GetCroppingTex(Texture2D texture, int startX, int startY, int width, int height)
        {
            var returnValue = new Texture2D(width - startX, height - startY);
            var pix = new Color[returnValue.width * returnValue.height];
            var index = 0;
            for (var y = startY; y < height; y++)
                for (var x = startX; x < width; x++)
                    pix[index++] = texture.GetPixel(x, y);

            await TaskUtility.WaitOneFrame();
            returnValue.SetPixels(pix);
            await TaskUtility.WaitOneFrame();
            returnValue.Apply();
            return returnValue;
        }
        #endregion

    }
}
