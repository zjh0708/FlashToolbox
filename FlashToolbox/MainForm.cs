namespace FlashToolbox;

public sealed class MainForm : Form
{
    private readonly ToolStripStatusLabel _statusLabel = new("就绪");
    private readonly ModuleClient _moduleClient;
    private readonly IReadOnlyDictionary<string, string> _configuration;
    private readonly Label _connectionSummaryLabel;
    private readonly Label _connectionStateValue;
    private readonly Label _modeSerialValue;

    public MainForm(string moduleBaseUrl)
    {
        _configuration = new Dictionary<string, string> { ["ModuleBaseUrl"] = moduleBaseUrl };
        _moduleClient = new ModuleClient(moduleBaseUrl);
        Text = "刷机工具箱";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(660, 480);
        ClientSize = new Size(700, 500);

        var menuStrip = BuildMenuStrip();
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_statusLabel);

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12),
        };
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        (_connectionSummaryLabel, _) = BuildConnectionSummary();
        content.Controls.Add(_connectionSummaryLabel, 0, 0);
        var deviceGroup = BuildDeviceGroup(out _connectionStateValue, out _modeSerialValue);
        content.Controls.Add(deviceGroup, 0, 1);
        content.Controls.Add(BuildFunctionGroup(), 0, 2);

        Controls.Add(content);
        Controls.Add(statusStrip);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        Shown += (_, _) => RefreshConnectedDeviceUi();
    }

    private static MenuStrip BuildMenuStrip()
    {
        var menuStrip = new MenuStrip();
        menuStrip.Items.AddRange(
        [
            new ToolStripMenuItem("文件(&F)"),
            new ToolStripMenuItem("设备(&D)"),
            new ToolStripMenuItem("窗口(&W)"),
            new ToolStripMenuItem("帮助(&H)"),
        ]);
        return menuStrip;
    }

    private static (Label summary, object _) BuildConnectionSummary()
    {
        var label = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 10),
            Text = "○ 未检测到设备（将不传 -s）",
            ForeColor = Color.DimGray,
        };
        return (label, null!);
    }

    private static GroupBox BuildDeviceGroup(out Label connectionStateValue, out Label modeSerialValue)
    {
        var group = new GroupBox
        {
            Text = "当前设备",
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = new Padding(0, 0, 0, 12),
            Padding = new Padding(10),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 4,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        connectionStateValue = AddDeviceRow(table, 0, "连接状态", "未连接");
        AddDeviceRow(table, 1, "型号", "—");
        AddDeviceRow(table, 2, "系统 / 电量", "—");
        modeSerialValue = AddDeviceRow(table, 3, "模式 / 序列号", "—");
        group.Controls.Add(table);
        return group;
    }

    private GroupBox BuildFunctionGroup()
    {
        var group = new GroupBox
        {
            Text = "功能（点击后打开独立窗口）",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34F));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var deviceInfoButton = CreateFunctionButton("设备信息...");
        deviceInfoButton.Click += async (_, _) => await RunModuleAsync(deviceInfoButton, "device-info");

        var screenCastButton = CreateFunctionButton("投屏...");
        screenCastButton.Click += async (_, _) => await RunModuleAsync(screenCastButton, "screen-mirror");

        layout.Controls.Add(deviceInfoButton, 0, 0);
        layout.Controls.Add(screenCastButton, 1, 0);
        layout.Controls.Add(CreateDevelopmentButton("刷写专区..."), 0, 1);
        layout.Controls.Add(CreateDevelopmentButton("一键 Root..."), 1, 1);
        layout.Controls.Add(CreateDevelopmentButton("隐藏环境..."), 0, 2);
        layout.Controls.Add(CreateDevelopmentButton("Rom 专区..."), 1, 2);

        var note = new Label
        {
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 8, 3, 0),
            Text = "设备信息和投屏按需加载模块；其他功能开发中。",
        };
        layout.Controls.Add(note, 0, 3);
        layout.SetColumnSpan(note, 2);
        group.Controls.Add(layout);
        return group;
    }

    private async Task RunModuleAsync(Button button, string moduleId)
    {
        button.Enabled = false;
        _statusLabel.Text = $"正在加载模块 {moduleId}...";
        try
        {
            var serial = TryGetAdbSerial();
            RefreshConnectedDeviceUi(serial);
            var module = await _moduleClient.LoadAsync(moduleId);
            var host = new ModuleHost(this, serial, _configuration, message => _statusLabel.Text = message);
            module.Run(host);
            _statusLabel.Text = $"已启动 {module.Name} {module.Version}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "模块加载失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _statusLabel.Text = $"模块 {moduleId} 加载失败";
        }
        finally
        {
            button.Enabled = true;
        }
    }

    private void RefreshConnectedDeviceUi(string? serial = null)
    {
        serial ??= TryGetAdbSerial();
        if (string.IsNullOrWhiteSpace(serial))
        {
            _connectionSummaryLabel.Text = "○ 未检测到设备（投屏将不传 -s）";
            _connectionSummaryLabel.ForeColor = Color.DimGray;
            _connectionStateValue.Text = "未连接";
            _modeSerialValue.Text = "—";
            return;
        }

        _connectionSummaryLabel.Text = $"● 已连接    模式 ADB    序列号 {serial}";
        _connectionSummaryLabel.ForeColor = Color.DarkGreen;
        _connectionStateValue.Text = "已连接";
        _modeSerialValue.Text = $"ADB / {serial}";
    }

    /// <summary>
    /// Returns the first authorized ADB device serial, or null when none / adb missing.
    /// </summary>
    internal static string? TryGetAdbSerial()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "adb",
                Arguments = "devices",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process is null) return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(3000);
            foreach (var rawLine in output.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
                    continue;
                var parts = line.Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length >= 2 && parts[1].Equals("device", StringComparison.OrdinalIgnoreCase))
                    return parts[0];
            }
        }
        catch
        {
            // adb not installed or not runnable — treat as no device
        }

        return null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _moduleClient.Dispose();
        base.Dispose(disposing);
    }

    private Button CreateDevelopmentButton(string text)
    {
        var button = CreateFunctionButton(text);
        button.Click += (_, _) =>
        {
            MessageBox.Show(this, "模块未上架", "刷机工具箱", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _statusLabel.Text = "模块未上架";
        };
        return button;
    }

    private static Button CreateFunctionButton(string text)
    {
        return new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            MinimumSize = new Size(0, 44),
        };
    }

    private static Label AddDeviceRow(TableLayoutPanel table, int row, string name, string value)
    {
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.Controls.Add(new Label { AutoSize = true, Text = name, Margin = new Padding(3, 4, 3, 4) }, 0, row);
        var valueLabel = new Label { AutoSize = true, Text = value, Margin = new Padding(3, 4, 3, 4) };
        table.Controls.Add(valueLabel, 1, row);
        return valueLabel;
    }
}
