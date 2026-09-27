# FandNEL IRC 服务端

这是一个面向 Linux 的 Go 匿名聊天室服务端，使用标准库提供匿名在线状态、增量消息读取和消息发送。服务端只接收游戏 ID 作为显示名，不保存账号、密码、令牌、HWID 或其他身份资料。默认使用本地 JSON 文件保存最近消息，适合单实例部署；需要多实例或更大消息量时，可以只替换 `internal/store`，HTTP 合同保持不变。

## 运行

```bash
export IRC_LISTEN_ADDR=':5127'
export IRC_DATA_FILE='./data/messages.json'
go run ./cmd/irc-server
```

生产环境建议使用 systemd、反向代理和 HTTPS。服务不会记录令牌、硬件标识或消息正文。

## 接口

新客户端使用以下接口：

- `GET /healthz`
- `POST /api/v1/session`：登记匿名客户端并返回初始游标
- `GET /api/v1/chat/messages?after=<cursor>&limit=<n>`：按游标读取消息
- `POST /api/v1/chat/messages`：发送 `{ "text": "...", "clientMessageId": "..." }`

现有 FandNEL 客户端仍使用以下兼容接口，服务端通过适配层转到同一套匿名业务逻辑：

- `POST /api/auth/login`
- `POST /api/chat/poll`
- `POST /api/chat/send`

兼容层不会复制存储、在线状态或消息校验逻辑。新接口返回结构化错误 `{ "code": "...", "message": "..." }`；兼容接口保留旧的 `success/message` 字段。
