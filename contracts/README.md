# 跨平台约定

此目录用于保存 Windows 与 macOS 实现共同遵循的产品行为，不存放 C# 或 Swift 平台代码。

后续可在这里添加：

```text
contracts/
├─ api.md             API 路径、请求头、错误码和认证流程
└─ fixtures/          已脱敏的 JSON 响应样例
```

两个平台应针对相同的响应样例验证 JSON 解析、Token 刷新、会话绑定和配额计算。样例中不得包含真实服务器地址、Token 或用户信息。
