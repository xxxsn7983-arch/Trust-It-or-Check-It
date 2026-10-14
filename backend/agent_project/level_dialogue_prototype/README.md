# Level Dialogue Prototype

这个目录是独立测试区，用来验证“结构化知识包 + 生物背景 RAG + 对话判断器 + 状态机 + Agent 策略”的组合。

它不依赖现有 `agent_project` 的其他模块。`AgentPolicy` 会优先调用千问 `qwen-plus`，如果没有配置 API key，会自动退回到确定性假 Agent，方便先把流程跑通。

## 目录结构

```text
level_dialogue_prototype/
├─ data/cases/
│  └─ opossum_play_dead_001.json     # 结构化关卡知识包
├─ data/rag/
│  └─ opossum_chunks.json            # 与负鼠有关的背景事实
├─ knowledge_store.py                # 知识包加载 + 简易背景检索
├─ dialogue_evaluator.py             # 儿童自由回复/证据提交判断
├─ agent_policy.py                   # 当前状态下的 Agent 输出策略
├─ qwen_client.py                     # 千问 qwen-plus OpenAI 兼容调用
├─ state_machine.py                  # 后端教学状态机
└─ run_demo.py                       # 三条路径测试
```

## 职责边界

```text
结构化知识包：
- 关卡问题
- 知识卡
- 可控错误观点
- 正确解释
- 类比解释
- 证据卡
- 最终总结

RAG 背景知识：
- 生物基础信息
- 栖息地
- 身体特征
- 行为事实
- 与该生物有关的真实背景

对话判断器：
- 判断儿童是否说到证据
- 判断儿童是否说到概念
- 判断是否只是反对、跑题或出现新误解

状态机：
- 控制当前阶段
- 控制自由聊天轮次
- 识别儿童是否问到关键问题
- 在第 4 轮开始苏格拉底引导
- 控制什么时候抛出可控错误
- 控制 REFLECTION_CHOICE 之后的分支
- 控制前端 view 建议
```

当前 Step4 节奏：

```text
FREE_CHAT
  第 1-3 轮：基于 RAG 自由聊天
  儿童问到关键问题：直接进入可控错误
  第 4 轮还没问到：进入 SOCRATIC_LEAD_IN

SOCRATIC_LEAD_IN
  先问“遇到天敌会怎么做？”
  再问“一动不动像死了一样，是为什么？”
  儿童仍未命中时，状态机强制抛出可控错误

REFLECTION_CHOICE
  接受 / 寻找线索 / 怀疑一下
  现在不再进入证据卡拖拽，都会进入 AI_REPAIR，只是修正话术不同。
```

## 运行

在项目根目录执行：

```bash
python3 agent_project/level_dialogue_prototype/run_demo.py
```

启动给 Unity 使用的 HTTP API：
```bash
uvicorn agent_project.level_dialogue_prototype.api_server:app --reload --port 8000
```

接口：
```text
POST /api/level/start
POST /api/level/chat
POST /api/level/choice
GET  /api/level/session/{session_id}
```

开始关卡时可以带匿名用户 id；没有登录系统时，Unity 可以自己生成一个 UUID 存在本地：

```json
{
  "case_id": "opossum_play_dead_001",
  "child_initial_belief": "unknown",
  "anonymous_user_id": "unity-local-uuid"
}
```

前端普通聊天回传格式：
```json
{
  "session_id": "xxx",
  "content": "负鼠遇到危险会怎么样"
}
```
前端选项回传格式：
```json
{
  "session_id": "xxx",
  "choice": 2
}
```

其中选项编号固定为：
```text
1 = 接受
2 = 寻找线索
3 = 怀疑一下
```

后端普通聊天发送格式：
```json
{
  "send_format": "chat",
  "content": "它可能先逃跑、爬树、露牙或发出声音。"
}
```
后端选项聊天发送格式：
```json
{
  "send_format": "choice",
  "content": "我觉得负鼠是真的吓晕了，所以才完全不能动。\n你觉得我刚才的判断可靠吗？你可以选择：接受、寻找线索、怀疑一下。"
}
```

日志会按 session 写入：

```text
agent_project/level_dialogue_prototype/data/logs/sessions/{session_id}.json
```

日志内容包括匿名用户 id、case、初始判断、儿童输入、AI 输出、状态变化和 debug 信息。

如果要启用千问 `qwen-plus`，先配置百炼 API key：

```bash
export DASHSCOPE_API_KEY="你的百炼APIKey"
python3 agent_project/level_dialogue_prototype/run_demo.py
```

可选配置：

```bash
export QWEN_MODEL="qwen-plus"
export QWEN_BASE_URL="https://dashscope.aliyuncs.com/compatible-mode/v1"
export QWEN_ENABLE_THINKING="false"
export QWEN_EMBEDDING_MODEL="text-embedding-v4"
```

如果你已经配置了 key，但想临时关闭真实 API：

```bash
export QWEN_DISABLED="true"
```

RAG 检索默认是 `auto`：

```text
有 DASHSCOPE_API_KEY / QWEN_API_KEY 时，优先使用 Qwen embedding 向量检索。
没有 key 或 embedding 调用失败时，退回本地 TF-IDF 向量检索。
```

也可以手动指定：

```bash
export RAG_RETRIEVAL_MODE="qwen"   # 强制使用 Qwen embedding
export RAG_RETRIEVAL_MODE="local"  # 强制使用本地向量 fallback
export RAG_RETRIEVAL_MODE="auto"   # 默认
```

会跑三条路径：

1. 儿童选择“怀疑一下”，直接给出有效理由，进入 `AI_REPAIR`
2. 儿童选择“相信AI”，AI 主动调用证据自我纠错，进入 `AI_REPAIR`
3. 儿童选择“寻找线索”，进入证据探索与证据提交，再进入 `AI_REPAIR`

## 后续接真实 Agent 的位置

现在真实 LLM 调用已经集中在 `qwen_client.py`，状态内策略集中在 `agent_policy.py`。建议继续保持这条边界：

```text
状态机决定能调用什么。
知识库提供内容。
Agent 只负责当前状态允许的话术生成。
```
