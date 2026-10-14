# 双态动态 Agent FastAPI 服务

这个目录是独立的 AB 双态 Agent 服务，不改动 `level_dialogue_prototype` 的工具态服务。

双态含义：

- `A`：伙伴态 Agent。无 RAG，直接调用主力 LLM，对话流程与关卡状态机一致。
- `B`：工具态 Agent。复用 `level_dialogue_prototype`，通过 RAG 检索负鼠知识后回答。
- `AB`：双态动态 Agent。同时维护 A/B 两条子会话，根据儿童当下交互信号计算信任分数，再决定返回 A 或 B 的回复。

## 信任路由

当前信任模型使用参数化计算：

```text
instant_trust = a * text + b * choice + c * behavior + d * time
```

默认权重：

```text
text=0.30, choice=0.35, behavior=0.20, time=0.15
```

然后使用平滑更新：

```text
trust_t = 0.65 * trust_(t-1) + 0.35 * instant_trust
```

当进入反思选择、直接解释、AI 修正、最终结论等教学关键状态时，服务会强制返回 B 工具态，因为这些阶段需要可解释性更高的处理。

## 启动

建议使用独立端口，例如：

```bash
venv/bin/uvicorn agent_project.dual_state_agent.api_server:app --host 127.0.0.1 --port 8030
```

## 接口

```text
GET  /health
POST /api/dual-state/start
POST /api/dual-state/message
POST /api/dual-state/chat
POST /api/dual-state/choice
GET  /api/dual-state/session/{session_id}
```

开始关卡：

```json
{
  "case_id": "opossum_play_dead_001",
  "child_initial_belief": "unknown",
  "anonymous_user_id": "unity-local-user"
}
```

普通聊天：

```json
{
  "session_id": "xxx",
  "content": "负鼠吃什么？"
}
```

选择回复：

```json
{
  "session_id": "xxx",
  "choice": 3
}
```

返回会包含：

```json
{
  "session_id": "xxx",
  "agent_type": "AB",
  "selected_agent": "B",
  "switch_reason": "forced_tool_state",
  "trust_score": 0.48,
  "send_format": "choice",
  "state": "REFLECTION_CHOICE",
  "view": "ChatWithChoiceButtonsView",
  "content": "..."
}
```

## 日志

AB 路由日志保存在：

```text
agent_project/dual_state_agent/data/logs/sessions
```

日志会记录每轮选择了 A 还是 B、信任分数、切换原因，以及 A/B 子会话 ID。
