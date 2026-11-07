# 独立动画预览

## 运行与按键

从源码构建后运行 `Preview.cmd` 或 `build/DragonCodexBoot/DragonCodexBoot.exe --preview`。解压发布包后运行同目录的 `Preview.cmd`。

无参数打开 exe 也是预览。默认静音、16 秒、16:9 画面；结束淡出到黑场并关闭。

- Esc：关闭预览。
- Space：暂停 / 继续。
- R：回到开头，保留暂停状态。
- 1–5：跳到相应分镜，保留暂停状态。

`--self-test` 仅检查配置和几何，不显示动画。`--preview-frames` 用同一绘制器导出 8 个时间点的 PNG 到运行目录 `qa/preview/`，不启动真实客户端。

## 默认分镜

| 时间 | 内容 |
| --- | --- |
| 0–2.1 秒 | 黑底、小图案与中央 Codex 标题 |
| 1.6–7 秒 | 紫灰界面、多层旋转圆环、刻度、扫描线、左右装饰面板 |
| 7–11.5 秒 | 界面暗下，粒子聚合并显现人物图层 |
| 11.4–14.6 秒 | 右侧人物、左侧欢迎文字与 Codex，背景粒子和线条持续运动 |
| 14.6–16 秒 | 全画面淡出到黑场后关闭 |

分镜有意重叠；总时长改变时等比例缩放。装饰文案不表示真实账号验证、模型加载或后台进度。

## 公开模板与本地配置

公开模板为 `config/launcher.example.json`。实际读取 exe 同目录的 `launcher.json`；从源码构建时位于 `build/DragonCodexBoot/launcher.json`。

公开模板默认使用 `assets/character-placeholder.png`。构建不会覆盖已经存在的欢迎文字或人物路径，但会确保两个集成开关保持 false。本地自定义图片及配置不会随公开包分发。

```json
{
  "AutoReplaceEntrypoints": false,
  "ScanAllLocalDrives": false,
  "Volume": 0,
  "Preview": {
    "WelcomeText": "欢迎回来，创造者。",
    "DurationSeconds": 16,
    "CharacterPath": "assets/character-placeholder.png",
    "CharacterIsPlaceholder": true,
    "AudioPath": ""
  }
}
```

这是相关字段节选，编辑实际配置中的对应字段即可。

| 字段 | 说明 |
| --- | --- |
| `WelcomeText` | 最多 120 字，可为空；超出左侧区域的长文字省略 |
| `DurationSeconds` | 8–120 秒，默认 16 |
| `CharacterPath` | PNG / JPG / JPEG / GIF 本地路径；相对路径以 exe 目录为基准 |
| `CharacterIsPlaceholder` | 占位素材为 true，自定义人物为 false |
| `AudioPath` | 可选本地 MP3 / WAV，默认空 |
| `Volume` | 顶层字段，0–1，默认 0 |

将自己的图片放到运行目录 `assets/`，填写相对路径，例如 `assets/my-character.png`。素材文件须自行确认使用权限，公开仓库仅包含占位剪影。建议透明 PNG、约 1200×1600、头发和肩部边缘完整，单文件不超过 40 MB。人物保持比例放在右侧、底部对齐；不透明图片会在显示时柔化边缘。

GIF 按原始帧间隔循环，暂停、重播与跳转和预览时间轴同步，解码缓存上限为 128 MB。静态图片没有角色动作，不支持人物 MP4 视频。素材无法加载时回退到明确标注的占位剪影；音频缺失时静音继续。URL 和网络共享路径不接受。

## 与启动模式的边界

本文的无参数、`--preview`、`--preview-frames` 和 `--self-test` 模式不会启动或操作真实 Codex。第二阶段新增独立的 `--launch`，使用原有 `BootWindow` 执行客户端启动、探测和 DWM 交接，详见 [LAUNCH.md](LAUNCH.md)。混合模式参数会被拒绝。

两种模式都要求关闭自动替换入口和全盘扫描，不修改官方客户端。macOS 和原快捷方式替换脚本属于保留的上游实现，不属于当前验证范围。
