using MDPro3.YGOSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using MDPro3.Utility;

namespace MDPro3.Net
{
    public static class OnlineDeck
    {
        public static OnlineDeckData[] decks;

        #region Const
        const string url = "http://rarnu.xyz:38383";
        const string liteAPI = "/api/mdpro3/deck/list/lite";
        const string getAPI = "/api/mdpro3/deck/";
        const string likeAPI = "/api/mdpro3/deck/like/";

        const string getAllAPI = "/api/mdpro3/sync/";
        const string syncAllAPI = "/api/mdpro3/sync/multi";
        const string getIdsAPI = "/api/mdpro3/deck/deckIds?count=";
        const string getIdAPI = "/api/mdpro3/deck/deckId";
        const string syncSigleAPI = "/api/mdpro3/sync/single";
        const string publicAPI = "/api/mdpro3/deck/public";

        const string reqHeader = "ReqSource";
        const string reqValue = "MDPro3";
        const string contentTypeHeader = "Content-Type";
        const string jsonHeader = "application/json";
        const string tokenHeader = "token";
        #endregion

        #region Online Get
        public static async Task<OnlineDeckData[]> FetchSimpleDeckList(int size, string keyWord = "", string contributor = "", bool sortLike = true)
        {
            string apiUrl = url + liteAPI + $"?size={size}&keyWord={keyWord}&contributor={contributor}&sortLike={sortLike}";
            using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader(reqHeader, reqValue);

            try
            {
                var send = request.SendWebRequest();
                await TaskUtility.WaitUntil(() => send.isDone);
                if(!Application.isPlaying)
                    return null;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    string jsonResult = request.downloadHandler.text;
                    var responseData = JsonUtility.FromJson<ResponseMultiSimpleData>(request.downloadHandler.text);
                    return responseData.data;
                }
                else
                {
                    MessageManager.Cast("FetchSimpleDeckList Error : " + request.error);
                    return null;
                }
            }
            catch(Exception e)
            {
                Debug.Log("FetchSimpleDeckList Error: " + e);
                return null;
            }
            finally
            {
                request.Dispose();
                if(request.downloadHandler != null)
                    request.downloadHandler.Dispose();
            }
        }
        public static async Task<OnlineDeckData> GetDeck(string deckID)
        {
            string apiUrl = url + getAPI + deckID;
            using UnityWebRequest request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader(reqHeader, reqValue);

            try
            {
                var send = request.SendWebRequest();
                await TaskUtility.WaitUntil(() => send.isDone);
                if(!Application.isPlaying)
                    return null;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    return JsonUtility.FromJson<ResponseSingleData>(request.downloadHandler.text).data;
                }
                else
                {
                    MessageManager.Cast("FetchSimpleDeckList Error: " + request.error);
                    return null;
                }
            }
            catch (Exception e)
            {
                Debug.Log("FetchSimpleDeckList Error: " + e);
                return null;
            }
            finally
            {
                request.Dispose();
                if (request.downloadHandler != null)
                    request.downloadHandler.Dispose();
            }
        }
        public static async Task<OnlineDeckData[]> GetAllDecks()
        {
            var account = MyCard.account;
            if (account?.user == null)
                return null;

            string apiUrl = url + getAllAPI + account.user.id;
            using var request = UnityWebRequest.Get(apiUrl);
            request.SetRequestHeader(reqHeader, reqValue);
            request.SetRequestHeader(tokenHeader, account.token);

            try
            {
                var send = request.SendWebRequest();
                await TaskUtility.WaitUntil(() => send.isDone);
                if (!Application.isPlaying || !ReferenceEquals(MyCard.account, account))
                    return null;

                if (request.result == UnityWebRequest.Result.Success
                    && TryReadResponse(request.downloadHandler.text, out ResponseMultiSimpleData response)
                    && response.code == 0 && response.data != null && response.data.All(deck => deck != null))
                {
                    // A failed or malformed refresh must never discard a usable cache.
                    decks = response.data;
                    return decks;
                }
                MessageManager.Cast(InterString.Get("获取MyCard卡组失败：") + request.error);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("GetAllDecks failed: " + exception.Message);
            }
            return null;
        }
        public static async void LikeDeck(string deckId)
        {
            string apiUrl = url + likeAPI + deckId;

            using UnityWebRequest request = UnityWebRequest.PostWwwForm(apiUrl, jsonHeader);
            request.SetRequestHeader(reqHeader, reqValue);
            request.SetRequestHeader(contentTypeHeader, jsonHeader);
            
            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if (!Application.isPlaying)
                return;

            if (request.result == UnityWebRequest.Result.Success)
            {
                var responseData = JsonUtility.FromJson<ResponseSingleData>(request.downloadHandler.text);
                if (responseData.code == 0)
                    MessageManager.Cast(InterString.Get("点赞卡组成功。"));
                else
                    MessageManager.Cast(InterString.Get("点赞卡组失败：") + InterString.Get(responseData.message, responseData.messageValue));
            }
            else
                MessageManager.Cast(InterString.Get("点赞卡组失败：") + request.error);
        }
        #endregion

        #region Online Post
        public static async Task<bool> UploadDecks(List<Deck> decks, List<string> deckNames)
        {
            var account = MyCard.account;
            if (account?.user == null || decks == null || deckNames == null
                || decks.Count == 0 || decks.Count != deckNames.Count
                || decks.Any(deck => deck == null) || deckNames.Any(name => !IsLocalDeckName(name)))
                return false;

            // Capture both the caller and its file before any network suspension.
            // A later save must not be replaced by this request's older acknowledgement.
            var sourceDecks = decks.ToArray();
            var sourceNames = deckNames.ToArray();
            var sourceYdks = sourceDecks.Select(deck => deck.GetYDK()).ToArray();
            var sourceFiles = new LocalDeckSnapshot[sourceDecks.Length];
            var destinationFiles = new LocalDeckSnapshot[sourceDecks.Length];
            for (int i = 0; i < sourceDecks.Length; i++)
                if (!TryCaptureLocalDeck(sourceNames[i], out sourceFiles[i]))
                    return false;

            string apiUrl = url + getIdsAPI + sourceDecks.Length;
            using var getIDs = UnityWebRequest.Get(apiUrl);
            getIDs.SetRequestHeader(reqHeader, reqValue);
            var send = getIDs.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if (!Application.isPlaying || !ReferenceEquals(MyCard.account, account))
                return false;

            if (getIDs.result != UnityWebRequest.Result.Success
                || !TryReadResponse(getIDs.downloadHandler.text, out ResponseDeckIDs allocated)
                || allocated.code != 0 || allocated.data == null || allocated.data.Length != sourceDecks.Length
                || allocated.data.Any(string.IsNullOrEmpty)
                || allocated.data.Distinct().Count() != sourceDecks.Length)
            {
                MessageManager.Cast(InterString.Get("上传卡组失败：") + getIDs.error);
                return false;
            }

            var stagedDecks = new Deck[sourceDecks.Length];
            var names = new string[sourceDecks.Length];
            var reservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var body = new PostAllDecksBody
            {
                deckContributor = account.user.username,
                userId = account.user.id,
                decks = new PostDeck[sourceDecks.Length]
            };
            for (int i = 0; i < sourceDecks.Length; i++)
            {
                var name = sourceNames[i];
                if (DeckNameExist(name) || reservedNames.Contains(name))
                {
                    do { name += " - " + InterString.Get("副本"); }
                    while (DeckNameExist(name) || reservedNames.Contains(name)
                        || File.Exists(Program.deckPath + name + Program.ydkExpansion)
                        || Directory.Exists(Program.deckPath + name + Program.ydkExpansion));
                }
                names[i] = name;
                reservedNames.Add(name);
                if (!TryCaptureLocalDeck(name, out destinationFiles[i]))
                    return false;
                // Stage a snapshot: neither the caller's deck nor its file changes before acknowledgement.
                stagedDecks[i] = new Deck(sourceYdks[i], allocated.data[i], account.user.id.ToString());
                body.decks[i] = new PostDeck(stagedDecks[i], allocated.data[i], name, stagedDecks[i].GetYDK());
            }

            using var request = UnityWebRequest.Post(url + syncAllAPI, JsonUtility.ToJson(body), jsonHeader);
            request.SetRequestHeader(reqHeader, reqValue);
            request.SetRequestHeader(contentTypeHeader, jsonHeader);
            request.SetRequestHeader(tokenHeader, account.token);
            send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if (!Application.isPlaying || !ReferenceEquals(MyCard.account, account))
                return false;

            if (request.result != UnityWebRequest.Result.Success
                || !TryReadResponse(request.downloadHandler.text, out ResponseMultiData response)
                || response.code != 0 || response.data != sourceDecks.Length)
            {
                MessageManager.Cast(InterString.Get("上传卡组失败：") + request.error);
                return false;
            }

            bool saved = true;
            for (int i = 0; i < sourceDecks.Length; i++)
            {
                if (SaveUnchangedLocalDeck(stagedDecks[i], names[i], DateTime.Now, sourceNames[i],
                    sourceFiles[i], destinationFiles[i], sourceDecks[i], sourceYdks[i]))
                {
                    sourceDecks[i].deckId = stagedDecks[i].deckId;
                    sourceDecks[i].userId = stagedDecks[i].userId;
                }
                else
                    saved = false;
            }
            await GetAllDecks();
            if (!saved)
                MessageManager.Cast("Deck upload reached the server, but a local file could not be saved. The original file was retained.");
            return saved;
        }
        public static async Task<bool> SyncDeck(string deckId, string deckName, Deck deck, bool showHint = true)
        {
            var account = MyCard.account;
            if (account?.user == null || deck == null || !IsLocalDeckName(deckName))
                return false;

            var od = GetByID(deckId);
            if (od == null || od.isDelete)
                return await UploadDecks(new List<Deck> { deck }, new List<string> { deckName });

            var sourceYdk = deck.GetYDK();
            if (!TryCaptureLocalDeck(deckName, out var sourceFile))
                return false;
            var stagedDeck = new Deck(sourceYdk, deckId, account.user.id.ToString());
            var ydk = stagedDeck.GetYDK();
            var body = new PostDeckBody
            {
                userId = account.user.id,
                deckContributor = account.user.username,
                deck = new PostDeck(stagedDeck, deckId, deckName, ydk)
            };
            using var request = UnityWebRequest.Post(url + syncSigleAPI, JsonUtility.ToJson(body), jsonHeader);
            request.SetRequestHeader(reqHeader, reqValue);
            request.SetRequestHeader(contentTypeHeader, jsonHeader);
            request.SetRequestHeader(tokenHeader, account.token);

            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if (!Application.isPlaying || !ReferenceEquals(MyCard.account, account))
                return false;

            if (request.result != UnityWebRequest.Result.Success
                || !TryReadResponse(request.downloadHandler.text, out SyncResponseSingleData response)
                || response.code != 0 || !response.data)
            {
                MessageManager.Cast(InterString.Get("云端卡组同步失败：") + request.error);
                return false;
            }

            if (!SaveUnchangedLocalDeck(stagedDeck, deckName, DateTime.Now, null,
                sourceFile, sourceFile, deck, sourceYdk))
            {
                MessageManager.Cast("Deck sync reached the server, but the local file could not be saved. The original file was retained.");
                return false;
            }
            od.deckYdk = ydk;
            od.deckName = deckName;
            od.deckCoverCard1 = body.deck.deckCoverCard1;
            od.deckCoverCard2 = body.deck.deckCoverCard2;
            od.deckCoverCard3 = body.deck.deckCoverCard3;
            od.deckCase = body.deck.deckCase;
            od.deckProtector = body.deck.deckProtector;
            deck.deckId = stagedDeck.deckId;
            deck.userId = stagedDeck.userId;
            if (showHint)
                MessageManager.Cast(InterString.Get("云端卡组「[?]」已同步。", deckName));
            return true;
        }
        public static async Task<bool> DeleteDecks(List<string> ids)
        {
            var account = MyCard.account;
            if (account?.user == null || ids == null || ids.Count == 0 || decks == null)
                return false;

            var toDelete = decks.Where(deck => deck != null && !deck.isDelete && ids.Contains(deck.deckId))
                .GroupBy(deck => deck.deckId).Select(group => group.First()).ToArray();
            if (toDelete.Length == 0)
                return false;
            var body = new PostAllDecksBody
            {
                deckContributor = account.user.username,
                userId = account.user.id,
                decks = toDelete.Select(deck => new PostDeck { deckId = deck.deckId, isDelete = true }).ToArray()
            };
            using var request = UnityWebRequest.Post(url + syncAllAPI, JsonUtility.ToJson(body), jsonHeader);
            request.SetRequestHeader(reqHeader, reqValue);
            request.SetRequestHeader(contentTypeHeader, jsonHeader);
            request.SetRequestHeader(tokenHeader, account.token);

            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if (!Application.isPlaying || !ReferenceEquals(MyCard.account, account))
                return false;
            if (request.result != UnityWebRequest.Result.Success
                || !TryReadResponse(request.downloadHandler.text, out ResponseMultiData response)
                || response.code != 0 || response.data != toDelete.Length)
            {
                MessageManager.Cast(InterString.Get("删除云端卡组失败：") + request.error);
                return false;
            }
            foreach (var deck in toDelete)
                deck.isDelete = true;
            await GetAllDecks();
            return true;
        }

        public static async Task<bool> UpdatePublicState(string deckId, bool isPublic)
        {
            var account = MyCard.account;
            if (account?.user == null || string.IsNullOrEmpty(deckId))
                return false;
            var apiUrl = url + publicAPI;
            var body = new PostPublicBody
            {
                deckId = deckId,
                isPublic = isPublic,
                userId = account.user.id
            };

            var json = JsonUtility.ToJson(body);
            using var request = UnityWebRequest.Post(apiUrl, json, jsonHeader);
            request.SetRequestHeader(reqHeader, reqValue);
            request.SetRequestHeader(contentTypeHeader, jsonHeader);
            request.SetRequestHeader(tokenHeader, account.token);

            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if (!Application.isPlaying || !ReferenceEquals(MyCard.account, account))
                return false;

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("UpdatePublicState Success: " + isPublic);
                return true;
            }
            else
            {
                Debug.Log("UpdatePublicState Failed: " + request.error);
                return false;
            }
        }

        #endregion

        #region Functions
        private static bool TryReadResponse<T>(string json, out T response) where T : class
        {
            response = null;
            if (string.IsNullOrWhiteSpace(json))
                return false;
            try
            {
                response = JsonUtility.FromJson<T>(json);
                return response != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Invalid online deck response: " + exception.Message);
                return false;
            }
        }

        private static bool IsLocalDeckName(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && name != "." && name != ".."
                && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                && name.IndexOf('/') < 0 && name.IndexOf('\\') < 0;
        }

        public sealed class LocalDeckSnapshot
        {
            internal string Path;
            internal byte[] Contents;

            internal bool HasIdentity(string deckId, string userId)
            {
                if (Contents == null)
                    return false;
                var deck = new Deck(Encoding.UTF8.GetString(Contents), string.Empty, string.Empty);
                return deck.deckId == deckId && deck.userId == userId;
            }

            internal bool IsCurrent()
            {
                try
                {
                    if (Contents == null)
                        return !File.Exists(Path) && !Directory.Exists(Path);
                    return File.Exists(Path) && Contents.SequenceEqual(File.ReadAllBytes(Path));
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Could not verify unchanged local deck: " + exception.Message);
                    return false;
                }
            }
        }

        public static bool TryCaptureLocalDeck(string name, out LocalDeckSnapshot snapshot)
        {
            snapshot = null;
            if (!IsLocalDeckName(name))
                return false;
            try
            {
                var path = Path.GetFullPath(Path.Combine(Program.deckPath, name + Program.ydkExpansion));
                if (Directory.Exists(path))
                    return false;
                snapshot = new LocalDeckSnapshot
                {
                    Path = path,
                    Contents = File.Exists(path) ? File.ReadAllBytes(path) : null
                };
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not read local deck before sync: " + exception.Message);
                return false;
            }
        }

        private static bool SaveUnchangedLocalDeck(Deck stagedDeck, string name, DateTime saveTime,
            string oldName, LocalDeckSnapshot sourceFile, LocalDeckSnapshot destinationFile,
            Deck originalDeck, string sourceYdk)
        {
            try
            {
                // Deck.Save starts uploads without awaiting them. Responses can arrive in any
                // order, so retain any newer file or live edit and leave its identity untouched.
                if (originalDeck.GetYDK() != sourceYdk || !sourceFile.IsCurrent()
                    || !destinationFile.IsCurrent())
                {
                    Debug.LogWarning("A newer local deck change was retained after an earlier upload completed.");
                    return false;
                }
                return SaveLocalDeck(stagedDeck, name, saveTime, oldName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not verify local deck before sync: " + exception.Message);
                return false;
            }
        }

        // Write beside the destination and replace atomically. A failed download/save must not
        // truncate the current deck or remove the old name before the new file is committed.
        public static bool SaveLocalDeck(Deck deck, string name, DateTime saveTime, string oldName = null)
        {
            if (deck == null || !IsLocalDeckName(name) || (oldName != null && !IsLocalDeckName(oldName)))
                return false;
            var destination = Path.Combine(Program.deckPath, name + Program.ydkExpansion);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, deck.GetYDK(), Encoding.UTF8);
                File.SetLastWriteTime(temporary, saveTime);
                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save local deck: " + exception.Message);
                return false;
            }
            finally
            {
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) { Debug.LogWarning("Could not remove temporary deck: " + exception.Message); }
            }

            if (oldName != null && !string.Equals(oldName, name, StringComparison.Ordinal))
            {
                // Cleanup is best effort: a saved deck plus its backup is safer than data loss.
                try
                {
                    if (string.Equals(oldName, name, StringComparison.OrdinalIgnoreCase))
                    {
                        // On case-insensitive volumes both spellings identify the committed file.
                        // Delete only when the directory really contains two distinct spellings.
                        var entries = Directory.EnumerateFiles(Program.deckPath).Select(Path.GetFileName).ToArray();
                        if (!entries.Contains(oldName + Program.ydkExpansion, StringComparer.Ordinal)
                            || !entries.Contains(name + Program.ydkExpansion, StringComparer.Ordinal))
                            return true;
                    }
                    File.Delete(Path.Combine(Program.deckPath, oldName + Program.ydkExpansion));
                }
                catch (Exception exception) { Debug.LogWarning("Saved deck; could not remove previous filename: " + exception.Message); }
            }
            return true;
        }

        // A reconciliation pass can wait for uploads before reaching these mutations.
        // Its original file snapshots and account identity must still match at commit time.
        public static bool DeleteLocalDeck(string name, string deckId, LocalDeckSnapshot expectedFile)
        {
            var account = MyCard.account;
            if (account?.user == null || !IsLocalDeckName(name) || expectedFile == null)
                return false;
            try
            {
                var path = Path.GetFullPath(Path.Combine(Program.deckPath, name + Program.ydkExpansion));
                if (expectedFile.Path != path || !expectedFile.IsCurrent()
                    || !expectedFile.HasIdentity(deckId, account.user.id.ToString()))
                    return false;
                File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not remove unchanged local deck: " + exception.Message);
                return false;
            }
        }

        // Both download paths share validation and the same guarded atomic local commit.
        public static bool SaveDownloadedDeck(OnlineDeckData data, LocalDeckSnapshot expectedDestination,
            string oldName = null, LocalDeckSnapshot expectedSource = null)
        {
            var account = MyCard.account;
            if (account?.user == null || data == null || string.IsNullOrEmpty(data.deckId)
                || string.IsNullOrEmpty(data.deckYdk) || !IsLocalDeckName(data.deckName)
                || expectedDestination == null || (oldName != null && !IsLocalDeckName(oldName)))
                return false;
            try
            {
                var destination = Path.GetFullPath(Path.Combine(Program.deckPath, data.deckName + Program.ydkExpansion));
                if (expectedDestination.Path != destination || !expectedDestination.IsCurrent())
                    return false;
                if (oldName != null)
                {
                    var source = Path.GetFullPath(Path.Combine(Program.deckPath, oldName + Program.ydkExpansion));
                    if (expectedSource == null || expectedSource.Path != source || !expectedSource.IsCurrent()
                        || !expectedSource.HasIdentity(data.deckId, account.user.id.ToString()))
                        return false;
                }
                if (File.Exists(destination))
                {
                    var existing = new Deck(destination);
                    // A failed upload may leave an unrelated local deck with the cloud name.
                    if (existing.deckId != data.deckId || existing.userId != account.user.id.ToString())
                        return false;
                }
                var downloaded = new Deck(data.deckYdk, data.deckId, account.user.id.ToString());
                return SaveLocalDeck(downloaded, data.deckName, data.GetUpdateTime(), oldName);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not save downloaded deck: " + exception.Message);
                return false;
            }
        }

        public static OnlineDeckData GetByID(string deckId)
        {
            if (decks == null)
                return null;
            foreach (var deck in decks)
                if (deck.deckId == deckId)
                    return deck;
            return null;
        }
        public static bool GetDeckPublicState(string deckId)
        {
            if (decks == null)
                return false;
            foreach(var deck in decks)
                if(deck.deckId == deckId)
                    return deck.isPublic;
            return false;
        }
        public static bool StringIsIdFormat(string deckId)
        {
            return !string.IsNullOrEmpty(deckId);
        }
        private static bool DeckNameExist(string deckName)
        {
            if (decks == null)
                return false;
            foreach (var deck in decks)
                if (deck.deckName == deckName && !deck.isDelete)
                    return true;
            return false;
        }

        #endregion

        #region Structure

        [Serializable]
        public class OnlineDeckData
        {
            public string deckId;
            public string deckContributor;
            public string deckName;
            public int deckRank = 0;
            public int deckLike = 0;
            public string deckUploadDate;
            public string deckUpdateDate;
            public int deckCoverCard1 = 0;
            public int deckCoverCard2 = 0;
            public int deckCoverCard3 = 0;
            public int deckCase = 0;
            public int deckProtector = 0;
            public string lastDate;
            public string deckYdk;
            public string deckMainSerial;
            public int userid;
            public bool isPublic;
            public string description;
            public bool isDelete;

            public OnlineDeckData() { }

            public DateTime GetUpdateTime()
            {
                try
                {
                    return DateTime.Parse(deckUpdateDate);
                }
                catch
                {
                    return DateTime.Parse(deckUploadDate);
                }
            }
        }

        [Serializable]
        public class ResponseSingleData
        {
            public int code = 0;
            public string message;
            public string messageValue;
            public OnlineDeckData data;
        }

        [Serializable]
        public class ResponseMultiData
        {
            public int code = 0;
            public string message;
            public int data;
        }

        [Serializable]
        public class SyncResponseSingleData
        {
            public int code;
            public string message;
            public bool data;
        }
        [Serializable]
        public class ResponseMultiSimpleData
        {
            public int code = 0;
            public string message;
            public string messageValue;
            public OnlineDeckData[] data;
        }

        [Serializable]
        public class ResponseRecords
        {
            public int current;
            public int size;
            public int total;
            public int pages;
            public OnlineDeckData[] records;
        }
        [Serializable]
        public class ResponseDeckID
        {
            public int code;
            public int message;
            public string data;
        }
        [Serializable]
        public class ResponseDeckIDs
        {
            public int code;
            public int message;
            public string[] data;
        }

        [Serializable]
        public class PostAllDecksBody
        {
            public string deckContributor;
            public int userId;
            public PostDeck[] decks;
        }

        [Serializable]
        public class PostDeckBody
        {
            public string deckContributor;
            public int userId;
            public PostDeck deck;
        }

        [Serializable]
        public class PostDeck
        {
            public string deckId;
            public string deckName;
            public int deckCoverCard1;
            public int deckCoverCard2;
            public int deckCoverCard3;
            public int deckCase;
            public int deckProtector;
            public string deckYdk;
            public bool isDelete;

            public PostDeck()
            {
            }

            public PostDeck(Deck deck, string deckId, string deckName, string ydk)
            {
                this.deckId = deckId;
                this.deckName = deckName;
                if(deck.Pickup.Count > 0)
                    deckCoverCard1 = deck.Pickup[0];
                if (deck.Pickup.Count > 1)
                    deckCoverCard2 = deck.Pickup[1];
                if (deck.Pickup.Count > 2)
                    deckCoverCard3 = deck.Pickup[2];
                deckCase = deck.Case;
                deckProtector = deck.Protector;
                deckYdk = ydk;
                isDelete = false;
            }
        }

        [Serializable]
        public class PostPublicBody
        {
            public int userId;
            public string deckId;
            public bool isPublic;
        }
        #endregion
    }
}
