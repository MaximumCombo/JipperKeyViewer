#pragma once
#include <windows.h>
#include <string>
#include <thread>
#include <atomic>
#include <mutex>

class KeyHook;

class PipeServer
{
public:
    PipeServer(KeyHook& hook) : m_hook(hook) {}
    ~PipeServer() { Stop(); }
    bool Start();
    void Stop();

private:
    void Run();
    void HandleCommand(const std::string& cmd, HANDLE hPipe);

    KeyHook& m_hook;
    HANDLE m_hPipe = INVALID_HANDLE_VALUE;
    std::mutex m_handleMutex;
    std::thread m_thread;
    std::atomic<bool> m_running{false};

    static constexpr const wchar_t* PIPE_NAME = L"\\\\.\\pipe\\JipperKeyBlocker";
};
