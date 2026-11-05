using System.Diagnostics;
using FlashToolbox.Contracts;

namespace FlashToolbox.Module.ScreenMirror;

public sealed class ScreenMirrorModule : IToolboxModule
{
    public string Id => "screen-mirror";
    public string Name => "投屏";
    public Version Version => new(1, 0, 0);

    public void Run(IModuleHost host)
    {
        var executable = FindOnPath("scrcpy.exe") ?? FindOnPath("scrcpy");
        if (executable is null)
        {
            MessageBox.Show(host.Owner, "未在 PATH 中找到 scrcpy，请先安装 scrcpy 并将其目录加入 PATH。", "缺少 scrcpy", MessageBoxButtons.OK, MessageBoxIcon.Information);
            host.Log("未找到 scrcpy");
            return;
        }

        var startInfo = new ProcessStartInfo(executable) { UseShellExecute = false };
        if (!string.IsNullOrWhiteSpace(host.ConnectedDeviceSerial))
        {
            startInfo.ArgumentList.Add("-s");
            startInfo.ArgumentList.Add(host.ConnectedDeviceSerial);
        }

        Process.Start(startInfo);
        host.Log("已启动 scrcpy");
    }

    private static string? FindOnPath(string fileName)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), fileName);
                if (File.Exists(candidate)) return candidate;
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException) { }
        }
        return null;
    }
}
