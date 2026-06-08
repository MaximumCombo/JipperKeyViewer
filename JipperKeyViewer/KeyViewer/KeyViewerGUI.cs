// Settings GUI window drawn inside UnityModManager / 在 UnityModManager 内绘制的设置 GUI 窗口
// All user-facing configuration UI: language, fonts, position, layout, colors, key rebinding, text editing / 所有面向用户的配置 UI：语言、字体、位置、布局、颜色、按键重绑定、文本编辑

using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

namespace JipperKeyViewer.KeyViewer
{
    /// <summary>
    /// Settings window rendered via UnityModManager.OnGUI / 通过 UnityModManager.OnGUI 渲染的设置窗口
    /// Uses IMGUI (GUILayout) for immediate-mode UI / 使用 IMGUI (GUILayout) 即时模式 UI
    /// </summary>
    public partial class KeyViewer : MonoBehaviour
    {
        private static float FloatSliderField(GUIContent label, float value, float min, float max, string format = "F2")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(100));
            value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(120));
            string text = GUILayout.TextField(value.ToString(format), FloatFieldWidth(value.ToString(format)));
            if (float.TryParse(text, out float parsed))
                value = Mathf.Clamp(parsed, min, float.MaxValue);
            GUILayout.EndHorizontal();
            return value;
        }

        private static float FloatSliderField(string label, float value, float min, float max, string format = "F2")
            => FloatSliderField(new GUIContent(label), value, min, max, format);

        private static bool DrawFoldoutButton(string label, bool expanded)
        {
            if (GUILayout.Button((expanded ? "◢ " : "▶ ") + label, GUI.skin.label))
                return !expanded;
            return expanded;
        }

        private static int DrawFoldoutButton(string label, int expandedType, int expandValue = 0)
        {
            if (GUILayout.Button((expandedType >= 0 ? "◢ " : "▶ ") + label, GUI.skin.label))
                return expandedType >= 0 ? -1 : expandValue;
            return expandedType;
        }

        private static void DrawFoldoutItemButton(string label, ref int state, int itemIndex)
        {
            if (GUILayout.Button((state == itemIndex ? "◢ " : "▶ ") + label, GUI.skin.label))
                state = state == itemIndex ? -1 : itemIndex;
        }

        /// <summary>
        /// Draw the main settings window / 绘制主设置窗口
        /// Contains: language toggle, enable/disable, font selection, placement, custom positioning, layout, size, rain, key change, text change, colors / 包含：语言切换、启用/禁用、字体选择、位置、自定义定位、布局、大小、雨滴、按键更改、文本更改、颜色
        /// </summary>
        public void DrawSettingsWindow()
        {
            GUILayout.BeginVertical();
            DrawLanguageSection();
            DrawCountResetSection();
            DrawFontSection();
            DrawFolderButtons();
            GUILayout.Space(10);
            DrawCustomPositionSection();
            DrawLayoutSection();
            DrawDisplaySection();
            GUILayout.Space(10);
            DrawRainSection();
            GUILayout.Space(10);
            DrawBindingSection();
            GUILayout.Space(5);
            DrawColorSection();
            GUILayout.EndVertical();
        }

        /// <summary>
        /// Draw the key rebinding section / 绘制按键重绑定区域
        /// Shows all keys for the current layout as clickable buttons / 将当前布局的所有按键显示为可点击的按钮
        /// </summary>
        private void DrawKeyChangeSection()
        {
            GUILayout.BeginVertical("box");
            KeyCode[] keyCodes = GetKeyCode();
            DrawMainKeyRows(I18n.Tr("row1_keys"), I18n.Tr("row2_keys"), I18n.Tr("row3_keys"),
                keyCodes, (i, _) => { SelectedKey = i; changeState = 0; });
            DrawFootKeyRows(I18n.Tr("foot_keys_list"), 20,
                (i, _) => { SelectedKey = i; changeState = 0; });
            if (SelectedKey != -1 && changeState == 0)
                GUILayout.Label("<b>" + I18n.Tr("press_new_key") + "</b>");
            GUILayout.EndVertical();
        }

        private void DrawMainKeyRows(string row1Label, string row2Label, string row3Label,
            KeyCode[] keyCodes, System.Action<int, KeyCode> onKeyClick, System.Func<int, KeyCode, string> labelFunc = null)
        {
            labelFunc ??= (i, kc) => KeyToString(kc);
            GUILayout.Label(row1Label + ":");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 8; i++)
                if (GUILayout.Button(labelFunc(i, keyCodes[i])))
                    onKeyClick(i, keyCodes[i]);
            GUILayout.EndHorizontal();

            byte[] backSequence = GetBackSequence();
            if (backSequence.Length > 0)
            {
                GUILayout.Label(row2Label + ":");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < backSequence.Length && i < 8; i++)
                    if (GUILayout.Button(labelFunc(backSequence[i], keyCodes[backSequence[i]])))
                        onKeyClick(backSequence[i], keyCodes[backSequence[i]]);
                GUILayout.EndHorizontal();
            }

            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
            {
                GUILayout.Label(row3Label + ":");
                GUILayout.BeginHorizontal();
                for (int i = 16; i < 20 && i < keyCodes.Length; i++)
                    if (GUILayout.Button(labelFunc(i, keyCodes[i])))
                        onKeyClick(i, keyCodes[i]);
                GUILayout.EndHorizontal();
            }
        }

        private void DrawFootKeyRows(string label, int baseIndex, System.Action<int, KeyCode> onKeyClick,
            System.Func<int, KeyCode, string> labelFunc = null)
        {
            labelFunc ??= (i, kc) => KeyToString(kc);
            KeyCode[] footKeyCodes = GetFootKeyCode();
            if (footKeyCodes == null || footKeyCodes.Length == 0) return;
            GUILayout.Label(label + ":");
            if (footKeyCodes.Length <= 8)
            {
                GUILayout.BeginHorizontal();
                for (int i = 0; i < footKeyCodes.Length; i++)
                    if (GUILayout.Button(labelFunc(baseIndex + i, footKeyCodes[i])))
                        onKeyClick(baseIndex + i, footKeyCodes[i]);
                GUILayout.EndHorizontal();
            }
            else
            {
                int remaining = footKeyCodes.Length - 8;
                GUILayout.BeginHorizontal();
                for (int i = 0; i < 8; i++)
                    if (GUILayout.Button(labelFunc(baseIndex + i, footKeyCodes[i])))
                        onKeyClick(baseIndex + i, footKeyCodes[i]);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                for (int s = 0; s < 8 - remaining; s++)
                    GUILayout.FlexibleSpace();
                for (int i = 8; i < footKeyCodes.Length; i++)
                    if (GUILayout.Button(labelFunc(baseIndex + i, footKeyCodes[i])))
                        onKeyClick(baseIndex + i, footKeyCodes[i]);
                for (int s = 0; s < 8 - remaining; s++)
                    GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
        }

        /// <summary>
        /// Draw the ghost key rebinding section / 绘制鬼键重绑定区域
        /// Shows ghost key slots — click unbound to bind, click bound to clear / 显示鬼键槽位 — 点击未绑定的进入绑定，点击已绑定的清除
        /// </summary>
        private void DrawGhostKeyChangeSection()
        {
            GUILayout.BeginVertical("box");
            KeyCode[] ghostKeyCodes = GetGhostKeyCode();

            GUILayout.Label(I18n.Tr("row1_keys") + ":");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 8; i++)
                DrawGhostKeyButton(i, ghostKeyCodes);
            GUILayout.EndHorizontal();

            byte[] backSequence = GetBackSequence();
            if (backSequence.Length > 0)
            {
                GUILayout.Label(I18n.Tr("row2_keys") + ":");
                GUILayout.BeginHorizontal();
                for (int i = 0; i < backSequence.Length && i < 8; i++)
                    DrawGhostKeyButton(backSequence[i], ghostKeyCodes);
                GUILayout.EndHorizontal();
            }

            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
            {
                GUILayout.Label(I18n.Tr("row3_keys") + ":");
                GUILayout.BeginHorizontal();
                for (int i = 16; i < 20; i++)
                    DrawGhostKeyButton(i, ghostKeyCodes);
                GUILayout.EndHorizontal();
            }

            if (SelectedKey != -1 && changeState == 2)
                GUILayout.Label("<b>" + I18n.Tr("press_new_key") + "</b>");
            GUILayout.EndVertical();
        }

        private void DrawGhostKeyButton(int i, KeyCode[] ghostKeyCodes)
        {
            bool isBound = ghostKeyCodes[i] != KeyCode.None;
            string label = isBound ? KeyToString(ghostKeyCodes[i]) : "-";
            bool selected = i == SelectedKey && changeState == 2;
            if (GUILayout.Button(selected ? "<b>" + label + "</b>" : label))
            {
                if (isBound)
                {
                    ghostKeyCodes[i] = KeyCode.None;
                    SelectedKey = -1;
                    SaveSettings();
                }
                else
                {
                    SelectedKey = i;
                    changeState = 2;
                }
            }
        }

        /// <summary>
        /// Draw the custom text editing section / 绘制自定义文本编辑区域
        /// Allows typing custom labels for each key / 允许为每个按键输入自定义标签
        /// </summary>
        private void DrawTextChangeSection()
        {
            GUILayout.BeginVertical("box");
            KeyCode[] keyCodes = GetKeyCode();
            string[] keyTexts = GetKeyText();
            KeyCode[] footKeyCodes = GetFootKeyCode();
            string[] footKeyTexts = GetFootKeyText();

            DrawMainKeyRows(I18n.Tr("row1_text"), I18n.Tr("row2_text"), I18n.Tr("row3_text"),
                keyCodes, (i, _) => { SelectedKey = i; changeState = 1; },
                (i, kc) => GetKeyTextLabel(keyTexts, keyCodes, i));
            DrawFootKeyRows(I18n.Tr("foot_keys_text"), 20,
                (i, _) => { SelectedKey = i; changeState = 1; },
                (i, kc) => GetFootKeyTextLabel(footKeyTexts, footKeyCodes, i - 20));

            if (SelectedKey != -1 && changeState == 1)
                DrawTextEditArea(keyTexts, keyCodes, footKeyTexts, footKeyCodes);
            GUILayout.EndVertical();
        }

        private static string GetKeyTextLabel(string[] keyTexts, KeyCode[] keyCodes, int i) =>
            keyTexts != null && i < keyTexts.Length && !string.IsNullOrEmpty(keyTexts[i])
                ? keyTexts[i] : KeyToString(i < keyCodes.Length ? keyCodes[i] : KeyCode.None);

        private static string GetFootKeyTextLabel(string[] footKeyTexts, KeyCode[] footKeyCodes, int fi) =>
            footKeyTexts != null && fi < footKeyTexts.Length && !string.IsNullOrEmpty(footKeyTexts[fi])
                ? footKeyTexts[fi] : KeyToString(fi < footKeyCodes.Length ? footKeyCodes[fi] : KeyCode.None);

        private void DrawTextEditArea(string[] keyTexts, KeyCode[] keyCodes, string[] footKeyTexts, KeyCode[] footKeyCodes)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(I18n.Tr("input_text") + ":");
            if (SelectedKey < 20)
                DrawMainKeyTextField(keyTexts, keyCodes);
            else
                DrawFootKeyTextField(footKeyTexts, footKeyCodes);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(I18n.Tr("reset")))
            {
                if (SelectedKey < 20)
                {
                    keyTexts[SelectedKey] = null;
                    if (Keys != null && SelectedKey < Keys.Length && Keys[SelectedKey] != null)
                        Keys[SelectedKey].text.text = KeyToString(keyCodes[SelectedKey]);
                }
                else
                {
                    int footIndex = SelectedKey - 20;
                    footKeyTexts[footIndex] = null;
                    if (Keys != null && SelectedKey < Keys.Length && Keys[SelectedKey] != null)
                        Keys[SelectedKey].text.text = KeyToString(footKeyCodes[footIndex]);
                }
                SelectedKey = -1;
                SaveSettings();
            }
            if (GUILayout.Button(I18n.Tr("save_btn")))
            {
                SelectedKey = -1;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawMainKeyTextField(string[] keyTexts, KeyCode[] keyCodes)
        {
            string currentText = !string.IsNullOrEmpty(keyTexts[SelectedKey])
                ? keyTexts[SelectedKey] : KeyToString(keyCodes[SelectedKey]);
            string newText = GUILayout.TextField(currentText, GUILayout.Width(150));
            if (keyTexts[SelectedKey] != newText)
            {
                if (Keys != null && SelectedKey < Keys.Length && Keys[SelectedKey] != null)
                    Keys[SelectedKey].text.text = newText;
                keyTexts[SelectedKey] = string.IsNullOrEmpty(newText) || newText == KeyToString(keyCodes[SelectedKey]) ? null : newText;
            }
        }

        private void DrawFootKeyTextField(string[] footKeyTexts, KeyCode[] footKeyCodes)
        {
            int footIndex = SelectedKey - 20;
            string currentText = footKeyTexts != null && !string.IsNullOrEmpty(footKeyTexts[footIndex])
                ? footKeyTexts[footIndex] : KeyToString(footKeyCodes[footIndex]);
            string newText = GUILayout.TextField(currentText, GUILayout.Width(150));
            if (footKeyTexts[footIndex] != newText)
            {
                if (Keys != null && SelectedKey < Keys.Length && Keys[SelectedKey] != null)
                    Keys[SelectedKey].text.text = newText;
                footKeyTexts[footIndex] = string.IsNullOrEmpty(newText) || newText == KeyToString(footKeyCodes[footIndex]) ? null : newText;
            }
        }

        private void DrawColorSettings()
        {
            GUILayout.BeginVertical("box");
            string[] colorNames = {
                I18n.Tr("color_bg"), I18n.Tr("color_bg_clicked"), I18n.Tr("color_outline"), I18n.Tr("color_outline_clicked"),
                I18n.Tr("color_text"), I18n.Tr("color_text_clicked"),
                I18n.Tr("color_rain1"), I18n.Tr("color_rain2"), I18n.Tr("color_rain3"),
                I18n.Tr("ghost_rain_color1"), I18n.Tr("ghost_rain_color2"), I18n.Tr("ghost_rain_color3")
            };
            Color[] defaultColors = {
                Background, BackgroundClicked, Outline, OutlineClicked,
                Text, TextClicked,
                RainColor, RainColor2, RainColor3,
                GhostRainColorDefault, GhostRainColor2Default, GhostRainColor3Default
            };
            for (int i = 0; i < 12; i++)
            {
                if (i >= 6 && i < 9 && !Settings.EnableRainEffect)
                    continue;
                if (i >= 9 && !Settings.EnableGhostRain)
                    continue;
                ColorExpanded[i] = DrawFoldoutButton(colorNames[i], ColorExpanded[i]);
                if (ColorExpanded[i])
                {
                    GUILayout.BeginVertical("box");
                    GUILayout.BeginHorizontal();
                    GUILayout.Space(15);
                    Color currentColor = GetColorByIndex(i);
                    Color newColor = DrawColorPicker(colorNames[i], currentColor, defaultColors[i]);
                    if (newColor != currentColor)
                    {
                        SetColorByIndex(i, newColor);
                        UpdateAllKeyColors();
                        SaveSettings();
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();
                }
            }
            GUILayout.Space(5);
            DrawKpsTotalColors(36, I18n.Tr("kps_colors"), ref kpsColorType);
            GUILayout.Space(3);
            DrawKpsTotalColors(37, I18n.Tr("total_colors"), ref totalColorType);
            GUILayout.EndVertical();
        }

        // ===== KPS & Total independent color state =====
        int kpsColorType = -1;
        int totalColorType = -1;

        private static Color KpsTotalColor(int pi, int t) => pi == 36
            ? t switch { 0 => Settings.KpsBackground, 1 => Settings.KpsOutline, _ => Settings.KpsText }
            : t switch { 0 => Settings.TotalBackground, 1 => Settings.TotalOutline, _ => Settings.TotalText };

        private static void SetKpsTotalColor(int pi, int t, Color c)
        {
            if (pi == 36)
            {
                if (t == 0) Settings.KpsBackground = c;
                else if (t == 1) Settings.KpsOutline = c;
                else Settings.KpsText = c;
            }
            else
            {
                if (t == 0) Settings.TotalBackground = c;
                else if (t == 1) Settings.TotalOutline = c;
                else Settings.TotalText = c;
            }
        }

        private void DrawKpsTotalColors(int pi, string label, ref int expandedType)
        {
            expandedType = DrawFoldoutButton(label, expandedType);
            if (expandedType < 0) return;

            string[] typeNames = {
                I18n.Tr("color_bg"), I18n.Tr("color_outline"), I18n.Tr("color_text")
            };
            Color[] defaults = { Background, Outline, Text };

            for (int t = 0; t < 3; t++)
            {
                DrawFoldoutItemButton(typeNames[t], ref expandedType, t);
                if (expandedType != t) continue;

                GUILayout.BeginVertical("box");
                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                Color cur = KpsTotalColor(pi, t);
                Color newColor = DrawColorPicker(typeNames[t], cur, defaults[t]);
                if (newColor != cur)
                {
                    SetKpsTotalColor(pi, t, newColor);
                    UpdateAllKeyColors();
                    SaveSettings();
                }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }
        private Color DrawColorPicker(string label, Color currentColor, Color defaultColor)
        {
            GUILayout.BeginVertical();
            GUILayout.Label(label);
            void DrawChannel(string name, ref float channel)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(name + ":", GUILayout.Width(20));
                channel = GUILayout.HorizontalSlider(channel, 0f, 1f, GUILayout.Width(150));
                string txt = GUILayout.TextField(channel.ToString("F2"), GUILayout.Width(40));
                if (float.TryParse(txt, out float val))
                    channel = Mathf.Clamp01(val);
                GUILayout.EndHorizontal();
            }
            DrawChannel("R", ref currentColor.r);
            DrawChannel("G", ref currentColor.g);
            DrawChannel("B", ref currentColor.b);
            DrawChannel("A", ref currentColor.a);
            GUILayout.BeginHorizontal();
            GUILayout.Label(I18n.Tr("preview") + ":", GUILayout.Width(40));
            Rect previewRect = GUILayoutUtility.GetRect(100, 20);
            GUIUtils.DrawRect(previewRect, currentColor);
            GUILayout.EndHorizontal();
            if (GUILayout.Button(I18n.Tr("reset_default")))
            {
                currentColor = defaultColor;
            }
            GUILayout.EndVertical();
            return currentColor;
        }

        private int perKeyColorSelected = -1;
        private int perKeyColorTypeIndex = -1;

        private void DrawPerKeyColorSettings()
        {
            GUILayout.BeginVertical("box");
            KeyCode[] keyCodes = GetKeyCode();
            KeyCode[] footKeyCodes = GetFootKeyCode();

            GUILayout.Label(I18n.Tr("row1_keys") + ":");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 8; i++) DrawPerKeyColorBtn(i, KeyToString(keyCodes[i]));
            GUILayout.EndHorizontal();

            byte[] backSequence = GetBackSequence();
            if (backSequence.Length > 0)
            {
                GUILayout.Label(I18n.Tr("row2_keys") + ":");
                GUILayout.BeginHorizontal();
                for (int b = 0; b < backSequence.Length && b < 8; b++)
                    DrawPerKeyColorBtn(backSequence[b], KeyToString(keyCodes[backSequence[b]]));
                GUILayout.EndHorizontal();
            }

            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
            {
                GUILayout.Label(I18n.Tr("row3_keys") + ":");
                GUILayout.BeginHorizontal();
                for (int i = 16; i < 20 && i < keyCodes.Length; i++)
                    DrawPerKeyColorBtn(i, KeyToString(keyCodes[i]));
                GUILayout.EndHorizontal();
            }

            if (footKeyCodes != null && footKeyCodes.Length > 0)
            {
                GUILayout.Label(I18n.Tr("foot_keys") + ":");
                int rows = footKeyCodes.Length <= 8 ? 1 : 2;
                for (int r = 0; r < rows; r++)
                {
                    GUILayout.BeginHorizontal();
                    int start = r * 8;
                    int end = Mathf.Min(start + 8, footKeyCodes.Length);
                    for (int f = start; f < end; f++)
                        DrawPerKeyColorBtn(20 + f, KeyToString(footKeyCodes[f]));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            DrawPerKeyColorBtn(36, "KPS");
            DrawPerKeyColorBtn(37, "Total");
            GUILayout.EndHorizontal();

            if (perKeyColorSelected >= 0 && perKeyColorSelected < 38)
                DrawPerKeyColorEditor(perKeyColorSelected);

            if (GUILayout.Button(I18n.Tr("per_key_color_reset")))
                { Settings.InitPerKeyColors(); UpdateAllKeyColors(); SaveSettings(); }
            if (GUILayout.Button(I18n.Tr("auto_rainbow")))
                AutoAssignRainbowColors();

            GUILayout.EndVertical();
        }

        private void DrawPerKeyColorBtn(int idx, string label)
        {
            Color c = Settings.PerKeyBackground[idx];
            var style = new GUIStyle(GUI.skin.button);
            style.normal.textColor = c.grayscale > 0.5f ? Color.black : Color.white;
            if (perKeyColorSelected == idx)
                GUI.backgroundColor = Color.Lerp(c, Color.white, 0.4f);
            else
                GUI.backgroundColor = c;
            bool pressed = GUILayout.Button(label, style);
            GUI.backgroundColor = Color.white;
            if (pressed)
            {
                if (perKeyColorSelected != idx) perKeyColorTypeIndex = -1;
                perKeyColorSelected = perKeyColorSelected == idx ? -1 : idx;
            }
        }

        private static string PerKeyLabel(int s) => s switch
        {
            36 => "KPS",
            37 => "Total",
            _ => KeyToString(GetKeyCodeForIndex(s))
        };

        private static int[] PerKeyTypeOrder(int s) => s >= 36
            ? new int[] { 0, 2, 4 }
            : s >= 20 ? new int[] { 0, 1, 2, 3, 4, 5 }
            : new int[] { 0, 1, 2, 3, 4, 5, 6 };

        private bool DrawColorFoldout(int t, string name)
        {
            DrawFoldoutItemButton(name, ref perKeyColorTypeIndex, t);
            return perKeyColorTypeIndex == t;
        }

        private void DrawPerKeyColorEditor(int s)
        {
            GUILayout.Space(5);
            GUILayout.Label("Key " + s + " (" + PerKeyLabel(s) + ")");
            string rainKey = s < 8 ? "color_rain1" : s < 16 ? "color_rain2" : s < 20 ? "color_rain3" : "";

            string[] typeNames = {
                I18n.Tr("color_bg"), I18n.Tr("color_bg_clicked"),
                I18n.Tr("color_outline"), I18n.Tr("color_outline_clicked"),
                I18n.Tr("color_text"), I18n.Tr("color_text_clicked"),
                I18n.Tr(rainKey) + " (" + s + ")"
            };
            Color[] values = {
                Settings.PerKeyBackground[s], Settings.PerKeyBackgroundClicked[s],
                Settings.PerKeyOutline[s], Settings.PerKeyOutlineClicked[s],
                Settings.PerKeyText[s], Settings.PerKeyTextClicked[s],
                Settings.PerKeyRainColor[s]
            };
            Color[] defaults = {
                Background, BackgroundClicked, Outline, OutlineClicked, Text, TextClicked, RainColor
            };

            int[] typeOrder = PerKeyTypeOrder(s);
            for (int ti = 0; ti < typeOrder.Length; ti++)
            {
                int t = typeOrder[ti];
                if (!DrawColorFoldout(t, typeNames[t])) continue;

                GUILayout.BeginHorizontal();
                GUILayout.Space(15);
                GUILayout.BeginVertical("box");
                Color newColor = DrawColorPicker(typeNames[t], values[t], defaults[t]);
                if (newColor != values[t])
                {
                    SetPerKeyColor(s, t, newColor);
                    UpdateAllKeyColors();
                    SaveSettings();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }

            if (s < 36 && Settings.Count != null && s < Settings.Count.Length)
                DrawPerKeyCountReset(s);
        }

        private static void SetPerKeyColor(int s, int t, Color color)
        {
            switch (t)
            {
                case 0: Settings.PerKeyBackground[s] = color; break;
                case 1: Settings.PerKeyBackgroundClicked[s] = color; break;
                case 2: Settings.PerKeyOutline[s] = color; break;
                case 3: Settings.PerKeyOutlineClicked[s] = color; break;
                case 4: Settings.PerKeyText[s] = color; break;
                case 5: Settings.PerKeyTextClicked[s] = color; break;
                case 6: Settings.PerKeyRainColor[s] = color; break;
            }
        }

        private void DrawPerKeyCountReset(int s)
        {
            GUILayout.Space(5);
            var redStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = Color.red } };
            if (GUILayout.Button(I18n.Tr("reset_counts") + " (" + Settings.Count[s] + ")", redStyle))
            {
                Settings.Count[s] = 0;
                if (keyPressTimes != null && s < keyPressTimes.Length && keyPressTimes[s] != null)
                    keyPressTimes[s].Clear();
                if (lastPerKeyKps != null && s < lastPerKeyKps.Length)
                    lastPerKeyKps[s] = 0;
                if (Keys != null && s < Keys.Length && Keys[s]?.value != null)
                    Keys[s].value.text = "0";
                SaveSettings();
            }
        }

        private static KeyCode GetKeyCodeForIndex(int idx)
        {
            KeyCode[] main = GetKeyCode();
            if (main != null && idx < main.Length) return main[idx];
            KeyCode[] foot = GetFootKeyCode();
            int fi = idx - 20;
            if (foot != null && fi >= 0 && fi < foot.Length) return foot[fi];
            return KeyCode.None;
        }

        private static readonly (int flag, string label)[] FontStyleFlagLabels =
        {
            (1, "B"), (2, "I"), (4, "U"), (8, "Lc"),
            (16, "Uc"), (32, "Sc"), (64, "St"), (128, "Sup"), (256, "Sub")
        };

        private string BuildFontStyleSummary()
        {
            int f = Settings.FontStyleFlags;
            if (f == 0) return "Normal";
            var parts = new List<string>(4);
            foreach (var (flag, label) in FontStyleFlagLabels)
                if ((f & flag) != 0) parts.Add(label);
            return string.Join(" ", parts);
        }

        private void DrawLanguageSection()
        {
            GUILayout.BeginHorizontal();
            string[] langLabels = { "English", "中文", "한국어" };
            int langIdx = Settings.Language == "en" ? 0 : Settings.Language == "zh" ? 1 : 2;
            if (GUILayout.Button(I18n.Tr("language") + ": " + langLabels[langIdx]))
            {
                langIdx = (langIdx + 1) % 3;
                Settings.Language = langIdx == 0 ? "en" : langIdx == 1 ? "zh" : "ko";
                I18n.Lang = Settings.Language;
                SaveSettings();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawCountResetSection()
        {
            bool newEnabled = GUILayout.Toggle(Settings.Enabled, (Settings.Enabled ? "\u2713 " : "\u2717 ") + I18n.Tr("key_display_on"));
            if (newEnabled != Settings.Enabled)
            {
                Settings.Enabled = newEnabled;
                SaveSettings();
            }

            GUILayout.Space(5);
            GUILayout.BeginHorizontal();
            var redTextStyle = new GUIStyle(GUI.skin.button) { normal = { textColor = Color.red } };
            if (GUILayout.Button(I18n.Tr("reset_counts"), redTextStyle, GUILayout.MinWidth(120)))
                ExecuteCountReset();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            bool newFormatting = GUILayout.Toggle(Settings.EnableCountFormatting, I18n.Tr("count_formatting"));
            if (newFormatting != Settings.EnableCountFormatting)
            {
                Settings.EnableCountFormatting = newFormatting;
                SaveSettings();
                RefreshAllCountDisplay();
            }
            GUILayout.EndHorizontal();
        }

        private void ExecuteCountReset()
        {
            lastTotal = -1;
            lastKps = -1;
            Settings.TotalCount = 0;
            for (int i = 0; i < Settings.Count.Length; i++)
                Settings.Count[i] = 0;
            PressTimes?.Clear();
            if (keyPressTimes != null)
                for (int i = 0; i < keyPressTimes.Length; i++)
                    keyPressTimes[i]?.Clear();
            if (lastPerKeyKps != null)
                for (int i = 0; i < lastPerKeyKps.Length; i++)
                    lastPerKeyKps[i] = 0;
            if (Keys != null)
                for (int i = 0; i < Keys.Length; i++)
                    if (Keys[i]?.value != null)
                        Keys[i].value.text = "0";
            if (Kps?.value != null) Kps.value.text = "0";
            if (Total?.value != null) Total.value.text = "0";
            SaveSettings();
        }

        private void DrawFontSection()
        {
            GUILayout.Label(I18n.Tr("font_style") + ":");
            string curFont = fontList.Count > 0 ? fontList[Mathf.Clamp(Settings.FontIndex, 0, fontList.Count - 1)].name : "None";
            if (GUILayout.Button((fontListExpanded ? "\u25BC " : "\u25B6 ") + curFont, GUILayout.MinWidth(200)))
                fontListExpanded = !fontListExpanded;
            if (fontListExpanded)
            {
                if (fontList.Count > 0)
                {
                    int newIdx = Settings.FontIndex;
                    for (int i = 0; i < fontList.Count; i++)
                    {
                        bool selected = i == Settings.FontIndex;
                        if (GUILayout.Button((selected ? "\u2713 " : "  ") + fontList[i].name, GUILayout.MinWidth(200)))
                            newIdx = i;
                    }
                    if (newIdx != Settings.FontIndex)
                    {
                        Settings.FontIndex = newIdx;
                        Settings.FontName = fontList[newIdx].name;
                        fontRestored = false;
                        UpdateAllFonts();
                        SaveSettings();
                    }
                }
                else
                    GUILayout.Label("\u25B8 " + I18n.Tr("no_fonts_found"), GUILayout.MinWidth(200));

                GUILayout.Space(5);
                GUILayout.BeginVertical("box");
                GUILayout.Label(I18n.Tr("custom_font_tip"));
                GUILayout.Label($"CustomFont : {Path.Combine(Path.GetDirectoryName(Main.Mod?.Path) ?? ".", "CustomFont")}");
                GUILayout.EndVertical();
            }

            GUILayout.Space(3);
            string styleSummary = BuildFontStyleSummary();
            fontStyleExpanded = DrawFoldoutButton(I18n.Tr("font_style") + ": " + styleSummary, fontStyleExpanded);
            if (fontStyleExpanded)
            {
                string[] styleNames = { "Bold", "Italic", "Underline", "Lowercase", "Uppercase", "SmallCaps", "Strikethrough", "Superscript", "Subscript" };
                int[] styleFlags = { 1, 2, 4, 8, 16, 32, 64, 128, 256 };
                int[] styleGroups = { 0, 0, 0, 1, 1, 1, 0, 2, 2 };
                bool changed = false;
                for (int i = 0; i < styleFlags.Length; i++)
                {
                    bool active = (Settings.FontStyleFlags & styleFlags[i]) != 0;
                    bool newActive = GUILayout.Toggle(active, styleNames[i]);
                    if (newActive != active)
                    {
                        if (newActive)
                        {
                            if (styleGroups[i] == 1)
                                Settings.FontStyleFlags &= ~(8 | 16 | 32);
                            else if (styleGroups[i] == 2)
                                Settings.FontStyleFlags &= ~(128 | 256);
                        }
                        Settings.FontStyleFlags = newActive ? Settings.FontStyleFlags | styleFlags[i] : Settings.FontStyleFlags & ~styleFlags[i];
                        changed = true;
                    }
                }
                if (changed)
                {
                    UpdateAllFonts();
                    SaveSettings();
                }
            }
        }

        private void DrawFolderButtons()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(I18n.Tr("open_config_folder"), GUILayout.MinWidth(120)))
            {
                string dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    System.Diagnostics.Process.Start("explorer.exe", dir);
            }
            if (GUILayout.Button(I18n.Tr("open_font_folder"), GUILayout.MinWidth(120)))
            {
                string modPath = Path.GetDirectoryName(Main.Mod?.Path) ?? ".";
                string customFontDir = Path.Combine(modPath, "CustomFont");
                if (!Directory.Exists(customFontDir)) Directory.CreateDirectory(customFontDir);
                System.Diagnostics.Process.Start("explorer.exe", customFontDir);
            }
            GUILayout.EndHorizontal();

            bool newDownLocation = GUILayout.Toggle(Settings.DownLocation, I18n.Tr("place_below"));
            if (newDownLocation != Settings.DownLocation)
            {
                Settings.DownLocation = newDownLocation;
                ResetKeyViewer();
                ResetFootKeyViewer();
                SaveSettings();
            }
        }

        private void DrawCustomPositionSection()
        {
            bool newCustomPosition = DrawFoldoutButton(I18n.Tr("custom_pos"), Settings.CustomPositionEnabled);
            if (newCustomPosition != Settings.CustomPositionEnabled)
            {
                Settings.CustomPositionEnabled = newCustomPosition;
                SaveSettings();
                if (Settings.CustomPositionEnabled)
                {
                    ResetKeyViewerPosition();
                    ResetFootKeyViewerPosition();
                }
                else
                {
                    ResetKeyViewer();
                    ResetFootKeyViewer();
                }
            }

            if (Settings.CustomPositionEnabled)
            {
                GUILayout.BeginVertical("box");
                GUILayout.Label(I18n.Tr("main_key_pos") + ":");
                Vector2 tempMainPos = Settings.MainKeyViewerPosition;
                Vector2 tempFootPos = Settings.FootKeyViewerPosition;
                bool positionChanged = false;

                float newMainX = FloatSliderField("X", tempMainPos.x, 0f, 1f);
                if (newMainX != tempMainPos.x) { tempMainPos.x = newMainX; positionChanged = true; }
                float newMainY = FloatSliderField("Y", tempMainPos.y, 0f, 1f);
                if (newMainY != tempMainPos.y) { tempMainPos.y = newMainY; positionChanged = true; }

                GUILayout.Label(I18n.Tr("foot_key_pos") + ":");
                float newFootX = FloatSliderField("X", tempFootPos.x, 0f, 1f);
                if (newFootX != tempFootPos.x) { tempFootPos.x = newFootX; positionChanged = true; }
                float newFootY = FloatSliderField("Y", tempFootPos.y, 0f, 1f);
                if (newFootY != tempFootPos.y) { tempFootPos.y = newFootY; positionChanged = true; }

                if (positionChanged)
                {
                    Settings.MainKeyViewerPosition = tempMainPos;
                    Settings.FootKeyViewerPosition = tempFootPos;
                    ResetKeyViewerPosition();
                    ResetFootKeyViewerPosition();
                    SaveSettings();
                }

                if (GUILayout.Button(I18n.Tr("reset_pos")))
                {
                    Settings.MainKeyViewerPosition = new Vector2(0, 1);
                    Settings.FootKeyViewerPosition = new Vector2(0.24f, 1f);
                    ResetKeyViewerPosition();
                    ResetFootKeyViewerPosition();
                    SaveSettings();
                }
                GUILayout.EndVertical();
            }
        }

        private void DrawLayoutSection()
        {
            GUILayout.Label(I18n.Tr("key_layout") + ":");
            KeyviewerStyle newStyle = (KeyviewerStyle)GUILayout.SelectionGrid((int)Settings.KeyViewerStyle, KeyLayoutNames, 3);
            if (newStyle != Settings.KeyViewerStyle)
            {
                Settings.KeyViewerStyle = newStyle;
                ChangeKeyViewer();
                SaveSettings();
            }

            GUILayout.Label(I18n.Tr("foot_keys") + ":");
            FootKeyviewerStyle newFootStyle = (FootKeyviewerStyle)GUILayout.SelectionGrid((int)Settings.FootKeyViewerStyle, FootKeyLayoutNames, 5);
            if (newFootStyle != Settings.FootKeyViewerStyle)
            {
                Settings.FootKeyViewerStyle = newFootStyle;
                ResetFootKeyViewer();
                SaveSettings();
            }

            float newSettingsSize = FloatSliderField(I18n.Tr("size"), Settings.Size, 0.1f, 2f);
            if (newSettingsSize != Settings.Size)
            {
                Settings.Size = newSettingsSize;
                if (KeyViewerSizeObject != null)
                    KeyViewerSizeObject.transform.localScale = new Vector3(Settings.Size, Settings.Size, 1);
                SaveSettings();
            }
        }

        private void DrawDisplaySection()
        {
            bool newHideCount = GUILayout.Toggle(Settings.HideMainKeyCount, I18n.Tr("hide_main_count"));
            if (newHideCount != Settings.HideMainKeyCount)
            {
                Settings.HideMainKeyCount = newHideCount;
                ResetKeyViewer();
                SaveSettings();
            }

            if (!Settings.HideMainKeyCount)
            {
                bool newPerKeyKps = GUILayout.Toggle(Settings.EnablePerKeyKps, I18n.Tr("per_key_kps"));
                if (newPerKeyKps != Settings.EnablePerKeyKps)
                {
                    Settings.EnablePerKeyKps = newPerKeyKps;
                    RefreshAllCountDisplay();
                    SaveSettings();
                }
            }

            bool newStreamer = GUILayout.Toggle(Settings.StreamerMode, I18n.Tr("streamer_mode"));
            if (newStreamer != Settings.StreamerMode)
            {
                Settings.StreamerMode = newStreamer;
                if (Kps != null) Kps.gameObject.SetActive(!newStreamer);
                if (Total != null) Total.gameObject.SetActive(!newStreamer);
                SaveSettings();
            }
        }

        private void DrawRainSection()
        {
            bool newRainEffect = GUILayout.Toggle(Settings.EnableRainEffect, I18n.Tr("rain_effect"));
            if (newRainEffect != Settings.EnableRainEffect)
            {
                Settings.EnableRainEffect = newRainEffect;
                if (!Settings.EnableRainEffect)
                    rainSystem.ClearActiveDrops(Keys);
                SaveSettings();
            }

            if (!Settings.EnableRainEffect) return;

            GUILayout.Label(I18n.Tr("rain_rows") + ":");
            GUILayout.BeginHorizontal();
            Settings.EnableRainForRow1 = GUILayout.Toggle(Settings.EnableRainForRow1, I18n.Tr("rain_row1"));
            Settings.EnableRainForRow2 = GUILayout.Toggle(Settings.EnableRainForRow2, I18n.Tr("rain_row2"));
            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
                Settings.EnableRainForRow3 = GUILayout.Toggle(Settings.EnableRainForRow3, I18n.Tr("rain_row3"));
            GUILayout.EndHorizontal();

            GUILayout.Label(I18n.Tr("rain_height") + ":");
            Settings.RainHeightRow1 = FloatSliderField(I18n.Tr("rain_row1"), Settings.RainHeightRow1, 1f, 2000f);
            Settings.RainHeightRow2 = FloatSliderField(I18n.Tr("rain_row2"), Settings.RainHeightRow2, 1f, 2000f);
            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
                Settings.RainHeightRow3 = FloatSliderField(I18n.Tr("rain_row3"), Settings.RainHeightRow3, 1f, 2000f);

            GUILayout.Label(I18n.Tr("rain_speed") + ":");
            Settings.RainSpeedRow1 = FloatSliderField(I18n.Tr("rain_row1"), Settings.RainSpeedRow1, 50f, 2000f, "F0");
            Settings.RainSpeedRow2 = FloatSliderField(I18n.Tr("rain_row2"), Settings.RainSpeedRow2, 50f, 2000f, "F0");
            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
                Settings.RainSpeedRow3 = FloatSliderField(I18n.Tr("rain_row3"), Settings.RainSpeedRow3, 50f, 2000f, "F0");

            GUILayout.Label(I18n.Tr("rain_width") + ":");
            Settings.RainWidthRow1 = FloatSliderField(I18n.Tr("rain_width_row1"), Settings.RainWidthRow1, 10f, 200f, "F0");
            Settings.RainWidthRow2 = FloatSliderField(I18n.Tr("rain_width_row2"), Settings.RainWidthRow2, 10f, 200f, "F0");
            if (Settings.KeyViewerStyle == KeyviewerStyle.Key20)
                Settings.RainWidthRow3 = FloatSliderField(I18n.Tr("rain_width_row3"), Settings.RainWidthRow3, 10f, 200f, "F0");

            GUILayout.Space(5);
            bool newRainFade = GUILayout.Toggle(Settings.EnableRainFade, I18n.Tr("rain_fade"));
            if (newRainFade != Settings.EnableRainFade)
            {
                Settings.EnableRainFade = newRainFade;
                if (!newRainFade && rainSystem != null && Keys != null)
                    rainSystem.ClearActiveDrops(Keys);
                SaveSettings();
            }
            if (Settings.EnableRainFade)
            {
                float newFadeDur = FloatSliderField(I18n.Tr("fade_duration"), Settings.RainFadeDuration, 0.03f, 5.0f);
                if (newFadeDur != Settings.RainFadeDuration)
                {
                    Settings.RainFadeDuration = newFadeDur;
                    SaveSettings();
                }
            }

            GUILayout.Space(5);
            bool newGradient = GUILayout.Toggle(Settings.EnableRainGradient, I18n.Tr("rain_gradient"));
            if (newGradient != Settings.EnableRainGradient)
            {
                Settings.EnableRainGradient = newGradient;
                SaveSettings();
            }
            if (Settings.EnableRainGradient)
            {
                float newFadePx = FloatSliderField(I18n.Tr("gradient_percent"), Settings.RainFadePx, 1f, 200f, "F0");
                if (!Mathf.Approximately(newFadePx, Settings.RainFadePx))
                {
                    Settings.RainFadePx = newFadePx;
                    SaveSettings();
                }
            }

            GUILayout.Space(5);
            bool newGhostRain = GUILayout.Toggle(Settings.EnableGhostRain, I18n.Tr("ghost_rain"));
            if (newGhostRain != Settings.EnableGhostRain)
            {
                Settings.EnableGhostRain = newGhostRain;
                if (!newGhostRain && rainSystem != null && Keys != null)
                    rainSystem.ClearActiveDrops(Keys);
                SaveSettings();
            }
        }

        private void DrawBindingSection()
        {
            KeyChangeExpanded = DrawFoldoutButton(I18n.Tr("key_change"), KeyChangeExpanded);
            if (KeyChangeExpanded)
                DrawKeyChangeSection();

            if (Settings.EnableRainEffect && Settings.EnableGhostRain)
            {
                GhostRainChangeExpanded = DrawFoldoutButton(I18n.Tr("ghost_rain"), GhostRainChangeExpanded);
                if (GhostRainChangeExpanded)
                    DrawGhostKeyChangeSection();
            }

            TextChangeExpanded = DrawFoldoutButton(I18n.Tr("text_change"), TextChangeExpanded);
            if (TextChangeExpanded)
                DrawTextChangeSection();
        }

        private void DrawColorSection()
        {
            bool colorsExpanded = DrawFoldoutButton(I18n.Tr("colors"), ColorExpanded != null);
            if (colorsExpanded && ColorExpanded == null) ColorExpanded = new bool[12];
            if (!colorsExpanded) ColorExpanded = null;
            if (ColorExpanded == null) return;

            bool pk = GUILayout.Toggle(Settings.EnablePerKeyColors, I18n.Tr("per_key_colors"));
            if (pk != Settings.EnablePerKeyColors)
            {
                Settings.EnablePerKeyColors = pk;
                ResetKeyViewer();
                UpdateAllKeyColors();
                SaveSettings();
            }
            if (Settings.EnablePerKeyColors)
                DrawPerKeyColorSettings();
            else
                DrawColorSettings();
        }
    }
}
