from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from agent_policy import AgentPolicy
from dialogue_evaluator import DialogueEvaluator, EvaluationResult
from knowledge_store import KnowledgeStore


KNOWLEDGE_PREP = "KNOWLEDGE_PREP"
FREE_CHAT = "FREE_CHAT"
SOCRATIC_LEAD_IN = "SOCRATIC_LEAD_IN"
REFLECTION_CHOICE = "REFLECTION_CHOICE"
AI_CONTROLLED_MISTAKE = FREE_CHAT
REFLECTION_PROMPT = REFLECTION_CHOICE
CHILD_DIRECT_REPLY = "CHILD_DIRECT_REPLY"
EVIDENCE_EXPLORE = "EVIDENCE_EXPLORE"
EVIDENCE_SUBMIT = "EVIDENCE_SUBMIT"
AI_SELF_CORRECTION_WITH_EVIDENCE = "AI_SELF_CORRECTION_WITH_EVIDENCE"
AI_REPAIR = "AI_REPAIR"
FINAL_CONCLUSION = "FINAL_CONCLUSION"


@dataclass
class LevelMemory:
    current_state: str = KNOWLEDGE_PREP
    child_initial_belief: str | None = None
    ai_chat_round_count: int = 0
    socratic_prompt_count: int = 0
    mistake_revealed: bool = False
    response_mode: str | None = None
    submitted_evidence_ids: list[str] = field(default_factory=list)
    last_evaluation: EvaluationResult | None = None


