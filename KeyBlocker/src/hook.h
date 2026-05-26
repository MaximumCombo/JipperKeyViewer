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

    inline static HHOOK s_hHook = nullptr;
    inline static bool s_enabled = true;
    inline static std::unordered_set<DWORD> s_allowedKeys;
    inline static DWORD s_blockedCount = 0;
    inline static std::mutex s_mutex;
    inline static volatile LONG s_pendingUninstall = 0;

    static KeyHook* s_instance;
};
