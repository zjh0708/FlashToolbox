namespace FlashToolbox.Module.DeviceInfo;

internal sealed class DeviceInfoForm : Form
{
    private readonly Label _batteryValue = new();
    private readonly ToolStripStatusLabel _statusLabel = new("模块窗口 · 模拟数据");
    private readonly string _serial;
    private int _batteryLevel = 76;

    public DeviceInfoForm(string? serial)
    {
        _serial = string.IsNullOrWhiteSpace(serial) ? "未连接" : serial;
        Text = "设备信息";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(390, 310);

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(12) };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var informationTable = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6, CellBorderStyle = TableLayoutPanelCellBorderStyle.Single };
        informationTable.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        informationTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        for (var row = 0; row < 6; row++) informationTable.RowStyles.Add(new RowStyle(SizeType.Percent, 16.67F));

        AddInformationRow(informationTable, 0, "品牌型号", "Xiaomi 14");
        AddInformationRow(informationTable, 1, "Android", "14");
        AddInformationRow(informationTable, 2, "电量", _batteryValue);
        AddInformationRow(informationTable, 3, "Root 状态", "未 Root");
        AddInformationRow(informationTable, 4, "序列号", _serial);
        AddInformationRow(informationTable, 5, "当前模式", "ADB");
        _batteryValue.Text = $"{_batteryLevel}%";

        var buttons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 12, 0, 0) };
        var refreshButton = new Button { Text = "刷新", AutoSize = true };
        var copyButton = new Button { Text = "复制", AutoSize = true };
        var closeButton = new Button { Text = "关闭", AutoSize = true };
        refreshButton.Click += (_, _) => { _batteryLevel = Math.Clamp(_batteryLevel + Random.Shared.Next(-2, 3), 1, 100); _batteryValue.Text = $"{_batteryLevel}%"; _statusLabel.Text = $"已刷新 · {DateTime.Now:HH:mm:ss}"; };
        copyButton.Click += (_, _) => CopyInformation();
        closeButton.Click += (_, _) => Close();
        buttons.Controls.AddRange([refreshButton, copyButton, closeButton]);
        content.Controls.Add(informationTable, 0, 0);
        content.Controls.Add(buttons, 0, 1);
        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_statusLabel);
        Controls.Add(content);
        Controls.Add(statusStrip);
        AcceptButton = refreshButton;
        CancelButton = closeButton;
    }

    private void CopyInformation()
    {
        var text = $"品牌型号：Xiaomi 14{Environment.NewLine}Android：14{Environment.NewLine}电量：{_batteryLevel}%{Environment.NewLine}Root 状态：未 Root{Environment.NewLine}序列号：{_serial}{Environment.NewLine}当前模式：ADB";
        try { Clipboard.SetText(text); _statusLabel.Text = "设备信息已复制到剪贴板"; }
        catch (System.Runtime.InteropServices.ExternalException) { MessageBox.Show(this, "剪贴板暂时不可用，请稍后重试。", "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private static void AddInformationRow(TableLayoutPanel table, int row, string name, string value) => AddInformationRow(table, row, name, new Label { Text = value });
    private static void AddInformationRow(TableLayoutPanel table, int row, string name, Label valueLabel)
    {
        var nameLabel = new Label { Text = name };
        foreach (var label in new[] { nameLabel, valueLabel }) { label.AutoSize = false; label.Dock = DockStyle.Fill; label.TextAlign = ContentAlignment.MiddleLeft; label.Padding = new Padding(6, 0, 0, 0); label.Margin = Padding.Empty; }
        table.Controls.Add(nameLabel, 0, row);
        table.Controls.Add(valueLabel, 1, row);
    }
}
