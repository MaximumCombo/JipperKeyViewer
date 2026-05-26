// Key detection and rebinding logic / 按键检测和重新绑定逻辑
// Handles listening for new key presses during rebinding and converting KeyCodes to display strings / 处理重绑定期间监听新按键，以及将 KeyCode 转换为显示字符串

using System.Collections.Generic;
using UnityEngine;

namespace JipperKeyViewer.KeyViewer
{
    /// <summary>
    /// Input processing: async key detection and display string conversion
    /// Uses AsyncInputManager (GetAsyncKeyState background thread) for frame-rate independent input
    /// </summary>
    public partial class KeyViewer : MonoBehaviour
    {
        /// <summary>Async input manager — polls keys on background thread at 1000Hz</summary>
        private AsyncInputManager _asyncInput;
        /// <summary>Reusable buffer for draining async key events each frame</summary>
        private readonly List<AsyncInputManager.KeyEvent> _inputEvents = new List<AsyncInputManager.KeyEvent>(64);
        /// <summary>VK code → key index (0-19 main, 20-35 foot) lookup for O(1) event routing</summary>
        private Dictionary<int, int> _vkToKeyIndex;
        /// <summary>Virtual key codes currently tracked by the async input manager</summary>
        private int[] _trackedVKs;

        /// <summary>
        /// Listen for a key press when the user is rebinding a key / 当用户正在重绑定时监听按键按下
        /// Waits for any key down, then assigns it to the SelectedKey / 等待任意键按下，然后分配给 SelectedKey
        /// </summary>
        private void ProcessKeySelection()
        {
            if (SelectedKey == -1 || changeState == 1 || !Application.isFocused) return;
            if (!Input.anyKeyDown) return;

            foreach (KeyCode keyCode in AllKeyCodes)
            {
                if (Input.GetKeyDown(keyCode))
                {
                    SetupKey(keyCode);
                    return;
                }
            }
        }

        /// <summary>
        /// Assign a key code to the selected slot and update the display / 将按键代码分配给选中的槽位并更新显示
        /// </summary>
        private void SetupKey(KeyCode keyCode)
        {
            if (changeState == 2)
            {
                KeyCode[] ghostKeyCodes = GetGhostKeyCode();
                if (SelectedKey < ghostKeyCodes.Length)
                {
                    ghostKeyCodes[SelectedKey] = keyCode;
                    if (SelectedKey < ghostKeyStates.Length)
                        ghostKeyStates[SelectedKey] = false;
                }
                SelectedKey = -1;
                SaveSettings();
                return;
            }
            KeyCode[] keyCodes = GetKeyCode();
            KeyCode[] footKeyCodes = GetFootKeyCode();
            string[] keyTexts = GetKeyText();
            if (SelectedKey < 20)
            {
                keyCodes[SelectedKey] = keyCode;
            }
            else if (footKeyCodes != null && SelectedKey - 20 < footKeyCodes.Length)
            {
                footKeyCodes[SelectedKey - 20] = keyCode;
            }
            else
            {
                SelectedKey = -1;
                return;
            }
            if (Keys != null && SelectedKey < Keys.Length && Keys[SelectedKey] != null)
            {
                string displayText;
                if (SelectedKey < 20 && !string.IsNullOrEmpty(keyTexts[SelectedKey]))
                    displayText = keyTexts[SelectedKey];
                else if (SelectedKey >= 20)
                {
                    string[] footTexts = GetFootKeyText();
                    int footIndex = SelectedKey - 20;
                    displayText = footTexts != null && footIndex < footTexts.Length && !string.IsNullOrEmpty(footTexts[footIndex])
                        ? footTexts[footIndex] : KeyToString(keyCode);
                }
                else
                    displayText = KeyToString(keyCode);
                Keys[SelectedKey].text.text = displayText;
            }
            SelectedKey = -1;
            SaveSettings();
        }

        static readonly Dictionary<KeyCode, string> KeyDisplayNames = new Dictionary<KeyCode, string>();

        /// <summary>
        /// Convert a Unity KeyCode to a short display-friendly string / 将 Unity KeyCode 转换为简短友好的显示字符串
        /// Uses a pre-built dictionary to avoid per-call allocations / 使用预建字典避免每次调用产生分配
        /// </summary>
        public static string KeyToString(KeyCode keyCode)
        {
            if (KeyDisplayNames.Count == 0 && AllKeyCodes != null)
                BuildKeyDisplayNames();
            return KeyDisplayNames.TryGetValue(keyCode, out var name) ? name : keyCode.ToString();
        }

