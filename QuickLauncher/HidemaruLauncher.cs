using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Serilog;

namespace QuickLauncher;

internal sealed class HidemaruLauncher
{
    private readonly ILogger logger;

    internal HidemaruLauncher(ILogger logger)
    {
        this.logger = logger;
    }

    internal void Launch()
    {
        try
        {
            logger.Debug("秀丸エディタの起動処理を開始します。");

            IntPtr existingWindow = FindHidemaruWindow();
            logger.Debug("既存ウィンドウの検索結果: {Found} (Handle={Handle})", existingWindow != IntPtr.Zero, existingWindow);

            if (existingWindow != IntPtr.Zero)
            {
                ActivateExistingWindow(existingWindow);
                return;
            }

            string? hidemaruPath = GetHidemaruPath();
            if (!string.IsNullOrEmpty(hidemaruPath) && File.Exists(hidemaruPath))
            {
                logger.Information("秀丸エディタをパス {HidemaruPath} から起動します。", hidemaruPath);
                _ = Process.Start(hidemaruPath);
            }
            else
            {
                logger.Warning("秀丸エディタのパスを特定できなかったため、既定名での起動を試みます。");
                _ = Process.Start("Hidemaru.exe");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "秀丸エディタの起動に失敗しました。");
        }
        finally
        {
            logger.Debug("HidemaruLauncher 処理を終了します。既存ウィンドウハンドル={Handle}", FindHidemaruWindow());
        }
    }

    private void ActivateExistingWindow(IntPtr windowHandle)
    {
        logger.Information("既存の秀丸エディタ ウィンドウをアクティブ化します。");

        const int SW_RESTORE = 9;
        bool restoreResult = ShowWindow(windowHandle, SW_RESTORE);
        logger.Debug("ShowWindow(SW_RESTORE) の結果: {Result}", restoreResult);

        bool foregroundResult = SetForegroundWindow(windowHandle);
        logger.Debug("SetForegroundWindow の結果: {Result}", foregroundResult);

        logger.Information("既存ウィンドウを前面に表示しました。ショートカット送信は行いません。");
    }

    private IntPtr FindHidemaruWindow()
    {
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows((hWnd, lParam) =>
        {
            var className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);

            if (className.ToString().Contains("Hidemaru32Class"))
            {
                logger.Debug("ウィンドウクラス名の一致で秀丸を検出しました (Handle={Handle}).", hWnd);
                foundWindow = hWnd;
                return false;
            }

            var windowText = new StringBuilder(256);
            GetWindowText(hWnd, windowText, windowText.Capacity);
            if (windowText.ToString().Contains("秀丸"))
            {
                logger.Debug("ウィンドウタイトルに秀丸を含むウィンドウを検出しました (Handle={Handle}).", hWnd);
                foundWindow = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        logger.Debug("秀丸エディタのウィンドウ検索を完了しました。結果: {Found}", foundWindow != IntPtr.Zero);

        return foundWindow;
    }

    private string? GetHidemaruPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\HidemaruLaunch", false);
            if (key != null)
            {
                var path = key.GetValue("HidemaruPath") as string;
                if (!string.IsNullOrEmpty(path))
                {
                    logger.Information("レジストリ設定から秀丸のパスを取得しました: {HidemaruPath}", path);
                    return path;
                }

                logger.Debug("レジストリに秀丸パスの設定が見つかりませんでした。");
            }
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "レジストリから秀丸のパスを取得できませんでした。");
        }

        string[] possiblePaths =
        {
            @"C:\\Program Files\\Hidemaru\\Hidemaru.exe",
            @"C:\\Program Files (x86)\\Hidemaru\\Hidemaru.exe",
            @"C:\\Program Files\\秀丸\\Hidemaru.exe",
            @"C:\\Program Files (x86)\\秀丸\\Hidemaru.exe"
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                logger.Information("既定の候補パスから秀丸の実行ファイルを検出しました: {HidemaruPath}", path);
                return path;
            }
        }

        logger.Warning("秀丸エディタの実行ファイルを検出できませんでした。");
        return null;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}