class LevelStateMachine:
    def __init__(
        self,
        case_id: str,
        store: KnowledgeStore | None = None,
        evaluator: DialogueEvaluator | None = None,
        agent: AgentPolicy | None = None,
        min_chat_rounds_before_mistake: int | None = None,
    ):
        self.case_id = case_id
        self.store = store or KnowledgeStore()
        self.evaluator = evaluator or DialogueEvaluator()
        self.agent = agent or AgentPolicy()
        self.case_package = self.store.load_case(case_id)
        self.memory = LevelMemory()
        self.min_chat_rounds_before_mistake = min_chat_rounds_before_mistake

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
                prompt = self.agent.socratic_prompt(self.case_package, prompt_index=1)
                self.memory.socratic_prompt_count += 1
                return self._response(prompt, view="ChatView", intent="force_focus")

            if self.memory.ai_chat_round_count >= policy.get("socratic_start_round", 4):
                self.memory.current_state = SOCRATIC_LEAD_IN
                prompt = self.agent.socratic_prompt(self.case_package, prompt_index=0)
                self.memory.socratic_prompt_count += 1
                return self._response(prompt, view="ChatView", intent="socratic_lead_in")

            chunks = self._retrieve_free_chat_chunks(message_text)
            message = self.agent.background_chat(
                self.case_package,
                chunks,
                message_text,
                chat_round=self.memory.ai_chat_round_count,
                socratic_start_round=policy.get("socratic_start_round", 4),
            )
            if self._should_block_early_focus_bridge(message_text, message):
                message = self._neutral_background_reply(chunks)
            message = self._limit_chat_message(message, max_chars=75)
            return self._response(message, view="ChatView", retrieved_chunk_ids=[chunk["chunk_id"] for chunk in chunks])

        if state == SOCRATIC_LEAD_IN and event == "CHILD_CHAT":
            self.memory.ai_chat_round_count += 1
            message_text = payload.get("message", "")
            if payload.get("force_reveal") is True:
                return self._reveal_controlled_wrong_claim(trigger="manual_force_reveal")
            if self._focus_intent(message_text) in {"critical_question", "focus_answer"}:
                return self._reveal_controlled_wrong_claim(trigger="socratic_response")

            if self.memory.socratic_prompt_count == 1:
                prompt = self.agent.socratic_prompt(self.case_package, prompt_index=1)
                self.memory.socratic_prompt_count += 1
                return self._response(prompt, view="ChatView", intent="force_focus")

            return self._reveal_controlled_wrong_claim(trigger="forced_after_socratic")

        if state == REFLECTION_CHOICE and event == "REFLECTION_CHOICE_SELECTED":
            choice = payload.get("choice")
            self.memory.response_mode = choice
            if choice == "revise":
                self.memory.current_state = AI_REPAIR
                return self._response(self.agent.repair_with_clues(self.case_package, choice="revise"), view="ChatView")
            if choice == "reject":
                self.memory.current_state = CHILD_DIRECT_REPLY
                return self._response(self.agent.prompt_child_direct_reply(), view="ChatInputView")
            if choice == "agree":
                self.memory.current_state = AI_REPAIR
                return self._response(self.agent.repair_with_clues(self.case_package, choice="agree"), view="ChatView")
            return self._response("这个选择暂时不能处理。", view="ChatWithChoiceButtonsView")

        if state == CHILD_DIRECT_REPLY and event == "CHILD_DIRECT_REPLY_SUBMITTED":
            evaluation = self.evaluator.evaluate_direct_reply(payload.get("message", ""), self.case_package)
            self.memory.last_evaluation = evaluation
            if evaluation.reply_quality == "valid":
                self.memory.current_state = AI_REPAIR
                return self._response(self.agent.repair(self.case_package, evaluation), view="ChatView", evaluation=evaluation)
            self.memory.current_state = AI_REPAIR
            return self._response(self.agent.repair_with_clues(self.case_package, choice="reject"), view="ChatView", evaluation=evaluation)

        if state == EVIDENCE_EXPLORE and event == "EVIDENCE_SELECTED":
            self.memory.submitted_evidence_ids = payload.get("evidence_ids", [])
            self.memory.current_state = EVIDENCE_SUBMIT
            return self.handle_event("EVIDENCE_SUBMITTED", {"evidence_ids": self.memory.submitted_evidence_ids})

        if state == EVIDENCE_SUBMIT and event == "EVIDENCE_SUBMITTED":
            evaluation = self.evaluator.validate_evidence(payload.get("evidence_ids", []), self.case_package)
            self.memory.last_evaluation = evaluation
            if evaluation.reply_quality == "valid":
                self.memory.current_state = AI_REPAIR
                return self._response(self.agent.repair(self.case_package, evaluation), view="ChatView", evaluation=evaluation)
            self.memory.current_state = EVIDENCE_EXPLORE
            return self._response("这个证据还不够有力，我们再找一张更能反驳 AI 的证据卡。", view="EvidenceExploreView", evaluation=evaluation)

        if state == AI_REPAIR and event == "AI_REPAIR_DONE":
            self.memory.current_state = FINAL_CONCLUSION
            return self._response(self.agent.final_conclusion(self.case_package), view="ConclusionView")

        return self._response(f"当前状态 {state} 不能处理事件 {event}。", view="NoChange")

    def _reveal_controlled_wrong_claim(self, trigger: str) -> dict[str, Any]:
        self.memory.mistake_revealed = True
        self.memory.current_state = REFLECTION_CHOICE
        message = self.agent.reveal_controlled_mistake(self.case_package)
        return self._response(
            message,
            view="ChatWithChoiceButtonsView",
            followup=self.agent.reflection_prompt(),
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
        legacy_round = self.min_chat_rounds_before_mistake
        if legacy_round is not None:
            return self.memory.ai_chat_round_count >= legacy_round
        return bool(force_round and self.memory.ai_chat_round_count >= force_round)

    def _retrieve_free_chat_chunks(self, message_text: str) -> list[dict[str, Any]]:
        policy = self._step4_policy()
        socratic_start_round = policy.get("socratic_start_round", 4)
        chunks = self.store.load_rag_chunks(self.case_id)
        focus_intent = self._focus_intent(message_text)
        if self.memory.ai_chat_round_count < socratic_start_round and focus_intent != "broad_context":
            chunks = [
                chunk
                for chunk in chunks
                if chunk.get("topic") not in {"special_phenomenon_surface", "defense_general"}
            ]
        if focus_intent == "broad_context":
            chunks = [
                chunk
                for chunk in chunks
                if chunk.get("topic") in {"predators", "defense_general", "body", "habits"}
            ] or chunks
        return self.store.retriever.retrieve(message_text, chunks, top_k=3)

    def _should_block_early_focus_bridge(self, child_message: str, agent_message: str) -> bool:
        policy = self._step4_policy()
        if self.memory.ai_chat_round_count >= policy.get("socratic_start_round", 4):
            return False
        child_allowed_terms = ["危险", "天敌", "敌人", "防御", "害怕", "受惊", "不动", "装死", "晕倒", "为什么不跑"]
        if any(term in child_message for term in child_allowed_terms):
            return False
        agent_focus_terms = ["危险", "天敌", "敌人", "防御", "受惊", "一动不动", "装死", "晕倒", "为什么不跑"]
        return any(term in agent_message for term in agent_focus_terms)

    def _neutral_background_reply(self, chunks: list[dict[str, Any]]) -> str:
        if not chunks:
            return "我们先继续了解负鼠的基本信息。你想问它吃什么、住在哪里，还是小负鼠怎么长大？"
        fact = chunks[0]["text"]
        first_sentence = fact.split("。")[0] + "。" if "。" in fact else fact
        return f"{first_sentence} 你还想问它吃什么、住在哪里，还是小负鼠怎么长大？"

    def _limit_chat_message(self, message: str, max_chars: int) -> str:
        if len(message) <= max_chars:
            return message
        first_sentence = message.split("。")[0] + "。" if "。" in message else message
        if len(first_sentence) <= max_chars:
            return first_sentence
        return first_sentence[: max_chars - 1].rstrip("，。； ") + "。"

    def _response(self, message: str, view: str, **extra: Any) -> dict[str, Any]:
        data = {
            "state": self.memory.current_state,
            "view": view,
            "agent_message": message
        }
        for key, value in extra.items():
            if isinstance(value, EvaluationResult):
                data[key] = value.__dict__
            else:
                data[key] = value
        return data
