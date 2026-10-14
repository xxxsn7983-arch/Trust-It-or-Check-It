# Companion Agent A

这个目录是 A 条件的伙伴态 Agent 后端。

它和 `level_dialogue_prototype` 使用同样的关卡流程和接口形状：

- `start`
- `chat`
- `choice`
- `message`
- `session`

区别是：A 不调用 RAG 检索，也不展示知识来源或证据筛选过程。它只调用 Qwen 进行黑盒式自由回复，并通过隐藏状态机控制轮次、可控错误引入和 choice 流程。

## 启动

```bash
cd /Users/your-user/Downloads/agent_game
source venv/bin/activate
export DASHSCOPE_API_KEY="你的千问 key"
export QWEN_MODEL="qwen-plus"

venv/bin/uvicorn agent_project.companion_agent.api_server:app --host 127.0.0.1 --port 8020
```

离线测试可关闭 Qwen：

```bash
export QWEN_DISABLED=true
```

## 接口

```text
GET  /health
POST /api/companion/start
POST /api/companion/chat
POST /api/companion/choice
POST /api/companion/message
GET  /api/companion/session/{session_id}
```

## 与 B 工具态的区别

| 条件 | A 伙伴态 | B 工具态 |
| --- | --- | --- |
| 后端入口 | `/api/companion/*` | `/api/level/*` |
| 知识来源 | Qwen 黑盒生成 | RAG + 状态机 |
| 可解释性 | 低 | 高 |
| 轮次控制 | 有 | 有 |
| 可控错误 | 有 | 有 |
| Choice 流程 | 有 | 有 |
| 日志目录 | `companion_agent/data/logs/sessions` | `level_dialogue_prototype/data/logs/sessions` |
