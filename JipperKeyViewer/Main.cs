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
        /// <summary>
        /// Reference to the mod entry / Mod 入口引用
        /// </summary>
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        public static UnityModManager.ModEntry? Mod { get; private set; }
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。

        /// <summary>
        /// Harmony instance for patching / Harmony 补丁实例
        /// </summary>
#pragma warning disable CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。
        public static Harmony? Harmony { get; private set; }
#pragma warning restore CS8632 // 只能在 "#nullable" 注释上下文内的代码中使用可为 null 的引用类型的注释。

        /// <summary>
        /// Mod settings / Mod 设置
        /// </summary>
        public static Settings Settings { get; private set; } = null!;

        static GameObject KeyViewerGO;

        /// <summary>
        /// Mod entry point called by UnityModManager
        /// UnityModManager 调用的 Mod 入口点
        /// </summary>
        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;

            // Load settings / 加载设置
            Settings = Settings.Load(modEntry);

            // Setup callbacks / 设置回调
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = Settings.OnGUI;
            modEntry.OnSaveGUI = Settings.OnSaveGUI;

            // Create Harmony instance / 创建 Harmony 实例
            Harmony = new Harmony(modEntry.Info.Id);

            modEntry.Logger.Log("Mod loaded / Mod 已加载");
            return true;
        }

        /// <summary>
        /// Called when mod is toggled on/off
        /// Mod 启用/禁用切换时调用
        /// </summary>
        private static bool OnToggle(UnityModManager.ModEntry modEntry, bool value)
        {
            if (value)
            {
                modEntry.Logger.Log("Mod enabled / Mod 已启用");
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
                modEntry.Logger.Log("Mod disabled / Mod 已禁用");
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
