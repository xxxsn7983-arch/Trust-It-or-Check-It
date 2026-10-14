# Two-State RAG Agent

独立的双态 Agent FastAPI 服务，不依赖 `level_dialogue_prototype`。

## 启动

```bash
cd /Users/your-user/Downloads/agent_game
source venv/bin/activate
export DASHSCOPE_API_KEY="你的千问 key"
export QWEN_MODEL="qwen-plus"
export QWEN_EMBEDDING_MODEL="text-embedding-v4"
venv/bin/uvicorn agent_project.two_state_rag_agent.api_server:app --host 127.0.0.1 --port 8010
```

如果暂时不想调用 Qwen，可以加：

```bash
export QWEN_DISABLED="true"
```

## 接口

### 开始会话

```http
POST /api/two-state/start
```

```json
{
  "creature_id": "orchid_mantis"
}
```

### 发送聊天

```http
POST /api/two-state/chat
```

```json
{
  "session_id": "上一步返回的 session_id",
  "content": "兰花螳螂为什么像花？"
}
```

### 返回格式

```json
{
  "session_id": "...",
  "agent_mode": "companion",
  "creature_id": "orchid_mantis",
  "creature": "兰花螳螂",
  "content": "兰花螳螂像花属于拟态...",
  "retrieved_chunk_ids": ["orchid_mantis_bg_005"]
}
```

`agent_mode` 有两种：

- `companion`：伙伴态，回答兰花螳螂知识和陪伴式聊天。
- `tool`：工具态，回答关卡操作、下一步、卡牌使用等问题。
