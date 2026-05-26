#include "pipe_server.h"
#include "hook.h"
#include <vector>
#include <sstream>
#include <algorithm>
#include <cctype>

bool PipeServer::Start()
{
    if (m_running.load()) return true;
    m_running.store(true);
    m_thread = std::thread(&PipeServer::Run, this);
    return true;
}

void PipeServer::Stop()
{
    m_running.store(false);
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
    try
    {
        size_t pos = 0;
        int val = 0;
        if (token.size() >= 2 && (token[0] == '0' && (token[1] == 'x' || token[1] == 'X')))
        {
            val = std::stoi(token.substr(2), &pos, 16);
            if (pos == token.size() - 2)
                return (DWORD)val;
        }
        else
        {
            val = std::stoi(token, &pos, 16);
            if (pos == token.size())
                return (DWORD)val;
        }
        val = std::stoi(token, &pos, 10);
        if (pos == token.size())
            return (DWORD)val;
    }
    catch (const std::exception&)
    {
    }
    return 0;
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
        PostQuitMessage(0);
        return;
    }
    else if (cmd.rfind("ALLOW", 0) == 0 && cmd.size() > 6)
    {
        std::string data = Trim(cmd.substr(5));
        auto tokens = Split(data, ',');
        std::unordered_set<DWORD> keys;
        for (auto& t : tokens)
        {
            DWORD vk = ParseVkCode(t);
            if (vk > 0) keys.insert(vk);
        }
        m_hook.SetAllowedKeys(keys);
        m_hook.SetEnabled(true);
        if (keys.empty())
            reply = "ERROR=no valid keys";
        else
            reply = "OK";
    }
    else if (cmd == "HEARTBEAT")
    {
        m_hook.Heartbeat();
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
    while (m_running.load())
    {
        HANDLE hPipe = CreateNamedPipeW(
            PIPE_NAME,
            PIPE_ACCESS_DUPLEX,
            PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
            PIPE_UNLIMITED_INSTANCES,
            4096, 4096, 0, nullptr);

        if (hPipe == INVALID_HANDLE_VALUE)
        {
            Sleep(1000);
            continue;
        }

        // Store handle for Stop() to cancel blocking ConnectNamedPipe
        {
            std::lock_guard<std::mutex> lock(m_handleMutex);
            m_hPipe = hPipe;
        }

        bool connected = ConnectNamedPipe(hPipe, nullptr) ? true : (GetLastError() == ERROR_PIPE_CONNECTED);
        if (!connected)
        {
            std::lock_guard<std::mutex> lock(m_handleMutex);
            CloseHandle(hPipe);
            m_hPipe = INVALID_HANDLE_VALUE;
            continue;
        }

        // Read multiple commands on the same connection (persistent pipe)
        char buf[4096];
        while (m_running.load())
        {
            DWORD read = 0;
            if (!ReadFile(hPipe, buf, sizeof(buf) - 1, &read, nullptr) || read == 0)
                break;
            buf[read] = '\0';
            std::string cmd = Trim(buf);
            if (!cmd.empty())
                HandleCommand(cmd, hPipe);
        }

        DisconnectNamedPipe(hPipe);
        {
            std::lock_guard<std::mutex> lock(m_handleMutex);
            CloseHandle(hPipe);
            m_hPipe = INVALID_HANDLE_VALUE;
        }
    }
}
