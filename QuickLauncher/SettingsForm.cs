using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using Serilog;

namespace QuickLauncher
{
    // 設定フォーム
    public partial class SettingsForm : Form
    {
        private readonly Serilog.ILogger logger = Log.ForContext<SettingsForm>();
        private CheckBox chkStartup = null!;
        private TextBox txtHidemaruPath = null!;
        private Button btnBrowse = null!;
        private Button btnOK = null!;
        private Button btnCancel = null!;
        private Label lblDescription = null!;
        private Label lblPath = null!;

        public SettingsForm()
        {
            logger.Information("SettingsForm の初期化を開始します。");
            InitializeComponent();
            LoadSettings();
            logger.Information("SettingsForm の初期化が完了しました。Startup={StartupRegistered}, Path='{HidemaruPath}'",
                chkStartup.Checked, txtHidemaruPath.Text);
        }

        // UI初期化
        private void InitializeComponent()
        {
            Text = "QuickLauncher 設定";
            Size = new System.Drawing.Size(500, 280);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;

            // 説明ラベル
            lblDescription = new Label
            {
                Text = "使い方:\nCtrl + H を2回連続で押すと秀丸エディタが起動します。",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(450, 40),
                AutoSize = false
            };

            // スタートアップ登録チェックボックス
            chkStartup = new CheckBox
            {
                Text = "Windows起動時に自動起動",
                Location = new System.Drawing.Point(20, 70),
                Size = new System.Drawing.Size(250, 25),
                Checked = IsStartupRegistered()
            };

            // 秀丸パスラベル
            lblPath = new Label
            {
                Text = "秀丸エディタのパス:",
                Location = new System.Drawing.Point(20, 110),
                Size = new System.Drawing.Size(150, 25),
                AutoSize = false
            };

            // 秀丸パステキストボックス
            txtHidemaruPath = new TextBox
            {
                Location = new System.Drawing.Point(20, 135),
                Size = new System.Drawing.Size(350, 25),
                Text = GetHidemaruPath()
            };

            // 参照ボタン
            btnBrowse = new Button
            {
                Text = "参照...",
                Location = new System.Drawing.Point(380, 133),
                Size = new System.Drawing.Size(80, 25)
            };
            btnBrowse.Click += BtnBrowse_Click;

            // OKボタン
            btnOK = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(280, 190),
                Size = new System.Drawing.Size(80, 30),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            // キャンセルボタン
            btnCancel = new Button
            {
                Text = "キャンセル",
                Location = new System.Drawing.Point(370, 190),
                Size = new System.Drawing.Size(80, 30),
                DialogResult = DialogResult.Cancel
            };

            // コントロール追加
            Controls.AddRange(new Control[] {
                lblDescription,
                chkStartup,
                lblPath,
                txtHidemaruPath,
                btnBrowse,
                btnOK,
                btnCancel
            });
        }

        // 設定読み込み
        private void LoadSettings()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\QuickLauncher", false);
                if (key != null)
                {
                    var path = key.GetValue("HidemaruPath") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        txtHidemaruPath.Text = path;
                        logger.Information("レジストリから秀丸のパスを読み込みました: {HidemaruPath}", path);
                    }
                    else
                    {
                        logger.Debug("レジストリに秀丸のパス設定が見つかりませんでした。");
                    }
                }
                else
                {
                    logger.Debug("レジストリキー 'SOFTWARE\\QuickLauncher' が存在しません。");
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "レジストリから設定を読み込めませんでした。");
            }
        }

        // 秀丸エディタのパス取得
        private string GetHidemaruPath()
        {
            // 設定から読み込み
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\QuickLauncher", false);
                if (key != null)
                {
                    var path = key.GetValue("HidemaruPath") as string;
                    if (!string.IsNullOrEmpty(path))
                    {
                        logger.Information("GetHidemaruPath: レジストリから取得しました: {HidemaruPath}", path);
                        return path;
                    }
                    logger.Debug("GetHidemaruPath: レジストリから値を取得できませんでした。");
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "GetHidemaruPath: レジストリ参照に失敗しました。");
            }

            // デフォルトパス検索
            string[] possiblePaths =
            {
                @"C:\Program Files\Hidemaru\Hidemaru.exe",
                @"C:\Program Files (x86)\Hidemaru\Hidemaru.exe",
                @"C:\Program Files\秀丸\Hidemaru.exe",
                @"C:\Program Files (x86)\秀丸\Hidemaru.exe"
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    logger.Information("GetHidemaruPath: 既定候補から検出しました: {HidemaruPath}", path);
                    return path;
                }
            }

            logger.Warning("GetHidemaruPath: 秀丸エディタのパスを特定できませんでした。");
            return "";
        }

        // 参照ボタンクリック
        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            logger.Information("秀丸のパスを参照ダイアログで選択します。");
            using var dialog = new OpenFileDialog
            {
                Title = "秀丸エディタを選択",
                Filter = "実行ファイル (*.exe)|*.exe",
                FileName = "Hidemaru.exe"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                txtHidemaruPath.Text = dialog.FileName;
                logger.Information("参照ダイアログで秀丸のパスを選択しました: {HidemaruPath}", dialog.FileName);
            }
            else
            {
                logger.Debug("参照ダイアログがキャンセルされました。");
            }
        }

        // OKボタンクリック
        private void BtnOK_Click(object? sender, EventArgs e)
        {
            logger.Information("設定を保存しスタートアップ登録を更新します。");
            SaveSettings();  // 設定保存
            UpdateStartupRegistration();  // スタートアップ更新
        }

        // 設定保存
        private void SaveSettings()
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\QuickLauncher");
                if (key != null && !string.IsNullOrEmpty(txtHidemaruPath.Text))
                {
                    key.SetValue("HidemaruPath", txtHidemaruPath.Text);
                    logger.Information("設定を保存しました: {HidemaruPath}", txtHidemaruPath.Text);
                }
                else if (key == null)
                {
                    logger.Warning("設定保存に使用するレジストリキーを作成できませんでした。");
                }
                else
                {
                    logger.Debug("空のパスのためレジストリには保存しませんでした。");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "設定の保存に失敗しました。");
                MessageBox.Show($"設定の保存に失敗しました: {ex.Message}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // スタートアップ登録更新
        private void UpdateStartupRegistration()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

                if (key != null)
                {
                    if (chkStartup.Checked)
                    {
                        // 登録
                        string exePath = Application.ExecutablePath;
                        key.SetValue("QuickLauncher", $"\"{exePath}\"");
                        logger.Information("スタートアップに登録しました: {ExecutablePath}", exePath);
                    }
                    else
                    {
                        // 解除
                        key.DeleteValue("QuickLauncher", false);
                        logger.Information("スタートアップ登録を解除しました。");
                    }
                }
                else
                {
                    logger.Warning("スタートアップ更新用のレジストリキーを開けませんでした。");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "スタートアップ登録の更新に失敗しました。");
                MessageBox.Show($"スタートアップ登録の更新に失敗しました: {ex.Message}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // スタートアップ登録状態確認
        private bool IsStartupRegistered()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);

                if (key != null)
                {
                    var value = key.GetValue("QuickLauncher");
                    bool registered = value != null;
                    logger.Debug("スタートアップ登録状態を確認しました。IsRegistered={IsRegistered}", registered);
                    return registered;
                }
            }
            catch (Exception ex)
            {
                logger.Warning(ex, "スタートアップ登録状態の確認に失敗しました。");
            }
            logger.Debug("スタートアップ登録キーが見つかりませんでした。");
            return false;
        }
    }
}
