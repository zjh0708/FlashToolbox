# FlashToolbox

FlashToolbox 是 .NET 8 WinForms 模块化工具壳。主程序只保留入口和模块加载能力；当前模块为投屏（`screen-mirror`）和设备信息（`device-info`），其余入口显示“模块未上架”。

## 构建

```powershell
dotnet build FlashToolbox.sln
dotnet run --project FlashToolbox/FlashToolbox.csproj
```

工程目标为 `net8.0-windows`，已启用 `EnableWindowsTargeting`，实际运行需要 Windows。

## 云端模块契约

默认 `ModuleBaseUrl` 为 `https://powss.xyz/modules/`（证书域名 `powss.xyz`；勿用 IP 的 HTTPS）。可编辑输出目录旁的 `modules.json` 修改。BaseUrl 必须指向包含以下内容的目录：

```text
index.json
{id}/manifest.json
{id}/{version}/manifest.json
{id}/{version}/module.zip
```

`index.json` 是 schema 1 全量清单，条目的 `manifest` 指向版本 manifest。版本 manifest 包含 `entry`、`runtime`、`package`、`sha256`、`size`、`depends` 和 `minShell`。当前鉴权方式为公开 GET。下载包在 SHA-256 非空时校验，并解压到 `%LocalAppData%/FlashToolbox/modules/{id}/{version}/` 后通过独立 `AssemblyLoadContext` 启动。

开发时也可把已发布模块放进应用目录的 `modules-local/{id}/`；云端不可用时加载器会从这里兜底。

## 打包与本地静态服务

```powershell
./scripts/pack-modules.ps1
cd module-server
python -m http.server 8080
```

脚本会发布两个模块、生成 zip 和 SHA-256，并按云端契约写入 `./module-server/`。本地调试时将 `FlashToolbox/modules.json`（或构建输出中的副本）改为：

```json
{ "ModuleBaseUrl": "http://127.0.0.1:8080/" }
```

## scrcpy

投屏模块不包含预览控件，只启动 `scrcpy`。请先安装 adb 与 scrcpy，并确保 `scrcpy.exe`（或 `scrcpy`）可从 `PATH` 找到；连接序列号存在时模块会执行等价于 `scrcpy -s <serial>` 的命令。
