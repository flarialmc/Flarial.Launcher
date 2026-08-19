using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Flarial.Runtime.Services;

static class PackageService
{
    static readonly PackageManager s_manager = new();

    static readonly AddPackageOptions s_options = new()
    {
        ForceAppShutdown = true,
        ForceUpdateFromAnyVersion = true
    };

    internal static Package? Get(string packageFamilyName)
    {
        return s_manager.FindPackagesForUser(string.Empty, packageFamilyName).FirstOrDefault();
    }

    internal static Task AddAsync<T>(Uri packageUri, T progress) where T : IProgress<DeploymentProgress>
    {
        return s_manager.AddPackageByUriAsync(packageUri, s_options).AsTask(default, progress);
    }
}