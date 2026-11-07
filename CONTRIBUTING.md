# 贡献说明

当前重点是 Windows 科幻预览与外置启动：时间轴、绘制效果、素材适配、DPI、窗口交接和可恢复的手动入口安装。保留原有 WPF 技术方案与上游启动/窗口交接源码。

1. Fork 仓库，在分支中修改源码。
2. Windows 变更运行 `scripts/test.ps1`。发布前运行 `scripts/package-preview.ps1` 和 `scripts/verify-repository.ps1`，并用 `scripts/prepare-public-source.ps1` 导出的源码副本检查是否依赖本地文件。
3. 提交 PR，说明具体触发条件、变化后的表现及验证范围。

保持 `AutoReplaceEntrypoints=false`、`ScanAllLocalDrives=false`、默认静音；预览不得操作真实客户端。仅显式 `--launch` 可启动或复用客户端，禁止自动替换官方快捷方式。桌面集成回归使用 `scripts/test-launch-windows.ps1` 的可丢弃窗口，真实客户端启动单独验收。macOS 源码为上游继承实现，当前未验证。

提交代码适用仓库 MIT 许可证，并保留上游版权与来源。不要提交聊天数据、账户文件、凭据、个人配置、参考录屏、角色图片或生成记录。公开素材仅包含代码绘制的占位剪影；自定义人物与音频保留本地。

几何与配置变更应补充能发现真实回归的测试。绘制、播放和桌面交互不能只用离线几何检查作为验证。发布范围见 [GitHub 发布说明](docs/GITHUB.md)。
