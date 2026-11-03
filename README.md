# Cubic Desk

> 用途：介绍项目功能、版本下载与源码和便携版安装方式。

面向 HoloCubic / Cubic 摆件的 **Windows 原生桌面管理工具**。提供热点配网、应用与资源管理、Windows 音乐桥接、封面歌词同步，以及白色液态玻璃界面。

使用 WPF、C# 5 和 .NET Framework 4.8。无需浏览器、Node.js、.NET SDK，也不需要部署服务器。

[下载最新版本](https://github.com/ahwhshen/CubicDesk/releases/latest) · [完整使用说明](docs/使用说明.md) · [文件职责导航](docs/文件说明.md) · [更新记录](CHANGELOG.md)

## 选择下载版本

| 版本 | 下载文件 | 适合谁 |
| --- | --- | --- |
| Windows 打包版 | `CubicDesk-v1.5.1-windows-x64.zip` | 直接使用，已含程序、桥接脚本与 FFmpeg / FFprobe |
| 源码版 | `CubicDesk-v1.5.1-source.zip`，或克隆仓库 | 修改界面、接入功能、自行编译 |
| 校验文件 | `SHA256SUMS.txt` | 核对两个 ZIP 的完整性 |

## 安装打包版

1. 从 [Releases](https://github.com/ahwhshen/CubicDesk/releases) 下载 Windows x64 ZIP。
2. **解压整个压缩包**到可写目录，例如 `D:\Program\CubicDesk`；不要直接在压缩包内运行。
3. 打开解压后的 `CubicDesk` 文件夹，双击 `CubicDesk.exe`。
4. 保留旁边的脚本、`Theme.xaml`、`.config`、`dependencies.json`、`assets` 和 `tools` 目录。FFmpeg 已内置，无需另行配置。
5. 可为 `CubicDesk.exe` 创建桌面快捷方式。升级时彻底退出旧版，替换程序文件并保留自己的 `data` 文件夹。

系统要求：Windows 10 1809 或更高版本／Windows 11，x64，Windows PowerShell 5.1 与 .NET Framework 4.8。日常操作无需管理员权限；Windows 自身可能对热点、无线扫描或用户主动选择的端口放行提出要求。

## 第一次连接摆件

**尚未配网：**

1. 让摆件进入屏幕显示 **WiFi setup** 的状态。
2. 在“发现设备”点击“扫描摆件热点”，查找 `clocteck-cubic` 前缀的网络。
3. 右侧默认“本机移动热点”，自动使用电脑热点名称和密码；也可切换“其他 Wi-Fi”填写目标网络。
4. 在找到的摆件卡片点击“连接并配网”。程序临时连接摆件热点、提交目标网络信息，再尝试恢复电脑原网络并确认摆件新地址。

**已完成配网：** 在“发现设备”展开“已联网摆件：按地址连接”，输入摆件屏幕上的局域网 IP。电脑和摆件需要能够互相访问。

建议使用摆件支持的 2.4 GHz 网络。需要网页登录认证的校园网可先在电脑认证，再由电脑移动热点共享。仅扫描不会切换网络；配网切换受网卡驱动和原网络保存配置影响，恢复失败时会显示实际错误。

## 主要功能

- **应用管理**：读取实际安装的应用，打开应用、查看图标、编辑可识别的简单配置字段。
- **应用商店**：通过摆件读取在线目录，提供搜索、依赖提示和新应用安装入口；上游连接失败时显示错误并使用本地清单。
- **资源管理**：管理图片、GIF、音乐、LRC、视频和游戏文件；图片适配 320 × 240，视频使用内置组件转换。
- **设备设置**：亮度、语言、时区、天气地址、开机应用等。保存前读取并合并最新设置，保留未知字段并备份。
- **依赖安装**：统计应用需要的电脑环境或桥接，提供下载与配置说明，人工标记准备状态。
- **后台常驻**：关闭窗口后收起到托盘。开机启动可选择“关闭 / 前端 / 仅后端”；彻底退出在托盘菜单中操作。

应用图标优先从网上应用中心获取，再尝试设备本地图标。公开包不附带开发设备的图标、账号、日志、网络配置或备份。

## 同步 Windows 音乐

1. 连接摆件，进入“本机工具”。
2. 安装随附的 SMTC 桥接组件，点击“启动桥接”。
3. 在 QQ 音乐、网易云或其他支持 Windows 系统媒体会话的播放器中播放歌曲。
4. 检查自动填写的电脑地址，点击“写入摆件并打开 Windows 音乐”。摆件需要已安装兼容的 SMTC Music 应用。
5. 开启“自动匹配在线歌词”。系统媒体封面优先，缺失时可开启在线专辑封面补取。

桥接同步歌名、歌手、进度、封面和播放控制。在线歌词／封面按歌名、歌手和时长匹配，失败后自动重试；也可导入本地 LRC。只接受当前配对摆件与本机请求。

音乐软件必须提供系统媒体会话。在线匹配会向所选服务查询歌曲信息，不读取音乐账号凭据、不下载歌曲；服务、版权与网络条件可能影响可用性。

## 使用源码版

下载源码 ZIP 后解压，或执行：

```powershell
git clone https://github.com/ahwhshen/CubicDesk.git
cd CubicDesk
```

在项目根目录运行：

```powershell
.\build.ps1
.\outputs\CubicDesk\CubicDesk.exe
```

脚本直接使用系统 `.NET Framework` 编译器，兼容 C# 5，无需恢复 NuGet 包。若 PowerShell 提示文件来自 Internet，可先在文件属性中解除阻止；请自行检查脚本后再运行。

源码 ZIP 不含 FFmpeg 二进制。需要视频转码或重新打包时，将同版本 Windows 打包版中的 `tools\ffmpeg` 整个目录复制到本地 `outputs\CubicDesk\tools\ffmpeg`。

同时生成干净的源码包和 Windows 完整包：

```powershell
.\package.ps1 -Version 1.5.1
```

产物位于 `outputs\releases`。也可通过 `-MediaToolsPath` 指定完整媒体组件目录。打包脚本会核对组件哈希，只包含明确允许的源码、程序与说明，不带个人 `data` 或开发临时文件。

## 项目结构

```text
src/                  WPF 界面、设备通信、配网、音乐桥接与默认依赖清单
docs/                 完整使用说明
build.ps1             使用系统编译器构建
package.ps1           构建源码版、完整便携版及校验文件
outputs/              本地构建与发布产物（不进入 Git）
work/                 临时分析与测试（不进入 Git）
```

## 兼容与验证边界

已在开发环境验证编译、实机管理连接、热点扫描、QQ 音乐封面与歌词传输，以及网易云公开歌词／封面接口。完整 Wi-Fi 切换与恢复、应用商店安装流程、其他播放器实播仍需要结合实际设备和网络验证。

目前仅支持兼容现有 Cubic 后台协议的设备。部分应用的复杂专有设置尚未适配；固件 `1.203` 缺少新版服务启停接口时相应按钮会禁用。本工具不替代固件或设备应用。

## 许可与致谢

目前项目刚开始，还有很多不足，如果有想法或者建议欢迎共同讨论

Cubic Desk 原创代码使用 [MIT License](LICENSE)。FFmpeg / FFprobe 及设备应用资源保留各自许可，详见 [第三方说明](THIRD_PARTY_NOTICES.md)。

设备应用与协议参考 [clocteck/holocubic-apps](https://github.com/clocteck/holocubic-apps)。本项目是独立的 Windows 管理工具。
