using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using MDPro3.YGOSharp;
using UnityEngine.EventSystems;
using UnityEngine.AddressableAssets;
using MDPro3.Net;
using MDPro3.UI;
using MDPro3.Utility;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MDPro3
{
    public class Program : MonoBehaviour
    {
        public bool controlBool;
        public bool controlBool2;

        [Header("Public References")]
        public Transform container_3D;
        public Transform container_2D;
        public CardRenderer cardRenderer;

        [Header("Manager")]
        public CameraManager camera_;
        public UIManager ui_;
        public BackgroundManager background_;
        public AudioManager audio_;
        public TextureManager texture_;
        public MessageManager message_;
        public TimelineManager timeline_;
        public NewsManager news_;

        [Header("Servants")]
        public Menu menu;
        public Solo solo;
        public Online online;
        public SelectPuzzle puzzle;
        public SelectReplay replay;
        public MonsterCutin cutin;
        public MateView mate;
        public SelectDeck selectDeck;
        public Setting setting;
        public Appearance appearance;
        public SelectCharacter character;
        public OcgCore ocgcore;
        public Room room;
        public EditDeck editDeck;
        public DeckEditor deckEditor;
        public OnlineDeckViewer onlineDeckViewer;

        [Header("SidePanels")]

        [HideInInspector]
        public Servant currentServant;
        [HideInInspector]
        public Servant currentSubServant;
        [HideInInspector]
        public int depth;

        #region Const
        public static bool Running = true;
        public const string artPath = "Picture/Art/";
        public const string altArtPath = "Picture/Art2/";
        public const string cardPicPath = "Picture/CardGenerated/";
        public const string closeupPath = "Picture/Closeup/";
        public const string dataPath = "Data/";
        public const string localesPath = "Data/locales/";
        public const string configPath = "Data/config.conf";
        public const string lflistPath = "Data/lflist.conf";
        public const string deckPath = "Deck/";
        public const string expansionsPath = "Expansions/";
        public const string puzzlePath = "Puzzle/";
        public const string replayPath = "Replay/";
        public const string diyPath = "Picture/DIY/";
        public const string slash = "/";
        public const string ydkExpansion = ".ydk";
        public const string pngExpansion = ".png";
        public const string jpgExpansion = ".jpg";
        public const string yrpExpansion = ".yrp";
        public const string yrp3dExpansion = ".yrp3d";
        #endregion

        #region Initializement

        public static Program instance;
        public static Items items;

        List<Manager> managers = new List<Manager>();
        List<Servant> servants = new List<Servant>();

        void Initialize()
        {
            if (!Directory.Exists(dataPath))
                Directory.CreateDirectory(dataPath);
            Config.Initialize(configPath);

            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (items != null)
                InitializeRest();
            else
            {
                var handle = Addressables.LoadAssetAsync<Items>("Items");
                handle.Completed += (result) =>
                {
                    if (result.Status != AsyncOperationStatus.Succeeded || result.Result == null)
                    {
                        Debug.LogError("Cannot initialize Master-Duel: the Items Addressable failed to load. "
                            + result.OperationException, this);
                        Addressables.Release(result);
                        enabled = false;
                        return;
                    }
                    items = result.Result;
                    InitializeRest();
                };
            }
        }

        void InitializeRest()
        {
            ZipHelper.Initialize();
            items.Initialize();
            BanlistManager.Initialize();
            InitializeAllManagers();
            InitializeAllServants();
            ReadParams();

            //VoiceHelper.ExportAllCardsNotFound();
        }

        public static bool exitOnReturn = false;
        void ReadParams()
        {
            var options = CommandLineOptions.Parse(Environment.GetCommandLineArgs());
            if (options.Nickname != null)
                Config.Set("DuelPlayerName0", options.Nickname);
            if (options.Deck != null)
                Config.Set("DeckInUse", options.Deck);
            if (options.Nickname != null || options.Deck != null)
                Config.Save();

            if (options.Join)
            {
                online.KF_OnlineGame(options.Nickname, options.Host, options.Port, options.Password);
                exitOnReturn = true;
            }
            else if (options.Deck != null)
            {
                editDeck.SwitchCondition(EditDeck.Condition.EditDeck);
                ShiftToServant(editDeck);
                exitOnReturn = true;
            }
            else if (options.Replay != null)
            {
                replay.KF_Replay(options.Replay);
                exitOnReturn = true;
            }
            else if (options.Puzzle != null)
            {
                puzzle.StartPuzzle(puzzlePath + options.Puzzle);
                exitOnReturn = true;
            }
        }

        public void InitializeForDataChange()
        {
            ZipHelper.Initialize();
            BanlistManager.Initialize();
            StringHelper.Initialize();
            CardsManager.Initialize();
        }

        private void InitializeAllManagers()
        {
            managers.Add(texture_);
            managers.Add(ui_);
            managers.Add(camera_);
            managers.Add(audio_);
            managers.Add(timeline_);
            managers.Add(background_);
            managers.Add(message_);

            foreach (Manager manager in managers)
                manager.Initialize();
        }
        private void InitializeAllServants()
        {
            servants.Add(setting);
            servants.Add(menu);
            servants.Add(solo);
            servants.Add(online);
            servants.Add(puzzle);
            servants.Add(replay);
            servants.Add(cutin);
            servants.Add(mate);
            servants.Add(selectDeck);
            servants.Add(appearance);
            servants.Add(character);
            servants.Add(ocgcore);
            servants.Add(room);
            servants.Add(editDeck);
            servants.Add(deckEditor);
            servants.Add(onlineDeckViewer);
            foreach (Servant servant in servants)
                servant.Initialize();
        }

        #endregion

        #region MonoBehaviors

        public const string tempFolder = "TempFolder/";
        public const string rootWindows64 = "StandaloneWindows64/";
        public const string rootAndroid = "Android/";
        public static string root = rootWindows64;

        void Awake()
        {
#if UNITY_ANDROID
            root = rootAndroid;
#endif
            instance = this;
            Running = true;
            exitOnReturn = false;
            Initialize();
        }

        public float timeScale
        {
            get 
            { 
                return m_timeScale;
            }
            set 
            {
                m_timeScale = value;
                Time.timeScale = value;
            }
        }
        float m_timeScale = 1f;
#if UNITY_EDITOR
        public float timeScaleForEdit = 1;
#endif

        void Update()
        {
            TcpHelper.PerFrameFunction();
            foreach (Manager manager in managers) 
                manager.PerFrameFunction();
            foreach (Servant servant in servants) 
                servant.PerFrameFunction();

#if UNITY_EDITOR
            timeScale = timeScaleForEdit;
#endif
        }

        public void UnloadUnusedAssets()
        {
            if (gc == null)
            {
                gc = UnloadUnusedAssetsAsync();
                StartCoroutine(gc);
            }
        }
        IEnumerator gc;
        IEnumerator UnloadUnusedAssetsAsync()
        {
            var unload = Resources.UnloadUnusedAssets();
            while (!unload.isDone)
                yield return null;
            gc = null;
        }

        public static bool noAccess = false;

        #endregion

        #region Tools
        public static int TimePassed()
        {
            return (int)(Time.time * 1000f);
        }

        public void ShiftToServant(Servant servant)
        {
            currentServant = servant;
            foreach (var ser in servants)
                if (ser != servant)
                    ser.Hide(servant.depth);
            foreach (var ser in servants)
                if (ser == servant)
                    ser.Show(depth);
            depth = servant.depth;
        }
        public void ShowSubServant(Servant servant)
        {
            if (currentSubServant == null)
            {
                servant.Show(0);
                currentSubServant = servant;
            }
            else
            {
                currentSubServant.Hide(servant.depth);
                servant.Show(currentSubServant.depth);
                currentSubServant = servant;
            }
        }

        public void ExitCurrentServant()
        {
            if (currentSubServant != null)
                currentSubServant.OnReturn();
            else
            {
                if(currentServant == null)
                {
                    foreach(var servant in  servants)
                        if (servant.showing)
                        {
                            currentServant = servant;
                            break;
                        }
                }
                if (currentServant == null)
                {
                    foreach (var servant in servants)
                        if (servant.showing)
                        {
                            currentServant = servant;
                            break;
                        }
                    if(currentServant == null)
                        currentServant = online;
                }
                currentServant.OnReturn();
            }
        }

        public void ExitDuel()
        {
            currentSubServant.OnReturn();
            currentServant.OnReturn();
        }

        #endregion

        #region System
        private void OnApplicationQuit()
        {
            Running = false;
            Config.Save();
            ClearCache();
            YgoServer.StopServer();
            ZipHelper.Dispose();
            try
            {
                TcpHelper.tcpClient.Close();
            }
            catch { }
            TcpHelper.tcpClient = null;
            MyCard.CloseAthleticWatchListWebSocket();
        }

        private void ClearCache()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject cacheDir = currentActivity.Call<AndroidJavaObject>("getCacheDir");
            string cachePath = cacheDir.Call<string>("getAbsolutePath");
            Tools.ClearDirectoryRecursively(new DirectoryInfo(cachePath));
#else
            if (Directory.Exists(tempFolder))
                Directory.Delete(tempFolder, true);
#endif
        }

        public static void GameQuit()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        #endregion
    }
}
