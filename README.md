# ccDock

[![Build](https://github.com/miralexand/ccDock/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/miralexand/ccDock/actions/workflows/build.yml)

基于 [Syncthing](https://syncthing.net/) 的 Windows 文件夹同步备份工具。ccDock 提供简洁的中文图形界面，把 Syncthing 作为内置引擎托管，用户只需要选择文件夹、填写对方设备 ID 即可建立点对点备份。

## 功能

- **备份任务**：把一个本地文件夹“仅发送”到另一台电脑，适合照片、文档等日常备份。
- **接收共享**：接收对方分享的文件夹，以“仅接收”方式保存；可选保留 30 天历史版本，降低误删风险。
- **设备管理**：查看 / 复制本机设备 ID、重命名本机、通过设备 ID 添加对方电脑、处理待确认的设备请求。
- **高级连接**：显示每台设备是“直连”还是“中继”；可为设备设置静态地址；可开关全局发现、局域网发现、中继服务和 NAT 穿透。
- **状态总览**：同步引擎状态、任务数量、在线设备、实时同步进度与错误提示。
- **托盘常驻**：关闭或最小化窗口后继续在后台同步；支持开机自动启动。
- **本地优先**：文件只在你自己的设备之间端到端加密直连传输，不经过任何第三方服务器。ccDock 不收集、不上传任何文件或使用数据。

## 下载

预编译的 Windows x64 自包含版本请前往 [Releases](https://github.com/miralexand/ccDock/releases) 下载。

## 系统要求

- Windows 10 / 11（x64）
- 若使用框架依赖版，需要 [.NET 7 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/7.0)；
  自包含版无需安装 .NET。
- 首次运行时 Windows 防火墙会询问是否允许 Syncthing 通信，请至少允许“专用网络”。
  两台电脑需要在同一局域网内，或允许 Syncthing 通过中继服务器连接。

## 构建

需要 .NET 7 SDK。

```powershell
# 框架依赖版（体积小，需要 .NET 7 Desktop Runtime）
powershell -ExecutionPolicy Bypass -File .\build.ps1

# 自包含版（无需另装 .NET，体积较大）
powershell -ExecutionPolicy Bypass -File .\build.ps1 -SelfContained
```

输出目录：`dist\ccDock`，运行 `ccDock.exe` 即可。

构建脚本会自动检查 `vendor\syncthing\syncthing-windows-amd64-v2.1.7\syncthing.exe`；
缺失时会调用 `scripts\fetch-syncthing.ps1`，从官方 GitHub Release 下载并校验 SHA-256。

### GitHub Actions

仓库已配置 `.github/workflows/build.yml`。每次推送到 `main`、提交 Pull Request，
或手动触发 workflow 时，GitHub 会在 `windows-latest` 上：

1. 安装 .NET 7 SDK；
2. 下载并校验 Syncthing v2.1.7；
3. 执行 `build.ps1 -SelfContained`；
4. 上传名为 `ccDock-win-x64` 的自包含构建产物(保留 30 天)。

在 Actions 运行页面即可下载构建好的可执行目录。

推送形如 `v1.1.1` 的 tag 时，workflow 还会把构建结果打包为 zip，
并自动创建对应的 GitHub Release:

```powershell
git tag v1.1.1
git push origin v1.1.1
```

也可以直接开发运行：

```powershell
dotnet build .\src\CcDock\CcDock.csproj
.\src\CcDock\bin\Debug\net7.0-windows\ccDock.exe
```

构建时会把 `vendor\syncthing\syncthing-windows-amd64-v2.1.7` 中的 `syncthing.exe` 和
`LICENSE.txt` 复制到输出目录的 `syncthing` 子目录。

## 使用流程

1. 在两台电脑上分别运行 ccDock。
2. 打开 **设备** 页面，复制本机设备 ID，发给对方；在对方电脑上点击 **添加设备**，粘贴设备 ID。
3. 在备份方电脑上打开 **备份任务 → 新建备份任务**，选择文件夹、填写对方设备 ID。
4. 在接收方电脑的 **概览 → 待处理的分享请求** 中点击 **接收**，选择保存位置。
5. 任务卡片会显示同步进度；文件有修改时会自动增量同步。

### 跨网段 / NAT 下游

默认使用全局发现、NAT 穿透和中继，两台设备只要能上网通常就能连接。若其中一台在另一台所在网段的下游（经过 NAT），
推荐在**下游设备**上打开 **设备 → 连接设置**，为上游设备添加静态地址：

```
dynamic
tcp://192.168.1.50:22000
```

`192.168.1.50` 是**上游设备自己的局域网 IP**。同时在防火墙上放行 TCP/UDP 22000。
下游设备会主动连接上游设备；上游设备无需能访问下游的内网地址。

若一直无法直连，可以在 **设置 → 高级连接** 中确认中继已启用，或使用 Tailscale / ZeroTier
把两台设备放入同一虚拟网络。任务卡和“设备”页会显示“直连”或“中继连接”状态。

移除任务只会删除 ccDock 中的同步配置，不会删除任何本机文件。接收端如果启用“保留历史版本”，
被覆盖或删除的旧文件会在对方电脑的 `.stversions` 目录中保留 30 天。

## 数据与日志

所有程序数据保存在 `%LOCALAPPDATA%\ccDock`：

| 路径 | 说明 |
| --- | --- |
| `%LOCALAPPDATA%\ccDock\syncthing\` | 同步引擎配置、索引与证书 |
| `%LOCALAPPDATA%\ccDock\logs\app.log` | ccDock 运行日志（2 MB 自动轮转） |
| `%LOCALAPPDATA%\ccDock\settings.json` | 窗口与托盘设置 |

同步引擎只监听回环地址 `127.0.0.1:8384`，管理 API 使用随机生成的 API Key，
不会向局域网暴露管理界面。

## 技术说明

- UI：Windows Forms (.NET 7)，纯代码布局，支持 Per-Monitor V2 高 DPI。
- 引擎：内置 Syncthing v2.1.7，随子进程启动并通过本地 REST API 管理；
  子进程加入 Windows Job Object，ccDock 退出时自动结束，不会残留孤儿进程。
- 路径与设备 ID 均在进入 Syncthing 前校验；不会通过命令行外壳执行参数。

## 许可

ccDock 自身的源代码未附带特定许可声明。内置的 Syncthing 以
[Mozilla Public License 2.0](https://www.mozilla.org/en-US/MPL/2.0/) 发布，
其完整许可见 `syncthing\LICENSE.txt`。ccDock 与 Syncthing 项目无隶属关系。
