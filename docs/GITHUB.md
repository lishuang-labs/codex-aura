# 发布到 GitHub

目标仓库：[lishuang-labs/codex-aura](https://github.com/lishuang-labs/codex-aura)。仓库根目录应为 `codex-aura`，不是其父目录；父目录的 `references` 不属于项目源码。

## 适合提交

| 内容 | 处理 |
| --- | --- |
| `src/`、`tests/`、`config/` | 提交代码、公开模板和不依赖私人素材的测试 |
| `scripts/` | 提交构建、测试、公开打包与检查脚本；继承的集成脚本注明当前不启用 |
| `README.md`、`docs/`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`CHANGELOG.md`、`CONTRIBUTING.md`、`VERSION` | 提交，保留来源和许可，并准确说明预览阶段 |
| `.gitignore`、`.gitattributes`、`Preview.cmd` | 提交 |
| `assets/character-placeholder.png`、`assets/README.md` | 提交代码绘制的占位素材及说明 |
| `macos/`、`src/macos/` 和对应说明 | 可作为上游继承源码提交；不宣称已验证当前 macOS 功能 |
| `media/MEDIA_NOTICE.md` | 保留媒体许可边界说明；不提交视频文件 |

## 保留本地

- `references/`：原手机录屏、关键帧及参考人物图片。
- `assets/` 中除 README 和占位剪影外的所有文件：人物 JPG/GIF、参考角色重绘图、生成提示词和其他私人素材。未确认再分发权限前不公开；经过重绘不自动解决原角色素材的权利问题。
- `media/startup.mp4` 及其他视频、音频、图标。
- `build/`、`dist/`、`.local/`、`qa/`、`logs/`、`.integration/`、实际 `launcher.json`。
- 密钥、凭据、环境文件、证书、快捷方式、临时文件。

以上由 `.gitignore` 和公开文件检查脚本共同约束。`.gitignore` 不会自动停止跟踪已经提交过的文件，因此检查脚本也审查已跟踪路径。

## 发布前检查

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-repository.ps1 -SourceOnly
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\prepare-public-source.ps1
```

后者在 `.local/public-source-<随机标识>/` 生成只包含候选源码的副本，并记录 `.local/public-source-manifest.json`（相对路径、大小、SHA-256）。它不修改本地 Git 暂存区、不设置远端、不提交、不推送，也不上传文件。随后可在输出副本中构建和测试，以确认没有隐含的本地素材依赖。

确认候选清单后再进行 Git 初始化、提交和推送。本仓库保留上游 MIT 版权与来源链接，不把派生实现宣称为从零原创。

## Release 附件

运行 `scripts/test.ps1`、`scripts/package-preview.ps1`、`scripts/verify-repository.ps1`。

仅 `dist/Codex-Aura-Preview-Stage1.zip` 及其 `.sha256` 是当前公开预览包。包内只含程序、占位剪影、公开配置、许可证与使用说明；默认静音，关闭两项集成开关。旧的本地 ZIP 可能含私人素材，不属于本次可发布产物。

编译后的 ZIP 可手动上传到 GitHub Release，不加入源码历史。本阶段不自动发布 Release。

## 来源

基于 [dragon-codex-boot](https://github.com/yushua0808-cpu/dragon-codex-boot) 的 MIT 源码，基线提交为 `c63a203e5857e85fb3c8a9891eddb570e7a27b54`。保留原启动与窗口交接代码，当前只开放独立预览。
