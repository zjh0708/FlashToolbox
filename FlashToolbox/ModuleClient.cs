using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using FlashToolbox.Contracts;

namespace FlashToolbox;

internal sealed class ModuleClient : IDisposable
{
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly Uri _baseUri;
    private readonly string _cacheRoot;
    private readonly string _localRoot;

    public ModuleClient(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl.EndsWith('/') ? baseUrl : baseUrl + '/', UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException("ModuleBaseUrl 必须是有效的绝对地址。");
        _baseUri = baseUri;

        _cacheRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FlashToolbox", "modules");
        _localRoot = Path.Combine(AppContext.BaseDirectory, "modules-local");
    }

    public async Task<IToolboxModule> LoadAsync(string id, CancellationToken cancellationToken = default)
    {
        Exception? cloudError = null;
        try
        {
            var index = await GetJsonAsync<ModuleIndex>(new Uri(_baseUri, "index.json"), cancellationToken);
            if (index.Schema != 1) throw new InvalidDataException($"index.json 使用了不支持的 schema {index.Schema}。");
            var item = index.Modules.FirstOrDefault(module => string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"模块 {id} 未上架。");
            if (string.IsNullOrWhiteSpace(item.Manifest)) throw new InvalidDataException($"模块 {id} 的清单地址为空。");

            var manifestUri = new Uri(_baseUri, item.Manifest);
            var manifest = await GetJsonAsync<VersionManifest>(manifestUri, cancellationToken);
            ValidateManifest(manifest, id, item.Latest);
            var directory = await EnsureCachedAsync(manifest, manifestUri, cancellationToken);
            return LoadFromDirectory(directory, manifest.Entry, id);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            cloudError = ex;
        }

        var localDirectory = Path.Combine(_localRoot, id);
        if (Directory.Exists(localDirectory)) return LoadFromDirectory(localDirectory, null, id);
        throw new InvalidOperationException($"模块 {id} 加载失败：{DescribeError(cloudError)}；本地兜底目录 {localDirectory} 不存在。", cloudError);
    }

    private async Task<T> GetJsonAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new HttpRequestException($"请求 {uri} 失败（HTTP {(int)response.StatusCode} {response.ReasonPhrase}）。");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken) ?? throw new JsonException($"{uri} 返回了空清单。");
    }

    private async Task<string> EnsureCachedAsync(VersionManifest manifest, Uri manifestUri, CancellationToken cancellationToken)
    {
        var destination = Path.Combine(_cacheRoot, manifest.Id, manifest.Version);
        var marker = Path.Combine(destination, ".complete");
        if (File.Exists(marker) && (string.IsNullOrWhiteSpace(manifest.Sha256) ||
            string.Equals(File.ReadAllText(marker).Trim(), manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)))
            return destination;

        var staging = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(staging);
        try
        {
            var packageUri = new Uri(manifestUri, manifest.Package);
            var zipPath = Path.Combine(staging, "module.zip");
            using (var response = await _httpClient.GetAsync(packageUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                if (!response.IsSuccessStatusCode) throw new HttpRequestException($"下载模块包失败（HTTP {(int)response.StatusCode} {response.ReasonPhrase}）：{packageUri}");
                await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var target = File.Create(zipPath);
                await source.CopyToAsync(target, cancellationToken);
            }

            if (!string.IsNullOrWhiteSpace(manifest.Sha256))
            {
                await using var stream = File.OpenRead(zipPath);
                var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
                if (!string.Equals(actual, manifest.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException($"模块 {manifest.Id} 校验失败：SHA-256 不匹配。");
            }

            var extractPath = Path.Combine(staging, "content");
            ZipFile.ExtractToDirectory(zipPath, extractPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (Directory.Exists(destination)) Directory.Delete(destination, true);
            Directory.Move(extractPath, destination);
            File.WriteAllText(marker, manifest.Sha256.Trim());
            return destination;
        }
        finally
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }

    private static void ValidateManifest(VersionManifest manifest, string expectedId, string expectedVersion)
    {
        if (manifest.Schema != 1) throw new InvalidDataException($"模块 {expectedId} 使用了不支持的 schema {manifest.Schema}。");
        if (!string.Equals(manifest.Id, expectedId, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("版本清单中的模块 ID 不匹配。");
        if (!string.Equals(manifest.Version, expectedVersion, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("版本清单与 index.json 的 latest 不匹配。");
        if (string.IsNullOrWhiteSpace(manifest.Entry) || string.IsNullOrWhiteSpace(manifest.Package)) throw new InvalidDataException($"模块 {expectedId} 的版本清单不完整。");
    }

    private static IToolboxModule LoadFromDirectory(string directory, string? entryAssembly, string expectedId)
    {
        var assemblyPath = entryAssembly is null
            ? Directory.EnumerateFiles(directory, "FlashToolbox.Module.*.dll", SearchOption.AllDirectories).FirstOrDefault()
            : Path.Combine(directory, entryAssembly);
        if (assemblyPath is null || !File.Exists(assemblyPath)) throw new FileNotFoundException($"模块 {expectedId} 缺少入口 DLL。", assemblyPath);

        var context = new ModuleLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(Path.GetFullPath(assemblyPath));
        var moduleType = assembly.GetTypes().FirstOrDefault(type => !type.IsAbstract && typeof(IToolboxModule).IsAssignableFrom(type))
            ?? throw new InvalidOperationException($"{assemblyPath} 中未找到 IToolboxModule 实现。");
        var module = (IToolboxModule?)Activator.CreateInstance(moduleType) ?? throw new InvalidOperationException($"无法创建模块 {expectedId}。");
        if (!string.Equals(module.Id, expectedId, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"模块 Id 不匹配：期望 {expectedId}，实际 {module.Id}。");
        return module;
    }

    private static string DescribeError(Exception? error) => error switch
    {
        TaskCanceledException => "云端请求超时",
        JsonException => $"云端清单格式错误（{error.Message}）",
        _ => error?.Message ?? "未知错误",
    };

    public void Dispose() => _httpClient.Dispose();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed class ModuleLoadContext(string entryAssemblyPath) : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver = new(entryAssemblyPath);
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == typeof(IToolboxModule).Assembly.GetName().Name) return typeof(IToolboxModule).Assembly;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is null ? null : LoadFromAssemblyPath(path);
        }
    }

    private sealed record ModuleIndex(int Schema, DateTimeOffset UpdatedAt, List<ModuleIndexItem> Modules)
    {
        public ModuleIndex() : this(0, default, []) { }
    }

    private sealed record ModuleIndexItem(string Id, string Name, string Latest, string Manifest)
    {
        public ModuleIndexItem() : this("", "", "", "") { }
    }

    private sealed record VersionManifest(int Schema, string Id, string Version, string Entry, string Runtime, string Package, string Sha256, long Size, List<string> Depends, string MinShell)
    {
        public VersionManifest() : this(0, "", "", "", "", "", "", 0, [], "") { }
    }
}
