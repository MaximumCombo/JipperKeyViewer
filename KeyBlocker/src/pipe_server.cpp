#include "pipe_server.h"
#include "hook.h"
#include <vector>
#include <sstream>
#include <algorithm>
#include <cctype>
#include <charconv>

bool PipeServer::Start()
{
    if (m_running) return true;
    m_running = true;
    m_thread = std::thread(&PipeServer::Run, this);
    return true;
}

void PipeServer::Stop()
{
    m_running = false;
    if (m_hPipe != INVALID_HANDLE_VALUE)
    {
        CancelIoEx(m_hPipe, nullptr);
        CloseHandle(m_hPipe);
        m_hPipe = INVALID_HANDLE_VALUE;
    }
    if (m_thread.joinable())
        m_thread.join();
}

static std::string Trim(const std::string& s)
{
    auto start = std::find_if_not(s.begin(), s.end(), ::isspace);
    auto end = std::find_if_not(s.rbegin(), s.rend(), ::isspace).base();
    return start < end ? std::string(start, end) : "";
}

static std::vector<std::string> Split(const std::string& s, char delim)
{
    std::vector<std::string> parts;
    std::stringstream ss(s);
    std::string item;
    while (std::getline(ss, item, delim))
        parts.push_back(Trim(item));
    return parts;
}

static DWORD ParseVkCode(const std::string& token)
{
    // Try parsing as hex number (e.g., "41" or "0x41")
    int val = 0;
    auto result = std::from_chars(token.data(), token.data() + token.size(), val, 16);
    if (result.ec == std::errc{} && result.ptr == token.data() + token.size())
        return (DWORD)val;

    // Try decimal
    result = std::from_chars(token.data(), token.data() + token.size(), val);
    if (result.ec == std::errc{} && result.ptr == token.data() + token.size())
        return (DWORD)val;

    return 0; // unknown
}

void PipeServer::HandleCommand(const std::string& cmd, HANDLE hPipe)
{
    std::string reply;

    if (cmd == "ENABLE")
    {
        m_hook.SetEnabled(true);
        m_hook.ResetBlockedCount();
        reply = "OK";
    }
    else if (cmd == "DISABLE")
    {
        m_hook.SetEnabled(false);
        reply = "OK";
    }
    else if (cmd == "STATUS")
    {
        reply = "BLOCKED=" + std::to_string(m_hook.GetBlockedCount());
    }
    else if (cmd == "QUIT")
    {
        reply = "OK";
        DWORD written;
        WriteFile(hPipe, reply.data(), (DWORD)reply.size(), &written, nullptr);
        FlushFileBuffers(hPipe);
        // Signal main thread to quit
        PostQuitMessage(0);
        return;
    }
    else if (cmd.rfind("ALLOW", 0) == 0 && cmd.size() > 6)
    {
        // "ALLOW vk1,vk2,vk3,..."
        std::string data = Trim(cmd.substr(5));
        auto tokens = Split(data, ',');
        std::unordered_set<DWORD> keys;
        for (auto& t : tokens)
        {
            DWORD vk = ParseVkCode(t);
            if (vk > 0) keys.insert(vk);
        }
        m_hook.SetAllowedKeys(keys);
        if (keys.empty())
            reply = "ERROR=no valid keys";
        else
            reply = "OK";
    }
    else
    {
        reply = "ERROR=unknown command";
    }

    DWORD written;
    WriteFile(hPipe, reply.data(), (DWORD)reply.size(), &written, nullptr);
    FlushFileBuffers(hPipe);
}

void PipeServer::Run()
{
    while (m_running)
    {
        m_hPipe = CreateNamedPipeW(
            PIPE_NAME,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            4096, 4096, 0, nullptr);

        if (m_hPipe == INVALID_HANDLE_VALUE)
        {
            Sleep(1000);
            continue;
        }

        bool connected = ConnectNamedPipe(m_hPipe, nullptr) ? true : (GetLastError() == ERROR_PIPE_CONNECTED);
        if (!connected)
        {
            CloseHandle(m_hPipe);
            m_hPipe = INVALID_HANDLE_VALUE;
            continue;
        }

        char buf[4096];
        DWORD read;
        if (ReadFile(m_hPipe, buf, sizeof(buf) - 1, &read, nullptr) && read > 0)
        {
            buf[read] = '\0';
            std::string cmd = Trim(buf);
            HandleCommand(cmd, m_hPipe);
        }

        DisconnectNamedPipe(m_hPipe);
        CloseHandle(m_hPipe);
        m_hPipe = INVALID_HANDLE_VALUE;
    }
}
