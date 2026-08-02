# Sub2Bar Windows

Sub2Bar 是连接用户自建 Sub2API 服务的 Windows 系统托盘客户端。应用默认不显示任务栏主窗口；点击托盘图标可查看个人余额、订阅额度和重置时间，管理员还可查看选定账号与有效订阅。

## 系统要求

- Windows 10 22H2 或 Windows 11 x64
- Microsoft Edge WebView2 Evergreen Runtime
- 开发环境需要 .NET 10 SDK 和 Visual Studio 2026（含 .NET 桌面开发工作负载）

正式发布物为 `win-x64` self-contained，不要求目标电脑预装 .NET。安装器会检测并按需安装 WebView2 Runtime。

## 工程结构

```text
src/Sub2Bar.Core/       模型、容错 JSON 解析、HTTP API 与刷新状态机
src/Sub2Bar.Windows/    WPF 托盘、窗口、WebView2、DPAPI 和 Windows 集成
tests/Sub2Bar.Core.Tests/ 解析器与刷新流程测试
installer/Sub2Bar.iss   per-user Inno Setup 安装脚本
../scripts/package-windows.ps1 测试、发布和安装包生成脚本
```

## 开发

```powershell
dotnet restore Sub2Bar.Windows.sln
dotnet test Sub2Bar.Windows.sln
dotnet run --project src/Sub2Bar.Windows/Sub2Bar.Windows.csproj
```

首次启动会打开设置窗口。服务器地址支持带基础路径，例如 `https://example.com/sub2api`；保存后通过内嵌 WebView2 登录。登录成功后应用进入托盘，所有 API URL 都保留该基础路径。

## 发布

安装 Inno Setup 6 后执行：

```powershell
..\scripts\package-windows.ps1 -Version 0.1.0
```

发布目录为 `artifacts/publish`，安装包输出到 `artifacts`。安装范围为当前用户，默认目录是 `%LocalAppData%\Programs\Sub2Bar`，不需要管理员权限。

## 本地数据与安全

- 普通设置：`%LocalAppData%\Sub2Bar\settings.json`
- 登录凭据：`%LocalAppData%\Sub2Bar\credentials.dat`
- WebView2 用户数据：`%LocalAppData%\Sub2Bar\WebView2`

Access Token、Refresh Token 和绑定的 User-Agent 由 Windows DPAPI 以 `CurrentUser` 范围加密。应用不写网络响应或 Token 日志。更换服务器地址、会话绑定不匹配、Refresh Token 被拒绝或点击“重新登录”时会清理凭据和 WebView2 登录会话。

WebView2 与 API 请求使用完全相同的 User-Agent。登录脚本只在顶层页面工作，只捕获 `/api/v1/auth/login` 的成功响应；宿主还会严格核对消息来源的 scheme、host 和有效端口。

## 行为说明

- GET 传输错误最多重试一次，Refresh Token 的 POST 不自动重试。
- Access Token 被拒绝时最多刷新一次并完整重试；`SESSION_BINDING_MISMATCH` 直接要求重新登录。
- 网络或服务端错误会保留最后一次成功的个人额度，管理员接口失败不会清空个人数据。
- 刷新周期只在应用进程存活期间运行，不安装 Windows 后台服务。
- 管理员账号列表按接口约定固定读取第一页、每页 20 项；浮窗仅显示设置中勾选的账号。
- 升级安装保留设置和凭据；卸载时可选择同时删除个人数据，并始终删除开机启动项。
