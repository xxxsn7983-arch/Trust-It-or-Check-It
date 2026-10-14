"""TTS 模块 - 文本转语音"""

import asyncio
import os
from pathlib import Path
from config import TTS_CONFIG

# 延迟导入 edge_tts，避免在导入时出现依赖问题
edge_tts = None


class TTSModule:
    """文本转语音模块"""
    
    def __init__(self):
        """初始化 TTS 模块"""
        self.provider = TTS_CONFIG["provider"]
        self.voice = TTS_CONFIG["voice"]
        self.rate = TTS_CONFIG["rate"]
        print(f"✓ 已初始化 TTS 模块 (provider: {self.provider})")
    
    async def synthesize_speech(self, text: str, output_file: str = "output.mp3") -> bool:
        """
        将文本转换为语音
        
        Args:
            text: 要转换的文本
            output_file: 输出文件路径
            
        Returns:
            True 如果成功，False 如果失败
        """
        if self.provider == "edge-tts":
            return await self._synthesize_edge_tts(text, output_file)
        else:
            print(f"✗ 不支持的 TTS 提供商: {self.provider}")
            return False
    
    async def _synthesize_edge_tts(self, text: str, output_file: str) -> bool:
        """
        使用 Edge TTS 合成语音
        """
        try:
            # 动态导入 edge_tts
            import edge_tts as et
            
            # 确保输出目录存在
            Path(output_file).parent.mkdir(parents=True, exist_ok=True)
            
            # 创建语音合成通信对象
            communicate = et.Communicate(
                text,
                voice=self.voice,
                rate=self.rate
            )
            
            # 保存到文件
            await communicate.save(output_file)
            print(f"✓ 已生成语音文件: {output_file}")
            return True
            
        except ImportError:
            print("✗ edge-tts 库未安装，请运行: pip install edge-tts")
            return False
        except Exception as e:
            print(f"✗ TTS 生成失败: {e}")
            return False
    
    def synthesize_speech_sync(self, text: str, output_file: str = "output.mp3") -> bool:
        """
        同步版本的语音合成
        
        Args:
            text: 要转换的文本
            output_file: 输出文件路径
            
        Returns:
            True 如果成功，False 如果失败
        """
        try:
            # 尝试获取现有事件循环或创建新的
            try:
                loop = asyncio.get_running_loop()
                # 如果已有运行中的循环，使用 asyncio.run() 会报错
                # 这种情况通常发生在 Jupyter 或其他异步框架中
                print("⚠️  检测到现有事件循环，建议使用异步版本 synthesize_speech()")
                return False
            except RuntimeError:
                # 没有运行中的循环，创建新的
                loop = asyncio.new_event_loop()
                asyncio.set_event_loop(loop)
                try:
                    result = loop.run_until_complete(
                        self.synthesize_speech(text, output_file)
                    )
                    return result
                finally:
                    loop.close()
        except Exception as e:
            print(f"✗ 同步 TTS 失败: {e}")
            return False
    
    def get_available_voices(self) -> list:
        """获取可用的语音列表"""
        # Edge TTS 支持的语音列表（部分）
        edge_tts_voices = {
            "zh-CN": [
                "zh-CN-XiaoxiaoNeural",  # 女性
                "zh-CN-YunxiNeural",     # 男性
                "zh-CN-YunyangNeural",   # 女性
                "zh-CN-XiaohanNeural",   # 女性
            ],
            "en-US": [
                "en-US-AriaNeural",
                "en-US-GuyNeural",
            ]
        }
        
        return edge_tts_voices.get("zh-CN", [])


if __name__ == "__main__":
    # 测试代码
    import asyncio
    
    tts = TTSModule()
    
    # 获取可用语音
    print("可用语音:")
    for voice in tts.get_available_voices():
        print(f"  - {voice}")
    
    # 测试文本转语音
    test_text = "你好，我是一个自适应的AI助手。很高兴认识你。"
    print(f"\n测试文本: {test_text}")
    
    success = tts.synthesize_speech_sync(test_text, "test_output.mp3")
    if success:
        print("✓ TTS 测试成功！")
    else:
        print("✗ TTS 测试失败")
