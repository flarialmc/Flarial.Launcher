using System;
using System.IO;
using Flarial.Runtime.Exceptions;

namespace Flarial.Runtime.Game;

public sealed class Library(string? path)
{
    /// <summary>Validates the file is a PE image with the DLL characteristic by reading its headers (no code is loaded).</summary>
    public bool IsLoadable
    {
        get
        {
            if (_path is null) return false;
            try
            {
                using var stream = File.OpenRead(_path);
                Span<byte> dos = stackalloc byte[64];
                if (stream.Read(dos) != 64 || dos[0] != 'M' || dos[1] != 'Z') return false;

                stream.Position = BitConverter.ToInt32(dos[60..]);
                Span<byte> nt = stackalloc byte[24];
                if (stream.Read(nt) != 24 || nt[0] != 'P' || nt[1] != 'E' || nt[2] != 0 || nt[3] != 0) return false;

                const ushort IMAGE_FILE_DLL = 0x2000;
                return (BitConverter.ToUInt16(nt[22..]) & IMAGE_FILE_DLL) != 0;
            }
            catch { return false; }
        }
    }

    internal string EnsureLoadable()
    {
        if (_path is null)
            throw new LibraryLoadFailureException();

        if (!IsLoadable)
            throw new LibraryLoadFailureException();

        return _path;
    }

    readonly string? _path = Path.HasExtension(path) ? Path.GetFullPath(path) : null;
}