        static void BuildKeyDisplayNames()
        {
            foreach (KeyCode k in AllKeyCodes)
            {
                string s = k.ToString();
                if (s.StartsWith("Alpha")) s = s.Substring(5);
                else if (s.StartsWith("Keypad")) s = s.Substring(6);
                else if (s.StartsWith("Left")) s = 'L' + s.Substring(4);
                else if (s.StartsWith("Right")) s = 'R' + s.Substring(5);
                else if (s.StartsWith("Mouse")) s = "M" + s.Substring(5);
                if (s.EndsWith("Shift")) s = s.Substring(0, s.Length - 5) + "\u21E7";
                else if (s.EndsWith("Control")) s = s.Substring(0, s.Length - 7) + "Ctrl";
                s = s switch
                {
                    "Plus" => "+", "Minus" => "-", "Multiply" => "*", "Divide" => "/",
                    "Enter" => "\u21B5", "Equals" => "=", "Period" => ".", "Return" => "\u21B5",
                    "None" => " ", "Tab" => "\u21E5", "Backslash" => "\\", "Backspace" => "Back",
                    "Slash" => "/", "LBracket" => "[", "RBracket" => "]", "Semicolon" => ";",
                    "Comma" => ",", "Quote" => "'", "UpArrow" => "\u2191", "DownArrow" => "\u2193",
                    "LArrow" => "\u2190", "RArrow" => "\u2192", "Space" => "\u2423",
                    "BackQuote" => "`", "PageDown" => "Pg\u2193", "PageUp" => "Pg\u2191",
                    "CapsLock" => "\u21EA", "Insert" => "Ins",
                    _ => s
                };
                KeyDisplayNames[k] = s;
            }
        }

        /// <summary>Calculate IMGUI text field width based on content length / 根据内容长度计算 IMGUI 文本框宽度</summary>
        private static GUILayoutOption FloatFieldWidth(string text) => GUILayout.Width(Mathf.Max(30, text.Length * 9));

        // ======================== Input Processing (hot path) / 输入处理（热路径） ========================

        /// <summary>
        /// Initialize or reinitialize the async input manager for the current layout.
        /// Builds VK code → key index mapping and starts the background polling thread.
        /// </summary>
        private void InitAsyncInput()
        {
            _asyncInput?.Dispose();
            _asyncInput = null;
            _vkToKeyIndex = null;

            var mainKeys = GetKeyCode();
            var footKeys = GetFootKeyCode();
            int mainLen = mainKeys?.Length ?? 0;
            int footLen = footKeys?.Length ?? 0;

            var vks = new List<int>(mainLen + footLen);
            _vkToKeyIndex = new Dictionary<int, int>(mainLen + footLen);

            if (mainKeys != null)
            {
                for (int i = 0; i < mainKeys.Length; i++)
                {
                    if (AsyncInputManager.KeyCodeToVK.TryGetValue(mainKeys[i], out int vk))
                    {
                        vks.Add(vk);
                        _vkToKeyIndex[vk] = i;
                    }
                }
            }
            if (footKeys != null)
            {
                for (int i = 0; i < footKeys.Length; i++)
                {
                    if (AsyncInputManager.KeyCodeToVK.TryGetValue(footKeys[i], out int vk))
                    {
                        vks.Add(vk);
                        _vkToKeyIndex[vk] = i + 20;
                    }
                }
            }

            _trackedVKs = vks.ToArray();
            _asyncInput = new AsyncInputManager(_trackedVKs, pollHz: 1000);
            _asyncInput.Start();
        }

        /// <summary>
        /// Check each main key and foot key for state changes using async events.
        /// Events carry precise timestamps from the background thread, independent of frame rate.
        /// </summary>
        private void ProcessMainAndFootKeysInUpdate(long elapsedMilliseconds)
        {
            if (cachedKeyStyle != Settings.KeyViewerStyle || cachedFootStyle != Settings.FootKeyViewerStyle)
            {
                cachedKeyStyle = Settings.KeyViewerStyle;
                cachedFootStyle = Settings.FootKeyViewerStyle;
                cachedGhostKeys = GetGhostKeyCode();
                ghostKeyStates = new bool[cachedGhostKeys.Length];
                InitAsyncInput();
            }
            if (cachedGhostKeys == null)
            {
                cachedGhostKeys = GetGhostKeyCode();
                ghostKeyStates = new bool[cachedGhostKeys.Length];
            }
            if (_asyncInput == null) InitAsyncInput();

            // Drain events from the background thread
            _inputEvents.Clear();
            _asyncInput.DrainEvents(_inputEvents);

            int[] countArr = Settings.Count;
            bool rainEnabled = Settings.EnableRainEffect;

            for (int e = 0; e < _inputEvents.Count; e++)
            {
                var evt = _inputEvents[e];
                if (_vkToKeyIndex == null || !_vkToKeyIndex.TryGetValue(evt.VKey, out int idx))
                    continue;
                if (idx >= Keys.Length) continue;
                Key key = Keys[idx];
                if (key == null) continue;

                bool current = evt.Pressed;
                if (current == key.isPressed) continue;

                UpdateKeyColors(idx, current);
                key.isPressed = current;

                if (current)
                {
                    countArr[idx]++;
                    Settings.TotalCount++;
                    if (key.value != null && !Settings.EnablePerKeyKps)
                        key.value.text = FormatCount(countArr[idx]);
                    // Use the precise timestamp from the background thread
                    PressTimes.Enqueue(evt.TimestampMs);
                    if (keyPressTimes != null && idx < keyPressTimes.Length)
                    {
                        if (keyPressTimes[idx] == null) keyPressTimes[idx] = new Queue<long>();
                        keyPressTimes[idx].Enqueue(evt.TimestampMs);
                    }
                    if (rainEnabled) rainSystem.TriggerRainEffect(idx, key);
                }
                else
                {
                    if (rainEnabled) rainSystem.ReleaseRainEffect(idx, key);
                }
            }

            if (Total != null && Total.value != null && lastTotal != Settings.TotalCount)
            {
                lastTotal = Settings.TotalCount;
                Total.value.text = FormatCount(lastTotal);
            }
        }

