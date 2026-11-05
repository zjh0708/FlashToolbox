namespace FlashToolbox.Contracts;

public interface IToolboxModule
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }
    void Run(IModuleHost host);
}
