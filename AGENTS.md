# FandNEL 维护约定

全程使用简体中文。先解决当前问题，再沉淀必要的维护约定；不扩大到无关功能或 UI。

## 项目范围

- 主项目：`C:/Users/winme/Documents/ChatGPT/FandNEL`
- 后端入口与 Gateway：`src/FandNEL`
- 核心库：`src/FandNEL.Core`
- Minecraft 代理与协议：`src/FandNEL.Proxy`
- 游戏启动器：`src/FandNEL.GameLauncher`
- 前端源码：`ui/react-project`
- 后端内置前端产物：`src/FandNEL/wwwroot`
- 协议回归检查：`tests/FandNEL.Gateway.ProtocolChecks`

以下项目仅供只读参考，不得直接修改：

- `F:/Codexus/Codexus.Gateway`
- `F:/Codexus/Codexus_Server`
- `F:/Codexus/Codexus.OpenSDK`

## 每次修改前

1. 查看 `git status --short --branch`，区分用户已有修改与本次修改。
2. 先读取相关源文件、调用方、协议定义及已有检查，再确定修改范围。
3. 不使用 `git reset`、`git checkout`、强制覆盖或删除用户已有修改。
4. 默认不执行提交、推送、分支创建或分支切换；仅在用户明确授权相应操作后执行。授权在同一任务、同一目标和已说明的影响范围内持续有效，重试与验证不重复索要确认。
5. 不修改与当前问题无关的 UI。前端及其构建产物默认不变。

## Git 同步与提交

- 获取远端信息后，先检查领先/落后提交、文件范围和差异，再同步。
- 工作区干净且可以快进时使用 `git pull --ff-only`；发生分叉或冲突时先检查原因，不强制覆盖、不自动丢弃修改。
- 不自动暂存或隐藏用户已有修改。提交前审阅暂存差异，只添加本次授权范围内的文件。
- 无实际变更时不创建空提交。提交信息遵循现有历史风格，默认执行 Git 钩子。
- 使用普通推送，禁止强制推送；推送后核对远端分支提交与本地 HEAD，并再次检查工作区状态。

## 代码约定

- 遵循 SOLID、KISS、DRY、YAGNI 和现有项目风格，保持修改范围小、便于回滚。
- 先复用现有解析器、协议类型和错误处理方式，不添加没有实际用途的抽象。
- 对外协议字段集中管理，避免在不同入口重复解析或散落硬编码字段。
- 使用明确异常或结构化错误响应，避免吞掉会影响业务结果的错误。
- 日志保持低噪声，不输出令牌、密码及其他敏感信息，不永久添加刷屏调试日志。
- 保留现有中文和英文日志风格，代码注释语言与所在文件保持一致。
- 不为当前问题之外的重构、依赖升级或格式调整扩大变更范围。

## 构建与验证

后端至少执行：

```powershell
dotnet build "C:/Users/winme/Documents/ChatGPT/FandNEL/src/FandNEL/FandNEL.csproj" --no-restore
```

涉及 Gateway、认证、角色、WebSocket 或 NBT 行为时，按变更范围运行现有协议检查：

```powershell
dotnet run --project "C:/Users/winme/Documents/ChatGPT/FandNEL/tests/FandNEL.Gateway.ProtocolChecks/FandNEL.Gateway.ProtocolChecks.csproj" --no-restore
```

该检查不等于真实账号、真实游戏服务器或 IRC 聊天室联调。报告时区分编译、自动化检查与实际运行结果。

默认不构建前端。只有明确确认前端协议确实错误且需要修改前端时，才在 `ui/react-project` 执行 `npm run build`，并将构建产物同步到 `src/FandNEL/wwwroot`。

## 最终汇报

完成后仅汇报以下六项，区分远端拉取的变更与本次新增修改：

1. 根因。
2. 修改了哪些后端文件；若增加维护文档，在本项说明。
3. 为什么不需要修改前端；若确需修改，说明协议依据。
4. 构建结果。
5. 验证结果，包括本次授权的 Git 操作结果。
6. 尚未验证的部分。
