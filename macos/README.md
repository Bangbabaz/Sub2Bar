# Sub2Bar macOS

此目录用于 Swift、AppKit 和 SwiftUI 实现的 macOS 客户端。

建议将 Xcode 工程保持为独立的平台应用：

```text
macos/
├─ Sub2Bar.xcodeproj
├─ Sub2Bar/
└─ Sub2BarTests/
```

macOS 端应使用 `WKWebView` 完成登录、Keychain 保存凭据、`NSStatusItem` 提供菜单栏入口，并与 [跨平台约定](../contracts/README.md) 保持一致。

Xcode 工程就绪后，在仓库根目录执行：

```bash
./scripts/package-macos.sh 0.1.0
```

脚本默认使用 `Sub2Bar` scheme，并优先查找 `Sub2Bar.xcworkspace`。可通过 `SUB2BAR_MACOS_SCHEME`、`SUB2BAR_MACOS_CONTAINER` 和 `SUB2BAR_MACOS_PRODUCT_NAME` 调整工程参数，生成的 DMG 位于 `macos/artifacts`。
