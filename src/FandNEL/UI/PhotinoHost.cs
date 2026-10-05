using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Photino.NET;
using Serilog;
using FandNEL.Gateway.Management;

namespace FandNEL.UI;

/// <summary>使用本地浏览器窗口承载 React UI，窗口和网关共享同一份静态资源。</summary>
public sealed class PhotinoHost : IDisposable
{
    private const string ActionField = "action";
    private const string DragAction = "window:drag";
    private const string MinimizeAction = "window:minimize";
    private const string MaximizeAction = "window:maximize";
    private const string CloseAction = "window:close";
    private const uint WmNcLButtonDown = 0xA1;
    private const int HtCaption = 2;

    private readonly GatewayRuntime _runtime;
    private PhotinoWindow? _window;

    public PhotinoHost(GatewayRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public void Run()
    {
        var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        if (!File.Exists(indexPath))
        {
            Log.Error("找不到 React UI 入口文件：{Path}", indexPath);
            return;
        }

        var dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FandNEL", "WebView");
        Directory.CreateDirectory(dataPath);

        _window = new PhotinoWindow()
            .SetTitle("FandNEL")
            .SetChromeless(true)
            .SetGrantBrowserPermissions(true)
            .SetTemporaryFilesPath(dataPath)
            .SetSize(1200, 750)
            .SetMinSize(900, 600)
            .SetUseOsDefaultSize(false)
            .Center()
            .RegisterWebMessageReceivedHandler(HandleWindowMessage)
            .Load(_runtime.WebSocket.HttpAddress);

        Log.Information("FandNEL UI loaded from {Address}", _runtime.WebSocket.HttpAddress);
        _window.WaitForClose();
        _window = null;
    }

    private static void HandleWindowMessage(object? sender, string message)
    {
        if (sender is not PhotinoWindow window)
            return;

        string? action;
        try
        {
            using var document = JsonDocument.Parse(message);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty(ActionField, out var field) || field.ValueKind != JsonValueKind.String)
            {
                Log.Warning("原生窗口消息缺少有效的 action 字段。");
                return;
            }

            action = field.GetString();
        }
        catch (JsonException)
        {
            Log.Warning("原生窗口消息不是有效的 JSON。");
            return;
        }

        // 本地消息桥只处理窗口操作，游戏与账号请求继续通过 WebSocket 网关。
        if (action is not (DragAction or MinimizeAction or MaximizeAction or CloseAction))
            return;

        window.Invoke(() =>
        {
            switch (action)
            {
                case DragAction:
                    if (window.WindowHandle == IntPtr.Zero)
                        throw new InvalidOperationException("窗口尚未初始化，无法拖动。");
                    ReleaseCapture();
                    SendMessageW(window.WindowHandle, WmNcLButtonDown, (IntPtr)HtCaption, IntPtr.Zero);
                    break;
                case MinimizeAction:
                    window.SetMinimized(true);
                    break;
                case MaximizeAction:
                    window.SetMaximized(!window.Maximized);
                    break;
                case CloseAction:
                    window.Close();
                    break;
            }
        });
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr SendMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public void Dispose()
    {
        var window = _window;
        _window = null;
        window?.Close();
    }
}
