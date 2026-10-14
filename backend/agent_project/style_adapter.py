"""风格自适应模块 - 根据用户状态动态调整回应风格"""

from typing import Dict
from config import STYLE_RULES
from nlu import UserState


class StyleAdapter:
    """风格自适应模块"""
    
    def __init__(self):
        """初始化风格适配器"""
        self.style_history = []
        print("✓ 已初始化风格适配模块")
    
    def generate_dynamic_prompt(self, user_state: UserState, user_pattern: str) -> str:
        """
        根据用户状态生成动态系统提示词
        
        Args:
            user_state: 用户状态对象
            user_pattern: 用户说话模式描述
            
        Returns:
            动态系统提示词
        """
        # 获取对应的风格规则
        style_rules = STYLE_RULES.get(
            user_state.tone,
            STYLE_RULES["casual"]
        )
        
        # 构建系统提示词
        system_prompt = f"""你是一个自适应的AI助手，能够根据用户的风格和状态进行实时调整。

当前用户状态：
- 情绪: {user_state.emotion}
- 语气: {user_state.tone}
- 信任度: {user_state.trust:.2f}
- 依赖程度: {user_state.dependency:.2f}
- 风格偏好: {user_state.style_preference}

根据上述信息，调整你的回应风格：
{style_rules}

用户最近的输入显示了这样的特点：{user_pattern}

核心要求：
1. 如果用户语气是 casual，使用轻松、随意的表达方式
2. 如果用户语气是 formal，使用专业、正式的表达方式
3. 如果用户语气是 directive，直接给出答案，减少解释
4. 根据信任度调整解释的详细程度：信任度高时减少解释，信任度低时增加解释
5. 保持回应的一致性，避免频繁改变风格

请用相应的风格进行回应。"""
        
        # 记录风格历史
        self.style_history.append(user_state.to_dict())
        if len(self.style_history) > 20:
            self.style_history.pop(0)
        
        return system_prompt
    
    def smooth_style(self, current_style: Dict, history_window: int = 3) -> Dict:
        """
        使用历史信息平滑风格变化，防止AI频繁切换风格
        
        Args:
            current_style: 当前风格字典
            history_window: 历史窗口大小
            
        Returns:
            平滑后的风格字典
        """
        if len(self.style_history) < 2:
            return current_style
        
        # 获取最近的历史记录
        recent_history = self.style_history[-history_window:]
        
        # 计算平均值进行平滑
        smooth_style = {
            "emotion": self._smooth_value(
                [s.get("emotion", "calm") for s in recent_history],
                current_style["emotion"]
            ),
            "tone": self._smooth_value(
                [s.get("tone", "casual") for s in recent_history],
                current_style["tone"]
            ),
            "trust": sum(s.get("trust", 0.5) for s in recent_history) / len(recent_history),
            "dependency": sum(s.get("dependency", 0.5) for s in recent_history) / len(recent_history),
        }
        
        return smooth_style
    
    def _smooth_value(self, history_values: list, current_value: str, current_weight: float = 0.3):
        """平滑离散值"""
        # 对于离散值（emotion, tone），采用最常出现的值，但加入当前值的权重
        from collections import Counter
        
        weighted_history = history_values.copy()
        # 将当前值加入历史，增加其权重
        for _ in range(int(len(history_values) * current_weight)):
            weighted_history.append(current_value)
        
        # 返回最常见的值
        if weighted_history:
            counter = Counter(weighted_history)
            return counter.most_common(1)[0][0]
        
        return current_value
    
    def adapt_for_emotion(self, emotion: str) -> Dict[str, str]:
        """
        根据情绪调整回应策略
        
        Returns:
            {
                "tone_adjustment": "语气调整建议",
                "content_adjustment": "内容调整建议",
                "length_adjustment": "长度调整建议"
            }
        """
        adjustments = {
            "calm": {
                "tone_adjustment": "保持稳定、理性的语气",
                "content_adjustment": "提供详细的逻辑和解释",
                "length_adjustment": "可以提供较长的回应"
            },
            "excited": {
                "tone_adjustment": "使用积极、热情的语气",
                "content_adjustment": "强调积极的方面，简化复杂内容",
                "length_adjustment": "简洁但富有感染力"
            },
            "frustrated": {
                "tone_adjustment": "使用同情、支持的语气",
                "content_adjustment": "直接解决问题，避免冗长解释",
                "length_adjustment": "简洁有效，快速给出解决方案"
            }
        }
        
        return adjustments.get(emotion, adjustments["calm"])
    
    def adapt_for_trust_level(self, trust: float) -> str:
        """
        根据信任度调整解释详细程度
        
        Args:
            trust: 信任度 (0-1)
            
        Returns:
            提示词补充内容
        """
        if trust < 0.3:
            return "这个用户对你的信任度很低，请提供详细的解释和理由，建立信任。"
        elif trust < 0.6:
            return "这个用户对你有基本的信任，提供适度的解释和例子。"
        else:
            return "这个用户对你有较高的信任度，可以给出直接的建议而不需要过多解释。"


if __name__ == "__main__":
    # 测试代码
    from nlu import UserState
    
    adapter = StyleAdapter()
    
    # 测试用户状态
    user_state = UserState(
        emotion="excited",
        tone="casual",
        trust=0.7,
        dependency=0.5,
    )
    
    # 生成动态提示词
    prompt = adapter.generate_dynamic_prompt(user_state, "用户倾向于短句、直接表达需求")
    print("生成的系统提示词:")
    print(prompt)
    print("\n" + "="*50 + "\n")
    
    # 测试情绪调整
    adjustments = adapter.adapt_for_emotion("excited")
    print("情绪调整建议:")
    print(adjustments)
    print("\n" + "="*50 + "\n")
    
    # 测试信任度调整
    trust_advice = adapter.adapt_for_trust_level(0.7)
    print("信任度调整建议:")
    print(trust_advice)
