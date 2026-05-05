using HarmonyLib;
using JipperKeyViewer.KeyViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityModManagerNet;
using UnityEngine;

namespace JipperKeyViewer
{
    public class Main
    {
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        public static UnityModManager.ModEntry? Mod { get; private set; }
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        public static Harmony? Harmony { get; private set; }
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。

        static GameObject KeyViewerGO;
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;

            // Setup callbacks / 设置回调
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = (entry) => KeyViewer.KeyViewer.instance?.DrawSettingsWindow();

            // Create Harmony instance / 创建 Harmony 实例
            Harmony = new Harmony(modEntry.Info.Id);

            modEntry.Logger.Log("Mod loaded / Mod 已加载");
            return true;
        }

        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value)
            {
                Harmony?.PatchAll(Assembly.GetExecutingAssembly());

                if (KeyViewerGO == null)
                {
                    KeyViewerGO = new GameObject("JipperKeyViewer");
                    GameObject.DontDestroyOnLoad(KeyViewerGO);
                    KeyViewerGO.AddComponent<KeyViewer.KeyViewer>();
                }
            }
            else
            {
                Harmony?.UnpatchAll(modEntry.Info.Id);

                if (KeyViewerGO != null)
                {
                    GameObject.Destroy(KeyViewerGO);
                    KeyViewerGO = null;
                }
            }
            return true;
        }
    }
}
