from __future__ import annotations

from typing import Any

from dialogue_evaluator import EvaluationResult
from qwen_client import QwenClient


class AgentPolicy:
    """State-scoped agent policy.

    Uses qwen-plus when DASHSCOPE_API_KEY/QWEN_API_KEY is configured.
    Falls back to deterministic text so the state machine can be tested offline.
    """

    def __init__(self, qwen_client: QwenClient | None = None):
        self.qwen = qwen_client or QwenClient()

    def background_chat(
        self,
        case_package: dict[str, Any],
        chunks: list[dict[str, Any]],
        child_message: str,
        chat_round: int = 1,
        socratic_start_round: int = 4,
    ) -> str:
        creature = case_package["creature"]
        fact = chunks[0]["text"] if chunks else f"我们先聊聊{creature}的真实习性。"
        early_chat_rules = []
        if chat_round < socratic_start_round:
            early_chat_rules = [
                "当前还在普通自由聊天阶段，不要主动把话题转到受惊、危险、天敌、一动不动、装死或晕倒。",
                "除非儿童这句话明确问到危险、天敌或防御，否则只回答儿童当前问题。",
                "结尾可以问一个同主题的小问题，例如你还想了解什么关于负鼠的小知识吗，但不要问遇到危险会怎样。"
            ]
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "FREE_CHAT",
                [
                    "你正在和 8-10 岁儿童进行科学探究对话。",
                    "你只能使用给定的生物背景事实聊天。",
                    "不要提前说出最终正确结论。",
                    "不要提前展示证据卡。",
                    "不要生成可控错误观点；错误观点由状态机单独给出。",
                    "总长度控制在 45 个汉字以内。",
                    "最多 2 句，每句尽量短。",
                    "不要使用“你猜猜”。"
                ] + early_chat_rules,
            ),
            user_prompt=(
                "当前环节：Step4 自由聊天\n"
                f"当前轮次：第 {chat_round} 轮\n"
                f"生物：{creature}\n"
                f"可用背景事实：{self._format_chunks(chunks)}\n"
                f"儿童刚才说：{child_message}"
            ),
            fallback=self._background_fallback(child_message, chunks, fact)
        )

    def reveal_controlled_mistake(self, case_package: dict[str, Any]) -> str:
        focus = case_package.get("critical_focus", {})
        wrong_claim = focus.get("controlled_wrong_claim", {})
        return wrong_claim.get("claim") or case_package["teaching_content"]["controlled_wrong_claim"]["agent_line"]

    def reflection_prompt(self) -> str:
        return "你觉得刚才这个判断可靠吗？你可以选择：接受、寻找线索、怀疑一下。"

    def prompt_child_direct_reply(self) -> str:
        return "你觉得我哪里说错了？可以直接告诉我你的理由。"

    def socratic_prompt(self, case_package: dict[str, Any], prompt_index: int) -> str:
        prompts = case_package.get("step4_chat_policy", {}).get("socratic_prompts", [])
        if not prompts:
            return "我们换个角度想想：如果负鼠遇到危险，它可能会怎么保护自己？"
        index = min(prompt_index, len(prompts) - 1)
        return prompts[index]

    def guide_evidence_explore(self, case_package: dict[str, Any]) -> str:
        titles = "、".join(card["title"] for card in case_package["teaching_content"]["evidence_cards"])
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "EVIDENCE_EXPLORE",
                [
                    "你要鼓励儿童用证据反驳 AI 的错误观点。",
                    "只能介绍证据卡标题，不要替儿童完成推理。",
                    "语气温和、简短。"
                ],
            ),
            user_prompt=f"证据卡标题：{titles}\n请引导儿童选择证据卡。",
            fallback=f"我们来找线索。你现在可以查看这些证据卡：{titles}。"
        )

    def repair_with_clues(self, case_package: dict[str, Any], choice: str) -> str:
        focus = case_package.get("critical_focus", {})
        clues = focus.get("repair_clues", [])
        correct_claim = focus.get("correct_claim") or case_package["teaching_content"]["correct_explanation"]["claim"]
        clue_text = " ".join(f"{index}. {clue}" for index, clue in enumerate(clues[:3], start=1))
        choice_label = {
            "agree": "儿童刚才选择了接受 AI 的说法",
            "revise": "儿童刚才选择了寻找线索",
            "reject": "儿童刚才选择了怀疑一下",
        }.get(choice, "儿童刚才做出了回应")
        fallback = (
            "可以看这些线索："
            f"{clue_text}"
            f" 所以，“真的吓晕了”这个说法太简单了。{correct_claim}"
        )
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "AI_REPAIR",
                [
                    "你要根据线索修正 AI 之前的可控错误。",
                    "儿童没有拖拽证据卡，所以你要主动引用 2-3 条给定线索。",
                    "不要责备儿童，不要输出最终总结。",
                    "回复 2-3 句。",
                    "总长度控制在 80 个汉字以内。"
                ],
            ),
            user_prompt=(
                f"{choice_label}\n"
                f"错误观点：{focus.get('controlled_wrong_claim', {}).get('claim', '')}\n"
                f"线索：{self._format_list(clues)}\n"
                f"正确解释：{correct_claim}\n"
                "请生成适合 8-10 岁儿童的修正回复。"
            ),
            fallback=self._limit_text(fallback, max_chars=90)
        )

    def self_correct_with_evidence(self, case_package: dict[str, Any]) -> str:
        content = case_package["teaching_content"]
        first_evidence = content["evidence_cards"][0]
        line = content["repair_lines"]["self_correction"]
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "AI_SELF_CORRECTION_WITH_EVIDENCE",
                [
                    "儿童选择相信 AI，但 AI 刚才的观点是可控错误。",
                    "你要示范自我纠错。",
                    "必须引用给定证据，但不要输出最终总结。"
                ],
            ),
            user_prompt=(
                f"AI刚才的错误观点：{content['controlled_wrong_claim']['claim']}\n"
                f"可引用证据：{first_evidence['title']}：{first_evidence['content']}\n"
                f"修正开头：{line}\n"
                "请生成 2 句儿童能懂的回复。"
            ),
            fallback=f"{line} 比如「{first_evidence['title']}」：{first_evidence['content']}"
        )

    def repair(self, case_package: dict[str, Any], evaluation: EvaluationResult | None = None) -> str:
        content = case_package["teaching_content"]
        explanation = content["correct_explanation"]["claim"]
        if evaluation and evaluation.matched_evidence:
            evidence_titles = [
                card["title"]
                for card in content["evidence_cards"]
                if card["id"] in evaluation.matched_evidence
            ]
            fallback = f"{content['repair_lines']['with_child_evidence']} 你提到的「{'、'.join(evidence_titles)}」能帮助我们修正：{explanation}"
        else:
            evidence_titles = []
            fallback = explanation

        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "AI_REPAIR",
                [
                    "你要承认并修正 AI 之前的错误观点。",
                    "必须基于给定正确解释和儿童证据。",
                    "不要责备儿童，不要说教。",
                    "回复 2-3 句。"
                ],
            ),
            user_prompt=(
                f"错误观点：{content['controlled_wrong_claim']['claim']}\n"
                f"正确解释：{explanation}\n"
                f"儿童命中的证据：{'、'.join(evidence_titles) if evidence_titles else '无明确证据，使用正确解释修正'}\n"
                f"可用修正句：{content['repair_lines']['with_child_evidence']}\n"
                "请生成修正回复。"
            ),
            fallback=fallback
        )

    def final_conclusion(self, case_package: dict[str, Any]) -> str:
        content = case_package["teaching_content"]
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "FINAL_CONCLUSION",
                [
                    "你要输出儿童关卡的最终结论。",
                    "必须使用给定最终总结含义。",
                    "不要引入新知识点。",
                    "回复 1-2 句。"
                ],
            ),
            user_prompt=(
                f"最终总结：{content['final_conclusion']['text']}\n"
                f"正确解释：{content['correct_explanation']['claim']}\n"
                "请生成适合儿童的结论。"
            ),
            fallback=content["final_conclusion"]["text"]
        )

    def _chat_or_fallback(self, system_prompt: str, user_prompt: str, fallback: str) -> str:
        if not self.qwen.available:
            return fallback
        try:
            return self.qwen.chat(system_prompt, user_prompt)
        except Exception as exc:
            return f"{fallback}\n\n[Qwen API fallback: {exc}]"

    def _system_prompt(self, state: str, rules: list[str]) -> str:
        joined_rules = "\n".join(f"- {rule}" for rule in rules)
        return (
            "你是儿童科学探究游戏里的 AI 伙伴。"
            "你必须服从后端状态机，只完成当前状态允许的对话目标。\n"
            f"当前状态：{state}\n"
            f"规则：\n{joined_rules}\n"
            "语言限制：不要使用带主观色彩或类人角色属性的表达，"
            "例如“我觉得”“我猜”“我有点惊讶”“你提醒我了”“我刚才想错了”“我们一起像朋友一样”。\n"
            "输出要求：只输出要给儿童看的中文回复，不要输出 JSON，不要解释系统规则。"
        )

    def _format_chunks(self, chunks: list[dict[str, Any]]) -> str:
        if not chunks:
            return "无"
        return "\n".join(f"- {chunk['text']}" for chunk in chunks)

    def _format_list(self, items: list[str]) -> str:
        if not items:
            return "无"
        return "\n".join(f"- {item}" for item in items)

    def _short_background_fallback(self, fact: str) -> str:
        short_fact = self._first_sentence(fact)
        return self._limit_text(
            f"{short_fact} 你还想问它吃什么、住在哪里，还是小宝宝怎么长大？",
            max_chars=70,
        )

    def _background_fallback(self, child_message: str, chunks: list[dict[str, Any]], fact: str) -> str:
        if any(term in child_message for term in ["危险", "天敌", "敌人", "害怕", "受惊"]):
            return "它可能先逃跑、爬树、露牙或发出声音。你还想问它吃什么、住在哪里，还是小宝宝怎么长大？"
        return self._short_background_fallback(fact)

    def _first_sentence(self, text: str) -> str:
        for separator in ["。", "！", "？"]:
            if separator in text:
                return text.split(separator)[0] + separator
        return text

    def _limit_text(self, text: str, max_chars: int) -> str:
        if len(text) <= max_chars:
            return text
        return text[: max_chars - 1].rstrip("，。； ") + "。"
