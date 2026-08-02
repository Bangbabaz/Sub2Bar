using System.IO;
using System.IO.Pipes;
using System.Text;

namespace Sub2Bar.Windows.Infrastructure;

public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\com.sub2bar.windows";
    private const string PipeName = "com.sub2bar.windows.activate";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listenTask;

    public SingleInstance()
    {
        _mutex = new Mutex(true, MutexName, out var isPrimary);
        IsPrimary = isPrimary;
    }

    public bool IsPrimary { get; }

    public void StartListening(Action activationRequested)
    {
        if (!IsPrimary || _listenTask is not null)
        {
            return;
        }

        _listenTask = ListenAsync(activationRequested, _cancellation.Token);
    }

    public static async Task SignalPrimaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out,
                PipeOptions.Asynchronous);
            await client.ConnectAsync(800, cancellationToken);
            var payload = Encoding.UTF8.GetBytes("show");
            await client.WriteAsync(payload, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or TimeoutException or OperationCanceledException)
        {
            // The first instance may still be starting; a duplicate process should exit either way.
        }
    }

    private static async Task ListenAsync(Action activationRequested, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken);
                var buffer = new byte[16];
                var count = await server.ReadAsync(buffer, cancellationToken);
                if (Encoding.UTF8.GetString(buffer, 0, count).Equals("show", StringComparison.Ordinal))
                {
                    activationRequested();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (IOException)
            {
                // Recreate the pipe after a client disconnects unexpectedly.
            }
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _cancellation.Dispose();
    }
}
