$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$serverRoot = Join-Path $root 'module-server'
$packRoot = Join-Path $root '.module-pack'
$modules = @(
    @{ Id = 'screen-mirror'; Name = '投屏'; Version = '1.0.0'; Project = 'modules/ScreenMirror/FlashToolbox.Module.ScreenMirror/FlashToolbox.Module.ScreenMirror.csproj'; Entry = 'FlashToolbox.Module.ScreenMirror.dll'; Depends = @('adb', 'scrcpy') },
    @{ Id = 'device-info'; Name = '设备信息'; Version = '1.0.0'; Project = 'modules/DeviceInfo/FlashToolbox.Module.DeviceInfo/FlashToolbox.Module.DeviceInfo.csproj'; Entry = 'FlashToolbox.Module.DeviceInfo.dll'; Depends = @('adb') }
)

if (Test-Path $serverRoot) { Remove-Item $serverRoot -Recurse -Force }
if (Test-Path $packRoot) { Remove-Item $packRoot -Recurse -Force }
New-Item $serverRoot -ItemType Directory -Force | Out-Null

$indexItems = @()
foreach ($module in $modules) {
    $publish = Join-Path $packRoot $module.Id
    dotnet publish (Join-Path $root $module.Project) -c Release -o $publish --nologo
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish $($module.Id) 失败。" }
    Remove-Item (Join-Path $publish 'FlashToolbox.Contracts.dll') -ErrorAction SilentlyContinue
    Remove-Item (Join-Path $publish 'FlashToolbox.Contracts.pdb') -ErrorAction SilentlyContinue

    $versionDirectory = Join-Path $serverRoot "$($module.Id)/$($module.Version)"
    New-Item $versionDirectory -ItemType Directory -Force | Out-Null
    $packagePath = Join-Path $versionDirectory 'module.zip'
    Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $packagePath -Force
    $package = Get-Item $packagePath
    [ordered]@{
        schema = 1; id = $module.Id; version = $module.Version; entry = $module.Entry
        runtime = 'net8.0-windows'; package = 'module.zip'
        sha256 = (Get-FileHash $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
        size = $package.Length; depends = $module.Depends; minShell = '1.0.0'
    } | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $versionDirectory 'manifest.json') -Encoding utf8

    $moduleDirectory = Join-Path $serverRoot $module.Id
    [ordered]@{ schema = 1; id = $module.Id; latest = $module.Version; manifest = "$($module.Id)/$($module.Version)/manifest.json" } |
        ConvertTo-Json -Depth 4 | Set-Content (Join-Path $moduleDirectory 'manifest.json') -Encoding utf8
    $indexItems += [ordered]@{ id = $module.Id; name = $module.Name; latest = $module.Version; manifest = "$($module.Id)/$($module.Version)/manifest.json" }
}

[ordered]@{ schema = 1; updatedAt = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ'); modules = $indexItems } |
    ConvertTo-Json -Depth 5 | Set-Content (Join-Path $serverRoot 'index.json') -Encoding utf8
Remove-Item $packRoot -Recurse -Force
Write-Host "模块静态站已生成到 $serverRoot"
