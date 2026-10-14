# 自适应对话 Agent 系统

一个基于 design.md 实现的最小可运行的**语音 Agent 项目**，核心特性是能够根据用户的风格和状态实时调整回应方式。

## 🎯 核心理念

> **能听 → 能理解 → 能生成 → 能模仿你 → 能持续调整风格**

这不仅是一个语音助手，而是**一个能通过交互不断调整人格与表达方式的动态Agent系统**。

---

## 🏗️ 系统架构

系统包含 5 个核心模块：

### 1️⃣ **语音输入模块** (`speech_input.py`)
- 使用 Whisper 进行语音识别 (ASR)
- 支持多语言识别
- 从音频中检测基本情绪特征

### 2️⃣ **对话理解模块** (`nlu.py`)
- 意图识别（用户在做什么）
- 情绪识别（开心、兴奋、沮丧）
- 语气分析（随意、正式、直接）
- **用户状态建模**（核心模块）

### 3️⃣ **风格自适应模块** (`style_adapter.py`) ⭐
- 根据用户状态生成**动态系统提示词**
- 风格平滑（防止频繁切换）
- 信任度调节
- 这是本 Agent 与普通聊天机器人的本质区别

### 4️⃣ **对话生成模块** (`llm_core.py`)
- 调用 OpenAI API（需配置）
- 支持本地模拟模式（离线测试）
- 接收动态系统提示词，生成自适应回应

### 5️⃣ **语音输出模块** (`tts.py`)
- 使用 Edge TTS 或 ElevenLabs
- 文本转语音 (TTS)
- 支持多种语音选择

---

## 📦 安装依赖

```bash
# 进入项目目录
cd agent_project

# 安装依赖
pip install -r requirements.txt
```

### 可选依赖

如果要使用完整功能，需要安装额外的库：

```bash
# OpenAI API（用于高级 LLM 功能）
pip install openai

# 音频处理（用于从音频检测情绪）
pip install librosa

# 快速 Whisper（可选，比标准 Whisper 更快）
pip install faster-whisper
```

---

## 🚀 快速开始

### 1. 演示模式（推荐）

```bash
python main.py --mode demo
```

这会展示 Agent 如何根据用户的不同情绪和语气调整回应。

### 2. 交互模式

```bash
python main.py --mode interactive
```

进入持续对话模式，输入 `exit` 退出。

### 3. 单句输入

```bash
python main.py --mode text --input "你好，今天天气真好！"
```

### 4. 音频输入

```bash
python main.py --mode audio --input "your_audio.wav"
```

### 5. 启用文本转语音

```bash
python main.py --mode text --input "你好" --tts
```

---

## 🔧 配置

编辑 `config.py` 文件来配置系统：

```python
# LLM 配置
LLM_CONFIG = {
    "provider": "openai",  # 改为 "qwen" 或其他
    "model": "gpt-3.5-turbo",
}

# 语音识别配置
ASR_CONFIG = {
    "model": "base",  # tiny, base, small, medium, large
    "language": "zh",
}

# TTS 配置
TTS_CONFIG = {
    "provider": "edge-tts",
    "voice": "zh-CN-YunyangNeural",
}
```

---

## 🔐 API 配置

### OpenAI API

设置环境变量：

```bash
export OPENAI_API_KEY="your-api-key-here"
```

或在系统中配置。

---

## 📊 用户状态模型

Agent 维护一个用户状态向量：

```python
UserState = {
    情绪: "calm" / "excited" / "frustrated",      # 用户的感情状态
    语气: "casual" / "formal" / "directive",      # 用户的表达方式
    信任度: 0.0 ~ 1.0,                            # 对 Agent 的信任程度
    依赖程度: 0.0 ~ 1.0,                          # 对 Agent 的依赖程度
    风格偏好: "casual" / "formal" / "directive",  # 偏好的交互风格
}
```

