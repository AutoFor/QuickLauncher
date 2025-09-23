using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Serilog;

namespace QuickLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ConfigureLogging();
        ToastService.Initialize();

        using var mutex = new Mutex(false, "QuickLauncher_SingleInstance");
        var ownsMutex = false;

        try
        {
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            ownsMutex = mutex.WaitOne(0, false);
            if (!ownsMutex)
            {
                Log.Warning("既に起動しているインスタンスが検出されたため、新しいインスタンスを終了します。");
                MessageBox.Show("既に起動しています", "QuickLauncher", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Log.Information("アプリケーションを開始します");

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "アプリケーションの致命的なエラー");
            throw;
        }
        finally
        {
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }

            Application.ThreadException -= OnThreadException;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

            ToastService.Shutdown();
            Log.Information("アプリケーションを終了します");
            Log.CloseAndFlush();
        }
    }

    private static void ConfigureLogging()
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "QuickLauncher",
            "Logs");

        Directory.CreateDirectory(logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                Path.Combine(logDirectory, "QuickLauncher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                encoding: System.Text.Encoding.UTF8,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("ロギングを初期化しました。ログの保存先: {LogDirectory}", logDirectory);
    }

    private static void OnThreadException(object? sender, ThreadExceptionEventArgs e)
    {
        if (e.Exception is null)
        {
            Log.Error("UI スレッドで未処理の例外が発生しましたが、Exception が null でした。");
            return;
        }

        Log.Error(e.Exception, "UI スレッドで未処理の例外が発生しました。");
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "アプリケーションドメインで未処理の例外が発生しました。IsTerminating={IsTerminating}", e.IsTerminating);
        }
        else
        {
            Log.Fatal("アプリケーションドメインで未処理の例外が発生しましたが、ExceptionObject を解釈できませんでした。IsTerminating={IsTerminating}", e.IsTerminating);
        }
    }
}
