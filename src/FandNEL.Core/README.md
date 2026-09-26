# FandNEL.Core

Core 只依赖 .NET 基础库，按职责分为以下层次：

- `Security/`：DPAPI、AES-GCM、敏感数据清理。
- `Storage/`：版本化加密 JSON、设备标识和原子写入。
- `Diagnostics/`：HTTP trace 和敏感字段脱敏。
- `Http/`：通用 HTTP 请求描述、响应快照、参数编码和传输接口。
- `Authentication/`：渠道登录请求、结果、验证码/浏览器挑战和提供者注册表。
- `Protocol/`：WPFLauncher、MPay、4399、G79、协议响应和游戏目录接口。
- `Connection/`：网易认证 TCP、ChaCha8 封装和 Authlib 本地适配器。
- `Services/`：Codexus 远程 API 客户端，包含 GameCipher、PeGameCipher 和验证码接口。
- `Entities/`：网易渠道、WPFLauncher、G79、Minecraft 和认证响应实体。

4399 的协议实现已经按渠道隔离：

- `Protocol/Pc4399.cs`：电脑版网页登录和 sauth 生成。
- `Protocol/Com4399.cs`：手机版 OAuth 登录和 sauth 生成。
- `Entities/Pc4399/` 与 `Entities/Com4399/`：各自渠道的响应实体。

`AccountLoginService` 会把渠道 sauth 继续提交给网易 `login-otp` 和 `authentication-otp`，返回可用于 Java 进服的游戏 token。`NetEaseConnection` 再使用 `api.codexus.today` 计算握手和进服载荷。
