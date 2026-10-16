using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using UnityWebSocket;
using Newtonsoft.Json.Linq;
using System.IO;
using MDPro3.Utility;

namespace MDPro3.Net
{
    public static class MyCard
    {
        public const string duelUrl = "tiramisu.moenext.com";
        public const int entertainPort = 7911;
        public const int athleticPort = 8911;

        const string loginUrl = "https://sapi.moecube.com:444/accounts/signin";
        const string authUrl = "https://sapi.moecube.com:444/accounts/authUser";
        const string appsUrl = "https://sapi.moecube.com:444/release/update/apps.json";
        const string expUrl = "https://sapi.moecube.com:444/ygopro/arena/user?username=";
        const string matchUrl = "https://sapi.moecube.com:444/ygopro/match";
        const string userUrl = "https://sapi.moecube.com:444/accounts/users/{username}.json";
        const string athleticWatchUrl = "wss://tiramisu.moenext.com:8923?filter=started";
        const string entertainWatchUrl = "wss://tiramisu.moenext.com:7923?filter=started";
        const string contentTypeHeader = "Content-Type";
        const string jsonHeader = "application/json";
        const string authHeader = "Authorization";
        public static MyCardAccount account;
        public static MyCardApp ygopro;
        public static Texture2D avatar;

        const int avatarSize = 256;

        public static async Task<MyCardAccount> Login(string account, string password)
        {
            string json = JsonConvert.SerializeObject(new { account, password });
            using var request = UnityWebRequest.Post(loginUrl, json, jsonHeader);

            request.SetRequestHeader("Content-Type", jsonHeader);
            request.SetRequestHeader("Origin", "https://accounts.moecube.com");
            request.SetRequestHeader("Referer", "https://accounts.moecube.com/");

            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if(!Application.isPlaying)
                return null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                MyCard.account = JsonUtility.FromJson<MyCardAccount>(request.downloadHandler.text);

                await OnlineDeck.GetAllDecks();
                if (!Application.isPlaying)
                    return null;

                return MyCard.account;
            }
            else
            {
                return CreateFailedAccount(request.downloadHandler?.text);
            }
        }

