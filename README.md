# 微信多开助手

Windows 10/11 x64 的微信多开工具，提供 WPF 图形界面和 CLI 入口。当前实现使用公开 Windows 进程/句柄接口，不修改微信文件，不读取聊天内容，不提供消息自动化或协议能力。

当前版本：1.1.0

## 开发环境

- .NET SDK 8.0.x
- Windows 10/11 x64
- 需要管理员权限进行真实句柄操作

## 运行

```text
wechat-multiopen.exe
wechat-multiopen.exe -n 3
wechat-multiopen.exe -p "C:\Program Files\Tencent\Weixin\Weixin.exe" -n 2
wechat-multiopen.exe --version
wechat-multiopen.exe --doctor
wechat-multiopen.exe --doctor --verbose
```

无参数启动 GUI；带参数启动 CLI。默认实例数为 2，允许范围为 2～4。

GUI 界面会显示检测到的微信路径、版本和当前已运行实例数；微信安装在非默认位置时，可点击"浏览…"手动选择 WeChat.exe / Weixin.exe。

3.x 默认配置包含已知互斥体特征；当前已为微信 4.1.15.56 写入两类已确认的 V4 特征：`XWeChat_XWEB_Instance_Identity_Mutex_Name` 和 `*_WeChat_Win_User_Identity_Mutex_Name`。其他 4.x 版本仍需运行侦察后确认，不会在未知特征下尝试关闭句柄。

`--doctor --verbose` 仅做只读侦察，列出当前微信进程的命名同步对象，供版本适配使用；不会关闭任何句柄。

## 新版本微信适配

微信大版本更新后互斥体特征可能变化，多开失败时会提示"未找到可关闭的多开特征"。适配流程：

1. 确保新版本微信已在运行，执行 `wechat-multiopen.exe --doctor --verbose`。
2. 查看输出末尾自动生成的特征 JSON 模板（优先收录名称含 wechat / weixin / identity / xweb 的 Mutant）。
3. 核对模板中的特征名后，将其保存为 `%LocalAppData%\wechat-multiopen\mutex_features.json`。

程序优先读取该外部文件；文件缺失或损坏时回退到内置特征表，不会导致程序无法启动。`namePattern` 支持 `*` 通配符，保留前缀 `*` 可吞掉名称前的随机串（如 GUID）。

## 发布

```powershell
.\scripts\publish.ps1
```

脚本生成 `artifacts\Release\wechat-multiopen.exe` 和 SHA256 文件。发布物不加壳、不混淆、不联网。

`artifacts\` 已加入 `.gitignore`，二进制发布物不进 git 仓库，建议通过 GitHub Release 分发。

## 合规声明

本工具仅通过操作系统公开接口操作本机进程句柄以实现多账号登录，不修改、不分发微信软件本体，不涉及通信协议与消息内容，不提供任何自动化功能。使用者应确保用途合法合规，并自行遵守微信软件许可及服务协议。
