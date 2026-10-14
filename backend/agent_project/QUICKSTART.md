# 🚀 快速开始指南

## 项目概述

这是一个**最小可运行的自适应对话 Agent 系统**，基于 `design.md` 的架构实现。

### 核心特性
✨ **自适应风格调整** - Agent 会根据用户的情绪和语气自动调整回应方式
🎤 **语音识别** - 使用 Whisper 进行实时语音转文本
🤖 **对话生成** - 集成 OpenAI/本地模式支持
🔊 **文本转语音** - Edge TTS 或 ElevenLabs 支持
🧠 **用户状态建模** - 跟踪情绪、语气、信任度等

---

## 📋 文件结构

```
agent_project/
├── main.py                 # 主程序入口
├── config.py              # 配置文件
├── nlu.py                 # 对话理解 + 用户状态建模
├── speech_input.py        # 语音输入模块（Whisper）
├── style_adapter.py       # 风格自适应模块 ⭐核心
├── llm_core.py            # LLM 生成模块
├── tts.py                 # 文本转语音模块
├── requirements.txt       # 依赖包列表
└── README.md             # 详细文档
```

---

## 🛠️ 安装步骤

### 1. 进入项目目录

```bash
cd agent_project
```

### 2. 安装依赖

```bash
pip install -r requirements.txt
```

### 3. （可选）配置 OpenAI API

如果要使用 OpenAI 的完整功能而不是本地模拟：

```bash
export OPENAI_API_KEY="your-api-key-here"
```

---

## 🎮 使用示例

### 1️⃣ 演示模式（推荐首先运行）

展示 Agent 如何根据用户的不同情绪和语气调整回应：

```bash
python main.py --mode demo
```

**输出示例：**
```
[第 1 步] 文本分析...
  • 意图: question
  • 情绪: excited
  • 语气: casual
  • 关键词: AI, 介绍

[第 2 步] 更新用户状态...
  • 当前情绪: excited
  • 当前语气: casual
  • 信任度: 0.50
  
[第 3 步] 分析用户模式...
  • 用户倾向于短句、直接表达需求

[第 4 步] 生成自适应提示词...

[第 5 步] 生成 AI 回应...
🤖 AI 回应:
哈哈，关于'这太棒了，能给我介绍一下 AI 吗？'，我的想法是：这个话题很有意思！...
```

### 2️⃣ 交互模式

进入持续对话，像聊天一样与 Agent 互动：

```bash
python main.py --mode interactive
```

然后输入：
```
👤 你: 你好！
🤖 AI 回应: ...

👤 你: 告诉我关于 Python 的事
🤖 AI 回应: ...

👤 你: exit
再见！
```

### 3️⃣ 单句文本输入

处理单个文本输入：

```bash
python main.py --mode text --input "你能帮我吗？"
```

### 4️⃣ 语音输入（需要音频文件）

```bash
python main.py --mode audio --input "./audio_sample.wav"
```

### 5️⃣ 启用文本转语音

在任何模式后添加 `--tts` 参数：

```bash
python main.py --mode text --input "你好" --tts
```

生成的语音会保存为 `agent_response.mp3`

---

## 🔧 模块说明

### 📝 NLU 模块 (`nlu.py`)
```python
# 检测用户的情绪、语气、意图
nlu = NLUModule()
analysis = nlu.analyze_text("这太棒了！")
# 返回: {
#   "emotion": "excited",
#   "tone": "casual",
#   "intent": "statement",
#   ...
# }
```

### 🎨 风格自适应模块 (`style_adapter.py`) ⭐

这是最核心的模块！根据用户状态生成动态系统提示词：

```python
adapter = StyleAdapter()
user_state = UserState(emotion="excited", tone="casual", trust=0.7)

# 生成针对该用户的自定义系统提示词
system_prompt = adapter.generate_dynamic_prompt(user_state, user_pattern)

# 这个提示词告诉 LLM:
# - 用户兴奋且随意
# - 应该用活泼、轻松的语气回应
# - 可以减少过度的解释
```

### 🧠 LLM 核心模块 (`llm_core.py`)
```python
llm = LLMCore()  # 会从 OPENAI_API_KEY 环境变量读取

# 生成回应
response = llm.generate_response(system_prompt, user_input)
```

### 🎤 语音输入模块 (`speech_input.py`)
```python
speech = SpeechInputModule()

# 识别音频文件
result = speech.transcribe_audio("audio.wav")
print(result["text"])  # 转录的文本
```

### 🔊 TTS 模块 (`tts.py`)
```python
from tts import TTSModule

tts = TTSModule()

# 同步生成语音
success = tts.synthesize_speech_sync(
    "你好，很高兴认识你",
    "output.mp3"
)
```

---

