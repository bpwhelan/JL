using System.IO;
using System.Net.WebSockets;
using System.Text;
using JL.Core;
using JL.Core.Utilities;
using JL.Windows.GUI;

namespace JL.Windows.Utilities;
internal static class WebSocketUtils
{
    private static Task? s_webSocketTask = null;
    private static CancellationTokenSource? s_webSocketCancellationTokenSource = null;
    public static void HandleWebSocket()
    {
        if (!ConfigManager.CaptureTextFromWebSocket)
        {
            s_webSocketTask = null;
        }
        else if (s_webSocketTask is null)
        {
            s_webSocketCancellationTokenSource?.Dispose();
            s_webSocketCancellationTokenSource = new();
            ListenWebSocket(s_webSocketCancellationTokenSource.Token);
        }
        else
        {
            s_webSocketCancellationTokenSource!.Cancel();
            s_webSocketCancellationTokenSource.Dispose();
            s_webSocketCancellationTokenSource = new();
            ListenWebSocket(s_webSocketCancellationTokenSource.Token);
        }
    }

    public static void ListenWebSocket(CancellationToken cancellationToken)
    {
        s_webSocketTask = Task.Factory.StartNew(async () =>
        {
            try
            {
                using ClientWebSocket webSocketClient = new();
                await webSocketClient.ConnectAsync(ConfigManager.WebSocketUri, CancellationToken.None).ConfigureAwait(false);
                byte[] buffer = new byte[1024];

                while (ConfigManager.CaptureTextFromWebSocket && !cancellationToken.IsCancellationRequested && webSocketClient.State == WebSocketState.Open)
                {
                    try
                    {
                        WebSocketReceiveResult result = await webSocketClient.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);

                        if (!ConfigManager.CaptureTextFromWebSocket || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        if (result.MessageType == WebSocketMessageType.Text)
                        {
                            using MemoryStream memoryStream = new();
                            memoryStream.Write(buffer, 0, result.Count);

                            while (!result.EndOfMessage)
                            {
                                result = await webSocketClient.ReceiveAsync(buffer, CancellationToken.None).ConfigureAwait(false);
                                memoryStream.Write(buffer, 0, result.Count);
                            }

                            _ = memoryStream.Seek(0, SeekOrigin.Begin);

                            string text = Encoding.UTF8.GetString(memoryStream.ToArray());
                            _ = Task.Run(async () => await MainWindow.Instance.CopyFromWebSocket(text).ConfigureAwait(false)).ConfigureAwait(false);
                        }
                    }
                    catch (WebSocketException webSocketException)
                    {
                        Utils.Logger.Warning(webSocketException, "WebSocket server is closed unexpectedly");
                        Storage.Frontend.Alert(AlertLevel.Error, "WebSocket server is closed");
                        break;
                    }
                }
            }

            catch (WebSocketException webSocketException)
            {
                Utils.Logger.Warning(webSocketException, "Couldn't connect to the WebSocket server, probably because it is not running");
                Storage.Frontend.Alert(AlertLevel.Error, "Couldn't connect to the WebSocket server, probably because it is not running");
            }
        }, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }
}