// Key detection and rebinding logic / 按键检测和重新绑定逻辑
// Handles listening for new key presses during rebinding and converting KeyCodes to display strings / 处理重绑定期间监听新按键，以及将 KeyCode 转换为显示字符串

using System.Collections.Generic;
using UnityEngine;

namespace JipperKeyViewer.KeyViewer
{
    /// <summary>Pre-allocated char buffer for TMP text updates (no per-call ToString allocation) / 预分配的字符缓冲区，用于 TMP 文本更新（无每调用 ToString 分配）</summary>
    internal static class NumBuffer
    {
        private static readonly char[] Buffer = new char[32];

        /// <summary>Write integer into Buffer, return char segment via out params / 将整数写入 Buffer，通过 out 参数返回字符片段</summary>
        public static void Format(int count, bool thousands, out char[] buf, out int offset, out int length)
        {
            int pos = Buffer.Length;
            if (count == 0) { Buffer[--pos] = '0'; buf = Buffer; offset = pos; length = Buffer.Length - pos; return; }
            long val = count;
            if (val < 0) val = -val;
            int seg = 0;
            while (val > 0)
            {
                if (thousands && seg == 3) { Buffer[--pos] = ','; seg = 0; }
                Buffer[--pos] = (char)('0' + val % 10);
                val /= 10;
                seg++;
            }
            if (count < 0) Buffer[--pos] = '-';
            buf = Buffer; offset = pos; length = Buffer.Length - pos;
        }
    }

    /// <summary>
    /// Input processing: key rebinding and display string conversion / 输入处理：按键重绑定和显示字符串转换
    /// </summary>
    public partial class KeyViewer : MonoBehaviour
    {
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
        /// Check each main key and foot key for state changes (press/release) every frame / 每帧检查每个主键和脚键的状态变化（按下/释放）
        /// </summary>
        private void ProcessMainAndFootKeysInUpdate(long elapsedMilliseconds)
        {
            if (cachedKeyStyle != Settings.KeyViewerStyle)
            {
                cachedMainKeys = GetKeyCode();
                cachedGhostKeys = GetGhostKeyCode();
                cachedKeyStyle = Settings.KeyViewerStyle;
                ghostKeyStates = new bool[cachedGhostKeys.Length];
            }
            else if (cachedGhostKeys == null)
            {
                cachedGhostKeys = GetGhostKeyCode();
                ghostKeyStates = new bool[cachedGhostKeys.Length];
            }
            if (cachedFootStyle != Settings.FootKeyViewerStyle)
            {
                cachedFootKeys = GetFootKeyCode();
                cachedFootStyle = Settings.FootKeyViewerStyle;
            }
            ProcessKeyGroup(cachedMainKeys, 0, elapsedMilliseconds);
            if (cachedFootKeys != null)
                ProcessKeyGroup(cachedFootKeys, 20, elapsedMilliseconds);
            if (Total != null && Total.value != null && lastTotal != Settings.TotalCount)
            {
                lastTotal = Settings.TotalCount;
                NumBuffer.Format(lastTotal, Settings.EnableCountFormatting, out var buf, out int off, out int len);
                Total.value.SetText(buf, off, len);
            }
        }

        /// <summary>
        /// Process a group of keys for input state changes / 处理一组按键的输入状态变化
        /// Local-caches Settings references for hot-path performance / 局部缓存 Settings 引用以优化热路径性能
        /// </summary>
        private void ProcessKeyGroup(KeyCode[] keyCodes, int baseIndex, long elapsedMs)
        {
            int[] countArr = Settings.Count;
            bool rainEnabled = Settings.EnableRainEffect;
            for (int i = 0; i < keyCodes.Length; i++)
            {
                int idx = baseIndex + i;
                if (idx >= Keys.Length) continue;
                Key key = Keys[idx];
                if (key == null) continue;
                bool current = Input.GetKey(keyCodes[i]);
                if (current != key.isPressed)
                {
                    UpdateKeyColors(idx, current);
                    key.isPressed = current;
                    if (current)
                    {
                        countArr[idx]++;
                        Settings.TotalCount++;
                        if (key.value != null && !Settings.EnablePerKeyKps)
                        {
                            NumBuffer.Format(countArr[idx], Settings.EnableCountFormatting, out var buf, out int off, out int len);
                            key.value.SetText(buf, off, len);
                        }
                        PressTimes.Enqueue(elapsedMs);
                        if (keyPressTimes != null && idx < keyPressTimes.Length)
                        {
                            keyPressTimes[idx].Enqueue(elapsedMs);
                            _hasKeyPressActivity = true;
                        }
                        if (rainEnabled) rainSystem.TriggerRainEffect(idx, key);
                    }
                    else
                    {
                        if (rainEnabled) rainSystem.ReleaseRainEffect(idx, key);
                    }
                }
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
                if (Kps != null && Kps.value != null)
                {
                    NumBuffer.Format(currentKps, Settings.EnableCountFormatting, out var buf, out int off, out int len);
                    Kps.value.SetText(buf, off, len);
                }
            }
        }

        /// <summary>
        /// Per-key KPS: clean timestamps older than 1s and update display / 每键 KPS：清理超过 1 秒的时间戳并更新显示
        /// </summary>
        private void ProcessPerKeyKpsInUpdate(long elapsedMilliseconds)
        {
            if (!_hasKeyPressActivity) return;
            if (!Settings.EnablePerKeyKps || keyPressTimes == null || Keys == null) return;
            for (int i = 0; i < Keys.Length && i < keyPressTimes.Length; i++)
            {
                var q = keyPressTimes[i];
                while (q.Count > 0 && elapsedMilliseconds - q.Peek() > 1000)
                    q.Dequeue();
                int kps = q.Count;
                if (lastPerKeyKps != null && i < lastPerKeyKps.Length && lastPerKeyKps[i] != kps)
                {
                    lastPerKeyKps[i] = kps;
                    if (Keys[i] != null && Keys[i].value != null)
                    {
                        NumBuffer.Format(kps, Settings.EnableCountFormatting, out var buf, out int off, out int len);
                        Keys[i].value.SetText(buf, off, len);
                    }
                }
            }
            bool anyActive = false;
            foreach (var q in keyPressTimes)
                if (q.Count > 0) { anyActive = true; break; }
            _hasKeyPressActivity = anyActive;
        }

        /// <summary>
        /// Process ghost key inputs — secondary keys that only trigger rain, no display/count / 处理鬼键输入 — 仅触发雨滴的副按键，无显示/计数
        /// ghostKeyStates is guaranteed non-null and same length as cachedGhostKeys (initialized in ProcessMainAndFootKeysInUpdate before this runs) / ghostKeyStates 保证非空且长度与 cachedGhostKeys 相同（在此方法之前由 ProcessMainAndFootKeysInUpdate 初始化）
        /// </summary>
        private void ProcessGhostKeysInUpdate()
        {
            if (cachedGhostKeys == null) return;
            bool rainEnabled = Settings.EnableRainEffect;
            bool ghostRainEnabled = Settings.EnableGhostRain;
            if (!rainEnabled || !ghostRainEnabled) return;

            KeyCode[] ghosts = cachedGhostKeys;
            for (int i = 0; i < ghosts.Length; i++)
            {
                if (ghosts[i] == KeyCode.None) continue;

                bool current = Input.GetKey(ghosts[i]);
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
