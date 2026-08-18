using System;
using Windows.ApplicationModel;

namespace Flarial.Runtime.Versions;

readonly struct GameVersion
{
    internal readonly int _major, _minor, _build;

    internal GameVersion(string version)
    {
        Version value = new(version);
        _major = value.Major;
        _minor = value.Minor;
        _build = value.Build;
    }

    internal GameVersion(PackageVersion version)
    {
        _major = version.Major;
        _minor = version.Minor;
        _build = version.Build / 100;
    }

    GameVersion(int major, int minor, int build)
    {
        _major = major;
        _minor = minor;
        _build = build;
    }

    internal GameVersion Truncate()
    {
        var build = _build / 10 * 10;
        return new(_major, _minor, build);
    }

    public override string ToString()
    {
        if (_minor >= 26) return $"{_minor}.{_build}";
        else return $"{_major}.{_minor}.{_build}";
    }

    public override int GetHashCode() => HashCode.Combine(_major, _minor, _build);
}