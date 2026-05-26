using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace JipperKeyViewer.KeyViewer
{
    /// <summary>
    /// Frame-rate independent input manager using Win32 GetAsyncKeyState.
    /// Polls key states on a background thread at ~1000Hz, producing timestamped
    /// press/release events that the main thread consumes each frame.
    /// </summary>
    public sealed class AsyncInputManager : IDisposable
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        /// <summary>Unity KeyCode → Win32 Virtual-Key code mapping</summary>
        public static readonly Dictionary<KeyCode, int> KeyCodeToVK = new Dictionary<KeyCode, int>
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
            // Modifiers
            [KeyCode.LeftShift] = 0xA0, [KeyCode.RightShift] = 0xA1,
            [KeyCode.LeftControl] = 0xA2, [KeyCode.RightControl] = 0xA3,
            [KeyCode.LeftAlt] = 0xA4, [KeyCode.RightAlt] = 0xA5,
            // Special
            [KeyCode.Tab] = 0x09, [KeyCode.Space] = 0x20, [KeyCode.Return] = 0x0D,
            [KeyCode.Escape] = 0x1B, [KeyCode.Backspace] = 0x08,
            [KeyCode.CapsLock] = 0x14,
            // Punctuation
            [KeyCode.Semicolon] = 0xBA, [KeyCode.Equals] = 0xBB, [KeyCode.Comma] = 0xBC,
            [KeyCode.Minus] = 0xBD, [KeyCode.Period] = 0xBE, [KeyCode.Slash] = 0xBF,
            [KeyCode.BackQuote] = 0xC0, [KeyCode.LeftBracket] = 0xDB,
            [KeyCode.Backslash] = 0xDC, [KeyCode.RightBracket] = 0xDD,
            [KeyCode.Quote] = 0xDE,
            // Navigation
            [KeyCode.UpArrow] = 0x26, [KeyCode.DownArrow] = 0x28,
            [KeyCode.LeftArrow] = 0x25, [KeyCode.RightArrow] = 0x27,
            [KeyCode.Insert] = 0x2D, [KeyCode.Delete] = 0x2E,
            [KeyCode.Home] = 0x24, [KeyCode.End] = 0x23,
            [KeyCode.PageUp] = 0x21, [KeyCode.PageDown] = 0x22,
        };

        /// <summary>A single key state change event with high-precision timestamp</summary>
        public struct KeyEvent
        {
            public int VKey;
            public bool Pressed;
            public long TimestampMs; // from Stopwatch.ElapsedMilliseconds
        }

        private readonly ConcurrentQueue<KeyEvent> _eventQueue = new ConcurrentQueue<KeyEvent>();
        private readonly Dictionary<int, bool> _lastState = new Dictionary<int, bool>();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private Thread _pollThread;
        private volatile bool _running;
        private readonly int[] _trackedVKeys;
        private readonly int _pollIntervalUs; // microseconds

        /// <summary>
        /// Create a new AsyncInputManager.
        /// </summary>
        /// <param name="vkeys">Array of Win32 virtual key codes to monitor</param>
        /// <param name="pollHz">Polling frequency in Hz (default 1000 = 1ms interval)</param>
        public AsyncInputManager(int[] vkeys, int pollHz = 1000)
        {
            _trackedVKeys = vkeys ?? Array.Empty<int>();
            _pollIntervalUs = 1_000_000 / pollHz;
            foreach (int vk in _trackedVKeys)
                _lastState[vk] = false;
        }

        /// <summary>Start the background polling thread</summary>
        public void Start()
        {
            if (_running) return;
            _running = true;
            _pollThread = new Thread(PollLoop)
            {
                Name = "AsyncInput Poller",
                IsBackground = true,
                Priority = System.Threading.ThreadPriority.AboveNormal
            };
            _pollThread.Start();
        }

        /// <summary>Stop the background polling thread</summary>
        public void Stop()
        {
            _running = false;
            _pollThread?.Join(500);
            _pollThread = null;
        }

        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// Drain all queued key events. Called from the main thread each frame.
        /// Returns events in chronological order (oldest first).
        /// </summary>
        public int DrainEvents(List<KeyEvent> buffer)
        {
            int count = 0;
            while (_eventQueue.TryDequeue(out var evt))
            {
                buffer.Add(evt);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Get the current real-time state of a key (bypasses event queue).
        /// Useful for initial state checks or fallback.
        /// </summary>
        public static bool IsKeyDown(int vkey)
        {
            return (GetAsyncKeyState(vkey) & 0x8000) != 0;
        }

        /// <summary>
        /// Update the set of tracked keys (e.g., when layout changes).
        /// Thread-safe: new keys are picked up on the next poll cycle.
        /// </summary>
        public void UpdateTrackedKeys(int[] vkeys)
        {
            lock (_lastState)
            {
                _lastState.Clear();
                foreach (int vk in vkeys)
                    _lastState[vk] = false;
            }
        }

        private void PollLoop()
        {
            var sw = new Stopwatch();
            sw.Start();

            while (_running)
            {
                long startTicks = sw.ElapsedTicks;

                lock (_lastState)
                {
                    long now = _stopwatch.ElapsedMilliseconds;
                    foreach (var kvp in _lastState)
                    {
                        int vk = kvp.Key;
                        bool wasDown = kvp.Value;
                        bool isDown = (GetAsyncKeyState(vk) & 0x8000) != 0;

                        if (isDown != wasDown)
                        {
                            _lastState[vk] = isDown;
                            _eventQueue.Enqueue(new KeyEvent
                            {
                                VKey = vk,
                                Pressed = isDown,
                                TimestampMs = now
                            });
                        }
                    }
                }

                // Spin-wait for precise timing (Thread.Sleep has ~1-15ms granularity)
                long targetTicks = startTicks + (long)(_pollIntervalUs * (Stopwatch.Frequency / 1_000_000.0));
                while (sw.ElapsedTicks < targetTicks && _running)
                    Thread.SpinWait(16);
            }
        }
    }
}
