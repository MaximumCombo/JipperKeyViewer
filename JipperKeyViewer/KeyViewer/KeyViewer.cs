using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JipperKeyViewer.KeyViewer
{
    /// <summary>
    /// Core mod controller (partial class, split across multiple files) / Mod 核心控制器（分部类，分散在多个文件中）
    /// Manages lifecycle, settings, key overlay, rain effect, and input / 管理生命周期、设置、按键覆盖层、雨滴效果和输入
    /// </summary>
    public partial class KeyViewer : MonoBehaviour
    {
        /// <summary>Global settings instance / 全局设置实例</summary>
        public static KeyViewerSettings Settings;

        // Default color values used as initial settings and reset targets / 默认颜色值，用于初始设置和重置目标
        public static readonly Color Background = new(0.5607843f, 0.2352941f, 1, 0.1960784f);
        public static readonly Color BackgroundClicked = Color.white;
        public static readonly Color Outline = new(0.5529412f, 0.2431373f, 1);
        public static readonly Color OutlineClicked = Color.white;
        public static readonly Color Text = Color.white;
        public static readonly Color TextClicked = Color.black;
        public static readonly Color RainColor = new(0.5137255f, 0.1254902f, 0.858823538f);
        public static readonly Color RainColor2 = Color.white;
        public static readonly Color RainColor3 = Color.magenta;

        // Back-row key index mapping for each layout style / 每种布局样式的后排按键索引映射
        // Each byte array defines which keys go in the second row, in display order / 每个字节数组定义了第二排有哪些按键及其显示顺序
        public static readonly byte[] BackSequence8 = Array.Empty<byte>();
        public static readonly byte[] BackSequence10 = new byte[] { 8, 9 };
        public static readonly byte[] BackSequence12 = new byte[] { 9, 8, 10, 11 };
        public static readonly byte[] BackSequence14 = new byte[] { 9, 8, 10, 11, 12, 13 };
        public static readonly byte[] BackSequence16 = new byte[] { 12, 13, 9, 8, 10, 11, 14, 15 };
        public static readonly byte[] BackSequence20 = new byte[] { 12, 13, 9, 8, 10, 11, 14, 15, 17, 16, 18, 19 };

        /// <summary>Display names for main key layout selection grid / 主按键布局选择网格的显示名称</summary>
        static readonly string[] KeyLayoutNames = { "12K", "16K", "20K", "10K", "8K", "14K" };
        /// <summary>Display names for foot key layout selection grid / 脚键布局选择网格的显示名称</summary>
        static readonly string[] FootKeyLayoutNames = { "Off", "2K", "4K", "6K", "8K", "10K", "12K", "14K", "16K" };

        /// <summary>
        /// Static constructor: pre-compute AllKeyCodes (all non-Joystick keys) for input detection / 静态构造函数：预计算 AllKeyCodes（所有非摇杆按键），用于按键检测
        /// </summary>
        static KeyViewer()
        {
            var all = (KeyCode[])Enum.GetValues(typeof(KeyCode));
            AllKeyCodes = Array.FindAll(all, k => !k.ToString().StartsWith("Joystick"));
        }

        // --- Instance fields ---

        /// <summary>Root canvas GameObject for the key overlay / 按键覆盖层的根画布 GameObject</summary>
        GameObject KeyViewerObject;
        /// <summary>Child GameObject that applies the Size scale transform / 应用大小缩放的子 GameObject</summary>
        GameObject KeyViewerSizeObject;
        /// <summary>The overlay canvas (ScreenSpaceOverlay) / 覆盖层画布</summary>
        Canvas Canvas;
        /// <summary>All key instances (index 0-19 main, 20-35 foot) / 所有按键实例（0-19 主键，20-35 脚键）</summary>
        Key[] Keys;
        /// <summary>KPS display key / KPS 显示按键</summary>
        Key Kps;
        /// <summary>Last frame's KPS value for change detection / 上一帧的 KPS 值，用于变化检测</summary>
        int lastKps;
        /// <summary>Last frame's total count for change detection / 上一帧的总计数，用于变化检测</summary>
        int lastTotal;
        /// <summary>Total count display key / 总计数显示按键</summary>
        Key Total;
        /// <summary>Queue of press timestamps for KPS calculation / 按下时间戳队列，用于 KPS 计算</summary>
        Queue<long> PressTimes;
        /// <summary>Per-key press timestamp queues for per-key KPS / 每键按下时间戳队列，用于每键 KPS</summary>
        Queue<long>[] keyPressTimes;
        /// <summary>Last frame per-key KPS values for change detection / 上一帧每键 KPS 值，用于变化检测</summary>
        int[] lastPerKeyKps;
        /// <summary>High-resolution stopwatch for timing / 用于计时的高精度秒表</summary>
        Stopwatch Stopwatch;
        /// <summary>Timestamp of last frame for delta calculation / 上一帧的时间戳，用于增量计算</summary>
        /// <summary>Whether the key change section in settings is expanded / 设置中按键更改区域是否展开</summary>
        bool KeyChangeExpanded;
        /// <summary>Whether the ghost rain key section in settings is expanded / 设置中鬼键区域是否展开</summary>
        bool GhostRainChangeExpanded;
        /// <summary>Whether the text change section in settings is expanded / 设置中文本更改区域是否展开</summary>
        bool TextChangeExpanded;
        /// <summary>Per-color-section expanded state in settings / 设置中每个颜色区域的展开状态</summary>
        bool[] ColorExpanded;
        /// <summary>Currently selected key index for rebinding (-1 = none) / 当前为重新绑定选中的按键索引（-1 = 无）</summary>
        int SelectedKey = -1;
        /// <summary>Current rebind mode: 0=key, 1=text, 2=ghost key / 当前重绑定模式：0=按键，1=文本，2=鬼键</summary>
        int changeState;

        /// <summary>Path to the settings JSON file / 设置 JSON 文件路径</summary>
        static string ConfigPath
        {
            get
            {
                if (configPath == null)
                {
                    string modPath = Path.GetDirectoryName(Main.Mod?.Path);
                    configPath = Path.Combine(modPath ?? Application.persistentDataPath, "config", "settings.json");
                }
                return configPath;
            }
        }
        static string configPath;

        /// <summary>Cached background sprite from AssetBundle / 从 AssetBundle 缓存的背景精灵</summary>
        Sprite keyBackgroundSprite;
        /// <summary>Cached outline sprite from AssetBundle / 从 AssetBundle 缓存的轮廓精灵</summary>
        Sprite keyOutlineSprite;
        /// <summary>Cached ghost rain sprite (loaded from PNG file) / 从 PNG 文件缓存的鬼雨精灵</summary>
        Sprite ghostRainSprite;
        /// <summary>Singleton instance reference / 单例实例引用</summary>
        public static KeyViewer instance;
        /// <summary>Rain effect system (object-pooled, zero-GC on hot path) / 雨滴效果系统</summary>
        private RainSystem rainSystem;
        /// <summary>Font name → index lookup dictionary / 字体名称到索引的查找字典</summary>
        static Dictionary<string, int> fontNameIndex;
        /// <summary>All non-joystick KeyCodes, cached for input detection / 所有非摇杆按键代码缓存，用于按键检测</summary>
        private static readonly KeyCode[] AllKeyCodes;
        /// <summary>Cached current style to avoid redundant GetKeyCode calls / 缓存当前样式，避免重复调用 GetKeyCode</summary>
        private KeyviewerStyle cachedKeyStyle = (KeyviewerStyle)(-1);
        /// <summary>Cached main key array / 缓存的主按键数组</summary>
        private KeyCode[] cachedMainKeys;
        /// <summary>Cached current foot style / 缓存当前的脚键样式</summary>
        private FootKeyviewerStyle cachedFootStyle = (FootKeyviewerStyle)(-1);
        /// <summary>Cached foot key array / 缓存的脚键数组</summary>
        private KeyCode[] cachedFootKeys;
        /// <summary>Cached ghost key array / 缓存的鬼键数组</summary>
        private KeyCode[] cachedGhostKeys;
        /// <summary>Ghost key press state tracking / 鬼键按下状态跟踪</summary>
        private bool[] ghostKeyStates;
        /// <summary>MapleStory font loaded from AssetBundle / 从 AssetBundle 加载的 MapleStory 字体</summary>
        private TMP_FontAsset mapleFont;
        /// <summary>Cache of per-font shadow materials / 每个字体的阴影材质缓存</summary>
        private Dictionary<TMP_FontAsset, Material> shadowMaterials = new Dictionary<TMP_FontAsset, Material>();
        /// <summary>List of all available fonts (built-in + custom) / 所有可用字体列表（内置 + 自定义）</summary>
        static readonly List<FontEntry> fontList = new List<FontEntry>();
        /// <summary>Whether the font selection list is expanded in settings / 设置中字体选择列表是否展开</summary>
        bool fontListExpanded;
        bool fontStyleExpanded;
        /// <summary>Whether the overlay was enabled last frame (for toggle detection) / 上一帧覆盖层是否启用（用于开关检测）</summary>
        private bool wasEnabled;
        /// <summary>Whether the font has been restored after scene load / 场景加载后字体是否已恢复</summary>
        private bool fontRestored;

        // KeyBlocker integration: named pipe client for sending key allowlist to native blocker
        // All pipe operations are dispatched to the main thread via UnityEngineMainThread.Enqueue()
        // to avoid cross-thread access. Background threads only handle blocking Connect().
        private System.IO.Pipes.NamedPipeClientStream _keyBlockerPipe;
        private System.Diagnostics.Process _keyBlockerProcess;
        private Coroutine _keyBlockerHeartbeat;
        private volatile bool _keyBlockerCancelConnect;

        // ======================== Unity Lifecycle / Unity 生命周期 ========================

        /// <summary>
        /// Initialize the mod: load settings, i18n, resources / 初始化 Mod：加载设置、国际化、资源
        /// </summary>
        void Awake()
        {
            instance = this;
            LoadSettings();
            I18n.Load();
            I18n.Lang = Settings.Language;
            rainSystem = new RainSystem(Settings);
            TryLoadResources();
            rainSystem.GhostRainSprite = ghostRainSprite;
            wasEnabled = Settings.Enabled;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Initialize KeyBlocker pipe client
            InitializeKeyBlockerPipe();
        }

        /// <summary>
        /// Restore the user's font selection after scene load (once per scene) / 场景加载后恢复用户字体选择（每场景一次）
        /// </summary>
        void RestoreFontOnce()
        {
            if (fontNameIndex == null || fontRestored || string.IsNullOrEmpty(Settings.FontName)) return;
            if (fontNameIndex.TryGetValue(Settings.FontName, out int idx))
            {
                Settings.FontIndex = idx;
                UpdateAllFonts();
                SaveSettings();
            }
            fontRestored = true;
        }

        /// <summary>
        /// Called when the GameObject becomes active / GameObject 变为活跃时调用
        /// </summary>
        void OnEnable()
        {
            if (Settings.Enabled) EnableKeyViewer();
            else DisableKeyViewer();
            if (Settings.CustomPositionEnabled)
            {
                ResetKeyViewerPosition();
                ResetFootKeyViewerPosition();
            }
        }

        /// <summary>
        /// Called when the GameObject becomes inactive / GameObject 变为不活跃时调用
        /// </summary>
        void OnDisable()
        {
            DisableKeyViewer();
        }

        /// <summary>
        /// Called when the GameObject is destroyed / GameObject 被销毁时调用
        /// </summary>
        void OnDestroy()
        {
            SaveSettings();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // Cleanup KeyBlocker pipe
            CleanupKeyBlockerPipe();
            rainSystem?.ClearAll(Keys);
            foreach (var mat in shadowMaterials.Values)
                Destroy(mat);
            shadowMaterials.Clear();
        }

        /// <summary>
        /// Called when a new scene is loaded: save counts, clean up rain, re-link fallback fonts / 新场景加载时调用：保存计数、清理雨滴、重新链接后备字体
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SaveSettings();
            for (int i = fontList.Count - 1; i >= 0; i--)
                if (fontList[i].font == null) fontList.RemoveAt(i);
            if (fontList.Count == 0 || Settings.FontIndex >= fontList.Count)
                Settings.FontIndex = 0;
            fontRestored = false;
            LinkFallbackFonts();
            rainSystem.ClearActiveDrops(Keys);
        }

        /// <summary>
        /// Start is called after OnEnable; restore font selection / Start 在 OnEnable 之后调用；恢复字体选择
        /// </summary>
        void Start()
        {
            RestoreFontOnce();
        }

        /// <summary>
        /// Main update loop: input detection, KPS calculation, rain effect update / 主更新循环：按键检测、KPS 计算、雨滴效果更新
        /// </summary>
        void Update()
        {
            // Skip all processing when game window is not focused / 窗口未激活时跳过所有处理
            if (!Application.isFocused) return;

            bool enabled = Settings.Enabled;
            // Detect toggle change for enabled/disabled / 检测启用/禁用状态切换
            if (wasEnabled != enabled)
            {
                if (enabled)
                {
                    EnableKeyViewer();
                    if (Settings.CustomPositionEnabled)
                    {
                        ResetKeyViewerPosition();
                        ResetFootKeyViewerPosition();
                    }
                }
                else DisableKeyViewer();
                wasEnabled = enabled;
            }
            if (KeyViewerObject != null && enabled)
            {
                long now = Stopwatch.ElapsedMilliseconds;
                UnityEngineMainThread.Drain();               // Execute queued background-thread actions on main thread
                ProcessKeySelection();              // Handle key rebinding input / 处理按键重新绑定输入
                ProcessMainAndFootKeysInUpdate(now); // Detect key presses / 检测按键按下
                ProcessKpsInUpdate(now);            // Update KPS counter / 更新 KPS 计数器
                ProcessPerKeyKpsInUpdate(now);       // Update per-key KPS / 更新每键 KPS
                ProcessGhostKeysInUpdate();          // Process ghost key inputs / 处理鬼键输入
                if (Settings.EnableRainEffect) rainSystem.UpdateEffects(Keys); // Update rain drop positions / 更新雨滴位置
            }
        }

        // ======================== Config Management / 配置管理 ========================

        /// <summary>
        /// Load settings from JSON file, with v1→v2 migration for normalized coordinates / 从 JSON 文件加载设置，支持 v1→v2 归一化坐标迁移
        /// </summary>
        private void LoadSettings()
        {
            string directory = Path.GetDirectoryName(ConfigPath);
            if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            if (File.Exists(ConfigPath))
            {
                try
                {
                    string json = File.ReadAllText(ConfigPath);
                    Settings = JsonUtility.FromJson<KeyViewerSettings>(json);
                    if (Settings != null)
                    {
                        // v1→v2: migrate absolute pixel offsets to normalized 0-1 (X: 0=left 1=right, Y: 0=top 1=bottom)
                        // v1→v2：将绝对像素偏移迁移到归一化 0-1 坐标（X：0=左 1=右，Y：0=顶 1=底）
                        if (Settings.Version < 2)
                        {
                            const float refW = 1920f, refH = 1080f;
                            float Clamp01(float v) => v < 0 ? 0 : (v > 1 ? 1 : v);
                            Settings.MainKeyViewerPosition = new Vector2(
                                Clamp01(Settings.MainKeyViewerPosition.x / refW),
                                1f - Clamp01(Settings.MainKeyViewerPosition.y / refH));
                            Settings.FootKeyViewerPosition = new Vector2(
                                Clamp01(Settings.FootKeyViewerPosition.x / refW),
                                1f - Clamp01(Settings.FootKeyViewerPosition.y / refH));
                            Settings.Version = 2;
                            SaveSettings();
                        }
                        // Ensure arrays are initialized (prevents null refs from old saves) / 确保数组已初始化（防止旧存档的空引用）
                        Settings.key8Text = Settings.key8Text ?? new string[8];
                        Settings.key10Text = Settings.key10Text ?? new string[10];
                        Settings.key12Text = Settings.key12Text ?? new string[12];
                        Settings.key14Text = Settings.key14Text ?? new string[14];
                        Settings.key16Text = Settings.key16Text ?? new string[16];
                        Settings.key20Text = Settings.key20Text ?? new string[20];
                        Settings.footkey2Text = Settings.footkey2Text ?? new string[2];
                        Settings.footkey4Text = Settings.footkey4Text ?? new string[4];
                        Settings.footkey6Text = Settings.footkey6Text ?? new string[6];
                        Settings.footkey8Text = Settings.footkey8Text ?? new string[8];
                        Settings.footkey10Text = Settings.footkey10Text ?? new string[10];
                        Settings.footkey12Text = Settings.footkey12Text ?? new string[12];
                        Settings.footkey14Text = Settings.footkey14Text ?? new string[14];
                        Settings.footkey16Text = Settings.footkey16Text ?? new string[16];
                        Settings.Count = Settings.Count ?? new int[36];
                        if (Settings.PerKeyBackground == null || Settings.PerKeyBackground.Length != 38)
                            Settings.InitPerKeyColors();
                    }
                    else
                    {
                        Main.Mod.Logger.Error("Failed to parse settings file (empty or corrupt), creating new settings");
                        Settings = new KeyViewerSettings();
                        SaveSettings();
                    }
                }
                catch (Exception e)
                {
                    Main.Mod.Logger.Error($"Failed to load settings: {e.Message}");
                    Settings = new KeyViewerSettings();
                }
            }
            else
            {
                Settings = new KeyViewerSettings();
                SaveSettings();
            }
        }

        // Key code mapping from Unity KeyCode to Windows Virtual-Key codes
        private static readonly Dictionary<KeyCode, ushort> KeyCodeToVK = new Dictionary<KeyCode, ushort>
        {
            // Letters
            [KeyCode.A] = 0x41, [KeyCode.B] = 0x42, [KeyCode.C] = 0x43, [KeyCode.D] = 0x44,
            [KeyCode.E] = 0x45, [KeyCode.F] = 0x46, [KeyCode.G] = 0x47, [KeyCode.H] = 0x48,
            [KeyCode.I] = 0x49, [KeyCode.J] = 0x4A, [KeyCode.K] = 0x4B, [KeyCode.L] = 0x4C,
            [KeyCode.M] = 0x4D, [KeyCode.N] = 0x4E, [KeyCode.O] = 0x4F, [KeyCode.P] = 0x50,
            [KeyCode.Q] = 0x51, [KeyCode.R] = 0x52, [KeyCode.S] = 0x53, [KeyCode.T] = 0x54,
            [KeyCode.U] = 0x55, [KeyCode.V] = 0x56, [KeyCode.W] = 0x57, [KeyCode.X] = 0x58,
            [KeyCode.Y] = 0x59, [KeyCode.Z] = 0x5A,

            // Numbers
            [KeyCode.Alpha0] = 0x30, [KeyCode.Alpha1] = 0x31, [KeyCode.Alpha2] = 0x32,
            [KeyCode.Alpha3] = 0x33, [KeyCode.Alpha4] = 0x34, [KeyCode.Alpha5] = 0x35,
            [KeyCode.Alpha6] = 0x36, [KeyCode.Alpha7] = 0x37, [KeyCode.Alpha8] = 0x38,
            [KeyCode.Alpha9] = 0x39,

            // Numpad
            [KeyCode.Keypad0] = 0x60, [KeyCode.Keypad1] = 0x61, [KeyCode.Keypad2] = 0x62,
            [KeyCode.Keypad3] = 0x63, [KeyCode.Keypad4] = 0x64, [KeyCode.Keypad5] = 0x65,
            [KeyCode.Keypad6] = 0x66, [KeyCode.Keypad7] = 0x67, [KeyCode.Keypad8] = 0x68,
            [KeyCode.Keypad9] = 0x69, [KeyCode.KeypadPeriod] = 0x6E, [KeyCode.KeypadDivide] = 0x6F,
            [KeyCode.KeypadMultiply] = 0x6A, [KeyCode.KeypadMinus] = 0x6D, [KeyCode.KeypadPlus] = 0x6B,
            [KeyCode.KeypadEnter] = 0x0D,

            // Function keys
            [KeyCode.F1] = 0x70, [KeyCode.F2] = 0x71, [KeyCode.F3] = 0x72, [KeyCode.F4] = 0x73,
            [KeyCode.F5] = 0x74, [KeyCode.F6] = 0x75, [KeyCode.F7] = 0x76, [KeyCode.F8] = 0x77,
            [KeyCode.F9] = 0x78, [KeyCode.F10] = 0x79, [KeyCode.F11] = 0x7A, [KeyCode.F12] = 0x7B,

            // Navigation
            [KeyCode.UpArrow] = 0x26, [KeyCode.DownArrow] = 0x28,
            [KeyCode.LeftArrow] = 0x25, [KeyCode.RightArrow] = 0x27,
            [KeyCode.Home] = 0x24, [KeyCode.End] = 0x23,
            [KeyCode.PageUp] = 0x21, [KeyCode.PageDown] = 0x22,

            // Editing
            [KeyCode.Insert] = 0x2D, [KeyCode.Delete] = 0x2E,

            // Modifiers
            [KeyCode.LeftShift] = 0xA0, [KeyCode.RightShift] = 0xA1,
            [KeyCode.LeftControl] = 0xA2, [KeyCode.RightControl] = 0xA3,
            [KeyCode.LeftAlt] = 0xA4, [KeyCode.RightAlt] = 0xA5,
            [KeyCode.LeftCommand] = 0x5B, [KeyCode.RightCommand] = 0x5C,

            // Special
            [KeyCode.Tab] = 0x09, [KeyCode.Space] = 0x20, [KeyCode.Return] = 0x0D,
            [KeyCode.Escape] = 0x1B, [KeyCode.Backspace] = 0x08,
            [KeyCode.CapsLock] = 0x14, [KeyCode.ScrollLock] = 0x91,
            [KeyCode.Pause] = 0x13,

            // Punctuation
            [KeyCode.Semicolon] = 0xBA, [KeyCode.Equals] = 0xBB, [KeyCode.Comma] = 0xBC,
            [KeyCode.Minus] = 0xBD, [KeyCode.Period] = 0xBE, [KeyCode.Slash] = 0xBF,
            [KeyCode.BackQuote] = 0xC0, [KeyCode.LeftBracket] = 0xDB,
            [KeyCode.Backslash] = 0xDC, [KeyCode.RightBracket] = 0xDD,
            [KeyCode.Quote] = 0xDE
        };

        /// <summary>
        /// Initialize the named pipe client to communicate with KeyBlocker native process.
        /// Runs connection attempts on a background thread to avoid blocking Unity's main thread.
        /// After connection, all pipe operations are dispatched to the main thread.
        /// </summary>
        private void InitializeKeyBlockerPipe()
        {
            if (!Settings.EnableKeyBlocker) return;
            _keyBlockerCancelConnect = false;
            System.Threading.ThreadPool.QueueUserWorkItem(_ => ConnectKeyBlockerPipe());
        }

        /// <summary>
        /// Background connection logic: try existing process first, then attempt to start KeyBlocker.exe.
        /// Only handles blocking Connect() calls. All pipe I/O is dispatched to the main thread.
        /// </summary>
        private void ConnectKeyBlockerPipe()
        {
            // Try to connect to existing KeyBlocker process
            if (TryConnectPipe(1000))
            {
                UnityEngineMainThread.Enqueue(OnPipeConnected);
                Main.Mod.Logger.Log("KeyViewer: Connected to existing KeyBlocker");
                return;
            }

            // No existing process found, try to start KeyBlocker.exe
            try
            {
                string modPath = Path.GetDirectoryName(Main.Mod?.Path);
                string keyBlockerPath = Path.Combine(modPath ?? ".", "KeyBlocker", "KeyBlocker.exe");

                if (!File.Exists(keyBlockerPath))
                {
                    Main.Mod.Logger.Error($"KeyViewer: KeyBlocker.exe not found at {keyBlockerPath}");
                    return;
                }

                Main.Mod.Logger.Log($"KeyViewer: Starting KeyBlocker.exe from {keyBlockerPath}");
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = keyBlockerPath,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    }
                };
                process.Start();
                UnityEngineMainThread.Enqueue(() => { if (!_keyBlockerCancelConnect) _keyBlockerProcess = process; });

                // Retry connection with backoff — process needs time to create its pipe
                for (int attempt = 0; attempt < 5; attempt++)
                {
                    System.Threading.Thread.Sleep(300 * (attempt + 1));
                    if (TryConnectPipe(2000))
                    {
                        UnityEngineMainThread.Enqueue(OnPipeConnected);
                        Main.Mod.Logger.Log("KeyViewer: Connected to KeyBlocker (started process)");
                        return;
                    }
                }

                Main.Mod.Logger.Error("KeyViewer: Failed to connect to KeyBlocker after starting process");
                UnityEngineMainThread.Enqueue(CleanupKeyBlockerProcess);
            }
            catch (Exception e)
            {
                Main.Mod.Logger.Error($"KeyViewer: Failed to start KeyBlocker.exe: {e.Message}");
                UnityEngineMainThread.Enqueue(CleanupKeyBlockerProcess);
            }
        }

        /// <summary>
        /// Called on the main thread after a successful pipe connection.
        /// Starts the heartbeat coroutine and sends the initial allowlist.
        /// </summary>
        private void OnPipeConnected()
        {
            if (_keyBlockerCancelConnect || _keyBlockerPipe == null || !_keyBlockerPipe.IsConnected) return;
            if (_keyBlockerHeartbeat == null)
                _keyBlockerHeartbeat = StartCoroutine(KeyBlockerHeartbeatCoroutine());
            SendCurrentKeyAllowlist();
        }

        /// <summary>
        /// Attempt a single pipe connection with the given timeout.
        /// Creates a new pipe and connects it. On failure, disposes the pipe.
        /// Only called from background threads. Does NOT touch _keyBlockerPipe.
        /// </summary>
        private bool TryConnectPipe(int timeoutMs)
        {
            var pipe = new System.IO.Pipes.NamedPipeClientStream(
                ".", "JipperKeyBlocker",
                System.IO.Pipes.PipeDirection.InOut,
                System.IO.Pipes.PipeOptions.None);

            try
            {
                pipe.Connect(timeoutMs);
                if (pipe.IsConnected)
                {
                    // Assign on the main thread to avoid race with CleanupKeyBlockerPipe
                    UnityEngineMainThread.Enqueue(() =>
                    {
                        if (!_keyBlockerCancelConnect)
                        {
                            SafeDisposePipe();
                            _keyBlockerPipe = pipe;
                        }
                        else
                        {
                            pipe.Dispose();
                        }
                    });
                    return true;
                }
            }
            catch (TimeoutException) { }
            catch (System.IO.IOException) { }
            catch (Exception e)
            {
                Main.Mod.Logger.Error($"KeyViewer: Pipe connect error: {e.Message}");
            }

            pipe.Dispose();
            return false;
        }

        /// <summary>Thread-safe queue for scheduling actions on Unity's main thread</summary>
        private static class UnityEngineMainThread
        {
            private static readonly Queue<Action> _queue = new Queue<Action>();

            public static void Enqueue(Action action)
            {
                lock (_queue) { _queue.Enqueue(action); }
            }

            public static void Drain()
            {
                lock (_queue)
                {
                    while (_queue.Count > 0)
                        _queue.Dequeue()?.Invoke();
                }
            }
        }

        /// <summary>
        /// Periodically sends HEARTBEAT and re-sends allowlist (in case layout changed).
        /// If the pipe disconnects, schedules reconnection on a background thread.
        /// Always runs on the main thread.
        /// </summary>
        private IEnumerator KeyBlockerHeartbeatCoroutine()
        {
            while (Settings.EnableKeyBlocker && !_keyBlockerCancelConnect)
            {
                yield return new WaitForSeconds(3f);

                if (_keyBlockerCancelConnect) break;
                if (_keyBlockerPipe == null || !_keyBlockerPipe.IsConnected)
                {
                    // Schedule reconnection on background thread (non-blocking)
                    System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                    {
                        if (TryConnectPipe(1000))
                            UnityEngineMainThread.Enqueue(OnPipeConnected);
                    });
                    continue;
                }

                try
                {
                    byte[] heartbeat = System.Text.Encoding.UTF8.GetBytes("HEARTBEAT\n");
                    _keyBlockerPipe.Write(heartbeat, 0, heartbeat.Length);
                    _keyBlockerPipe.Flush();
                    SendCurrentKeyAllowlist();
                }
                catch (Exception)
                {
                    SafeDisposePipe();
                }
            }
            _keyBlockerHeartbeat = null;
        }

        /// <summary>
        /// Send the current key layout's VK codes to KeyBlocker as allowlist.
        /// Always called on the main thread.
        /// </summary>
        private void SendCurrentKeyAllowlist()
        {
            if (_keyBlockerPipe == null || !_keyBlockerPipe.IsConnected)
                return;

            try
            {
                var vkCodes = new HashSet<ushort>();

                foreach (KeyCode kc in GetKeyCode())
                    if (KeyCodeToVK.TryGetValue(kc, out ushort vk)) vkCodes.Add(vk);
                foreach (KeyCode kc in GetFootKeyCode())
                    if (KeyCodeToVK.TryGetValue(kc, out ushort vk)) vkCodes.Add(vk);
                foreach (KeyCode kc in GetGhostKeyCode())
                    if (KeyCodeToVK.TryGetValue(kc, out ushort vk)) vkCodes.Add(vk);

                // System keys that must always be allowed
                vkCodes.Add(0x1B); // Escape
                vkCodes.Add(0x09); // Tab
                vkCodes.Add(0x0D); // Enter / Return
                vkCodes.Add(0x20); // Space
                vkCodes.Add(0x08); // Backspace

                // Modifier keys
                vkCodes.Add(0xA0); // Left Shift
                vkCodes.Add(0xA1); // Right Shift
                vkCodes.Add(0xA2); // Left Control
                vkCodes.Add(0xA3); // Right Control
                vkCodes.Add(0xA4); // Left Alt
                vkCodes.Add(0xA5); // Right Alt

                string hexList = string.Join(",", vkCodes.Select(vk => vk.ToString("X2")));
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes($"ALLOW {hexList}\n");
                _keyBlockerPipe.Write(bytes, 0, bytes.Length);
                _keyBlockerPipe.Flush();

                Main.Mod.Logger.Log($"KeyViewer: Sent allowlist with {vkCodes.Count} keys to KeyBlocker");
            }
            catch (Exception e)
            {
                Main.Mod.Logger.Error($"KeyViewer: Failed to send allowlist: {e.Message}");
                SafeDisposePipe();
            }
        }

        /// <summary>
        /// Dispose the pipe object regardless of connection state.
        /// Always called on the main thread.
        /// </summary>
        private void SafeDisposePipe()
        {
            if (_keyBlockerPipe != null)
            {
                try { _keyBlockerPipe.Dispose(); } catch { }
                _keyBlockerPipe = null;
            }
        }

        /// <summary>
        /// Cleanup the named pipe client and stop the heartbeat coroutine.
        /// Sends DISABLE command to C++ to immediately stop key interception.
        /// Always called on the main thread.
        /// </summary>
        private void CleanupKeyBlockerPipe()
        {
            _keyBlockerCancelConnect = true;
            if (_keyBlockerHeartbeat != null)
            {
                StopCoroutine(_keyBlockerHeartbeat);
                _keyBlockerHeartbeat = null;
            }
            // Notify C++ side to stop blocking immediately
            if (_keyBlockerPipe != null && _keyBlockerPipe.IsConnected)
            {
                try
                {
                    byte[] disable = System.Text.Encoding.UTF8.GetBytes("DISABLE\n");
                    _keyBlockerPipe.Write(disable, 0, disable.Length);
                    _keyBlockerPipe.Flush();
                }
                catch { }
            }
            SafeDisposePipe();
            CleanupKeyBlockerProcess();
        }

        /// <summary>
        /// Force-terminate the KeyBlocker process we started.
        /// Always called on the main thread.
        /// </summary>
        private void CleanupKeyBlockerProcess()
        {
            if (_keyBlockerProcess == null) return;
            try
            {
                if (!_keyBlockerProcess.HasExited)
                {
                    _keyBlockerProcess.Kill();
                    _keyBlockerProcess.WaitForExit(1000);
                }
                _keyBlockerProcess.Dispose();
            }
            catch (Exception e)
            {
                Main.Mod.Logger.Error($"KeyViewer: Error cleaning up KeyBlocker process: {e.Message}");
            }
            _keyBlockerProcess = null;
        }

        /// <summary>
        /// Save current settings to JSON file / 将当前设置保存到 JSON 文件
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                string directory = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
                string json = JsonUtility.ToJson(Settings, true);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception e)
            {
                Main.Mod.Logger.Error($"Failed to save settings: {e.Message}");
            }
        }
    }
}