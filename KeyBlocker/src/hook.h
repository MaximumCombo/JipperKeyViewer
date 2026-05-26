#pragma once
#include <windows.h>
#include <unordered_set>
#include <mutex>

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

private:
    static LRESULT CALLBACK LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam);

    static HHOOK s_hHook;
    static bool s_enabled;
    static std::unordered_set<DWORD> s_allowedKeys;
    static DWORD s_blockedCount;
    static std::mutex s_mutex;
    static volatile LONG s_pendingUninstall;

    static KeyHook* s_instance;
};
