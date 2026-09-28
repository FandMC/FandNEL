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

Core 的最终进服认证仍使用 Codexus 远程服务：先请求 `https://x19.update.netease.com/authserver.list`，再通过 `https://api.codexus.today/api/public/GameCipher/compute/authentication/*` 计算网易认证载荷。公开 X19 接口不需要 Nexus Token 或 `Authorization` 请求头；账号登录会执行 Codexus 的 `login-otp` 和 `authentication-otp` 激活流程，Proxy 只有在认证成功后才向远端发送加密响应。

## 构建

```powershell
dotnet restore "FandNEL.slnx"
dotnet build "FandNEL.slnx" -c Debug
dotnet run --project "src/FandNEL/FandNEL.csproj" -c Debug
```

Proxy 版本状态机覆盖 1.7.6、1.8.x、1.12.2、1.18、1.20、1.20.6、1.21、1.21.8 和 1.21.10。每个会话都有独立 `PacketRegistry`，同一包 ID 可以注册多个按优先级执行的处理器；`PacketContext.ReplaceRange` 会保留未改字段和尾部载荷。

Heypixel 的协议 766 适配会将玩家头顶队伍前后缀中的字面量 `\n`、`\r\n` 和实际换行显示为多行，复用悬浮字的客户端文字实体，保留文字组件样式、队伍可见性及名字下方分数。TAB 显示名保持原样；恢复单行、销毁实体或切换世界时清理对应显示实体。实体类型 ID 以对应版本的官方注册表报告为准。

该显示方式仍由客户端文字实体渲染：F1 隐藏界面和客户端实体视距缩放与原生名字不完全相同；载具带动乘客移动的专用联动尚未实现。

`users.json`、`cppusers.json`、`.game_cache` 均保存在 EXE 同级目录；启动器资源位于同级的 `resources`。Java 登录、激活、改名和删除统一使用 `users.json`；`accounts.json.dpapi` 已停用，账号仓库成功读取后会删除新旧位置中的该文件及其锁文件。游戏令牌只保存在进程内并在临近过期时刷新，不写入日志。

首次使用新路径时，程序会从 `%LOCALAPPDATA%/FandNEL` 复制缺失的账号文件，并从其 `launcher` 子目录复制缺失的 `.game_cache` 和 `resources`。复制完成后核对内容，自动删除对应旧文件及已清空的缓存目录；已有目标不覆盖，内容不同、目标缺失或被占用的旧文件保留并记录原因。缓存完整复制后才启用，缓存较大时首次启动需要等待复制完成。浏览器缓存、设备标识和可选诊断日志仍使用原位置。

Proxy 默认从 `20018` 开始监听，遇到占用或 Windows 保留端口逐个向上尝试；指定端口作为起点，`0` 兼容为默认起点。成功后始终使用实际绑定端口。

点击启动游戏后会立即提示并登记到 Games 列表，账号检查、资源准备和下载进度均可查看和取消。启动失败保留原因，运行中可停止；关闭控制页不会取消游戏。Java 进程自然退出时若退出码非零，会报告失败，具体游戏崩溃原因仍需结合游戏日志排查。

## Java 令牌维护与人工回归

`UserManager` 是已激活 Java token 的唯一来源，前台和后台复用同账号的串行刷新入口。Proxy 入服、启动器 Authlib、资源和皮肤请求在使用时读取当前凭据；刷新失败保留最后成功的 token 和时间，账号停用后长期通道不能回退旧值或自行激活账号。排查日志只记录刷新结果和账号 ID，不记录 token、Cookie 或认证载荷。

修改这条链路后，先执行后端构建，再用真实账号人工验证以下场景，不新增自动化测试：

1. 保持原代理通道超过 20 分钟，确认刷新成功后断开游戏，再通过同一通道重连；重复跨越第二次刷新。
2. 通过内置启动器启动游戏，跨越刷新后在同一 Java 进程中重新进服，并检查皮肤请求；对比新建通道的结果。
3. 刷新期间触发账号检查或重新激活，确认旧请求结束后不会覆盖新会话；短暂网络失败后恢复，确认刷新失败没有伪造新的成功时间。
4. 停用账号后尝试旧通道和原 Java 进程重连，应拒绝旧凭据；主动重新激活后应使用新 token。

编译通过只证明代码可构建，不能代替上述真实账号和服务器联调。

## 当前构建提示

启动器使用 SharpCompress 安装网易 7z 资源。NuGet 当前会报告 SharpCompress 0.41.0 的 NU1902 审计提示；这不影响编译，但发布前应升级到修复该公告的可用版本。
