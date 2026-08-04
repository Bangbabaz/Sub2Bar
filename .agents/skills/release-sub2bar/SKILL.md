---
name: release-sub2bar
description: "Publish and revise Sub2Bar versions end to end, including version bumps, Windows packaging, Git commit and push, tag and GitHub Release creation, asset verification, and Chinese release notes grouped as 新功能、优化、修复、移除 with empty sections omitted. Use when the user asks to 发布版本、发版、创建或修改 GitHub Release、生成发布说明，或检查 Sub2Bar 发布结果。"
---

# 发布 Sub2Bar

遵循本流程准备或发布 Sub2Bar 版本。当前公开版本只发布 Windows 10/11 x64 安装包，不要求 macOS 产物。

## 确认授权与范围

1. 读取当前分支、上游、工作区差异、远端标签和现有 Releases。
2. 将用户明确指定的版本作为目标版本；版本必须是无前导 `v` 的稳定语义版本 `x.y.z`。
3. 仅在用户明确要求提交、推送、发布或修改 Release 时执行相应外部写操作。只要求生成发布说明时，不提交、不推送、不创建标签或 Release。
4. 保留并审阅用户已有改动；不得撤销不属于本次发布的改动。发现无法安全纳入发布的无关改动时，先说明阻碍。

## 汇总版本内容

1. 找到目标版本之前最近的稳定版本标签，按语义版本排序。
2. 同时检查该标签到 `HEAD` 的提交和代码差异，不得只改写提交标题。
3. 只记录用户可感知的最终结果。合并同一结果涉及的多个提交，拆分单个提交中的独立结果。
4. 排除纯文档、测试、格式、内部重构和版本号更新，除非它们直接改变用户可见行为或发布产物。

## 编写 Release Notes

正文只允许使用以下四个二级标题，并保持固定顺序：

1. `## 新功能`：新增此前无法完成的能力。
2. `## 优化`：改善既有功能的体验、性能或流程，但不是修复错误。
3. `## 修复`：恢复预期行为，解决错误、兼容性或稳定性问题。
4. `## 移除`：删除此前可用的功能、入口或行为。

遵循以下格式规则：

- 没有内容的分类完全省略，不得保留空标题或写“无”。
- 不得添加 `## 更新内容`、摘要、提交清单或其他分类。
- 使用简体中文和单层无序列表，从用户视角描述具体结果。
- 每项只归入一个最准确的分类，不得重复。
- 不猜测无法从用户说明、提交或差异确认的内容。

示例：

```markdown
## 新功能

- 新增始终置顶开关，可随时控制浮窗置顶状态

## 修复

- 修复安装包版本信息不正确的问题

## 移除

- 移除托盘菜单中设置与退出之间的分隔线
```

## 更新版本号

将目标版本同步到以下位置：

- `windows/Directory.Build.props`：`VersionPrefix`、`AssemblyVersion`、`FileVersion`、`InformationalVersion`
- `windows/src/Sub2Bar.Windows/app.manifest`：`assemblyIdentity version`
- `windows/installer/Sub2Bar.iss`：默认 `AppVersion`
- `windows/README.md`：Windows 打包命令示例中的版本号

`AssemblyVersion`、`FileVersion` 和清单版本使用 `x.y.z.0`，其余位置使用 `x.y.z`。不要修改尚未公开的 macOS 示例版本，除非本次明确发布 macOS。

## 生成 Windows 安装包

1. 确认已安装 .NET SDK、Inno Setup 6 和已认证的 GitHub CLI。
2. 从仓库根目录执行：

```powershell
.\scripts\package-windows.ps1 -Version <目标版本>
```

3. 使用脚本生成 `windows/artifacts/Sub2Bar-<目标版本>-win-x64-setup.exe`。该脚本负责测试、还原、发布、EXE 版本检查、WebView2 引导程序下载、安装器编译和安装器版本检查。
4. 不启动应用，也不额外运行单独的构建命令进行验证。
5. 计算安装包 SHA-256，并确认文件版本与目标版本一致。

## 提交并推送

1. 执行 `git diff --check` 并再次核对发布范围与所有版本字段。
2. 只暂存本次发布相关文件；用户明确要求提交全部当前改动时，先确认它们都属于发布范围。
3. 使用 `Release v<目标版本>` 作为版本提交标题，正文简述主要内容。
4. 推送当前分支到上游，并确认远端分支提交与本地 `HEAD` 完全一致。
5. 读取 `git rev-parse HEAD` 的完整哈希；不得手工补全短哈希。

## 创建 GitHub Release

1. 使用 `gh release create v<目标版本>` 创建正式 Release。
2. 使用 `--target <完整 HEAD 哈希>`，标题使用 `Sub2Bar v<目标版本>`，正文使用按上述规则生成的 Release Notes。
3. 只上传 `Sub2Bar-<目标版本>-win-x64-setup.exe`。不要调用本地被忽略的 `scripts/package-and-release.ps1`，它要求当前不存在的 macOS 产物。
4. 正式版本标记为 Latest；只有用户明确要求时才创建草稿或预发布版本。

## 修改现有 Release

用户要求调整已发布版本的说明时，先读取当前 Release 和对应标签，仅用 `gh release edit` 修改指定字段。不得重新打包、移动标签或替换资产，除非用户明确要求。

## 发布后核对

逐项确认：

- 标签指向本次发布提交。
- Release 标题、标签、项目版本和安装包版本一致。
- Release 不是草稿或预发布版本，除非用户明确要求。
- Release Notes 标题顺序正确，空分类已省略。
- GitHub 资产名称、大小和 SHA-256 摘要与本地安装包一致。
- 当前分支与上游同步，工作区没有本次流程遗留的未提交源码改动。

最终向用户报告提交哈希、分支、Release 链接、资产名称和 SHA-256；未执行的测试或检查需明确说明。
