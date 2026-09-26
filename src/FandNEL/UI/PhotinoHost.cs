using System.IO;
using Photino.NET;
using Serilog;
using FandNEL.Gateway.Management;

namespace FandNEL.UI;

/// <summary>使用本地浏览器窗口承载 React UI，窗口和网关共享同一份静态资源。</summary>
public sealed class PhotinoHost : IDisposable
{
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
            .SetChromeless(false)
            .SetGrantBrowserPermissions(true)
            .SetTemporaryFilesPath(dataPath)
            .SetSize(1200, 750)
            .SetMinSize(900, 600)
            .SetUseOsDefaultSize(false)
            .Center()
            .Load(_runtime.WebSocket.HttpAddress);

        Log.Information("FandNEL UI loaded from {Address}", _runtime.WebSocket.HttpAddress);
        _window.WaitForClose();
    }

    public void Dispose()
    {
        _window?.Close();
        _window = null;
    }
}
