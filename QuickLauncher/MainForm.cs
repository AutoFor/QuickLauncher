using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;
using Serilog;

namespace QuickLauncher;

public class MainForm : Form
{
    private static readonly TimeSpan DoublePressInterval = TimeSpan.FromMilliseconds(500);

    private readonly ILogger logger = Log.ForContext<MainForm>();
    private readonly HidemaruLauncher hidemaruLauncher;
    private readonly YamabukiService yamabukiService;
    private readonly VsCodeLauncher vsCodeLauncher;
    private readonly HotkeyManager hotkeyManager;

    private NotifyIcon? notifyIcon;
    private ContextMenuStrip? contextMenu;

    public MainForm()
    {
        logger.Information("MainForm の初期化を開始します");

        hidemaruLauncher = new HidemaruLauncher(logger);
        yamabukiService = new YamabukiService(logger);
        vsCodeLauncher = new VsCodeLauncher(logger);
        hotkeyManager = new HotkeyManager(this, logger);

        InitializeForm();
        InitializeSystemTray();
        RegisterHotkeys();

        logger.Information("MainForm の初期化が完了しました。ダブルプレス判定間隔: {Interval}ms", DoublePressInterval.TotalMilliseconds);
    }

    protected override void WndProc(ref Message m)
    {
        if (!hotkeyManager.TryHandleHotkeyMessage(m))
        {
            base.WndProc(ref m);
        }
    }

    private void InitializeForm()
    {
        Text = "QuickLauncher";
        WindowState = FormWindowState.Minimized;
        ShowInTaskbar = false;
        Visible = false;
    }

    private void InitializeSystemTray()
    {
        contextMenu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("設定");
        settingsItem.Click += (_, _) => ShowSettings();
        contextMenu.Items.Add(settingsItem);

        var startupItem = new ToolStripMenuItem("スタートアップに登録")
        {
            Checked = IsStartupRegistered()
        };
        startupItem.Click += (_, _) => ToggleStartup(startupItem);
        contextMenu.Items.Add(startupItem);

        contextMenu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("終了");
        exitItem.Click += (_, _) => ExitApplication();
        contextMenu.Items.Add(exitItem);

        notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "QuickLauncher - Ctrl+H×2=秀丸 / Ctrl+Y×2=Yamabuki R",
            Visible = true,
            ContextMenuStrip = contextMenu
        };

        notifyIcon.DoubleClick += (_, _) => ShowSettings();

        logger.Information("システムトレイを初期化しました。スタートアップ登録状態: {IsStartupRegistered}", startupItem.Checked);
    }

    private void RegisterHotkeys()
    {
        try
        {
            hotkeyManager.RegisterDoublePress(Keys.H, HotkeyModifiers.Control, DoublePressInterval, "Ctrl+H", () =>
            {
                logger.Information("Ctrl+H のダブルプレスを検出しました。秀丸エディタを起動します。");
                hidemaruLauncher.Launch();
            });

            hotkeyManager.RegisterDoublePress(Keys.Y, HotkeyModifiers.Control, DoublePressInterval, "Ctrl+Y", () =>
            {
                logger.Information("Ctrl+Y のダブルプレスを検出しました。Yamabuki R を再起動します。");
                yamabukiService.Restart(notifyIcon);
            });

            hotkeyManager.RegisterSinglePress(Keys.C, HotkeyModifiers.Alt, "Alt+C", () =>
            {
                logger.Information("Alt+C を検出しました。VS Code を起動/アクティブ化します。");
                vsCodeLauncher.Launch();
            });
        }
        catch (Exception ex)
        {
            logger.Error(ex, "グローバルホットキーの登録に失敗しました。");
            MessageBox.Show("ホットキーの登録に失敗しました", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            hotkeyManager.Dispose();
            Close();
        }
    }

    private void ShowSettings()
    {
        logger.Information("設定フォームを表示します。");
        using var settingsForm = new SettingsForm();
        settingsForm.ShowDialog();
        logger.Information("設定フォームを閉じました。DialogResult={DialogResult}", settingsForm.DialogResult);
    }

    private void ExitApplication()
    {
        logger.Information("アプリケーションの終了処理を開始します。");
        hotkeyManager.Dispose();
        notifyIcon?.Dispose();
        Application.Exit();
        logger.Information("アプリケーションの終了処理が完了しました。");
    }

    private void ToggleStartup(ToolStripMenuItem menuItem)
    {
        if (menuItem.Checked)
        {
            logger.Information("スタートアップからの登録解除を実行します。");
            UnregisterStartup();
            menuItem.Checked = false;
        }
        else
        {
            logger.Information("スタートアップに登録します。");
            RegisterStartup();
            menuItem.Checked = true;
        }

        logger.Information("スタートアップ登録状態: {IsRegistered}", menuItem.Checked);
    }

    private void RegisterStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (key != null)
            {
                string exePath = Application.ExecutablePath;
                key.SetValue("QuickLauncher", $"\"{exePath}\"");
                notifyIcon?.ShowBalloonTip(2000, "成功", "スタートアップに登録しました", ToolTipIcon.Info);
                logger.Information("スタートアップに登録しました。パス: {ExecutablePath}", exePath);
            }
            else
            {
                logger.Warning("スタートアップ登録用のレジストリキーを開けませんでした。");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "スタートアップ登録に失敗しました。");
            MessageBox.Show($"スタートアップ登録に失敗しました: {ex.Message}", "エラー",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnregisterStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);

            if (key != null)
            {
                key.DeleteValue("QuickLauncher", false);
                notifyIcon?.ShowBalloonTip(2000, "成功", "スタートアップから解除しました", ToolTipIcon.Info);
                logger.Information("スタートアップからの登録解除を行いました。");
            }
            else
            {
                logger.Warning("スタートアップ解除用のレジストリキーを開けませんでした。");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, "スタートアップ解除に失敗しました。");
            MessageBox.Show($"スタートアップ解除に失敗しました: {ex.Message}", "エラー",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private bool IsStartupRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", false);
            if (key != null)
            {
                bool isRegistered = key.GetValue("QuickLauncher") != null;
                logger.Debug("スタートアップ登録状態を確認しました。IsRegistered={IsRegistered}", isRegistered);
                return isRegistered;
            }

            logger.Debug("スタートアップ登録キーが見つかりませんでした。");
        }
        catch (Exception ex)
        {
            logger.Warning(ex, "スタートアップ登録状態の確認に失敗しました。");
        }

        return false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            logger.Debug("MainForm を破棄します。");
            hotkeyManager.Dispose();
            notifyIcon?.Dispose();
            contextMenu?.Dispose();
        }

        base.Dispose(disposing);
        logger.Debug("MainForm の破棄が完了しました。disposing={Disposing}", disposing);
    }
}