## 📊 用户状态模型

Agent 跟踪每个用户的状态：

```python
UserState = {
    "emotion": "calm" | "excited" | "frustrated",
    "tone": "casual" | "formal" | "directive",
    "trust": 0.0 ~ 1.0,      # 信任度
    "dependency": 0.0 ~ 1.0,  # 依赖程度
    "style_preference": str
}
```

**Agent 如何应用这个状态：**

| 情况 | Agent 行为 |
|------|----------|
| 用户激动 (excited) | 使用热情、活泼的语气 |
| 用户沮丧 (frustrated) | 使用同情、支持的语气 |
| 用户直接 (directive tone) | 减少解释，快速给出方案 |
| 信任度高 | 可以省略背景说明 |
| 信任度低 | 提供详细解释建立信任 |

---

## ⚙️ 配置调整

编辑 `config.py` 文件：

```python
# 选择 LLM 提供商
LLM_CONFIG = {
    "provider": "openai",  # 或 "qwen"
    "model": "gpt-3.5-turbo",
    "temperature": 0.7,  # 0-1，值越高越创意
}

# 语音识别模型大小
ASR_CONFIG = {
    "model": "base",  # tiny < base < small < medium < large
    "language": "zh",
}

# TTS 语音选择
TTS_CONFIG = {
    "voice": "zh-CN-YunyangNeural",  # 中文女性
    # 其他选项:
    # "zh-CN-XiaoxiaoNeural" - 女性
    # "zh-CN-YunxiNeural" - 男性
}
```

---

## 🧪 测试各个模块

```bash
# 测试 NLU 分析
python nlu.py

# 测试风格适配
python style_adapter.py

# 测试 LLM 生成
python llm_core.py

# 测试 TTS
python tts.py

# 测试语音识别
python speech_input.py
```

---

## 🔄 完整工作流示例

```python
from main import AdaptiveAgent

# 初始化 Agent
agent = AdaptiveAgent()

# 处理用户输入（完整流程）
response = agent.process_text_input(
    user_input="你好，能帮我解释 AI 吗？",
    enable_tts=True  # 生成语音
)

# 输出:
# 1. [第1步] 文本分析 -> 检测情绪、语气、意图
# 2. [第2步] 更新用户状态 -> 记录用户风格
# 3. [第3步] 分析用户模式 -> 判断长句/短句偏好
# 4. [第4步] 生成自适应提示词 -> 核心！
# 5. [第5步] 调用 LLM -> 生成回应
# 6. [第6步] 可选 TTS -> 转换为语音
```

---

## 🌟 关键特性演示

### 风格适应能力

**用户 1**：激动且直接
```
用户: 直接说要点就行
Agent状态: excited + directive

系统提示词会包含:
"用户倾向于短句、直接表达需求"
"请直接给出答案，减少解释"

Agent回应: 答案：...（简洁、无冗长说明）
```

**用户 2**：冷静且正式
```
用户: 您能否详细解释一下这个概念？
Agent状态: calm + formal

系统提示词会包含:
"用户倾向于详细、完整的表达"
"请使用专业、正式的语言"
"提供详细的逻辑和解释"

Agent回应: 关于此问题，我们可以从以下几个方面进行阐述：...
```

---

## 🎓 学习路径

1. **运行演示** → `python main.py --mode demo`
2. **查看代码** → 从 `main.py` 开始，了解整体流程
3. **理解核心** → 仔细阅读 `style_adapter.py`
4. **修改配置** → 在 `config.py` 中调整参数
5. **自定义扩展** → 修改各模块适配你的需求

---

## ❓ 常见问题

**Q: 不配置 OpenAI 能运行吗？**
A: 可以！会进入本地模拟模式，生成示例回应。

**Q: 如何改成用 Qwen？**
A: 在 `config.py` 中改 `LLM_CONFIG["provider"]` 和相应的 API 调用。

**Q: 语音识别很慢怎么办？**
A: 在 `config.py` 中改 `ASR_CONFIG["model"]` 为 `"tiny"`（更快但准确率低）。

**Q: 支持英文吗？**
A: 支持！修改 `config.py` 中 `ASR_CONFIG["language"]` 为 `"en"`。

---

## 📞 获取帮助

- 查看 `README.md` 获取详细文档
- 查看模块代码中的 docstring
- 运行各模块的测试代码: `python module_name.py`

---

## 🎯 下一步

这是 MVP（最小可行产品）版本。后续可增加：

- ✨ 长期记忆模块 (持久化用户偏好)
- ✨ 多轮对话管理
- ✨ 手势/多模态输入识别
- ✨ 实时流式生成
- ✨ 对话打断处理
- ✨ 高级风格学习算法

---

祝你使用愉快！🚀
