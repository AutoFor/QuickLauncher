using System;
using System.Runtime.InteropServices;
using CommunityToolkit.WinUI.Notifications;
using Serilog;
using Windows.UI.Notifications;

namespace QuickLauncher;

internal static class ToastService
{
    private const string AppId = "QuickLauncher.App";
    private static bool initialized;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appID);

    internal static void Initialize()
    {
        if (!OperatingSystem.IsWindows())
        {
            Log.Information("トースト通知サービスは Windows 環境のみサポートされます。");
            return;
        }

        if (initialized)
        {
            return;
        }

        int result = SetCurrentProcessExplicitAppUserModelID(AppId);
        if (result != 0)
        {
            Log.Warning("AppUserModelID の設定に失敗しました。HRESULT=0x{HResult:X}", result);
            return;
        }

        initialized = true;
        Log.Information("トースト通知サービスを初期化しました。AppID={AppId}", AppId);
    }

    internal static void Shutdown()
    {
        initialized = false;
    }

    internal static void ShowYamabukiRestartToast(int delaySeconds)
    {
        if (!initialized)
        {
            Log.Debug("トースト通知サービスが初期化されていないため、通知をスキップします。");
            return;
        }

        var content = new ToastContentBuilder()
            .AddText("Yamabuki R を再起動します")
            .AddText($"{delaySeconds} 秒後に再起動を実行します。")
            .GetToastContent();

        var notification = new ToastNotification(content.GetXml())
        {
            ExpirationTime = DateTimeOffset.Now.AddSeconds(delaySeconds + 5),
            Tag = "YamabukiRestart",
            Group = "SystemActions"
        };

        try
        {
            ToastNotificationManager.CreateToastNotifier(AppId).Show(notification);
            Log.Information("Yamabuki R 再起動トーストを表示しました。DelaySeconds={DelaySeconds}", delaySeconds);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "トースト通知の表示に失敗しました。");
        }
    }
}
