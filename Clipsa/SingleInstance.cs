using System.IO;
using System.IO.Pipes;
using System.Security.Principal;

namespace Clipsa;

internal sealed class SingleInstance : IDisposable
{
    public const string MutexName = @"Local\Clipsa.Paco.Windows.v1";
    private readonly Mutex mutex = new(false, MutexName);
    private bool owns;
    private readonly CancellationTokenSource cancellation = new();
    private static string PipeName => "Clipsa-" + WindowsIdentity.GetCurrent().User!.Value.Replace('-', '_');
    public bool Acquire()
    {
        try { owns = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { owns = true; }
        return owns;
    }

    public static async Task NotifyExisting()
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(2000).ConfigureAwait(false);
            using var writer = new StreamWriter(pipe); await writer.WriteLineAsync("show").ConfigureAwait(false); await writer.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) { AppDiagnostics.Log(ex, "Activar instancia existente"); }
    }

    public async Task Listen(Action show)
    {
        while (!cancellation.IsCancellationRequested)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(cancellation.Token);
                using var reader = new StreamReader(pipe);
                if (await reader.ReadLineAsync(cancellation.Token) == "show") show();
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { AppDiagnostics.Log(ex, "Canal de instancia"); await Task.Delay(250, cancellation.Token); }
        }
    }

    public void Dispose() { cancellation.Cancel(); if (owns) { mutex.ReleaseMutex(); owns = false; } mutex.Dispose(); cancellation.Dispose(); }
}
