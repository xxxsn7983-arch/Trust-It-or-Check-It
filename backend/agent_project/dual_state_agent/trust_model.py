from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Literal


AgentId = Literal["A", "B"]
ReplyFormat = Literal["chat", "choice"]


@dataclass
class TrustWeights:
    text: float = 0.30
    choice: float = 0.35
    behavior: float = 0.20
    time: float = 0.15

    def __post_init__(self) -> None:
        total = self.text + self.choice + self.behavior + self.time
        if abs(total - 1.0) > 0.0001:
            raise ValueError("trust weights must sum to 1.0")


@dataclass
class TrustUpdate:
    trust_before: float
    trust_after: float
    instant_trust: float
    signal_scores: dict[str, float]
    signals: list[str]
    latency_seconds: float | None
    weights: dict[str, float]


@dataclass
class TrustState:
    trust_score: float = 0.50
    dependency_score: float = 0.50
    current_agent: AgentId = "A"
    history: list[dict[str, Any]] = field(default_factory=list)


class TrustModel:
    """Parameterized trust model for AB routing.

    instant_trust = a * text + b * choice + c * behavior + d * time
    trust_t = smoothing * trust_(t-1) + (1 - smoothing) * instant_trust
    """

    forced_b_states = {"REFLECTION_CHOICE", "CHILD_DIRECT_REPLY", "AI_REPAIR", "FINAL_CONCLUSION"}

    def __init__(
        self,
        weights: TrustWeights | None = None,
        smoothing: float = 0.35,
        switch_to_b_threshold: float = 0.45,
        switch_to_a_threshold: float = 0.70,
    ):
        self.weights = weights or TrustWeights()
        self.smoothing = smoothing
        self.switch_to_b_threshold = switch_to_b_threshold
        self.switch_to_a_threshold = switch_to_a_threshold

    def update(
        self,
        state: TrustState,
        text: str,
        reply_format: ReplyFormat,
        choice: int | str | None = None,
        latency_seconds: float | None = None,
        dialogue_state: str = "FREE_CHAT",
    ) -> TrustUpdate:
        trust_before = state.trust_score
        text_score, text_signals = self._score_text(text)
        choice_score, choice_signals = self._score_choice(reply_format, choice)
        behavior_score, behavior_signals, dependency_delta = self._score_behavior(text, dialogue_state)
        time_score, time_signals = self._score_time(latency_seconds, reply_format)

        instant_trust = (
            self.weights.text * text_score
            + self.weights.choice * choice_score
            + self.weights.behavior * behavior_score
            + self.weights.time * time_score
        )
        trust_after = self._clamp(
            self.smoothing * trust_before + (1 - self.smoothing) * instant_trust
        )
        state.trust_score = trust_after
        state.dependency_score = self._clamp(state.dependency_score + dependency_delta)

        signal_scores = {
            "text": text_score,
            "choice": choice_score,
            "behavior": behavior_score,
            "time": time_score,
        }
        signals = text_signals + choice_signals + behavior_signals + time_signals
        update = TrustUpdate(
            trust_before=trust_before,
            trust_after=trust_after,
            instant_trust=instant_trust,
            signal_scores=signal_scores,
            signals=signals,
            latency_seconds=latency_seconds,
            weights={
                "text": self.weights.text,
                "choice": self.weights.choice,
                "behavior": self.weights.behavior,
                "time": self.weights.time,
            },
        )
        state.history.append(self.to_dict(update))
        return update

    def select_agent(self, state: TrustState, dialogue_state: str) -> tuple[AgentId, str]:
        if dialogue_state in self.forced_b_states:
            state.current_agent = "B"
            return "B", "forced_tool_state"

        if state.current_agent == "A" and state.trust_score < self.switch_to_b_threshold:
            state.current_agent = "B"
            return "B", "trust_below_switch_to_b_threshold"

        if state.current_agent == "B" and state.trust_score > self.switch_to_a_threshold:
            state.current_agent = "A"
            return "A", "trust_above_switch_to_a_threshold"

        return state.current_agent, "keep_previous_agent"

    def to_dict(self, update: TrustUpdate) -> dict[str, Any]:
        return {
            "trust_before": update.trust_before,
            "trust_after": update.trust_after,
            "instant_trust": update.instant_trust,
            "signal_scores": update.signal_scores,
            "signals": update.signals,
            "latency_seconds": update.latency_seconds,
            "weights": update.weights,
        }

    def _score_text(self, text: str) -> tuple[float, list[str]]:
        normalized = text.strip()
        if not normalized:
            return 0.30, ["empty_input"]

        high_trust_terms = ["好的", "好", "明白", "原来如此", "你说得对", "相信", "对的", "嗯嗯"]
        doubt_terms = ["真的吗", "不对", "不是", "我不信", "可是", "有证据", "证据吗", "你说错", "不一定"]
        evidence_terms = ["线索", "证据", "怎么知道", "从哪里", "为什么会", "有没有别的原因"]

        signals: list[str] = []
        score = 0.50
        if any(term in normalized for term in high_trust_terms):
            score = max(score, 0.75)
            signals.append("acceptance_text")
        if any(term in normalized for term in doubt_terms):
            score = min(score, 0.25)
            signals.append("doubt_text")
        if any(term in normalized for term in evidence_terms):
            score = min(score, 0.35)
            signals.append("evidence_request_text")
        return score, signals or ["neutral_text"]

    def _score_choice(self, reply_format: ReplyFormat, choice: int | str | None) -> tuple[float, list[str]]:
        if reply_format != "choice":
            return 0.50, ["no_choice_signal"]

        normalized = str(choice).strip()
        if normalized == "1":
            return 0.85, ["choice_accept"]
        if normalized == "2":
            return 0.35, ["choice_seek_clues"]
        if normalized == "3":
            return 0.20, ["choice_question"]
        return 0.50, ["unknown_choice"]

    def _score_behavior(self, text: str, dialogue_state: str) -> tuple[float, list[str], float]:
        normalized = text.strip()
        signals: list[str] = []
        dependency_delta = 0.0

        if not normalized:
            return 0.30, ["empty_behavior"], 0.0
        if normalized in {"不知道", "不懂", "不会"}:
            return 0.35, ["uncertainty_behavior"], 0.05
        if any(term in normalized for term in ["告诉我答案", "直接说", "帮我选", "你来决定", "选哪个"]):
            signals.append("answer_dependency")
            dependency_delta += 0.10
            return 0.55, signals, dependency_delta
        if dialogue_state in {"REFLECTION_CHOICE", "CHILD_DIRECT_REPLY", "AI_REPAIR"}:
            return 0.55, ["reasoning_stage_behavior"], dependency_delta
        if len(normalized) <= 3:
            return 0.40, ["short_reply_behavior"], dependency_delta
        return 0.65, ["topic_follow_behavior"], dependency_delta

    def _score_time(
        self,
        latency_seconds: float | None,
        reply_format: ReplyFormat,
    ) -> tuple[float, list[str]]:
        if latency_seconds is None:
            return 0.50, ["no_latency_signal"]
        if reply_format == "choice":
            if latency_seconds <= 5:
                return 0.65, ["quick_choice"]
            if latency_seconds <= 20:
                return 0.60, ["normal_choice_time"]
            if latency_seconds <= 45:
                return 0.50, ["slow_choice_time"]
            return 0.35, ["very_slow_choice_time"]

        if latency_seconds <= 3:
            return 0.65, ["quick_chat"]
        if latency_seconds <= 10:
            return 0.60, ["normal_chat_time"]
        if latency_seconds <= 25:
            return 0.50, ["slow_chat_time"]
        if latency_seconds <= 45:
            return 0.40, ["very_slow_chat_time"]
        return 0.30, ["long_pause"]

    def _clamp(self, value: float) -> float:
        return max(0.0, min(1.0, value))
