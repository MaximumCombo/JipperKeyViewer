#include "hook.h"

KeyHook* KeyHook::s_instance = nullptr;
HHOOK KeyHook::s_hHook = nullptr;
bool KeyHook::s_enabled = true;
std::unordered_set<DWORD> KeyHook::s_allowedKeys;
DWORD KeyHook::s_blockedCount = 0;
std::mutex KeyHook::s_mutex;
volatile LONG KeyHook::s_pendingUninstall = 0;

KeyHook::~KeyHook()
{
    Uninstall();
}

bool KeyHook::Install()
{
    if (s_hHook) return true;
    s_instance = this;
    s_hHook = SetWindowsHookExW(WH_KEYBOARD_LL, LowLevelKeyboardProc, GetModuleHandleW(nullptr), 0);
    return s_hHook != nullptr;
}

void KeyHook::Uninstall()
{
    InterlockedExchange(&s_pendingUninstall, 1);
    if (s_hHook)
    {
        UnhookWindowsHookEx(s_hHook);
        s_hHook = nullptr;
    }
    InterlockedExchange(&s_pendingUninstall, 0);
}

void KeyHook::SetAllowedKeys(const std::unordered_set<DWORD>& keys)
{
    std::lock_guard<std::mutex> lock(s_mutex);
    s_allowedKeys = keys;
}

void KeyHook::SetEnabled(bool enabled)
{
    std::lock_guard<std::mutex> lock(s_mutex);
    s_enabled = enabled;
    if (!enabled) ResetBlockedCount();
}

bool KeyHook::IsEnabled() const
{
    std::lock_guard<std::mutex> lock(s_mutex);
    return s_enabled;
}

DWORD KeyHook::GetBlockedCount() const
{
    // atomic read, no lock needed for single DWORD aligned access
    return s_blockedCount;
}

void KeyHook::ResetBlockedCount()
{
    s_blockedCount = 0;
}

LRESULT CALLBACK KeyHook::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (s_pendingUninstall) return CallNextHookEx(nullptr, nCode, wParam, lParam);
    if (nCode != HC_ACTION) return CallNextHookEx(nullptr, nCode, wParam, lParam);

    // Only filter key down/up events
    if (wParam != WM_KEYDOWN && wParam != WM_KEYUP &&
        wParam != WM_SYSKEYDOWN && wParam != WM_SYSKEYUP)
        return CallNextHookEx(nullptr, nCode, wParam, lParam);

    {
        std::lock_guard<std::mutex> lock(s_mutex);
        if (!s_enabled) return CallNextHookEx(nullptr, nCode, wParam, lParam);

        auto& kbd = *(KBDLLHOOKSTRUCT*)lParam;

        // Always allow Escape key (VK_ESCAPE = 0x1B) regardless of allowlist
        if (kbd.vkCode == VK_ESCAPE)
            return CallNextHookEx(nullptr, nCode, wParam, lParam);

        if (s_allowedKeys.empty() || s_allowedKeys.count(kbd.vkCode) == 0)
        {
            // Block this key — return 1 to prevent the message from being dispatched
            s_blockedCount++;
            return 1;
        }
    }

    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}
