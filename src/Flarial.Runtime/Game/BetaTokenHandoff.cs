using System;
using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Flarial.Runtime.Unmanaged;
using Windows.Win32.Foundation;
using static Windows.Win32.PInvoke;
using static Windows.Win32.System.LibraryLoader.LOAD_LIBRARY_FLAGS;
using static Windows.Win32.System.ProcessStatus.ENUM_PROCESS_MODULES_EX_FLAGS;
using static Windows.Win32.System.Threading.PROCESS_ACCESS_RIGHTS;

namespace Flarial.Runtime.Game;

static class BetaTokenHandoff
{
    internal static unsafe bool Deliver(uint processId, string path, string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken) || accessToken.IndexOfAny(['\r', '\n', '\0']) >= 0)
            return false;

        var token = Encoding.UTF8.GetBytes(accessToken);
        var payload = new byte[4100];
        try
        {
            if (token.Length > 4096) return false;
            BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)token.Length);
            token.CopyTo(payload, 4);

            path = Path.GetFullPath(path);
            if (DONT_RESOLVE_DLL_REFERENCES.Open(path) is not { } module) return false;
            using (module)
            {
                fixed (byte* name = "FlarialBetaAccessTokenHandoff"u8)
                {
                    var export = GetProcAddress(module, new(name));
                    if (export.IsNull) return false;
                    var offset = (nint)export - (nint)((HMODULE)module).Value;

                    if ((PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE |
                         PROCESS_VM_OPERATION).Open(processId) is not { } process) return false;
                    using (process)
                    {
                        var modules = new HMODULE[4096];
                        uint needed;
                        fixed (HMODULE* handles = modules)
                        {
                            if (!EnumProcessModulesEx(process, handles,
                                    (uint)(modules.Length * sizeof(HMODULE)), &needed, LIST_MODULES_64BIT) ||
                                needed > modules.Length * sizeof(HMODULE)) return false;
                        }

                        var filename = new char[32768];
                        for (var index = 0; index < needed / sizeof(HMODULE); ++index)
                        {
                            fixed (char* filenamePtr = filename)
                            {
                                var length = GetModuleFileNameEx(process, modules[index],
                                    filenamePtr, (uint)filename.Length);
                                if (length == 0 || length >= filename.Length ||
                                    !string.Equals(new string(filenamePtr, 0, (int)length), path,
                                        StringComparison.OrdinalIgnoreCase)) continue;
                            }

                            var target = (byte*)modules[index].Value + offset;
                            nuint written;
                            fixed (byte* payloadPtr = payload)
                            {
                                if (!WriteProcessMemory(process, target + 4, payloadPtr,
                                        (nuint)payload.Length, &written) ||
                                    written != (nuint)payload.Length) return false;
                            }
                            uint ready = 1;
                            return WriteProcessMemory(process, target, &ready, sizeof(uint), &written) &&
                                written == sizeof(uint);
                        }
                    }
                }
            }
            return false;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(token);
            CryptographicOperations.ZeroMemory(payload);
        }
    }
}
