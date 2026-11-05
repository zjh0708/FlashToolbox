using FlashToolbox.Contracts;

namespace FlashToolbox;

internal sealed class ModuleHost : IModuleHost
{
    private readonly Action<string> _logger;

    public ModuleHost(Form owner, string? connectedDeviceSerial, IReadOnlyDictionary<string, string> configuration, Action<string> logger)
    {
        Owner = owner;
        ConnectedDeviceSerial = connectedDeviceSerial;
        Configuration = configuration;
        _logger = logger;
    }

    public Form Owner { get; }
    public string? ConnectedDeviceSerial { get; }
    public IReadOnlyDictionary<string, string> Configuration { get; }
    public void Log(string message) => _logger(message);
}
