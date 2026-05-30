using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace JipperKeyViewer.Input
{
    /// <summary>
    /// Provides an abstraction layer for handling input events, allowing for custom input handling or fallback to the
    /// default Unity input system.
    /// </summary>
    /// <remarks>The <see cref="InputSystem"/> class enables the use of a custom input handling system when
    /// <see cref="APIActive"/> is set to <see langword="true"/>. When <see cref="APIActive"/> is <see
    /// langword="false"/>, the class delegates input handling to Unity's default input system. This allows for dynamic
    /// switching between custom and default input handling.</remarks>
    public class InputSystem
    {
        /// <summary>
        /// Gets or sets a value indicating whether the API is currently active.
        /// </summary>
        public static bool APIActive { 
            get => mIsApiActive; 
            set {
                if (!value) ResetAPI();
                mIsApiActive = value;
            } 
        }

        /// <summary>
        /// Gets a value indicating whether any key was pressed during the current frame.
        /// </summary>
        /// <remarks>This property checks for key presses either through the Unity input system or an
        /// alternative input mechanism, depending on the current API state.</remarks>
        public static bool anyKeyDown 
        { 
            get {
                if (!APIActive) return UnityEngine.Input.anyKeyDown;
                return keysDown.Count > 0;
            }
        }

        static List<KeyCode> keysDown = new List<KeyCode>();
        static bool mIsApiActive = false;

        /// <summary>
        /// Determines whether the specified key is currently being pressed.
        /// </summary>
        /// <remarks>If the API is not active, this method delegates to <see
        /// cref="UnityEngine.Input.GetKey(KeyCode)"/>. Otherwise, it checks the internal collection of keys currently
        /// marked as pressed.</remarks>
        /// <param name="keyCode">The key to check, represented as a <see cref="KeyCode"/>.</param>
        /// <returns><see langword="true"/> if the specified key is currently pressed; otherwise, <see langword="false"/>.</returns>
        public static bool GetKey(KeyCode keyCode)
        {
            if (!APIActive) return UnityEngine.Input.GetKey(keyCode);
            return keysDown.Contains(keyCode);
        }

        /// <summary>
        /// Determines whether the specified key was pressed during the current frame.
        /// </summary>
        /// <remarks>This method is a wrapper around <see cref="UnityEngine.Input.GetKeyDown(KeyCode)"/>
        /// and provides the same behavior. It is typically used to detect single key presses in real-time input
        /// handling scenarios.</remarks>
        /// <param name="keyCode">The key to check, represented as a <see cref="KeyCode"/>.</param>
        /// <returns><see langword="true"/> if the specified key was pressed during the current frame; otherwise, <see
        /// langword="false"/>.</returns>
        public static bool GetKeyDown(KeyCode keyCode)
        {
           return UnityEngine.Input.GetKeyDown(keyCode);
        }

        /// <summary>
        /// Simulates pressing a key by adding the specified key code to the active keys list.
        /// </summary>
        /// <remarks>This method requires the API to be active. If the API is not enabled, the method logs
        /// a message and does not perform any action.</remarks>
        /// <param name="keyCode">The key code representing the key to be pressed.</param>
        public static void PressKey(KeyCode keyCode)
        {
            if (!APIActive) { Main.Mod.Logger.Log("API is not enabled for PressKey function!"); return; }
            keysDown.Add(keyCode);
        }

        /// <summary>
        /// Releases the specified key, removing it from the list of currently pressed keys.
        /// </summary>
        /// <remarks>This method has no effect if the API is not active. Ensure the API is enabled before
        /// calling this method.</remarks>
        /// <param name="keyCode">The key to release, represented as a <see cref="KeyCode"/>.</param>
        public static void ReleaseKey(KeyCode keyCode)
        {
            if (!APIActive) { Main.Mod.Logger.Log("API is not enabled for ReleaseKey function!"); return; }
            keysDown.Remove(keyCode);
        }

        /// <summary>
        /// Resets the state of the API by clearing all tracked key presses.
        /// </summary>
        /// <remarks>This method clears the internal collection of keys that are currently marked as
        /// pressed. It is typically used to reset the API to its initial state, ensuring no keys are considered
        /// pressed.</remarks>
        static void ResetAPI()
        {
            keysDown.Clear();
        }
    }
}
