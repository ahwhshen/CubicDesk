# 第三方组件与来源

本仓库的 Cubic Desk 原创代码采用 MIT 许可，见 [LICENSE](LICENSE)。第三方组件不因此改变其原有许可。

## FFmpeg / FFprobe

Windows 便携版随附未经修改的 Gyan FFmpeg 9.0.2 full static x64 构建，通过独立进程执行视频探测与转码。

- 构建提供方及依赖库清单：[Gyan FFmpeg Builds](https://www.gyan.dev/ffmpeg/builds/)。
- 构建对应 FFmpeg 源代码：[946fcce07b](https://github.com/FFmpeg/FFmpeg/commit/946fcce07b)。
- 上游构建镜像：[GyanD/codexffmpeg](https://github.com/GyanD/codexffmpeg/releases)。
- 构建许可：GNU GPL v3；各第三方依赖仍保留各自许可。
- 便携包的 `tools/ffmpeg/LICENSE`、`README.txt`、`component.json` 保留许可证、构建配置、来源及二进制 SHA-256。
- 本项目的 Git 源码仓库和源码 ZIP 不包含 FFmpeg 二进制。源码编译用户可从同版本便携版取得完整 `tools/ffmpeg` 目录，或从上述构建提供方取得相同组件。

## 摆件应用与图标

摆件固件、Lua 应用、应用中心、应用图标及作者说明属于各自作者。本项目通过现有设备接口与应用中心读取它们，不包含固件、应用安装包或抓取的设备网页。公开发行包不包含开发设备上提取的应用图标；连接设备后可以读取实际图标，读取失败时使用原生占位图。

协议参考项目：[clocteck/holocubic-apps](https://github.com/clocteck/holocubic-apps)。Cubic Desk 是独立的桌面管理工具。

## 音乐服务

系统媒体会话来自 Windows。在线歌词与封面来自用户选择的音乐服务，版权属于相关权利人。本项目不附带歌曲、歌词正文或专辑封面，不读取音乐账号凭据。使用在线匹配会查询当前歌名、歌手及版本信息，服务可用性取决于上游接口。
