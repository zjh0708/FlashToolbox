namespace FlashToolbox;

using System.Text.Json;

internal static class Program
{
    private const string DefaultModuleBaseUrl = "https://powss.xyz/modules/";

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm(ReadModuleBaseUrl()));
    }

    private static string ReadModuleBaseUrl()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "modules.json");
        if (!File.Exists(path)) return DefaultModuleBaseUrl;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.TryGetProperty("ModuleBaseUrl", out var value)
            ? value.GetString() ?? DefaultModuleBaseUrl
            : DefaultModuleBaseUrl;
    }
}
