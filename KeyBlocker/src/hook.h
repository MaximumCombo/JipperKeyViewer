#pragma once
#include <windows.h>
#include <unordered_set>
#include <shared_mutex>
#include <atomic>
#include <chrono>

class KeyHook
{
public:
    ~KeyHook();
    bool Install();
    void Uninstall();
    void SetAllowedKeys(const std::unordered_set<DWORD>& keys);
    void SetEnabled(bool enabled);
    bool IsEnabled() const;
    DWORD GetBlockedCount() const;
    void ResetBlockedCount();
    void Heartbeat();

private:
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

    static HHOOK s_hHook;

    // Hot-path state: atomic for lock-free reads in the hook callback
    static std::atomic<bool> s_enabled;

    // Protected by s_mutex (shared lock in hook, exclusive in SetAllowedKeys)
    static std::shared_mutex s_mutex;
    static std::unordered_set<DWORD> s_allowedKeys;

    // Heartbeat: if no heartbeat for HEARTBEAT_TIMEOUT_MS while enabled, auto-disable
    static std::atomic<LONG64> s_lastHeartbeatMs;
    static constexpr LONG64 HEARTBEAT_TIMEOUT_MS = 10000; // 10 seconds

    static std::atomic<DWORD> s_blockedCount;
    static volatile LONG s_pendingUninstall;

    static KeyHook* s_instance;
};
