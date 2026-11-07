# Codex Aura

面向 Windows Codex 的可定制科幻启动动画项目。支持 **独立预览** 和 **外置启动** 两种模式：WPF 绘制紫灰圆环、扫描线、装饰面板和粒子聚合，支持可替换的人物图片及 GIF。

**独立社区项目，与 OpenAI 无隶属关系。** 无参数运行仍为独立预览；只有显式 `--launch` 才启动或复用 Codex。两种模式均不修改官方客户端，不扫描磁盘，不自动替换快捷方式。

## 预览效果

黑底图案与标题 → 环形科幻界面展开 → 界面暗下与粒子聚合 → 右侧人物、左侧欢迎文字 → 淡出关闭。

公开版本附带代码绘制的占位剪影。人物 PNG/JPG/GIF 与可选音频由使用者自行提供；静态图片不会被描述为角色动画。

## 快速开始

环境：Windows 10/11 x64、.NET Framework 4.8。无需安装 Codex、Node、Python、NuGet 或配置 API key。

在仓库根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
.\Preview.cmd
```

也可以从已经解压的预览包中运行 `Preview.cmd`。默认静音、16 秒；Esc 退出，Space 暂停/继续，R 重播，1–5 跳到对应分镜。

要带动画启动真实客户端，运行 `Launch.cmd` 或构建目录中的 `Codex Aura.lnk`。启动模式在末段等待真实窗口稳定后交接，Esc 跳过；Space / R / 数字跳转只用于预览。本机需先安装 Codex，启动设置和快捷方式见 [启动说明](docs/LAUNCH.md)。

要从名为 **Codex**、使用本机客户端图标的入口启动，手动运行 `scripts/install-codex-entry.ps1`。它只处理当前用户桌面和开始菜单程序目录中的两个 `Codex.lnk`，复用上游备份与恢复机制；已有固定项需重新固定到这个快捷方式。动画全程跟随真实窗口位置和尺寸，任务栏与 Alt-Tab 不增加动画程序条目。详见 [入口安装与恢复](docs/ENTRYPOINTS.md)。

实际配置位于 `build/DragonCodexBoot/launcher.json`（解压版为 exe 同目录）。支持欢迎文字、时长、音量及人物素材路径配置，详见 [预览说明](docs/PREVIEW.md)。

## 检查与打包

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-preview.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1
```

公开包为 `dist/Codex-Aura-Stage2.zip`，包含预览、启动和手动快捷方式安装/恢复工具，始终使用占位剪影与默认静音配置，不包含本机自定义人物、音频或原参考录屏。ZIP 与 `.sha256` 适合作为 GitHub Release 附件，不加入源码提交。

## 功能边界与源码布局

- `src/SciFiPreview.cs`：独立预览、时间轴、图片与 GIF 图层。
- `src/DragonCodexBoot.cs`：复用上游启动、窗口探测与 DWM 交接逻辑；`--launch` 将现有 `BootWindow` 接到科幻绘制器上。
- `src/LaunchSession.cs`：可离线测试的窗口稳定判定、等待和淡出时间轴。
- `config/`、`scripts/`、`tests/`：公开配置、构建/验证工具及测试。
- `macos/`、`src/macos/` 和相应脚本：继承的上游实现，保留供后续维护；不属于本次 Windows 预览的验证范围。
- `scripts/install-codex-entry.ps1`：显式安装、检查和恢复两个指定快捷方式；复用 `integrate-entrypoints.ps1`，关闭自动安装和磁盘扫描。

启动模式只移动动画覆盖层，不移动或调整 Codex 窗口大小。窗口可见、响应且短时稳定后开始动画；后台画面检查确认主界面内容持续出现后才淡出，加载页期间停留在人物画面。此检查是视觉启发式，不代表登录成功、模型或网络请求就绪。目前不提供软件内常驻人物背景。

启动窗口在首帧绘制好后才显现，以 Codex 标识直接开场并短暂过渡，不显示“Esc 跳过”按钮。键盘 Esc 仍可中断启动动画，独立预览的操作保持不变。

## 来源与许可证

本项目基于 [yushua0808-cpu/dragon-codex-boot](https://github.com/yushua0808-cpu/dragon-codex-boot)，基线提交为 `c63a203e5857e85fb3c8a9891eddb570e7a27b54`。复用了原有 WPF/.NET Framework 技术方案和窗口交接源码，新增独立科幻预览、可配置文案、人物图片/GIF、分镜导出与发布隔离。

代码使用 [MIT 许可证](LICENSE)，保留上游版权声明。媒体素材单独处理，详见 [第三方说明](THIRD_PARTY_NOTICES.md)。公开源码和预览包不分发本地参考视频、角色素材或由参考角色重绘的图片。

仓库：[lishuang-labs/codex-aura](https://github.com/lishuang-labs/codex-aura)。提交范围与发布步骤见 [GitHub 发布说明](docs/GITHUB.md)。
