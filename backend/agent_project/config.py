"""项目配置文件"""

# LLM 配置
LLM_CONFIG = {
    "provider": "openai",  # openai / qwen
    "model": "gpt-3.5-turbo",
    "temperature": 0.7,
    "max_tokens": 500,
}

# 语音识别配置
ASR_CONFIG = {
    "model": "base",  # tiny, base, small, medium, large
    "language": "zh",
}

# TTS 配置
TTS_CONFIG = {
    "provider": "edge-tts",  # edge-tts / elevenlabs
    "voice": "zh-CN-YunyangNeural",  # 中文女性声音
    "rate": "0.0",  # -50 to 50
}

# 用户状态配置
USER_STATE_CONFIG = {
    "emotion_options": ["calm", "excited", "frustrated"],
    "tone_options": ["casual", "formal", "directive"],
    "emotion_weight": 0.3,
    "tone_weight": 0.3,
    "trust_weight": 0.2,
    "dependency_weight": 0.2,
}

# 系统提示词模板
SYSTEM_PROMPT_TEMPLATE = """你是一个自适应的AI助手，能够根据用户的风格和状态进行实时调整。

当前用户状态：
- 情绪: {emotion}
- 语气: {tone}
- 信任度: {trust:.2f}
- 依赖程度: {dependency:.2f}
- 风格偏好: {style_preference}

根据上述信息，调整你的回应风格：
{style_rules}

用户最近的输入显示了这样的特点：{user_pattern}

请用相应的风格进行回应。"""

# 风格规则映射
STYLE_RULES = {
    "casual": "- 使用非正式的语言，可以使用一些口语\n- 可以偶尔使用emoji\n- 保持轻松的语气",
    "formal": "- 使用正式、专业的语言\n- 避免口语和非正式表达\n- 保持严谨的逻辑",
    "directive": "- 直接陈述事实和建议\n- 减少解释和装饰\n- 快速提供要点",
}

# API 密钥配置
API_KEYS = {
    "openai": None,  # 从环境变量读取
    "elevenlabs": None,  # 从环境变量读取
}
