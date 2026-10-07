using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
namespace CipherRain;
public static class Ipc
{
    public const string Controller = "CipherRain.Control.v1";
    public static string Host(int pid) => "CipherRain.Host." + pid;
    public static async Task<string> Send(string pipe, string command, int timeout = 2000)
    {
        using var client = new NamedPipeClientStream(".", pipe, PipeDirection.InOut, PipeOptions.Asynchronous);
        using var cancel = new CancellationTokenSource(timeout);
        await client.ConnectAsync(cancel.Token);
        using var writer = new StreamWriter(client, new System.Text.UTF8Encoding(false), 1024, true) { AutoFlush = true };
        using var reader = new StreamReader(client, System.Text.Encoding.UTF8, true, 1024, true);
        await writer.WriteLineAsync(command.AsMemory(), cancel.Token);
        return await reader.ReadLineAsync(cancel.Token) ?? "";
    }
    public static Task Listen(string name, Func<string, Task<string>> handler, CancellationToken token) => Task.Run(async () => { while (!token.IsCancellationRequested) { try { using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly); await server.WaitForConnectionAsync(token); using var reader = new StreamReader(server, System.Text.Encoding.UTF8, true, 1024, true); using var writer = new StreamWriter(server, new System.Text.UTF8Encoding(false), 1024, true) { AutoFlush = true }; var command = await reader.ReadLineAsync(token); if (command != null && command.Length < 1500000) await writer.WriteLineAsync(await handler(command)); } catch (OperationCanceledException) { break; } catch (Exception e) { Log.Write("IPC", e.GetType().Name); await Task.Delay(100, token).ConfigureAwait(false); } } }, token);
}
public static class Log
{
    static readonly object Gate = new();
    public static void Write(string area, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Settings.DataDir);
                string path = Path.Combine(Settings.DataDir, "CipherRain.log");
                if (File.Exists(path) && new FileInfo(path).Length > 512000)
                    File.Move(path, path + ".1", true);
                File.AppendAllText(path, DateTime.UtcNow.ToString("O") + " " + area + " " + message + Environment.NewLine);
            }
        }
        catch { }
    }
}
