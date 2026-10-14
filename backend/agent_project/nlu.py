"""自然语言理解模块 - NLU + 用户状态建模"""

from dataclasses import dataclass
from typing import Dict, List
import re


@dataclass
class UserState:
    """用户状态数据类"""
    emotion: str = "calm"  # calm / excited / frustrated
    tone: str = "casual"  # casual / formal / directive
    trust: float = 0.5  # 0-1
    dependency: float = 0.5  # 0-1
    style_preference: str = "casual"
    
    def to_dict(self):
        return {
            "emotion": self.emotion,
            "tone": self.tone,
            "trust": self.trust,
            "dependency": self.dependency,
            "style_preference": self.style_preference,
        }


class NLUModule:
    """自然语言理解模块"""
    
    def __init__(self):
        """初始化 NLU 模块"""
        self.user_state = UserState()
        self.conversation_history: List[str] = []
        self.emotion_keywords = {
            "excited": ["太好了", "太棒了", "棒", "太浓厉", "厉害", "非常好", "完美"],
            "frustrated": ["太差了", "不行", "糟糕", "很烦", "讨厌", "烦死了"],
            "calm": ["好的", "可以", "明白", "了解"],
        }
        self.tone_patterns = {
            "directive": ["直接", "快说", "好吧", "好", "行"],
            "casual": ["呃", "就是", "嘿", "怎么样"],
            "formal": ["您", "请问", "恳请", "尊敬"],
        }
        print("✓ 已初始化 NLU 模块")
    
    def analyze_text(self, text: str) -> Dict:
        """
        分析用户输入的文本
        
        Returns:
            {
                "intent": "用户意图",
                "emotion": "情绪",
                "tone": "语气",
                "keywords": ["关键词"],
                "sentence_length": 句子长度
            }
        """
        # 检测情绪
        detected_emotion = self._detect_emotion(text)
        
        # 检测语气
        detected_tone = self._detect_tone(text)
        
        # 提取关键词
        keywords = self._extract_keywords(text)
        
        # 计算句子长度
        sentence_length = len(text)
        
        # 简单意图识别
        intent = self._recognize_intent(text)
        
        return {
            "intent": intent,
            "emotion": detected_emotion,
            "tone": detected_tone,
            "keywords": keywords,
            "sentence_length": sentence_length,
            "text": text,
        }
    
    def _detect_emotion(self, text: str) -> str:
        """检测文本中的情绪"""
        for emotion, keywords in self.emotion_keywords.items():
            for keyword in keywords:
                if keyword in text:
                    return emotion
        return self.user_state.emotion  # 保持现有情绪
    
    def _detect_tone(self, text: str) -> str:
        """检测文本中的语气"""
        tone_scores = {"casual": 0, "formal": 0, "directive": 0}
        
        for tone, patterns in self.tone_patterns.items():
            for pattern in patterns:
                if pattern in text:
                    tone_scores[tone] += 1
        
        # 返回得分最高的语气
        if max(tone_scores.values()) > 0:
            return max(tone_scores, key=tone_scores.get)
        
        return self.user_state.tone  # 保持现有语气
    
    def _extract_keywords(self, text: str) -> List[str]:
        """
        提取文本关键词（简单版）
        
        实际应用可以使用 jieba 或其他分词库
        """
        # 分割成单词（这里做简化处理）
        words = re.split(r'[\s，。？！；、]+', text)
        # 保留长度 > 2 的词
        keywords = [w for w in words if len(w) > 1][:5]
        return keywords
    
    def _recognize_intent(self, text: str) -> str:
        """简单的意图识别"""
        if any(word in text for word in ["帮", "做", "可以", "行", "能"]):
            return "request"
        elif any(word in text for word in ["怎么", "什么", "为什么", "？"]):
            return "question"
        elif any(word in text for word in ["谢", "感谢", "谢谢"]):
            return "thanks"
        else:
            return "statement"
    
    def update_user_state(self, analysis: Dict) -> UserState:
        """
        根据文本分析更新用户状态
        
        使用加权平均来平滑状态变化
        """
        # 检测到的值
        detected_emotion = analysis["emotion"]
        detected_tone = analysis["tone"]
        
        # 根据句子长度判断用户偏好
        if analysis["sentence_length"] < 10:
            detected_tone = "directive"  # 短句用户倾向直接
        elif analysis["sentence_length"] > 50:
            detected_tone = "formal"  # 长句用户更正式
        
        # 平滑更新：保留历史信息，避免剧烈波动
        alpha = 0.3  # 平滑系数
        
        if detected_emotion != self.user_state.emotion:
            self.user_state.emotion = detected_emotion
        
        if detected_tone != self.user_state.tone:
            self.user_state.tone = detected_tone
        
        # 信任度和依赖程度相对稳定，缓慢增长
        if analysis["intent"] in ["thanks", "statement"]:
            self.user_state.trust = min(1.0, self.user_state.trust + 0.05)
            self.user_state.dependency = min(1.0, self.user_state.dependency + 0.05)
        
        self.user_state.style_preference = self.user_state.tone
        
        # 记录对话历史
        self.conversation_history.append(analysis["text"])
        if len(self.conversation_history) > 10:
            self.conversation_history.pop(0)
        
        return self.user_state
    
    def get_user_pattern(self) -> str:
        """获取用户的说话模式描述"""
        if not self.conversation_history:
            return "用户刚开始交互"
        
        avg_length = sum(len(h) for h in self.conversation_history) / len(self.conversation_history)
        
        if avg_length < 10:
            pattern = "用户倾向于短句、直接表达需求"
        elif avg_length < 30:
            pattern = "用户倾向于简洁、清晰的表达"
        else:
            pattern = "用户倾向于详细、完整的表达"
        
        return pattern


if __name__ == "__main__":
    # 测试代码
    nlu = NLUModule()
    
    test_texts = [
        "这太棒了！",
        "直接给我答案",
        "您能否详细解释一下这个概念？",
    ]
    
    for text in test_texts:
        analysis = nlu.analyze_text(text)
        state = nlu.update_user_state(analysis)
        print(f"输入: {text}")
        print(f"分析: {analysis}")
        print(f"状态: {state}")
        print()
