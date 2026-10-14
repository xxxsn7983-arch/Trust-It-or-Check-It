"""LLM 核心模块 - 调用大语言模型"""

import os
from typing import Optional
from config import LLM_CONFIG


class LLMCore:
    """LLM 核心模块"""
    
    def __init__(self, api_key: Optional[str] = None):
        """
        初始化 LLM 核心
        
        Args:
            api_key: OpenAI API 密钥（可选，会从环境变量读取）
        """
        self.provider = LLM_CONFIG["provider"]
        self.model = LLM_CONFIG["model"]
        self.temperature = LLM_CONFIG["temperature"]
        self.max_tokens = LLM_CONFIG["max_tokens"]
        
        # 获取 API 密钥
        if api_key is None:
            api_key = os.getenv("OPENAI_API_KEY")
        
        if not api_key and self.provider == "openai":
            print("⚠️  警告: 没有找到 OPENAI_API_KEY 环境变量")
            print("   请设置 OPENAI_API_KEY 来使用 OpenAI API")
            self.api_key = None
        else:
            self.api_key = api_key
        
        print(f"✓ 已初始化 LLM 核心 (provider: {self.provider}, model: {self.model})")
    
    def generate_response(self, system_prompt: str, user_message: str) -> str:
        """
        生成 AI 回应
        
        Args:
            system_prompt: 系统提示词（包含用户状态和风格信息）
            user_message: 用户输入消息
            
        Returns:
            AI 生成的回应文本
        """
        if self.provider == "openai":
            return self._generate_openai(system_prompt, user_message)
        else:
            return self._generate_mock(system_prompt, user_message)
    
    def _generate_openai(self, system_prompt: str, user_message: str) -> str:
        """
        使用 OpenAI API 生成回应
        """
        try:
            import openai
            
            if not self.api_key:
                return "✗ 错误: 没有配置 OpenAI API 密钥"
            
            # 设置 API 密钥
            openai.api_key = self.api_key
            
            # 调用 OpenAI API
            response = openai.ChatCompletion.create(
                model=self.model,
                messages=[
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_message}
                ],
                temperature=self.temperature,
                max_tokens=self.max_tokens,
            )
            
            return response.choices[0].message.content
            
        except ImportError:
            return "✗ 错误: 未安装 openai 库，请运行 pip install openai"
        except Exception as e:
            return f"✗ OpenAI API 调用失败: {e}"
    
    def _generate_mock(self, system_prompt: str, user_message: str) -> str:
        """
        模拟 LLM 响应（当 API 不可用时）
        
        这是一个简化版本，用于演示和离线测试
        """
        # 根据用户信息生成简单回应
        responses = {
            "casual": [
                f"哈哈，关于'{user_message}'，我的想法是：这个话题很有意思！你想深入了解吗？",
                f"嗯，{user_message}？我觉得我们可以这样看..."
            ],
            "formal": [
                f"关于您提到的'{user_message}'，我有以下观点：...",
                f"就'{user_message}'这一问题，我们可以从以下几个方面进行阐述..."
            ],
            "directive": [
                f"答案：{user_message}",
                f"直接回答：在这种情况下应该..."
            ]
        }
        
        # 从系统提示词中提取语气
        tone = "casual"  # 默认值
        if "formal" in system_prompt or "正式" in system_prompt:
            tone = "formal"
        elif "directive" in system_prompt or "直接" in system_prompt:
            tone = "directive"
        
        import random
        return random.choice(responses.get(tone, responses["casual"]))
    
    def stream_response(self, system_prompt: str, user_message: str):
        """
        流式生成 AI 回应（按字符逐步返回）
        
        Yields:
            生成的文本片段
        """
        # 这是一个简化的流式实现
        response = self.generate_response(system_prompt, user_message)
        
        # 模拟流式输出
        for char in response:
            yield char


if __name__ == "__main__":
    # 测试代码
    from nlu import UserState
    from style_adapter import StyleAdapter
    
    # 初始化模块
    llm = LLMCore()
    adapter = StyleAdapter()
    
    # 创建测试用户状态
    user_state = UserState(
        emotion="excited",
        tone="casual",
        trust=0.7,
    )
    
    # 生成动态提示词
    system_prompt = adapter.generate_dynamic_prompt(
        user_state,
        "用户倾向于短句、直接表达需求"
    )
    
    # 测试消息
    test_message = "如何学习 AI？"
    
    print("测试 LLM 生成:")
    print(f"用户消息: {test_message}")
    print(f"\n生成的回应:")
    
    response = llm.generate_response(system_prompt, test_message)
    print(response)
