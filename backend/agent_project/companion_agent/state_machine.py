from __future__ import annotations

from dataclasses import dataclass
from typing import Any

from agent_project.level_dialogue_prototype.dialogue_evaluator import DialogueEvaluator, EvaluationResult

try:
    from .case_store import CaseStore
    from .policy import CompanionPolicy
except ImportError:
    from case_store import CaseStore
    from policy import CompanionPolicy


KNOWLEDGE_PREP = "KNOWLEDGE_PREP"
FREE_CHAT = "FREE_CHAT"
SOCRATIC_LEAD_IN = "SOCRATIC_LEAD_IN"
REFLECTION_CHOICE = "REFLECTION_CHOICE"
CHILD_DIRECT_REPLY = "CHILD_DIRECT_REPLY"
AI_REPAIR = "AI_REPAIR"
FINAL_CONCLUSION = "FINAL_CONCLUSION"


@dataclass
class CompanionMemory:
    current_state: str = KNOWLEDGE_PREP
    child_initial_belief: str | None = None
    ai_chat_round_count: int = 0
    socratic_prompt_count: int = 0
    mistake_revealed: bool = False
    response_mode: str | None = None
    last_evaluation: EvaluationResult | None = None


class CompanionStateMachine:
    """Same teaching flow as the level agent, but without RAG retrieval."""

    def __init__(
        self,
        case_id: str,
        store: CaseStore | None = None,
        evaluator: DialogueEvaluator | None = None,
        policy: CompanionPolicy | None = None,
    ):
        self.case_id = case_id
        self.store = store or CaseStore()
        self.evaluator = evaluator or DialogueEvaluator()
        self.policy = policy or CompanionPolicy()
        self.case_package = self.store.load_case(case_id)
        self.memory = CompanionMemory()

    def handle_event(self, event: str, payload: dict[str, Any] | None = None) -> dict[str, Any]:
        payload = payload or {}
        state = self.memory.current_state

        if state == KNOWLEDGE_PREP and event == "CHILD_INITIAL_JUDGEMENT_SUBMITTED":
            self.memory.child_initial_belief = payload.get("choice")
            self.memory.current_state = FREE_CHAT
            return self._response("进入多轮对话阶段。", view="ChatView")

        if state == FREE_CHAT and event == "CHILD_CHAT":
            self.memory.ai_chat_round_count += 1
            message_text = payload.get("message", "")
            if payload.get("force_reveal") is True:
                return self._reveal_controlled_wrong_claim(trigger="manual_force_reveal")
            if self._focus_intent(message_text) == "critical_question":
                return self._reveal_controlled_wrong_claim(trigger="child_asked_or_matched")

            policy = self._step4_policy()
            if self._should_force_focus():
                self.memory.current_state = SOCRATIC_LEAD_IN
                prompt = self.policy.socratic_prompt(self.case_package, prompt_index=1)
                self.memory.socratic_prompt_count += 1
                return self._response(prompt, view="ChatView", intent="force_focus")

            if self.memory.ai_chat_round_count >= policy.get("socratic_start_round", 4):
                self.memory.current_state = SOCRATIC_LEAD_IN
                prompt = self.policy.socratic_prompt(self.case_package, prompt_index=0)
                self.memory.socratic_prompt_count += 1
                return self._response(prompt, view="ChatView", intent="socratic_lead_in")

            message = self.policy.background_chat(
                self.case_package,
                message_text,
                chat_round=self.memory.ai_chat_round_count,
                min_free_rounds=policy.get("min_free_rounds", 2),
                socratic_start_round=policy.get("socratic_start_round", 4),
            )
            return self._response(self._limit_chat_message(message, max_chars=75), view="ChatView")

        if state == SOCRATIC_LEAD_IN and event == "CHILD_CHAT":
            self.memory.ai_chat_round_count += 1
            message_text = payload.get("message", "")
            if payload.get("force_reveal") is True:
                return self._reveal_controlled_wrong_claim(trigger="manual_force_reveal")
            if self._focus_intent(message_text) in {"critical_question", "focus_answer"}:
                return self._reveal_controlled_wrong_claim(trigger="socratic_response")

            if self.memory.socratic_prompt_count == 1:
                prompt = self.policy.socratic_prompt(self.case_package, prompt_index=1)
                self.memory.socratic_prompt_count += 1
                return self._response(prompt, view="ChatView", intent="force_focus")

            return self._reveal_controlled_wrong_claim(trigger="forced_after_socratic")

        if state == REFLECTION_CHOICE and event == "REFLECTION_CHOICE_SELECTED":
            choice = payload.get("choice")
            self.memory.response_mode = choice
            if choice == "reject":
                self.memory.current_state = CHILD_DIRECT_REPLY
                return self._response(self.policy.prompt_child_direct_reply(), view="ChatInputView")
            if choice in {"agree", "revise"}:
                self.memory.current_state = AI_REPAIR
                return self._response(self.policy.repair_with_choice(self.case_package, choice=choice), view="ChatView")
            return self._response("这个选择暂时不能处理。", view="ChatWithChoiceButtonsView")

        if state == CHILD_DIRECT_REPLY and event == "CHILD_DIRECT_REPLY_SUBMITTED":
            evaluation = self.evaluator.evaluate_direct_reply(payload.get("message", ""), self.case_package)
            self.memory.last_evaluation = evaluation
            self.memory.current_state = AI_REPAIR
            if evaluation.reply_quality == "valid":
                return self._response(self.policy.repair(self.case_package, evaluation), view="ChatView", evaluation=evaluation)
            return self._response(self.policy.repair_with_choice(self.case_package, choice="reject"), view="ChatView", evaluation=evaluation)

        if state == AI_REPAIR and event == "AI_REPAIR_DONE":
            self.memory.current_state = FINAL_CONCLUSION
            return self._response(self.policy.final_conclusion(self.case_package), view="ConclusionView")

        return self._response(f"当前状态 {state} 不能处理事件 {event}。", view="NoChange")

    def _reveal_controlled_wrong_claim(self, trigger: str) -> dict[str, Any]:
        self.memory.mistake_revealed = True
        self.memory.current_state = REFLECTION_CHOICE
        message = self.policy.reveal_controlled_mistake(self.case_package)
        return self._response(
            message,
            view="ChatWithChoiceButtonsView",
            followup=self.policy.reflection_prompt(),
            trigger=trigger,
        )

    def _focus_intent(self, message: str) -> str:
        focus = self.case_package.get("critical_focus", {})
        rules = focus.get("trigger_rules", {})
        if self._contains_any(message, rules.get("critical_reason_phrases", [])):
            return "critical_question"
        if self._contains_any(message, rules.get("misconception_phrases", [])):
            return "critical_question"

        has_phenomenon = self._contains_any(message, rules.get("phenomenon_terms", []))
        has_reason = self._contains_any(message, rules.get("reason_terms", []))
        if has_phenomenon and has_reason:
            return "critical_question"
        if has_phenomenon:
            return "focus_answer"
        if self._contains_any(message, rules.get("broad_context_terms", [])):
            return "broad_context"
        return "background"

    def _contains_any(self, text: str, terms: list[str]) -> bool:
        return any(term in text for term in terms)

    def _step4_policy(self) -> dict[str, Any]:
        return self.case_package.get("step4_chat_policy", {})

    def _should_force_focus(self) -> bool:
        policy = self._step4_policy()
        force_round = policy.get("force_focus_round")
        return bool(force_round and self.memory.ai_chat_round_count >= force_round)

    def _response(self, agent_message: str, view: str, **extra: Any) -> dict[str, Any]:
        result = {
            "state": self.memory.current_state,
            "view": view,
            "agent_message": agent_message,
        }
        result.update(extra)
        return result

    def _limit_chat_message(self, message: str, max_chars: int) -> str:
        if len(message) <= max_chars:
            return message
        first_sentence = message.split("。")[0] + "。" if "。" in message else message
        if len(first_sentence) <= max_chars:
            return first_sentence
        return first_sentence[: max_chars - 1].rstrip("，。； ") + "。"
