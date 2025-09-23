using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Serilog;

namespace QuickLauncher;

internal sealed class YamabukiService
{
    private const string YamabukiExecutablePath = @"C:\Prog\YamabukiR\yamabuki_r.exe";
    private const string YamabukiProcessName = "yamabuki_r";
    private readonly ILogger logger;

    internal YamabukiService(ILogger logger)
    {
        this.logger = logger;
    }

    internal void Restart(NotifyIcon? notifyIcon)
    {
        try
        {
            const int restartDelaySeconds = 3;
            logger.Debug("Yamabuki R の再起動処理を開始します。対象パス={YamabukiPath}", YamabukiExecutablePath);

            ToastService.ShowYamabukiRestartToast(restartDelaySeconds);
            logger.Information("Yamabuki R 再起動前に {DelaySeconds} 秒待機します。", restartDelaySeconds);
            Thread.Sleep(TimeSpan.FromSeconds(restartDelaySeconds));

            var runningProcesses = Process.GetProcessesByName(YamabukiProcessName);
            logger.Debug("既存 Yamabuki R プロセス数: {Count}", runningProcesses.Length);

            foreach (var process in runningProcesses)
            {
                try
                {
                    logger.Debug("Yamabuki R プロセスを終了要求します。PID={Pid}, MainWindowHandle={Handle}", process.Id, process.MainWindowHandle);
                    bool closeResult = process.CloseMainWindow();
                    logger.Debug("CloseMainWindow の結果: {Result}", closeResult);

                    if (!process.WaitForExit(2000))
                    {
                        logger.Warning("CloseMainWindow の待機がタイムアウトしました。Kill() を実行します。PID={Pid}", process.Id);
                        process.Kill();
                        if (!process.WaitForExit(2000))
                        {
                            logger.Warning("Kill() 後もプロセス終了を確認できませんでした。PID={Pid}", process.Id);
                        }
                    }

                    logger.Debug("Yamabuki R プロセスの終了確認: HasExited={HasExited}", process.HasExited);
                }
                catch (Exception terminationEx)
                {
                    logger.Warning(terminationEx, "Yamabuki R プロセスの終了処理でエラーが発生しました。PID={Pid}", process.Id);
                }
                finally
                {
                    process.Dispose();
                }
            }

            if (!File.Exists(YamabukiExecutablePath))
            {
                logger.Error("Yamabuki R の実行ファイルが見つかりません: {YamabukiPath}", YamabukiExecutablePath);
                notifyIcon?.ShowBalloonTip(3000, "エラー", "Yamabuki R の実行ファイルが見つかりません", ToolTipIcon.Error);
                return;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = YamabukiExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(YamabukiExecutablePath) ?? string.Empty,
                UseShellExecute = false
            };

            var restartedProcess = Process.Start(startInfo);
            if (restartedProcess != null)
            {
                logger.Information("Yamabuki R を起動しました。PID={Pid}", restartedProcess.Id);
                try
                {
                    if (!restartedProcess.WaitForInputIdle(2000))
                    {
                        logger.Debug("WaitForInputIdle はタイムアウトしました。PID={Pid}", restartedProcess.Id);
                    }
                }
                catch (InvalidOperationException)
                {
                    logger.Debug("WaitForInputIdle がサポートされていないプロセスです。PID={Pid}", restartedProcess.Id);
                }
                finally
                {
                    restartedProcess.Dispose();
                }
            }
            else
            {
                logger.Warning("Yamabuki R の起動で Process.Start が null を返しました。");
            }

            Thread.Sleep(TimeSpan.FromSeconds(1));
            var postProcesses = Process.GetProcessesByName(YamabukiProcessName);
            var summary = postProcesses.Select(p => $"PID={p.Id}, HasExited={p.HasExited}").ToArray();
            logger.Information("Yamabuki R 再起動後のプロセス状態: {Summary}", string.Join("; ", summary));

            bool restarted = postProcesses.Any(p => !p.HasExited);

            if (!restarted)
            {
                logger.Error("Yamabuki R の再起動を確認できませんでした。プロセスが存在しません。");
                notifyIcon?.ShowBalloonTip(3000, "エラー", "Yamabuki R の再起動を確認できませんでした", ToolTipIcon.Error);
            }
            else
            {
                var runningCount = postProcesses.Count(p => !p.HasExited);
                logger.Information("Yamabuki R の再起動確認が完了しました。稼働中のプロセス数: {Count}", runningCount);
            }

            foreach (var proc in postProcesses)
            {
                proc.Dispose();
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Yamabuki R の再起動に失敗しました。");
            notifyIcon?.ShowBalloonTip(3000, "エラー", $"Yamabuki R の再起動に失敗しました: {ex.Message}", ToolTipIcon.Error);
        }
    }
}
