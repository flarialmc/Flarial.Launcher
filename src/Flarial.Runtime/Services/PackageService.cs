using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Foundation;
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

    internal static Package? Get(string packageFamilyName) => s_manager.FindPackagesForUser(string.Empty, packageFamilyName).FirstOrDefault();

    internal unsafe static void Add(Uri uri, Action<int> callback)
    {
        var handle = CreateEvent(null, true, false, null);
        var info = s_manager.AddPackageByUriAsync(uri, s_options);

        try
        {
            info.Progress += OnProgress;
            info.Completed += OnCompleted;

            WaitForSingleObject(handle, INFINITE);
            if (info.Status is Error) throw info.ErrorCode;
        }
        finally
        {
            CloseHandle(handle);
            info.Close();
        }

        void OnCompleted(object sender, AsyncStatus args) => SetEvent(handle);
        void OnProgress(object sender, DeploymentProgress args) => callback((int)args.percentage);
    }
}