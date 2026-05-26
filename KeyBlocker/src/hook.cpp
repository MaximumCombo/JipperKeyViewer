#include "hook.h"
#include <chrono>

KeyHook* KeyHook::s_instance = nullptr;
HHOOK KeyHook::s_hHook = nullptr;
std::atomic<bool> KeyHook::s_enabled{true};
std::unordered_set<DWORD> KeyHook::s_allowedKeys;
std::shared_mutex KeyHook::s_mutex;
std::atomic<LONG64> KeyHook::s_lastHeartbeatMs{0};
std::atomic<DWORD> KeyHook::s_blockedCount{0};
volatile LONG KeyHook::s_pendingUninstall = 0;

static LONG64 NowMs()
{
    return std::chrono::duration_cast<std::chrono::milliseconds>(
        std::chrono::steady_clock::now().time_since_epoch()).count();
}

KeyHook::~KeyHook()
{
    Uninstall();
}

bool KeyHook::Install()
{
    if (s_hHook) return true;
    s_instance = this;
    s_lastHeartbeatMs.store(NowMs());
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
    std::unique_lock lock(s_mutex);
    s_allowedKeys = keys;
    s_lastHeartbeatMs.store(NowMs());
}

void KeyHook::SetEnabled(bool enabled)
{
    s_enabled.store(enabled);
    if (!enabled) s_blockedCount.store(0);
}

bool KeyHook::IsEnabled() const
{
    return s_enabled.load();
}

DWORD KeyHook::GetBlockedCount() const
{
    return s_blockedCount.load();
}

void KeyHook::ResetBlockedCount()
{
    s_blockedCount.store(0);
}

void KeyHook::Heartbeat()
{
    s_lastHeartbeatMs.store(NowMs());
}

LRESULT CALLBACK KeyHook::LowLevelKeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (s_pendingUninstall) return CallNextHookEx(nullptr, nCode, wParam, lParam);
    if (nCode != HC_ACTION) return CallNextHookEx(nullptr, nCode, wParam, lParam);

    if (wParam != WM_KEYDOWN && wParam != WM_KEYUP &&
        wParam != WM_SYSKEYDOWN && wParam != WM_SYSKEYUP)
        return CallNextHookEx(nullptr, nCode, wParam, lParam);

    // Heartbeat timeout: auto-disable if C# process died / disconnected
    if (s_enabled.load())
    {
        LONG64 elapsed = NowMs() - s_lastHeartbeatMs.load();
        if (elapsed > HEARTBEAT_TIMEOUT_MS)
        {
            s_enabled.store(false);
            s_blockedCount.store(0);
        }
    }

    if (!s_enabled.load())
        return CallNextHookEx(nullptr, nCode, wParam, lParam);

    auto& kbd = *(KBDLLHOOKSTRUCT*)lParam;

    // Always allow Escape regardless of allowlist
    if (kbd.vkCode == VK_ESCAPE)
        return CallNextHookEx(nullptr, nCode, wParam, lParam);

    // Shared lock: multiple hook invocations can check the set concurrently
    {
        std::shared_lock lock(s_mutex);
        if (s_allowedKeys.empty() || s_allowedKeys.count(kbd.vkCode) == 0)
        {
            s_blockedCount.fetch_add(1);
            return 1;
        }
    }

    return CallNextHookEx(nullptr, nCode, wParam, lParam);
}
