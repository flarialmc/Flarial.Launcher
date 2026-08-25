using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using static Windows.Foundation.AsyncStatus;
using static Windows.Win32.PInvoke;

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
        var packages = s_manager.FindPackagesForUser(string.Empty, packageFamilyName);
        return packages.FirstOrDefault();
    }

    internal static Task AddAsync<T>(Uri packageUri, T progress) where T : IProgress<DeploymentProgress>
    {
        var args = (packageUri, progress);

        static void Action(in (Uri, T) args)
        {
            Add(args.Item1, args.Item2);
        }

        return Task.Run(Action, args);
    }

    unsafe static void Add<T>(Uri packageUri, T progress) where T : IProgress<DeploymentProgress>
    {
        var handle = CreateEvent(null, false, false, null);
        var operation = s_manager.AddPackageByUriAsync(packageUri, s_options);
        try
        {
            operation.Completed += (_, _) => SetEvent(handle);
            operation.Progress += (sender, args) => progress.Report(args);

            WaitForSingleObject(handle, INFINITE);
            if (operation.Status is Error) throw operation.ErrorCode;
        }
        finally
        {
            CloseHandle(handle);
            operation.Close();
        }
    }
}