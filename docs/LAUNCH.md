# 第二阶段：带动画启动 Codex

这是外置启动器。它通过 Windows 已注册的 AppsFolder 入口启动客户端，或复用已经打开的窗口。提供手动、可恢复的快捷方式接入；不会修改官方文件、扫描其他磁盘或提供进入软件后的常驻人物背景。

## 运行

先运行 `scripts/build.ps1`，然后选择：

- `Launch.cmd` 或 `build/DragonCodexBoot/Codex Aura.lnk`：播放动画并交接给 Codex。
- `Preview.cmd`：只看动画，保留暂停、重播和跳转分镜。
- `DragonCodexBoot.exe --launch`：明确启动模式。
- 无参数 / `--preview`：独立预览。不要把自检当预览或启动入口。

解压公开包后直接运行对应 CMD。只使用一个模式参数；混合参数和旧的自动入口替换参数会被拒绝。

## 启动时会发生什么

1. 根据配置中的进程名与安装路径特征寻找主窗口；存在时复用，最小化时恢复，否则请求系统启动入口。
2. 等到真实窗口尺寸与首帧内容都准备好，再显示动画层。等待期间整个窗口由 DWM 隐藏，避免先露出空黑框；首帧已含 Codex 标识，使用约 0.28 秒的实时窗口过渡进入动画。DWM 缩略图关闭或不可用时直接显示已准备好的首帧。播放中跟随真实窗口移动与缩放；冷启动时真实窗口刚出现仍可能短暂可见。
3. 播放现有科幻分镜。默认 16 秒，末段从第 14.6 秒开始交接。动画不增加任务栏或 Alt-Tab 条目。
4. 目标窗口须可见、可响应，并连续稳定至少约 0.6 秒。没有就绪时停留在人物画面，显示真实的窗口等待提示。此判定不是账号、模型或页面内容的加载状态。
5. 使用 DWM 实时缩略图渐显真实窗口，最后关闭动画层并把焦点交给客户端。只有覆盖层会改变位置和尺寸，Codex 的窗口布局保持原样。
6. DWM 不可用或手动关闭时，对覆盖层执行淡出，再交接真实窗口。

启动画面不再显示“Esc 跳过”按钮或提示。键盘 Esc 仍可立刻结束动画，若找到真实窗口则交给它。它不会关闭 Codex，也不会取消已经发给系统的启动请求；若启动尚未完成，Codex 仍可能稍后出现。独立预览仍支持 Esc 退出，Space / R / 数字键仅在预览中生效。

同一登录会话内只允许一个启动动画实例；重复打开会立即退出重复实例。首次窗口等待超时直接退出，返回码 2；播放中等待超时会显示提示 3 秒后退出，返回码 2；正常完成或主动跳过返回 0。

## 配置

实际配置是 exe 同目录的 `launcher.json`。源码构建目录为 `build/DragonCodexBoot/launcher.json`；公开模板为 `config/launcher.example.json`。

| 字段 | 用途 |
| --- | --- |
| `Preview.WelcomeText` / `CharacterPath` / `DurationSeconds` | 延续预览配置，人物、欢迎文字和时长通用 |
| `Volume` / `Preview.AudioPath` | 默认静音；没有音频文件时静音继续 |
| `AppLaunch` | `shell:AppsFolder\包族名!应用ID`，仅允许系统应用标识，不接受任意命令 |
| `ProcessNames` / `ProcessPathContains` | 共同约束目标进程，避免误认同名程序；不按聊天标题匹配 |
| `MaxWaitSeconds` | 首次等窗口和末段等就绪分别使用此预算，1–300 秒，默认 60；不包含正常动画时长 |
| `Launch.UseDwmTransition` | 默认 true；false 使用窗口淡出交接，旧配置省略此段时默认 true |
| `PlayerWidth` / `PlayerHeight` | 独立预览尺寸；启动模式以真实窗口尺寸为准 |
| `AutoReplaceEntrypoints` / `ScanAllLocalDrives` | 必须一直为 false；两种模式都会检查 |

动画覆盖包括标题栏在内的完整可见窗口，不强制修改客户端宽高。`MatchClientToPlayer` 和旧视频校准字段不控制本次科幻启动，不必导入 MP4。

如 Codex 安装标识变化，可只读运行 `Get-StartApps` 查找 `OpenAI.Codex` 对应的 AppID，然后更新 `AppLaunch`；不要填写 CLI 辅助进程的路径。

## 独立快捷方式

推荐入口是手动安装的 `Codex.lnk`：名称为 Codex、使用已安装客户端图标，点击后进入动画启动流程。运行 `scripts/install-codex-entry.ps1` 安装，使用 `-Action Restore` 撤销，详细操作及开始菜单重新固定步骤见 [ENTRYPOINTS.md](ENTRYPOINTS.md)。

构建会在输出目录创建 `Codex Aura.lnk`。也可以运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\create-launch-shortcut.ps1
```

如需在桌面创建独立入口，在 PowerShell 中运行：

```powershell
.\scripts\create-launch-shortcut.ps1 -Destination ([Environment]::GetFolderPath('Desktop'))
```

这个备用脚本只创建 `Codex Aura.lnk`，不枚举或修改其他快捷方式。同名但目标不同的现有入口会保留并报错。原 MSIX 注册入口及其旧固定项仍直接打开客户端；需要改用或重新固定安装后的快捷方式。

停止使用时删除自己创建的 Aura 快捷方式即可。移动构建目录会使已创建的快捷方式失效，需要在新位置重新创建；不要把临时测试目录用作长期快捷方式目标。

## 日志与验收

运行目录的 `logs/launcher.log` 记录窗口复用、启动请求、等待、DWM 连接和焦点交接结果，不记录聊天标题或内容。检查输出前保留日志在本地。

自动和人工验证范围见 [PREVIEW-TESTS.md](PREVIEW-TESTS.md)。完全退出客户端后的冷启动、多显示器/DPI、最小化恢复以及不同机器上的媒体播放仍需对应环境验收。不要为了测试而强行结束有工作进行中的客户端。
