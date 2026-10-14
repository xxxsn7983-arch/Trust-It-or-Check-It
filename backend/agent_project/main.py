"""
自适应对话 Agent 系统 - 主程序

这是一个最小可运行的语音 Agent 项目，实现了以下功能：
1. 语音输入（ASR）
2. 自然语言理解（NLU）+ 用户状态建模
3. 风格自适应
4. 对话生成（LLM）
5. 语音输出（TTS）
"""

import os
import sys
from pathlib import Path

# 添加当前目录到 Python 路径
sys.path.insert(0, str(Path(__file__).parent))

from nlu import NLUModule, UserState
from speech_input import SpeechInputModule
from style_adapter import StyleAdapter
from llm_core import LLMCore
from tts import TTSModule


class AdaptiveAgent:
    """自适应对话 Agent 主类"""
    
    def __init__(self):
        """初始化 Agent 系统"""
        print("=" * 60)
        print("🤖 初始化自适应对话 Agent 系统")
        print("=" * 60)
        
        # 初始化各个模块
        self.nlu = NLUModule()
        self.speech_input = SpeechInputModule()
        self.style_adapter = StyleAdapter()
        self.llm = LLMCore()
        self.tts = TTSModule()
        
        print("\n✓ 所有模块初始化完成！\n")
    
    def process_text_input(self, user_input: str, enable_tts: bool = False) -> str:
        """
        处理文本输入（完整工作流）
        
        Args:
            user_input: 用户输入的文本
            enable_tts: 是否生成语音输出
            
        Returns:
            AI 生成的回应文本
        """
        print("\n" + "=" * 60)
        print(f"📝 用户输入: {user_input}")
        print("=" * 60)
        
        # 1️⃣ NLU 分析
        print("\n[第 1 步] 文本分析...")
        analysis = self.nlu.analyze_text(user_input)
        print(f"  • 意图: {analysis['intent']}")
        print(f"  • 情绪: {analysis['emotion']}")
        print(f"  • 语气: {analysis['tone']}")
        print(f"  • 关键词: {', '.join(analysis['keywords'])}")
        
        # 2️⃣ 更新用户状态
        print("\n[第 2 步] 更新用户状态...")
        user_state = self.nlu.update_user_state(analysis)
        print(f"  • 当前情绪: {user_state.emotion}")
        print(f"  • 当前语气: {user_state.tone}")
        print(f"  • 信任度: {user_state.trust:.2f}")
        print(f"  • 依赖程度: {user_state.dependency:.2f}")
        
        # 3️⃣ 获取用户说话模式
        print("\n[第 3 步] 分析用户模式...")
        user_pattern = self.nlu.get_user_pattern()
        print(f"  • {user_pattern}")
        
        # 4️⃣ 生成动态系统提示词
        print("\n[第 4 步] 生成自适应提示词...")
        system_prompt = self.style_adapter.generate_dynamic_prompt(
            user_state,
            user_pattern
        )
        print(f"  • 提示词长度: {len(system_prompt)} 字符")
        print(f"  • 风格: {user_state.tone}")
        
        # 5️⃣ 调用 LLM 生成回应
        print("\n[第 5 步] 生成 AI 回应...")
        response = self.llm.generate_response(system_prompt, user_input)
        print(f"  ✓ 回应生成完成")
        
        # 6️⃣ 输出回应
        print("\n" + "-" * 60)
        print(f"🤖 AI 回应:")
        print(response)
        print("-" * 60)
        
        # 7️⃣ 可选：生成语音输出
        if enable_tts:
            print("\n[第 6 步] 生成语音...")
            success = self.tts.synthesize_speech_sync(
                response,
                "agent_response.mp3"
            )
            if success:
                print("  ✓ 语音文件已生成: agent_response.mp3")
            else:
                print("  ✗ 语音生成失败")
        
        return response
    
    def process_audio_input(self, audio_file: str, enable_tts: bool = False) -> str:
        """
        处理音频输入（完整工作流）
        
        Args:
            audio_file: 音频文件路径
            enable_tts: 是否生成语音输出
            
        Returns:
            AI 生成的回应文本
        """
        print("\n" + "=" * 60)
        print(f"🎤 处理音频文件: {audio_file}")
        print("=" * 60)
        
        # 1️⃣ 语音识别
        print("\n[第 1 步] 语音识别 (ASR)...")
        asr_result = self.speech_input.transcribe_audio(audio_file)
        
        if not asr_result["text"]:
            print(f"  ✗ 识别失败: {asr_result.get('error', '未知错误')}")
            return "抱歉，我没有听清楚。"
        
        text = asr_result["text"]
        print(f"  ✓ 识别结果: {text}")
        
        # 2️⃣ 调用文本处理
        return self.process_text_input(text, enable_tts=enable_tts)
    
    def interactive_mode(self):
        """交互模式 - 持续对话"""
        print("\n" + "=" * 60)
        print("💬 进入交互模式 (输入 'exit' 或 'quit' 退出)")
        print("=" * 60)
        
        while True:
            try:
                user_input = input("\n👤 你: ").strip()
                
                if user_input.lower() in ["exit", "quit", "bye"]:
                    print("\n再见！")
                    break
                
                if not user_input:
                    continue
                
                self.process_text_input(user_input, enable_tts=False)
                
            except EOFError:
                print("\n再见！")
                break
            except KeyboardInterrupt:
                print("\n\n已中断")
                break
    
    def demo_workflow(self):
        """演示工作流 - 展示 Agent 的完整功能"""
        print("\n" + "=" * 60)
        print("📚 演示工作流 - 展示 Agent 的完整功能")
        print("=" * 60)
        
        # 演示对话
        demo_inputs = [
            "你好！",
            "这太棒了，能给我介绍一下 AI 吗？",
            "直接说要点就行",
            "谢谢你的帮助！",
        ]
        
        print("\n这个演示展示了 Agent 如何根据用户的情绪和语气调整回应风格。\n")
        
        for user_input in demo_inputs:
            self.process_text_input(user_input, enable_tts=False)
            print("\n" + "=" * 60)


def main():
    """主函数"""
    import argparse
    
    parser = argparse.ArgumentParser(
        description="自适应对话 Agent 系统 - 最小可运行版本"
    )
    parser.add_argument(
        "--mode",
        choices=["interactive", "demo", "text", "audio"],
        default="demo",
        help="运行模式"
    )
    parser.add_argument(
        "--input",
        type=str,
        help="输入文本或音频文件路径"
    )
    parser.add_argument(
        "--tts",
        action="store_true",
        help="启用文本转语音 (TTS)"
    )
    
    args = parser.parse_args()
    
    # 初始化 Agent
    agent = AdaptiveAgent()
    
    # 根据模式运行
    if args.mode == "interactive":
        agent.interactive_mode()
    elif args.mode == "demo":
        agent.demo_workflow()
    elif args.mode == "text":
        if not args.input:
            print("✗ 错误: --text 模式需要提供 --input 参数")
            sys.exit(1)
        agent.process_text_input(args.input, enable_tts=args.tts)
    elif args.mode == "audio":
        if not args.input:
            print("✗ 错误: --audio 模式需要提供 --input 参数")
            sys.exit(1)
        if not os.path.exists(args.input):
            print(f"✗ 错误: 音频文件不存在: {args.input}")
            sys.exit(1)
        agent.process_audio_input(args.input, enable_tts=args.tts)


if __name__ == "__main__":
    main()
