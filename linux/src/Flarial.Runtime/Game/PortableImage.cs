using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.PortableExecutable;

namespace Flarial.Runtime.Game;

/// <summary>
/// Managed view of a 64-bit PE DLL (headers, import and export tables) so that the checks upstream performs through
/// LoadLibraryEx(DONT_RESOLVE_DLL_REFERENCES) + GetProcAddress work without loading any code and on any OS.
/// </summary>
sealed class PortableImage
{
    internal required IReadOnlyList<string> Imports { get; init; }
    internal required IReadOnlySet<string> Exports { get; init; }

    /// <summary>Null when the file is not a loadable x64 DLL (same outcome as a failing LoadLibraryEx + IMAGE_FILE_DLL check).</summary>
    internal static PortableImage? Open(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using PEReader reader = new(stream);

            var headers = reader.PEHeaders;
            var header = headers.PEHeader;

            if (!headers.IsDll || header is not { Magic: PEMagic.PE32Plus })
                return null;

            if (headers.CoffHeader.Machine is not Machine.Amd64)
                return null;

            return new()
            {
                Imports = ReadImports(reader, header),
                Exports = ReadExports(reader, header)
            };
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    static string ReadString(PEReader reader, int rva)
    {
        var block = reader.GetSectionData(rva);
        if (block.Length == 0) throw new BadImageFormatException();

        var blob = block.GetReader();
        var length = blob.IndexOf(0);
        if (length < 0) throw new BadImageFormatException();

        return blob.ReadUTF8(length);
    }

    static List<string> ReadImports(PEReader reader, PEHeader header)
    {
        List<string> imports = [];
        var directory = header.ImportTableDirectory;
        if (directory.RelativeVirtualAddress <= 0) return imports;

        var block = reader.GetSectionData(directory.RelativeVirtualAddress);
        if (block.Length == 0) throw new BadImageFormatException();

        // IMAGE_IMPORT_DESCRIPTOR is 20 bytes; Name is the fourth DWORD. The table ends at a zeroed entry.
        var blob = block.GetReader();
        while (blob.RemainingBytes >= 20)
        {
            _ = blob.ReadInt32(); _ = blob.ReadInt32(); _ = blob.ReadInt32();
            var name = blob.ReadInt32(); _ = blob.ReadInt32();

            if (name <= 0) break;
            imports.Add(ReadString(reader, name));
        }

        return imports;
    }

    static HashSet<string> ReadExports(PEReader reader, PEHeader header)
    {
        HashSet<string> exports = new(StringComparer.Ordinal);
        var directory = header.ExportTableDirectory;
        if (directory.RelativeVirtualAddress <= 0) return exports;

        var block = reader.GetSectionData(directory.RelativeVirtualAddress);
        if (block.Length < 40) throw new BadImageFormatException();

        // IMAGE_EXPORT_DIRECTORY: NumberOfNames at +24, AddressOfNames at +32.
        var blob = block.GetReader();
        blob.Offset = 24;
        var count = blob.ReadInt32();
        blob.Offset = 32;
        var names = blob.ReadInt32();
        if (count < 0 || count > 1 << 20) throw new BadImageFormatException();
        if (count == 0) return exports;

        var table = reader.GetSectionData(names);
        if (table.Length < count * 4) throw new BadImageFormatException();

        var entries = table.GetReader();
        for (var index = 0; index < count; index++)
            exports.Add(ReadString(reader, entries.ReadInt32()));

        return exports;
    }
}
