using System;
using System.Buffers.Binary;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace Flarial.Runtime.Game;

// One short-lived access token, delivered only to the target process under the same user.
sealed class BetaTokenHandoff : IDisposable
{
    readonly NamedPipeServerStream _pipe;
    readonly CancellationTokenSource _timeout = new(TimeSpan.FromSeconds(30));
    readonly Task _delivery;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    internal BetaTokenHandoff(uint processId, string accessToken)
    {
        if (string.IsNullOrEmpty(accessToken) || accessToken.Length > 4096)
            throw new ArgumentException("Invalid beta access token.", nameof(accessToken));
        _pipe = new NamedPipeServerStream($"Flarial.Beta.Auth.{processId}", PipeDirection.Out, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _delivery = DeliverAsync(processId, accessToken);
    }

    async Task DeliverAsync(uint processId, string accessToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(accessToken);
        try
        {
            while (true)
            {
                await _pipe.WaitForConnectionAsync(_timeout.Token).ConfigureAwait(false);
                if (GetNamedPipeClientProcessId(_pipe.SafePipeHandle, out var client) && client == processId)
                    break;
                _pipe.Disconnect();
            }
            byte[] prefix = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(prefix, bytes.Length);
            await _pipe.WriteAsync(prefix, _timeout.Token).ConfigureAwait(false);
            await _pipe.WriteAsync(bytes, _timeout.Token).ConfigureAwait(false);
            await _pipe.FlushAsync(_timeout.Token).ConfigureAwait(false);
        }
        finally { Array.Clear(bytes); }
    }

    internal bool WaitForDelivery()
    {
        try { _delivery.GetAwaiter().GetResult(); return true; }
        catch (IOException) { return false; }
        catch (OperationCanceledException) { return false; }
    }

    public void Dispose()
    {
        _timeout.Cancel();
        _pipe.Dispose();
        try { _delivery.GetAwaiter().GetResult(); } catch (IOException) { }
        catch (OperationCanceledException) { } catch (ObjectDisposedException) { }
        _timeout.Dispose();
    }
}
