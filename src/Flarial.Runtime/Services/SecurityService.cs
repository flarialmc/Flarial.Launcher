using System.Runtime.InteropServices;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.Security;
using static Windows.Win32.PInvoke;
using static Windows.Win32.Security.TOKEN_ACCESS_MASK;
using static Windows.Win32.Security.TOKEN_INFORMATION_CLASS;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;

namespace Flarial.Runtime.Services;

static class SecurityService
{
    static readonly uint? s_level = GetProcessIntegrity(GetCurrentProcessId());

    internal static bool IsProcessElevated(uint processId)
    {
        if (GetProcessIntegrity(processId) is { } level)
            return s_level is { } && s_level < level;
        return true;
    }

    static uint? GetProcessIntegrity(uint processId)
    {
        if (PROCESS_QUERY_LIMITED_INFORMATION.Open(processId) is not { } process)
            return null;

        using (process)
        {
            if (TOKEN_QUERY.Open(process) is not { } token)
                return null;

            using (token)
                return GetProcessIntegrity(token);
        }
    }

    static unsafe uint? GetProcessIntegrity(in NativeProcessToken token)
    {
        uint length = 0;
        GetTokenInformation(token, TokenIntegrityLevel, null, 0, &length);

        var info = NativeMemory.Alloc<TOKEN_MANDATORY_LABEL>(length);
        try
        {
            if (!GetTokenInformation(token, TokenIntegrityLevel, info, length, &length))
                return null;

            var count = (uint*)GetSidSubAuthorityCount(info->Label.Sid);
            if (count == null || *count < 0) return null;

            var rid = GetSidSubAuthority(info->Label.Sid, (*count) - 1);
            return rid != null ? *rid : null;
        }
        finally
        {
            NativeMemory.Free(info);
        }
    }
}