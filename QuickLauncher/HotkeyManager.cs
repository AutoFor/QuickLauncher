using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;

namespace QuickLauncher;

[Flags]
internal enum HotkeyModifiers : uint
{
    None = 0x0000,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008
}

internal sealed class HotkeyManager : IDisposable
{
    private const int WM_HOTKEY = 0x0312;

    private readonly Form hostForm;
    private readonly ILogger logger;
    private readonly Dictionary<int, HotkeyEntry> entries = new();
    private int nextHotkeyId = 1;

    internal HotkeyManager(Form hostForm, ILogger logger)
    {
        this.hostForm = hostForm;
        this.logger = logger;
    }

    internal void RegisterDoublePress(Keys key, HotkeyModifiers modifiers, TimeSpan interval, string label, Action action)
        => Register(key, modifiers, interval, label, action, requiresDoublePress: true);

    internal void RegisterSinglePress(Keys key, HotkeyModifiers modifiers, string label, Action action)
        => Register(key, modifiers, TimeSpan.Zero, label, action, requiresDoublePress: false);

    internal bool TryHandleHotkeyMessage(Message message)
    {
        if (message.Msg != WM_HOTKEY)
        {
            return false;
        }

        int hotkeyId = message.WParam.ToInt32();
        if (!entries.TryGetValue(hotkeyId, out var entry))
        {
            logger.Warning("未処理のホットキー ID を受信しました。HotkeyId={HotkeyId}", hotkeyId);
            return true;
        }

        var now = DateTime.Now;
        var elapsed = (now - entry.LastPressed).TotalMilliseconds;
        bool isInitial = entry.LastPressed == DateTime.MinValue;

        logger.Debug(
            "{HotkeyLabel} ホットキーイベント. 現在={Now:O}, 前回={Last:O}, 経過={ElapsedMilliseconds}ms, 前回未設定={IsInitial}",
            entry.Label,
            now,
            entry.LastPressed,
            elapsed,
            isInitial);

        if (entry.RequiresDoublePress)
        {
            if (!isInitial && elapsed < entry.DoublePressInterval.TotalMilliseconds)
            {
                entry.LastPressed = DateTime.MinValue;
                ExecuteAction(entry);
            }
            else
            {
                logger.Debug("{HotkeyLabel} の初回押下を記録しました。次の押下を待機します。", entry.Label);
                entry.LastPressed = now;
                logger.Debug("{HotkeyLabel} 記録したホットキー時刻: {LastHotKeyTime:O}", entry.Label, entry.LastPressed);
            }
        }
        else
        {
            ExecuteAction(entry);
        }

        return true;
    }

    public void Dispose()
    {
        foreach (var entry in entries.Values)
        {
            UnregisterHotKey(hostForm.Handle, entry.Id);
        }
        entries.Clear();
        logger.Debug("グローバルホットキーの登録を解除しました。");
    }

    private void Register(Keys key, HotkeyModifiers modifiers, TimeSpan interval, string label, Action action, bool requiresDoublePress)
    {
        int id = nextHotkeyId++;
        uint nativeKey = (uint)((int)key & 0xFFFF);
        if (!RegisterHotKey(hostForm.Handle, id, (uint)modifiers, nativeKey))
        {
            throw new InvalidOperationException($"{label} のホットキー登録に失敗しました。");
        }

        entries[id] = new HotkeyEntry(id, label, action, requiresDoublePress, interval);
        logger.Information(
            "グローバルホットキーを登録しました。Label={Label}, Id={Id}, Modifiers={Modifiers}, Key={Key}",
            label,
            id,
            modifiers,
            key);
    }

    private void ExecuteAction(HotkeyEntry entry)
    {
        try
        {
            entry.Action();
            logger.Debug("{HotkeyLabel} アクションの実行が完了しました。", entry.Label);
        }
        catch (Exception ex)
        {
            logger.Error(ex, "{HotkeyLabel} アクションの実行中にエラーが発生しました。", entry.Label);
        }
    }

    private sealed record HotkeyEntry(int Id, string Label, Action Action, bool RequiresDoublePress, TimeSpan DoublePressInterval)
    {
        public DateTime LastPressed { get; set; } = DateTime.MinValue;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