        /// <summary>
        /// Calculate KPS by removing presses older than 1 second / 通过移除超过 1 秒的按下记录计算 KPS
        /// </summary>
        private void ProcessKpsInUpdate(long elapsedMilliseconds)
        {
            if (PressTimes == null) return;
            while (PressTimes.Count > 0 && elapsedMilliseconds - PressTimes.Peek() > 1000)
                PressTimes.Dequeue();
            int currentKps = PressTimes.Count;
            if (lastKps != currentKps)
            {
                lastKps = currentKps;
                if (Kps != null && Kps.value != null) Kps.value.text = currentKps.ToString();
            }
        }

        /// <summary>
        /// Per-key KPS: clean timestamps older than 1s and update display / 每键 KPS：清理超过 1 秒的时间戳并更新显示
        /// </summary>
        private void ProcessPerKeyKpsInUpdate(long elapsedMilliseconds)
        {
            if (!Settings.EnablePerKeyKps || keyPressTimes == null || Keys == null) return;
            for (int i = 0; i < Keys.Length && i < keyPressTimes.Length; i++)
            {
                var q = keyPressTimes[i];
                if (q == null) continue;
                while (q.Count > 0 && elapsedMilliseconds - q.Peek() > 1000)
                    q.Dequeue();
                int kps = q.Count;
                if (lastPerKeyKps != null && i < lastPerKeyKps.Length && lastPerKeyKps[i] != kps)
                {
                    lastPerKeyKps[i] = kps;
                    if (Keys[i] != null && Keys[i].value != null)
                        Keys[i].value.text = kps.ToString();
                }
            }
        }

        /// <summary>
        /// Process ghost key inputs using async events.
        /// Ghost keys only trigger rain, no display or count.
        /// </summary>
        private void ProcessGhostKeysInUpdate()
        {
            if (cachedGhostKeys == null) return;
            bool rainEnabled = Settings.EnableRainEffect;
            bool ghostRainEnabled = Settings.EnableGhostRain;
            if (!rainEnabled || !ghostRainEnabled) return;

            // Check ghost keys using GetAsyncKeyState (instant, not frame-dependent)
            KeyCode[] ghosts = cachedGhostKeys;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] == KeyCode.None) continue;
                if (!AsyncInputManager.KeyCodeToVK.TryGetValue(ghosts[i], out int vk)) continue;

                bool current = AsyncInputManager.IsKeyDown(vk);
                if (current != ghostKeyStates[i])
                {
                    ghostKeyStates[i] = current;
                    if (current)
                        rainSystem.TriggerGhostRain(i, Keys[i]);
                    else
                        rainSystem.ReleaseGhostRain(i, Keys[i]);
                }
            }
        }

        /// <summary>
        /// Update key visual colors based on press state / 根据按下状态更新按键视觉颜色
        /// </summary>
        private void UpdateKeyColors(int i, bool pressed)
        {
            if (Keys == null || i >= Keys.Length) return;
            Key key = Keys[i];
            if (key == null) return;
            if (Settings.EnablePerKeyColors && i < 36)
            {
                key.background.color = pressed ? Settings.PerKeyBackgroundClicked[i] : Settings.PerKeyBackground[i];
                key.outline.color = pressed ? Settings.PerKeyOutlineClicked[i] : Settings.PerKeyOutline[i];
                key.text.color = pressed ? Settings.PerKeyTextClicked[i] : Settings.PerKeyText[i];
            }
            else
            {
                key.background.color = pressed ? Settings.BackgroundClicked : Settings.Background;
                key.outline.color = pressed ? Settings.OutlineClicked : Settings.Outline;
                key.text.color = pressed ? Settings.TextClicked : Settings.Text;
            }
            if (key.value != null) key.value.color = key.text.color;
        }
    }
}
