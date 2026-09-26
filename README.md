# FandNEL

FandNEL 是独立的 .NET 9 Windows 客户端，负责网易 Java 账号激活、游戏目录、资源准备、Minecraft Proxy 和启动器编排。

## 项目边界

```text
src/FandNEL/             WPF 界面、账号持久化和 Gateway 编排
src/FandNEL.Core/        Codexus Cipher 行为、渠道协议、网易 API、OTP 和 Codexus 远程认证
src/FandNEL.Proxy/       无开发者 SDK 的 Minecraft 连接、协议状态机和可组合拦截链
src/FandNEL.GameLauncher/资源下载、7z 安装、Java 参数和进程生命周期
```

`FandNEL.Proxy` 的协议行为迁移自 `Codexus.Interceptors`，网络会话和注册表结构参考 NeoOpenNEL 的 Proxy。Proxy 不引用 `Codexus.Development.SDK`，也不依赖插件才能处理握手、登录、配置、压缩和加密。

Core 的最终进服认证仍使用 Codexus 远程服务：先请求 `https://x19.update.netease.com/authserver.list`，再通过 `https://api.codexus.today/api/GameCipher/compute/authentication/*` 计算网易认证载荷。账号登录会执行 Codexus 的 `login-otp` 和 `authentication-otp` 激活流程，Proxy 只有在认证成功后才向远端发送加密响应。

## 构建

```powershell
dotnet restore "FandNEL.slnx"
dotnet build "FandNEL.slnx" -c Debug
dotnet run --project "src/FandNEL/FandNEL.csproj" -c Debug
```

Proxy 版本状态机覆盖 1.7.6、1.8.x、1.12.2、1.18、1.20、1.20.6、1.21、1.21.8 和 1.21.10。每个会话都有独立 `PacketRegistry`，同一包 ID 可以注册多个按优先级执行的处理器；`PacketContext.ReplaceRange` 会保留未改字段和尾部载荷。

账号文件保存在 `%LOCALAPPDATA%/FandNEL/accounts.json.dpapi`，使用 Windows DPAPI 加密。游戏令牌只保存在进程内并在临近过期时刷新，不写入日志。

## 当前构建提示

启动器使用 SharpCompress 安装网易 7z 资源。NuGet 当前会报告 SharpCompress 0.41.0 的 NU1902 审计提示；这不影响编译，但发布前应升级到修复该公告的可用版本。