Agent 根据这个状态自动调整：
- **系统提示词**的内容
- **回应的语气**
- **解释的详细程度**
- **表达的方式**

---

## 🎮 工作流程

```
┌─────────────────┐
│  用户输入       │
│ (文本或音频)    │
└────────┬────────┘
         ↓
    ┌────────────┐
    │ ASR/输入   │
    │ (Whisper)  │
    └────────┬───┘
             ↓
        ┌────────────────┐
        │ NLU + 状态建模 │     ← ⭐ 核心：识别情绪、语气
        │ (nlu.py)       │
        └────────┬───────┘
                 ↓
        ┌────────────────────┐
        │ 风格自适应         │    ← ⭐ 关键：生成动态提示词
        │ (style_adapter.py) │
        └────────┬───────────┘
                 ↓
        ┌────────────────┐
        │ LLM 生成回应   │    ← 调用 OpenAI/Qwen
        │ (llm_core.py)  │
        └────────┬───────┘
                 ↓
        ┌────────────────┐
        │ 可选：TTS      │    ← 文本转语音
        │ (tts.py)       │
        └────────┬───────┘
                 ↓
        ┌─────────────────┐
        │ 输出结果        │
        │ (文本/语音)     │
        └─────────────────┘
```

---

## 📝 代码示例

### 完整工作流

```python
from nlu import NLUModule
from style_adapter import StyleAdapter
from llm_core import LLMCore

# 初始化模块
nlu = NLUModule()
adapter = StyleAdapter()
llm = LLMCore()

# 用户输入
user_input = "这太棒了，能给我介绍一下AI吗？"

# 分析输入
analysis = nlu.analyze_text(user_input)
user_state = nlu.update_user_state(analysis)

# 生成动态提示词
system_prompt = adapter.generate_dynamic_prompt(
    user_state,
    nlu.get_user_pattern()
)

# 生成回应
response = llm.generate_response(system_prompt, user_input)
print(response)
```

---

## 🧪 测试各模块

```bash
# 测试 NLU 模块
python nlu.py

# 测试 Style Adapter
python style_adapter.py

# 测试 LLM 核心
python llm_core.py

# 测试 TTS 模块
python tts.py

# 测试语音识别
python speech_input.py
```

---

## 📈 后续增强方向

### Level 1: 规则映射（当前）
- 用户说短句 → AI也短句
- 用户用emoji → AI也用一点
- 用户语气直接 → AI减少解释

### Level 2: Embedding 风格匹配
- 提取用户最近N句的embedding
- 计算平均句长、词汇复杂度、情绪强度
- 映射到风格标签

### Level 3: 风格学习（高级）
- 用 few-shot + 记忆机制
- 让 Agent 从交互历史中学习用户的持久风格

### 高级功能
- ✨ 记忆模块 - 让 Agent "越来越像你朋友"
- ✨ 多模态输入 - 加入手势、点击行为等
- ✨ 双态模型 - 工具态 vs 伙伴态的自动切换
- ✨ 实时打断 - 支持对话中断和自然交接

---

## ⚠️ 关键难点（已知问题）

1. **风格识别不稳定** - 短输入可能导致误判，需要更多上下文
2. **实时性问题** - API 调用存在延迟，需要优化或使用流式生成
3. **语音打断处理** - 当前实现不支持打断，需要流式处理
4. **风格变化频繁** - 需要使用平滑机制来稳定风格

---

## 🎓 项目学习资源

- [Whisper 文档](https://github.com/openai/whisper)
- [OpenAI API](https://platform.openai.com/docs)
- [Edge TTS](https://github.com/rany2/edge-tts)
- [LangChain](https://python.langchain.com) - 用于构建高级 Agent

---

## 📄 许可证

MIT License

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

## 📞 联系方式

有问题或建议？请在项目中提交 Issue。

---

## 🌟 系统一句话总结

> 一个能通过交互不断调整"人格与表达方式"的动态Agent系统，而不仅仅是一个能说话的机器。
