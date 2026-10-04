// Fake Windows.Security.Credentials.PasswordVault / PasswordCredential for Wine (which ships no windows.security.credentials.dll).
// Registered under HKLM\Software\Microsoft\WindowsRuntime\ActivatableClassId by the launcher's PrefixManager.
// Store: C:\ProgramData\Flarial\Vault\vault.dat, one entry per line: hex(resource) ' ' hex(user) ' ' hex(password), UTF-8 inside the hex.
// The Linux launcher (LinuxCredentialStore) reads/writes the same file. Never logs secrets.
#include <windows.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Security.Credentials.h>
#include <mutex>
#include <string>
#include <vector>

using namespace winrt;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Security::Credentials;

namespace {

constexpr wchar_t kDir[] = L"C:\\ProgramData\\Flarial\\Vault";
constexpr wchar_t kFile[] = L"C:\\ProgramData\\Flarial\\Vault\\vault.dat";
constexpr wchar_t kTemp[] = L"C:\\ProgramData\\Flarial\\Vault\\vault.dat.tmp";
const HRESULT kNotFound = HRESULT_FROM_WIN32(ERROR_NOT_FOUND);

struct Entry { std::string resource, user, password; };

// ponytail: in-process mutex only; a launcher write racing a client write can lose one update (read-modify-write, last rename wins).
std::mutex g_lock;

std::string Hex(const std::string& s) {
    static const char d[] = "0123456789abcdef";
    std::string o;
    for (unsigned char c : s) { o += d[c >> 4]; o += d[c & 15]; }
    return o;
}

std::string Unhex(const std::string& s) {
    auto v = [](char c) { return c <= '9' ? c - '0' : (c | 32) - 'a' + 10; };
    std::string o;
    for (size_t i = 0; i + 1 < s.size(); i += 2) o += char(v(s[i]) << 4 | v(s[i + 1]));
    return o;
}

std::vector<Entry> Load() {
    std::vector<Entry> all;
    HANDLE h = CreateFileW(kFile, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, nullptr, OPEN_EXISTING, 0, nullptr);
    if (h == INVALID_HANDLE_VALUE) return all;
    std::string data; char buf[4096]; DWORD n;
    while (ReadFile(h, buf, sizeof buf, &n, nullptr) && n) data.append(buf, n);
    CloseHandle(h);
    size_t pos = 0;
    while (pos < data.size()) {
        size_t end = data.find('\n', pos); if (end == std::string::npos) end = data.size();
        std::string line = data.substr(pos, end - pos); pos = end + 1;
        if (!line.empty() && line.back() == '\r') line.pop_back();
        size_t a = line.find(' '), b = a == std::string::npos ? a : line.find(' ', a + 1);
        if (b == std::string::npos) continue;
        all.push_back({ Unhex(line.substr(0, a)), Unhex(line.substr(a + 1, b - a - 1)), Unhex(line.substr(b + 1)) });
    }
    return all;
}

void Save(const std::vector<Entry>& all) {
    std::string data;
    for (auto& e : all) data += Hex(e.resource) + ' ' + Hex(e.user) + ' ' + Hex(e.password) + '\n';
    CreateDirectoryW(L"C:\\ProgramData\\Flarial", nullptr);
    CreateDirectoryW(kDir, nullptr);
    HANDLE h = CreateFileW(kTemp, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) throw_last_error();
    DWORD n; bool ok = WriteFile(h, data.data(), (DWORD)data.size(), &n, nullptr) && n == data.size() && FlushFileBuffers(h);
    CloseHandle(h);
    if (!ok || !MoveFileExW(kTemp, kFile, MOVEFILE_REPLACE_EXISTING)) throw_last_error();
}

struct Credential : implements<Credential, IPasswordCredential> {
    hstring m_resource, m_user, m_password;
    bool m_hidden = false; // like Windows: a credential from Retrieve has no password until RetrievePassword()

