using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace QuickLauncher;

internal sealed class VsCodeLauncher
{
    private const string ProcessName = "Code";
    private readonly ILogger logger;

    internal VsCodeLauncher(ILogger logger)
    {
        this.logger = logger;
    }

    internal void Launch()
    {
        try
        {
            logger.Debug("VS Code の起動処理を開始します。");

            var runningProcesses = Process.GetProcessesByName(ProcessName)
                .Where(p => p.MainWindowHandle != IntPtr.Zero)
                .ToList();

            if (runningProcesses.Count > 0)
            {
                ActivateExistingWindow(runningProcesses[0]);
                return;
            }

            var codePath = ResolveExecutablePath();
            if (codePath != null)
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = codePath,
                    UseShellExecute = true,
                    Arguments = "--reuse-window"
                };
                var process = Process.Start(startInfo);
                logger.Information("VS Code をパス {Path} から起動しました。PID={Pid}", codePath, process?.Id);
            }
            else
            {
                logger.Warning("VS Code の実行ファイルを特定できなかったため、'code' コマンドで起動を試みます。");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "code",
                    UseShellExecute = true,
                    Arguments = "--reuse-window"
                };
                var process = Process.Start(startInfo);
                logger.Information("VS Code を 'code' コマンドで起動しました。PID={Pid}", process?.Id);
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "VS Code の起動に失敗しました。");
        }
    }

    private void ActivateExistingWindow(Process process)
    {
        IntPtr handle = process.MainWindowHandle;
        if (handle == IntPtr.Zero)
        {
            logger.Debug("VS Code プロセスは MainWindowHandle を持っていません。PID={Pid}", process.Id);
            return;
        }

        const int SW_RESTORE = 9;
        bool restoreResult = ShowWindow(handle, SW_RESTORE);
        logger.Debug("VS Code ウィンドウの ShowWindow(SW_RESTORE) = {Result}", restoreResult);

        bool foregroundResult = SetForegroundWindow(handle);
        logger.Information("既存の VS Code ウィンドウを前面に表示しました。SetForegroundWindow={Result}", foregroundResult);
    }

    private string? ResolveExecutablePath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string?[] candidates =
        {
            Path.Combine(localAppData, "Programs", "Microsoft VS Code", "Code.exe"),
            @"C:\\Program Files\\Microsoft VS Code\\Code.exe",
            @"C:\\Program Files (x86)\\Microsoft VS Code\\Code.exe"
        };

        foreach (var path in candidates)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                logger.Debug("VS Code の実行ファイル候補を検出しました: {Path}", path);
                return path;
            }
        }

        return null;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
