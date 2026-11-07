# Codex 入口安装与恢复

复用上游 Windows 的可恢复 `.lnk` 方案：保留已有名称、图标和备份，将目标改为启动器的 `--launch`。缺少入口时创建名为 Codex、使用已安装客户端图标的快捷方式。

这是手动安装。预览和启动本身都不会改快捷方式；`AutoReplaceEntrypoints` 与 `ScanAllLocalDrives` 在公开模板和实际配置中必须保持 false。

## 安装

先构建项目，或把公开包解压到长期保留的目录。在项目/解压根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\install-codex-entry.ps1
```

安装脚本只处理当前用户的两个确切路径：

- 桌面目录中的 `Codex.lnk`。
- 开始菜单 Programs 目录中的 `Codex.lnk`。

它读取已安装应用的清单以确认 AppsFolder ID 和图标路径，不遍历目录或其他磁盘，不改公共目录、其他用户、应用注册或官方客户端文件。同名但指向无关软件的快捷方式会保留并报错。源码中的高级 `-SearchRoots` 仅供隔离测试；日常安装使用上面的包装脚本。

## 开始菜单和任务栏

安装后可点击桌面的 **Codex** 启动动画。若平时使用开始菜单固定项：

1. 对桌面的新 **Codex** 快捷方式点右键，选择“固定到‘开始’屏幕”；Windows 版本不同可能需要“显示更多选项”。
2. 从开始菜单的新固定项启动。旧的官方应用固定项仍直接启动客户端，可取消旧项固定以免混淆。
3. 任务栏同理：需要固定新快捷方式；已经固定的官方入口不会自动换目标。

名称和图标可以相同，但 MSIX 注册的应用入口、应用搜索结果、直接运行官方 exe、协议和其他程序的内部调用不在快捷方式替换范围内。没有后台监视器、系统服务或全局启动拦截。

## 检查与恢复

```powershell
.\scripts\install-codex-entry.ps1 -Action Verify
.\scripts\install-codex-entry.ps1 -Action Restore
```

报告位于程序目录 `.integration/report.json`，原文件备份在 `.integration/backups/`，状态与 SHA-256 校验在 `.integration/entrypoints-state.json`。这些文件只留本地，不能在恢复前清理。脚本报告的 `StartPinsAutomaticallyChanged: false` 表示脚本没有操作固定项；通过界面手动固定不由此报告追踪。

恢复时，只有内容仍与安装后哈希一致的入口才会恢复原文件；本工具新建的入口会移除。同名入口后来被用户改动或删除时保留用户状态，备份校验失败时拒绝覆盖。恢复不会自动取消开始菜单或任务栏的固定项，需自行取消。

移动或删除程序目录前先恢复入口，再从新目录重新安装和固定。不要只移动 exe；配置、素材与恢复记录也需保留。

`create-launch-shortcut.ps1` 仍可创建独立的 Codex Aura 备用入口。旧的 `--integrate-entrypoints`、`--restore-entrypoints` 参数及旧 CMD 不用于本版本；使用上述脚本完成操作。
