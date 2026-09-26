using System.IO;
using FandNEL.Accounts;
using FandNEL.Core.Protocol;
using FandNEL.Gateway;
using FandNEL.Gateway.Management;
using FandNEL.UI;
using Serilog;

namespace FandNEL;

/// <summary>FandNEL 桌面程序入口。</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var baseDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory;
        Directory.SetCurrentDirectory(baseDirectory);
        ConfigureLogger(baseDirectory);
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            Log.Fatal(eventArgs.ExceptionObject as Exception, "Unhandled FandNEL exception");

        WPFLauncher? launcher = null;
        AccountService? accounts = null;
        GameProxyService? proxy = null;
        GatewayRuntime? runtime = null;
        PhotinoHost? browser = null;
        try
        {
            launcher = new WPFLauncher();
            var dataDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FandNEL");
            accounts = new AccountService(launcher, dataDirectory);
            proxy = new GameProxyService(accounts);
            runtime = new GatewayRuntime(accounts, launcher, proxy);
            _ = runtime.StartAsync();

            browser = new PhotinoHost(runtime);
            browser.Run();
            return 0;
        }
        catch (Exception exception)
        {
            Log.Fatal(exception, "FandNEL failed to start");
            return 1;
        }
        finally
        {
            browser?.Dispose();
            if (runtime is not null)
            {
                try { runtime.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception exception) { Log.Error(exception, "Failed to dispose gateway runtime"); }
            }
            if (proxy is not null)
            {
                try { proxy.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception exception) { Log.Error(exception, "Failed to dispose proxy service"); }
            }
            if (accounts is not null)
            {
                try { accounts.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
                catch (Exception exception) { Log.Error(exception, "Failed to dispose account service"); }
            }
            launcher?.Dispose();
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogger(string baseDirectory)
    {
        var logDirectory = Path.Combine(baseDirectory, "logs");
        Directory.CreateDirectory(logDirectory);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logDirectory, "fandnel-.log"), rollingInterval: RollingInterval.Day)
            .CreateLogger();
    }
}