        public static async Task<MyCardAccount> TokenIn(string token)
        {
            using var request = UnityWebRequest.Get(authUrl);
            request.SetRequestHeader(authHeader, "Bearer " + token);

            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if(!Application.isPlaying)
                return null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                var user = JsonUtility.FromJson<MyCardUserInfo>(request.downloadHandler.text);
                account = new MyCardAccount();
                account.user = user;
                account.token = token;

                await OnlineDeck.GetAllDecks();
                if (!Application.isPlaying)
                    return null;

                return account;
            }
            else
            {
                return CreateFailedAccount(request.downloadHandler?.text);
            }
        }

        private static MyCardAccount CreateFailedAccount(string responseBody)
        {
            string message = null;
            if (!string.IsNullOrWhiteSpace(responseBody))
            {
                try
                {
                    message = JsonConvert.DeserializeObject<MyCardMessage>(responseBody)?.message;
                }
                catch (JsonException)
                {
                    // Proxies and offline servers may return HTML instead of the API's JSON error.
                }
            }

            return new MyCardAccount
            {
                user = new MyCardUserInfo
                {
                    username = string.IsNullOrWhiteSpace(message)
                        ? InterString.Get("Çë¼ì²éÍøÂçÁ¬½Ó") : message
                }
            };
        }

        const string avatarSavePath = "Picture/MyCardAvatars/";
        static readonly Dictionary<string, string> cachedAvatarAddress = new Dictionary<string, string>();
        static readonly Dictionary<string, Texture2D> cachedAvatars = new Dictionary<string, Texture2D>();
        public static async Task<Texture2D> GetAvatarAsync(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName))
                return null;

            string avatarName;
            lock (cachedAvatarAddress)
                cachedAvatarAddress.TryGetValue(userName, out avatarName);

            if (avatarName != null)
            {
                lock (cachedAvatars)
                    if (cachedAvatars.TryGetValue(avatarName, out var texture) && texture != null)
                        return texture;

                var loadedImage = await TextureManager.LoadPicFromFileAsync(
                    Path.Combine(avatarSavePath, avatarName + Program.pngExpansion));
                if (!Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(loadedImage);
                    return null;
                }

                if (loadedImage != null)
                    return CacheAvatar(userName, avatarName, loadedImage);
                // A missing or unreadable disk cache must not prevent downloading the avatar again.
            }

            string avatarAddress;
            using (var request = UnityWebRequest.Get(userUrl.Replace("{username}", Uri.EscapeDataString(userName))))
            {
                await request.SendWebRequest();
                if (!Application.isPlaying)
                    return null;

                if (request.result == UnityWebRequest.Result.Success)
                {
                    avatarAddress = JsonUtility.FromJson<MyCardRoomUserInfo>(request.downloadHandler.text)?.user?.avatar;
                }
                else
                {
                    Debug.LogError($"Failed to get user info: {request.error}");
                    return null;
                }
            }

            if (string.IsNullOrWhiteSpace(avatarAddress))
                return null;

            // The full URL distinguishes avatar.png files belonging to different users or versions.
            using (var hash = SHA256.Create())
                avatarName = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(avatarAddress)))
                    .Replace("-", string.Empty).ToLowerInvariant();

            lock (cachedAvatars)
                if (cachedAvatars.TryGetValue(avatarName, out var texture) && texture != null)
                {
                    lock (cachedAvatarAddress)
                        cachedAvatarAddress[userName] = avatarName;
                    return texture;
                }

            var downloadImage = await Tools.DownloadImageAsync(avatarAddress);
            if (!Application.isPlaying)
            {
                UnityEngine.Object.Destroy(downloadImage);
                return null;
            }
            if (downloadImage == null)
                return null;

            // ResizeTexture2D already destroys its source texture.
            var avatarTexture = downloadImage.width > avatarSize || downloadImage.height > avatarSize
                ? TextureManager.ResizeTexture2D(downloadImage, avatarSize, avatarSize) : downloadImage;
            avatarTexture = CacheAvatar(userName, avatarName, avatarTexture);

            try
            {
                Directory.CreateDirectory(avatarSavePath);
                File.WriteAllBytes(Path.Combine(avatarSavePath, avatarName + Program.pngExpansion), avatarTexture.EncodeToPNG());
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"Could not save MyCard avatar cache: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Debug.LogWarning($"Could not save MyCard avatar cache: {exception.Message}");
            }

            return avatarTexture;
        }

        private static Texture2D CacheAvatar(string userName, string avatarName, Texture2D texture)
        {
            lock (cachedAvatars)
            {
                if (cachedAvatars.TryGetValue(avatarName, out var existing) && existing != null)
                {
                    if (existing != texture)
                        UnityEngine.Object.Destroy(texture);
                    texture = existing;
                }
                else
                    cachedAvatars[avatarName] = texture;
            }
            lock (cachedAvatarAddress)
                cachedAvatarAddress[userName] = avatarName;
            return texture;
        }

        public static async Task<MyCardNews> GetNews()
        {
            using var request = UnityWebRequest.Get(appsUrl);
            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if(!Application.isPlaying) 
                return null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                var json = request.downloadHandler.text;
                json = json.Replace("\"news\":[]", "\"news\":{}");
                var apps = JsonConvert.DeserializeObject<MyCardApp[]>(json);
                foreach(var app in apps)
                    if (app.id == "ygopro")
                    {
                        ygopro = app;
                        return app.news;
                    }
                return null;
            }
            else
            {
                Debug.LogError($"Failed to get apps: {request.error}");
                return null;
            }
        }

        public static async Task<MyCardUserExp> GetExp()
        {
            if (account == null)
                return null;

            using var request = UnityWebRequest.Get(expUrl + Uri.EscapeDataString(account.user.username));
            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if(!Application.isPlaying)
                return null;

            if (request.result == UnityWebRequest.Result.Success)
                return JsonConvert.DeserializeObject<MyCardUserExp>(request.downloadHandler.text);
            else
            {
                Debug.LogError($"Failed to get Exp: {request.error}");
                return null;
            }
        }

        public static async Task<MyCardMatchInfo> GetMatchInfo(string arena)
        {
            using var request = UnityWebRequest.PostWwwForm(matchUrl + "?arena=" + Uri.EscapeDataString(arena), jsonHeader);
            request.SetRequestHeader(contentTypeHeader, jsonHeader);
            request.SetRequestHeader(authHeader, "Basic " + CustomBase64Encode(account.user.username + ":" + account.user.id));

            var send = request.SendWebRequest();
            await TaskUtility.WaitUntil(() => send.isDone);
            if(!Application.isPlaying)
                return null;

            if(request.result == UnityWebRequest.Result.Success)
            {
                return JsonUtility.FromJson<MyCardMatchInfo>(request.downloadHandler.text);
            }
            else
            {
                Debug.LogError($"{arena} Match error: {request.error}");
                return null;
            }
        }

        private static string CustomBase64Encode(string input)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(bytes);
        }

        #region WebSocket Athletic Watch

        private static WebSocket socket;
        public static void ConnectToAthleticWatchListWebSocket()
        {
            socket = new WebSocket(athleticWatchUrl);
            socket.OnOpen += OnSocketOpen;
            socket.OnClose += OnSocketClose;
            socket.OnMessage += OnSocketMessage;
            socket.OnError += OnSocketError;

            socket.ConnectAsync();
        }

        public static void CloseAthleticWatchListWebSocket()
        {
            try
            {
                socket?.CloseAsync();
                Program.instance.online.ClearWatchList();
            }
            catch { }

            socket = null;
        }

        private static void OnSocketOpen(object sender, OpenEventArgs e)
        {
            Debug.Log(string.Format("Socket Connected: {0}", athleticWatchUrl));
        }
        private static void OnSocketClose(object sender, CloseEventArgs e)
        {
            Debug.Log(string.Format("Socket Closed: StatusCode: {0}, Reason: {1}", e.StatusCode, e.Reason));
        }
        private static void OnSocketError(object sender, UnityWebSocket.ErrorEventArgs e)
        {
            Debug.Log(string.Format("Socket Error: {0}", e.Message));
        }
        private static void OnSocketMessage(object sender, MessageEventArgs e)
        {
            //Debug.Log(string.Format("Receive: {0}", e.Data));
            HandleWatchInfo(e.Data);
        }

        static string currentEventType;

        private static void HandleWatchInfo(string json)
        {
            var info = JsonConvert.DeserializeObject<MyCardWatchInfo>(json);
            currentEventType = info.eventType;
            switch (currentEventType)
            {
                case "init":
                case "create":
                case "update":
                    var roomsOrSingleRoom = JsonConvert.DeserializeObject<dynamic>(info.data.ToString());
                    if(roomsOrSingleRoom is JArray array)
                        HandleRooms(array);
                    else
                        HandleSingleRoom((JObject)roomsOrSingleRoom);
                    break;
                case "delete":
                    HandleDelete(info.data as string);
                    break;
                default:
                    Debug.LogWarning("Unknown event type: " + info.eventType);
                    break;
            }
        }
        private static void HandleRooms(JArray rooms)
        {
            var list = new List<MyCardRoom>();
            foreach(var room in rooms)
            {
                var roomInstance = room.ToObject<MyCardRoom>();
                list.Add(roomInstance);
            }
            Program.instance.online.SetWatchRooms(list);
        }
        private static void HandleSingleRoom(JObject room)
        {
            var roomInstance = room.ToObject<MyCardRoom>();
            switch(currentEventType)
            {
                case "create":
                    Program.instance.online.CreateWatchRoom(roomInstance); 
                    break;
                case "update":
                    Program.instance.online.UpdateWatchRoom(roomInstance);
                    break;
            }
        }
        private static void HandleDelete(string roomId)
        {
            Program.instance.online.DeleteWatchRoom(roomId);
        }
        #endregion

        #region Mycard Tools
        public static string GetJoinRoomPassword(MyCardRoomOptions options, string roomId, int userId, bool _private = false)
        {
            byte[] optionsBuffer = new byte[6];
            optionsBuffer[1] = (byte)((_private ? (int)RoomAction.JoinPrivate : (int)RoomAction.JoinPublic) << 4);

            EncryptBuffer(optionsBuffer, userId);

            string base64String = Convert.ToBase64String(optionsBuffer);

            return base64String + roomId;
        }
        public static string GetCreateRoomPasswd(MyCardRoomOptions options, string roomID, int userId, bool _private = false)
        {
            byte[] optionsBuffer = new byte[6];
            optionsBuffer[1] = (byte)(((byte)(_private ? RoomAction.CreatePrivate : RoomAction.CreatePublic) << 4) | (byte)(options.duel_rule << 1) | (options.auto_death ? 0x1 : 0));

            optionsBuffer[2] = (byte)(((byte)options.rule << 5) | ((byte)options.mode << 3) | (options.no_check_deck ? 1 << 1 : 0) | (options.no_shuffle_deck ? 1 : 0));
            WriteUInt16LE(optionsBuffer, 3, (ushort)options.start_lp);
            optionsBuffer[5] = (byte)(((byte)options.start_hand << 4) | options.draw_count);

            EncryptBuffer(optionsBuffer, userId);
            string base64String = Convert.ToBase64String(optionsBuffer);

            return base64String + roomID;
        }

        private static void EncryptBuffer(byte[] buffer, int external_id)
        {
            int checksum = 0;

            for (int i = 1; i < buffer.Length; i++)
            {
                checksum -= buffer[i];
            }

            buffer[0] = (byte)(checksum & 0xff);

            int secret = (external_id % 65535) + 1;

            for (int i = 0; i < buffer.Length; i += 2)
            {
                ushort value = ReadUInt16LE(buffer, i);
                ushort xorResult = (ushort)(value ^ secret);
                WriteUInt16LE(buffer, i, xorResult);
            }
        }

        public static int GetPrivateRoomID(int external_id)
        {
            return external_id ^ 0x54321;
        }

        private static ushort ReadUInt16LE(byte[] buffer, int offset)
        {
            return (ushort)((buffer[offset + 1] << 8) | buffer[offset]);
        }

        private static void WriteUInt16LE(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xff);
            buffer[offset + 1] = (byte)((value >> 8) & 0xff);
        }
        public enum RoomAction
        {
            CreatePublic = 1,
            CreatePrivate = 2,
            JoinPublic = 3,
            JoinPrivate = 5,
        }
        #endregion
    }

    [Serializable]
    public class MyCardAccount
    {
        public MyCardUserInfo user;
        public string token;
    }
    [Serializable]
    public class MyCardRoomUserInfo
    {
        public MyCardUserInfo user;
    }
    [Serializable]
    public class MyCardUserInfo
    {
        public int id;
        public string username;
        public string name; 
        public string email;
        public string password_hash;
        public string salt;
        public bool active;
        public bool admin;
        public string avatar;
        public string locale;
        public string registration_ip_address;
        public string ip_address;
        public string created_at;
        public string updated_at;
    }

    [Serializable]
    public class MyCardMessage
    {
        public string message;
    }

    [Serializable]
    public class MyCardApp
    {
        public string id;
        public MyCardServers[] servers;
        public MyCardWindbots[] windbot;
        public MyCardNews news;
    }

    [Serializable]
    public class MyCardServers
    {
        public string id;
        public string url;
        public string name;
        public int port;
        public bool custom;
        public bool replay;
        public string address;
        public string[] windbot;
    }
    [Serializable]
    public class MyCardWindbots
    {
        [JsonProperty("en-US")]
        public string[] EnglishUS { get; set; }
        [JsonProperty("zh-CN")]
        public string[] ChineseCN { get; set; }
    }
    [Serializable]
    public class MyCardNews
    {
        [JsonProperty("en-US")]
        public News[] EnglishUS { get; set; }
        [JsonProperty("zh-CN")]
        public News[] ChineseCN { get; set; }
    }
    [Serializable]
    public class News
    {
        public string url;
        public string text;
        public string image;
        public string title;
        public string updated_at;
    }
    [Serializable]
    public class MyCardUserExp
    {
        public int exp;
        public int pt;
        public int entertain_win;
        public int entertain_lose;
        public int entertain_draw;
        public int entertain_all;
        public string entertain_wl_ratio;
        public int exp_rank;
        public int athletic_win;
        public int athletic_lose;
        public int athletic_draw;
        public int athletic_all;
        public string athletic_wl_ratio;
        public int arena_rank;
    }
    [Serializable]
    public class MyCardMatchInfo
    {
        public string address;
        public int port;
        public string password;
    }

    [Serializable]
    public class MyCardWatchInfo
    {
        [JsonProperty("event")]
        public string eventType;
        public object data;
    }
    [Serializable]
    public class MyCardRoom
    {
        public string id;
        public string title;
        public MyCardRoomUser user;
        public MyCardRoomUser[] users;
        public MyCardRoomOptions options;
        public string arena;
    }
    [Serializable]
    public class MyCardRoomUser
    {
        public string username;
        public int position;
    }
    [Serializable]
    public class MyCardRoomOptions
    {
        public int lflist;
        public int rule;
        public int mode;
        public int duel_rule;
        public bool no_check_deck;
        public bool no_shuffle_deck;
        public int start_lp;
        public int start_hand;
        public int draw_count;
        public int time_limit;
        public bool no_watch;
        public bool auto_death;
        public int replay_mode;
    }
}
