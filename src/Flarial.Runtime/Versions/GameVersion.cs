using System;
using Windows.ApplicationModel;

namespace Flarial.Runtime.Versions;

sealed class GameVersion
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

    public override string ToString()
    {
        if (_minor >= 26) return $"{_minor}.{_build}";
        else return $"{_major}.{_minor}.{_build}";
    }
}