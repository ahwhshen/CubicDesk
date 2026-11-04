# 程序源码

> 用途：说明原生桌面程序的模块划分与阅读顺序。

- **启动与常驻**：Program、ResidentApplication、MainWindow、BackgroundPage、TrayMenu。
- **设备与配网**：Core、WifiRadio、ProvisioningPage、Native、DevicePages。
- **应用和资源**：AppStore、AppIcons、ConfigEditor、MediaPages。
- **音乐桥接**：MusicBridge、SmtcWorker、SmtcThumbnail、QQMusicLyrics、OnlineLyrics、OnlineArtwork。
- **依赖管理**：DependencyCatalog、DependencyCenter、DependencyPages、dependencies.json。
- **界面与验证**：Glass、Theme、app.ico、CubicDesk.exe.config、Diagnostics、Firewall。

完整的逐文件说明见 [文件职责导航](../docs/文件说明.md)，构建方式见 [项目首页](../README.md)。

本目录保持 C# 5 与 .NET Framework 4.8 兼容；从项目根目录运行 `build.ps1` 构建。
