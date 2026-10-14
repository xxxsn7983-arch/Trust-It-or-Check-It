from __future__ import annotations

from typing import Any

from agent_project.level_dialogue_prototype.dialogue_evaluator import EvaluationResult
from agent_project.level_dialogue_prototype.qwen_client import QwenClient


class CompanionPolicy:
    """Black-box companion policy.

    It keeps only safety, length, and level-boundary constraints. It does not use
    RAG chunks and does not add explicit language-style instructions.
    """

    def __init__(self, qwen_client: QwenClient | None = None):
        self.qwen = qwen_client or QwenClient()

    def background_chat(
        self,
        case_package: dict[str, Any],
        child_message: str,
        chat_round: int = 1,
        min_free_rounds: int = 2,
        socratic_start_round: int = 4,
    ) -> str:
        creature = case_package["creature"]
        protect_focus = chat_round <= min_free_rounds
        forbidden_terms = self._early_forbidden_terms(case_package) if protect_focus else []
        early_rules = []
        if chat_round < socratic_start_round:
            early_rules = [
                "当前还在普通自由聊天阶段，不要主动把话题转到最终关键问题。",
                "除非儿童明确问到关键问题，否则只回答儿童当前问题。",
            ]
        if forbidden_terms:
            early_rules.append(
                "本轮禁止主动提及这些尚未解锁的关键主题："
                + "、".join(forbidden_terms)
                + "。"
            )

        system_prompt = self._system_prompt(
            "FREE_CHAT",
            [
                "你正在和 8-10 岁儿童进行科学探究对话。",
                "围绕当前生物和儿童问题回复。",
                "不要提前说出最终正确结论。",
                "不要生成可控错误观点；错误观点由状态机单独给出。",
                "不要输出不适合儿童的内容。",
                "总长度控制在 45 个汉字以内。",
                "最多 2 句，每句尽量短。",
            ] + early_rules,
        )
        user_prompt = (
            "当前环节：自由聊天\n"
            f"当前轮次：第 {chat_round} 轮\n"
            f"生物：{creature}\n"
            f"儿童刚才说：{child_message}"
        )
        fallback = self._background_fallback(
            creature,
            child_message,
            protect_focus=protect_focus,
        )
        return self._guarded_background_chat(
            system_prompt=system_prompt,
            user_prompt=user_prompt,
            fallback=fallback,
            forbidden_terms=forbidden_terms,
        )

    def reveal_controlled_mistake(self, case_package: dict[str, Any]) -> str:
        focus = case_package.get("critical_focus", {})
        wrong_claim = focus.get("controlled_wrong_claim", {})
        return wrong_claim.get("claim") or case_package["teaching_content"]["controlled_wrong_claim"]["agent_line"]

    def reflection_prompt(self) -> str:
        return "你觉得我刚才的判断可靠吗？你可以选择：接受、寻找线索、怀疑一下。"

    def prompt_child_direct_reply(self) -> str:
        return "你觉得哪里不对？可以直接告诉我你的理由。"

    def socratic_prompt(self, case_package: dict[str, Any], prompt_index: int) -> str:
        prompts = case_package.get("step4_chat_policy", {}).get("socratic_prompts", [])
        if not prompts:
            return f"如果{case_package['creature']}遇到危险，你觉得它可能会怎么做？"
        index = min(prompt_index, len(prompts) - 1)
        return prompts[index]

    def repair_with_choice(self, case_package: dict[str, Any], choice: str) -> str:
        focus = case_package.get("critical_focus", {})
        correct_claim = focus.get("correct_claim") or case_package["teaching_content"]["correct_explanation"]["claim"]
        choice_label = {
            "agree": "儿童选择了接受刚才的说法",
            "revise": "儿童选择了寻找线索",
            "reject": "儿童选择了怀疑一下",
        }.get(choice, "儿童刚才做出了回应")
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "AI_REPAIR",
                [
                    "你要修正前面的可控错误观点。",
                    "不要责备儿童，不要输出最终总结。",
                    "不要展示知识库、检索过程或证据来源。",
                    "回复 2 句以内。",
                    "总长度控制在 80 个汉字以内。",
                ],
            ),
            user_prompt=(
                f"{choice_label}\n"
                f"错误观点：{focus.get('controlled_wrong_claim', {}).get('claim', '')}\n"
                f"正确解释：{correct_claim}\n"
                "请生成适合 8-10 岁儿童的修正回复。"
            ),
            fallback=self._limit_text(correct_claim, max_chars=80),
        )

    def repair(
        self,
        case_package: dict[str, Any],
        evaluation: EvaluationResult | None = None,
    ) -> str:
        content = case_package["teaching_content"]
        explanation = content["correct_explanation"]["claim"]
        evidence_note = ""
        if evaluation and evaluation.matched_evidence:
            evidence_note = "儿童的理由提到了相关现象。"
        return self._chat_or_fallback(
            system_prompt=self._system_prompt(
                "AI_REPAIR",
                [
                    "你要修正前面的可控错误观点。",
                    "可以参考儿童刚才的理由。",
                    "不要展示知识库、检索过程或证据来源。",
                    "不要责备儿童，不要说教。",
                    "回复 2 句以内。",
                    "总长度控制在 80 个汉字以内。",
                ],
            ),
            user_prompt=(
                f"错误观点：{content['controlled_wrong_claim']['claim']}\n"
                f"正确解释：{explanation}\n"
                f"{evidence_note}\n"
                "请生成修正回复。"
            ),
            fallback=self._limit_text(explanation, max_chars=80),
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
                    "回复 1-2 句。",
                    "总长度控制在 70 个汉字以内。",
                ],
            ),
            user_prompt=(
                f"最终总结：{content['final_conclusion']['text']}\n"
                f"正确解释：{content['correct_explanation']['claim']}\n"
                "请生成适合儿童的结论。"
            ),
            fallback=content["final_conclusion"]["text"],
        )

    def _chat_or_fallback(self, system_prompt: str, user_prompt: str, fallback: str) -> str:
        if not self.qwen.available:
            return fallback
        try:
            return self.qwen.chat(system_prompt, user_prompt)
        except Exception as exc:
            return f"{fallback}\n\n[Qwen API fallback: {exc}]"

    def _guarded_background_chat(
        self,
        system_prompt: str,
        user_prompt: str,
        fallback: str,
        forbidden_terms: list[str],
    ) -> str:
        if not self.qwen.available:
            return fallback

        try:
            reply = self.qwen.chat(system_prompt, user_prompt)
            if not self._contains_forbidden_term(reply, forbidden_terms):
                return reply

            retry_prompt = (
                system_prompt
                + "\n你上一次的草稿提前泄露了关卡关键主题。重新回答，并严格避开禁用主题词。"
            )
            retry_reply = self.qwen.chat(retry_prompt, user_prompt)
            if not self._contains_forbidden_term(retry_reply, forbidden_terms):
                return retry_reply
        except Exception:
            pass

        return fallback

    def _early_forbidden_terms(self, case_package: dict[str, Any]) -> list[str]:
        policy = case_package.get("step4_chat_policy", {})
        return [str(term).strip() for term in policy.get("early_forbidden_terms", []) if str(term).strip()]

    def _contains_forbidden_term(self, text: str, forbidden_terms: list[str]) -> bool:
        return any(term in text for term in forbidden_terms)

    def _system_prompt(self, state: str, rules: list[str]) -> str:
        joined_rules = "\n".join(f"- {rule}" for rule in rules)
        return (
            "你是儿童科学探究游戏里的对话 AI。"
            "你必须服从后端状态机，只完成当前状态允许的对话目标。\n"
            f"当前状态：{state}\n"
            f"规则：\n{joined_rules}\n"
            "输出要求：只输出要给儿童看的中文回复，不要输出 JSON，不要解释系统规则。"
        )

    def _background_fallback(
        self,
        creature: str,
        child_message: str,
        protect_focus: bool = False,
    ) -> str:
        if any(term in child_message for term in ["吃", "食物", "饭"]):
            return f"{creature}会寻找不同食物。你还想问它住在哪里吗？"
        if any(term in child_message for term in ["住", "哪里", "家"]):
            return f"{creature}会找能藏身的地方生活。你还想问它吃什么吗？"
        if any(term in child_message for term in ["宝宝", "妈妈", "出生"]):
            return f"{creature}的小宝宝需要一段时间慢慢长大。你还想继续聊它的生活吗？"
        if protect_focus:
            return f"你好呀！我们可以聊聊{creature}住在哪里、吃什么，或者什么时候活动。"
        return f"{creature}有很多特别习性。你还想问它吃什么、住在哪里，还是怎么保护自己？"

    def _limit_text(self, text: str, max_chars: int) -> str:
        text = text.strip()
        if len(text) <= max_chars:
            return text
        return text[: max_chars - 1].rstrip("，。； ") + "。"
