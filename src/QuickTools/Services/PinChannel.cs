using System.IO.Pipes;
using System.Text;

namespace QuickTools.Services;

/// <summary>
/// Carries "pin this path" from the short-lived process Explorer starts to the QuickTools already
/// running in the tray, over a pipe only the current user can open.
/// </summary>
internal static class PinChannel
{
    private const string PipeName = "QuickTools-Pin-5E0C9B7A";

    /// <summary>Sends the path to the running instance; false if none answered in time.</summary>
    public static bool TrySend(string path, int timeoutMilliseconds = 3000)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(timeoutMilliseconds);
            using var writer = new StreamWriter(client, new UTF8Encoding(false));
            writer.WriteLine(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or TimeoutException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Listens for paths on a background thread. <c>onPath</c> runs on that thread, so it must marshal
    /// to the UI thread itself.
    /// </summary>
    public sealed class Server : IDisposable
    {
        private readonly CancellationTokenSource _cancel = new();

        public Server(Action<string> onPath) => Task.Run(() => ListenAsync(onPath, _cancel.Token));

        public void Dispose() => _cancel.Cancel();

        private static async Task ListenAsync(Action<string> onPath, CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await server.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(server, new UTF8Encoding(false));
                    if (await reader.ReadLineAsync(token) is { Length: > 0 } path)
                    {
                        onPath(path);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (IOException)
                {
                    // A client that disconnected early must not stop the listener.
                }
            }
        }
    }
}
