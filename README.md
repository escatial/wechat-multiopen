# 微信多开助手

Windows 10/11 x64 的微信多开工具，提供 WPF 图形界面和 CLI 入口。当前实现使用公开 Windows 进程/句柄接口，不修改微信文件，不读取聊天内容，不提供消息自动化或协议能力。

## 开发环境

- .NET SDK 8.0.x
- Windows 10/11 x64
- 需要管理员权限进行真实句柄操作

## 运行

```text
wechat-multiopen.exe
wechat-multiopen.exe -n 3
wechat-multiopen.exe -p "C:\Program Files\Tencent\Weixin\Weixin.exe" -n 2
wechat-multiopen.exe --doctor
wechat-multiopen.exe --doctor --verbose
```

无参数启动 GUI；带参数启动 CLI。默认实例数为 2，允许范围为 2～4。

3.x 默认配置包含已知互斥体特征；当前已为微信 4.1.15.56 写入两类已确认的 V4 特征：`XWeChat_XWEB_Instance_Identity_Mutex_Name` 和 `*_WeChat_Win_User_Identity_Mutex_Name`。其他 4.x 版本仍需运行侦察后确认，不会在未知特征下尝试关闭句柄。

`--doctor --verbose` 仅做只读侦察，列出当前微信进程的命名同步对象，供版本适配使用；不会关闭任何句柄。

## 发布

```powershell
.\scripts\publish.ps1
```

脚本生成 `artifacts\Release\wechat-multiopen.exe` 和 SHA256 文件。发布物不加壳、不混淆、不联网。

## 合规声明

本工具仅通过操作系统公开接口操作本机进程句柄以实现多账号登录，不修改、不分发微信软件本体，不涉及通信协议与消息内容，不提供任何自动化功能。使用者应确保用途合法合规，并自行遵守微信软件许可及服务协议。
