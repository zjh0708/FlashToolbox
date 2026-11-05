using FlashToolbox.Contracts;

namespace FlashToolbox.Module.DeviceInfo;

public sealed class DeviceInfoModule : IToolboxModule
{
    public string Id => "device-info";
    public string Name => "设备信息";
    public Version Version => new(1, 0, 0);

    public void Run(IModuleHost host)
    {
        host.Log("正在打开设备信息模块");
        new DeviceInfoForm(host.ConnectedDeviceSerial).Show(host.Owner);
    }
}