    hstring Resource() { return m_resource; }
    void Resource(hstring const& v) { m_resource = v; }
    hstring UserName() { return m_user; }
    void UserName(hstring const& v) { m_user = v; }
    hstring Password() { return m_hidden ? hstring{} : m_password; }
    void Password(hstring const& v) { m_password = v; m_hidden = false; }
    void RetrievePassword() { m_hidden = false; }
    Collections::IPropertySet Properties() { throw_hresult(E_NOTIMPL); }
};

PasswordCredential MakeCredential(hstring r, hstring u, hstring p, bool hidden) {
    auto c = make_self<Credential>();
    c->m_resource = r; c->m_user = u; c->m_password = p; c->m_hidden = hidden;
    return c.as<PasswordCredential>();
}

struct Vault : implements<Vault, IPasswordVault> {
    void Add(PasswordCredential const& c) {
        std::lock_guard lock(g_lock);
        Entry n{ to_string(c.Resource()), to_string(c.UserName()), to_string(c.Password()) };
        auto all = Load(); bool found = false;
        for (auto& e : all) if (e.resource == n.resource && e.user == n.user) { e = n; found = true; }
        if (!found) all.push_back(n);
        Save(all);
    }
    void Remove(PasswordCredential const& c) {
        std::lock_guard lock(g_lock);
        auto r = to_string(c.Resource()), u = to_string(c.UserName());
        auto all = Load(); auto before = all.size();
        std::erase_if(all, [&](const Entry& e) { return e.resource == r && e.user == u; });
        if (all.size() == before) throw_hresult(kNotFound);
        Save(all);
    }
    Collections::IVectorView<PasswordCredential> Find(const std::string* r, const std::string* u, bool throwIfEmpty) {
        std::lock_guard lock(g_lock);
        std::vector<PasswordCredential> out;
        for (auto& e : Load())
            if ((!r || e.resource == *r) && (!u || e.user == *u))
                out.push_back(MakeCredential(to_hstring(e.resource), to_hstring(e.user), to_hstring(e.password), true));
        if (out.empty() && throwIfEmpty) throw_hresult(kNotFound);
        return single_threaded_vector(std::move(out)).GetView();
    }
    PasswordCredential Retrieve(hstring const& resource, hstring const& user) {
        auto r = to_string(resource), u = to_string(user);
        return Find(&r, &u, true).GetAt(0);
    }
    Collections::IVectorView<PasswordCredential> FindAllByResource(hstring const& resource) { auto r = to_string(resource); return Find(&r, nullptr, true); }
    Collections::IVectorView<PasswordCredential> FindAllByUserName(hstring const& user) { auto u = to_string(user); return Find(nullptr, &u, true); }
    Collections::IVectorView<PasswordCredential> RetrieveAll() { return Find(nullptr, nullptr, false); }
};

struct VaultFactory : implements<VaultFactory, IActivationFactory> {
    IInspectable ActivateInstance() { return make<Vault>(); }
};

struct CredentialFactory : implements<CredentialFactory, IActivationFactory, ICredentialFactory> {
    IInspectable ActivateInstance() { return make<Credential>(); }
    PasswordCredential CreatePasswordCredential(hstring const& r, hstring const& u, hstring const& p) { return MakeCredential(r, u, p, false); }
};

}

extern "C" HRESULT __stdcall DllGetActivationFactory(void* classId, void** factory) try {
    *factory = nullptr;
    hstring const& name = *reinterpret_cast<hstring const*>(&classId);
    if (name == L"Windows.Security.Credentials.PasswordVault") *factory = detach_abi(make<VaultFactory>());
    else if (name == L"Windows.Security.Credentials.PasswordCredential") *factory = detach_abi(make<CredentialFactory>());
    else return CLASS_E_CLASSNOTAVAILABLE;
    return S_OK;
} catch (...) { return to_hresult(); }

extern "C" HRESULT __stdcall DllCanUnloadNow() { return S_FALSE; }
