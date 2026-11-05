namespace FlashToolbox.Contracts;

public interface IModuleHost
{
    Form Owner { get; }
    string? ConnectedDeviceSerial { get; }
    IReadOnlyDictionary<string, string> Configuration { get; }
    void Log(string message);
}
