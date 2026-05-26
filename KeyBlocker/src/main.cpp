#include "hook.h"
#include "pipe_server.h"
#define _WIN32_WINNT 0x0601
#include <shellapi.h>

// Window message constants
static constexpr UINT WM_TRAY_CALLBACK = WM_APP + 1;
static constexpr UINT ID_TRAY_ENABLE   = 1001;
static constexpr UINT ID_TRAY_DISABLE  = 1002;
static constexpr UINT ID_TRAY_EXIT     = 1003;

static const wchar_t* CLASS_NAME  = L"JipperKeyBlockerWindow";
static const wchar_t* TRAY_TOOLTIP = L"Jipper Key Blocker";

static LRESULT CALLBACK WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam);
static bool AddTrayIcon(HWND hwnd);
static void RemoveTrayIcon(HWND hwnd);
static void ShowTrayMenu(HWND hwnd);

int WINAPI WinMain(HINSTANCE hInstance, HINSTANCE, LPSTR, int nCmdShow)
{
    WNDCLASSEXW wc = {};
    wc.cbSize        = sizeof(wc);
    wc.lpfnWndProc   = WindowProc;
    wc.hInstance     = hInstance;
    wc.lpszClassName = CLASS_NAME;
    if (!RegisterClassExW(&wc))
        return 1;

    HWND hwnd = CreateWindowExW(0, CLASS_NAME, L"", 0,
                                CW_USEDEFAULT, CW_USEDEFAULT, 0, 0,
                                nullptr, nullptr, hInstance, nullptr);
    if (!hwnd)
        return 1;

    // Initialize components (store hook ptr on the window for tray callbacks)
    KeyHook hook;
    SetWindowLongPtrW(hwnd, GWLP_USERDATA, (LONG_PTR)&hook);
    PipeServer pipe(hook);

    if (!hook.Install())
    {
        MessageBoxW(nullptr, L"Failed to install keyboard hook.\nAnother instance may already be running.", L"Error", MB_ICONERROR);
        DestroyWindow(hwnd);
        return 1;
    }

    pipe.Start();
    AddTrayIcon(hwnd);

    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessageW(&msg);
    }

    RemoveTrayIcon(hwnd);
    pipe.Stop();
    hook.Uninstall();
    return 0;
}

static LRESULT CALLBACK WindowProc(HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam)
{
    switch (msg)
    {
    case WM_TRAY_CALLBACK:
        if (lParam == WM_RBUTTONUP || lParam == WM_LBUTTONUP)
            ShowTrayMenu(hwnd);
        break;

    case WM_COMMAND:
    {
        auto* hook = (KeyHook*)GetWindowLongPtrW(hwnd, GWLP_USERDATA);
        switch (LOWORD(wParam))
        {
        case ID_TRAY_ENABLE:
            if (hook) { hook->SetEnabled(true); hook->ResetBlockedCount(); }
            break;
        case ID_TRAY_DISABLE:
            if (hook) hook->SetEnabled(false);
            break;
        case ID_TRAY_EXIT:
            PostQuitMessage(0);
            break;
        }
        break;
    }

    case WM_DESTROY:
        PostQuitMessage(0);
        break;

    default:
        return DefWindowProcW(hwnd, msg, wParam, lParam);
    }
    return 0;
}

static bool AddTrayIcon(HWND hwnd)
{
    NOTIFYICONDATAW nid = {};
    nid.cbSize           = sizeof(nid);
    nid.hWnd             = hwnd;
    nid.uID              = 1;
    nid.uFlags           = NIF_MESSAGE | NIF_ICON | NIF_TIP;
    nid.uCallbackMessage = WM_TRAY_CALLBACK;
    nid.hIcon            = LoadIconW(nullptr, IDI_APPLICATION);
    wcscpy_s(nid.szTip, TRAY_TOOLTIP);
    return Shell_NotifyIconW(NIM_ADD, &nid) != FALSE;
}

static void RemoveTrayIcon(HWND hwnd)
{
    NOTIFYICONDATAW nid = {};
    nid.cbSize = sizeof(nid);
    nid.hWnd   = hwnd;
    nid.uID    = 1;
    Shell_NotifyIconW(NIM_DELETE, &nid);
}

static void ShowTrayMenu(HWND hwnd)
{
    HMENU menu = CreatePopupMenu();
    AppendMenuW(menu, MF_STRING, ID_TRAY_ENABLE,  L"Enable  (block keys)");
    AppendMenuW(menu, MF_STRING, ID_TRAY_DISABLE, L"Disable (pass through)");
    AppendMenuW(menu, MF_SEPARATOR, 0, nullptr);
    AppendMenuW(menu, MF_STRING, ID_TRAY_EXIT, L"Exit");

    POINT pt;
    GetCursorPos(&pt);
    SetForegroundWindow(hwnd);
    TrackPopupMenu(menu, TPM_RIGHTBUTTON, pt.x, pt.y, 0, hwnd, nullptr);
    DestroyMenu(menu);
}
