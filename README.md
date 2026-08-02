# Sub2Bar

Sub2Bar 是连接用户自建 Sub2API 服务的桌面托盘客户端，用于查看个人余额、订阅额度和重置时间。管理员还可以查看选定账号及有效订阅。

当前公开版本仅支持 Windows 10/11 x64，macOS 版本将在完成后加入。

## 功能

- 常驻系统托盘，无需保持主窗口打开
- 查看个人余额、订阅额度及重置时间
- Access Token 失效后自动刷新并重试一次
- 管理员账号与订阅信息展示
- 网络异常时保留最后一次成功数据
- 支持连接带基础路径的自建 Sub2API 服务

## 安装

从本仓库的 Releases 页面下载 `Sub2Bar-<版本号>-win-x64-setup.exe` 并运行。安装器按当前用户安装，不需要管理员权限；缺少 Microsoft Edge WebView2 Runtime 时会自动安装。

首次启动后填写 Sub2API 服务器地址，并在内嵌登录页面完成认证。登录成功后应用进入系统托盘。

只应使用本仓库发布的官方安装包。Fork、第三方网盘或其他来源提供的构建不受信任。

## 系统要求

- Windows 10 22H2 或 Windows 11 x64
- 可访问用户配置的 Sub2API 服务

## Token 与本地数据

- Access Token、Refresh Token 和绑定的 User-Agent 使用 Windows DPAPI 按当前用户加密
- 应用不会把 Token 或网络响应写入日志
- 更换服务器、会话绑定不匹配或重新登录时会清理旧凭据
- WebView2 与 API 请求使用相同的 User-Agent，以满足服务端会话绑定要求

本地数据保存在 `%LocalAppData%\Sub2Bar`。卸载时可以选择同时删除设置和登录凭据。

## 代码政策

本仓库不接受 Pull Request。

反馈问题时，不要提交真实服务器地址、Access Token、Refresh Token、Cookie、凭据文件或包含这些信息的截图和日志。

Windows 端的详细运行方式、行为说明与安全设计见 [windows/README.md](windows/README.md)。
