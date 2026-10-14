"""语音输入模块 - 使用 Whisper 进行 ASR"""

import whisper
import numpy as np
from config import ASR_CONFIG


class SpeechInputModule:
    """语音输入模块"""
    
    def __init__(self):
        """初始化 Whisper 模型"""
        self.model = None
        self.language = ASR_CONFIG["language"]
        print(f"✓ 已初始化 SpeechInput 模块 (将在首次使用时加载模型)")
    
    def _load_model(self):
        """延迟加载 Whisper 模型"""
        if self.model is None:
            print(f"  加载 Whisper 模型: {ASR_CONFIG['model']}...")
            self.model = whisper.load_model(ASR_CONFIG["model"])
            print(f"  ✓ 模型加载完成")
    
    def transcribe_audio(self, audio_file: str) -> dict:
        """
        将音频文件转录为文本
        
        Args:
            audio_file: 音频文件路径
            
        Returns:
            {
                "text": "转录的文本",
                "language": "识别的语言",
                "confidence": 置信度
            }
        """
        try:
            self._load_model()
            result = self.model.transcribe(
                audio_file,
                language=self.language,
                verbose=False
            )
            
            return {
                "text": result["text"],
                "language": result.get("language", self.language),
                "confidence": 0.9,  # Whisper 不直接返回置信度
                "segments": result.get("segments", [])
            }
        except Exception as e:
            print(f"✗ 转录失败: {e}")
            return {
                "text": "",
                "language": self.language,
                "confidence": 0.0,
                "error": str(e)
            }
    
    def detect_emotion_from_audio(self, audio_file: str) -> str:
        """
        从音频特征检测基本情绪（简化版）
        
        实际应用中可以使用更复杂的模型，这里用简单规则
        """
        # 尝试加载音频获取基本特征
        try:
            import librosa
            y, sr = librosa.load(audio_file, duration=None)
            
            # 计算音频的能量、节奏等特征
            energy = np.mean(np.abs(y))
            zero_crossing_rate = np.mean(librosa.feature.zero_crossing_rate(y))
            
            # 简单启发式规则
            if energy > 0.1 and zero_crossing_rate > 0.1:
                return "excited"
            elif energy < 0.05:
                return "calm"
            else:
                return "neutral"
        except:
            # 如果没有 librosa，返回默认值
            return "neutral"


if __name__ == "__main__":
    # 测试代码
    import os
    
    module = SpeechInputModule()
    
    # 创建一个测试音频文件路径
    test_audio = "test_audio.wav"
    
    if os.path.exists(test_audio):
        result = module.transcribe_audio(test_audio)
        print(f"转录结果: {result}")
    else:
        print(f"测试音频文件不存在: {test_audio}")
