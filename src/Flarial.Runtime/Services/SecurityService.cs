using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;
using static Windows.Win32.PInvoke;
using static Windows.Win32.Security.TOKEN_ACCESS_MASK;
using static Windows.Win32.Security.TOKEN_INFORMATION_CLASS;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;

namespace Flarial.Runtime.Services;

static class SecurityService
{
    static readonly MANDATORY_LEVEL s_level = GetProcessIntegrity(GetCurrentProcessId());

    internal static bool IsProcessElevated(uint processId)
    {
        return GetProcessIntegrity(processId) <= s_level;
    }

    static unsafe MANDATORY_LEVEL GetProcessIntegrity(uint processId)
    {
        if (PROCESS_QUERY_LIMITED_INFORMATION.Open(processId) is not { } process)
            return MANDATORY_LEVEL.MandatoryLevelUntrusted;

        using (process)
        {
            HANDLE token = new();
            try
            {
                if (!OpenProcessToken(process, TOKEN_QUERY_SOURCE, &token))
                    return MANDATORY_LEVEL.MandatoryLevelUntrusted;

                uint length = 0;
                GetTokenInformation(token, TokenIntegrityLevel, null, 0, &length);

                var info = NativeMemory.Alloc<TOKEN_MANDATORY_LABEL>(length);
                try
                {
                    if (!GetTokenInformation(token, TokenIntegrityLevel, info, length, &length))
                        return MANDATORY_LEVEL.MandatoryLevelUntrusted;

                    var authority = GetSidSubAuthorityCount(info->Label.Sid) - 1;
                    var level = *GetSidSubAuthority(info->Label.Sid, *authority) >> 12;

                    return (MANDATORY_LEVEL)level;
                }
                finally
                {
                    NativeMemory.Free(info);
                }
            }
            finally
            {
                CloseHandle(token);
            }
        }
    }
}