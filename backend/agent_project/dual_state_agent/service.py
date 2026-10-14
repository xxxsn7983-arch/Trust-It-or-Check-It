from __future__ import annotations

import sys
from dataclasses import asdict, dataclass, field, is_dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Literal
from uuid import uuid4

from agent_project.companion_agent.service import CompanionDialogueService
from agent_project.level_dialogue_prototype.session_logger import SessionLogger

from .trust_model import AgentId, TrustModel, TrustState


LEVEL_DIR = Path(__file__).resolve().parents[1] / "level_dialogue_prototype"
if str(LEVEL_DIR) not in sys.path:
    sys.path.insert(0, str(LEVEL_DIR))

from dialogue_service import DialogueService


BASE_DIR = Path(__file__).resolve().parent
MessageType = Literal["chat", "choice"]
SendFormat = Literal["chat", "choice"]


@dataclass
class DualStateSession:
    session_id: str
    anonymous_user_id: str
    case_id: str
    a_session_id: str
    b_session_id: str
    trust_state: TrustState = field(default_factory=TrustState)
    log_record: dict[str, Any] = field(default_factory=dict)
    history: list[dict[str, Any]] = field(default_factory=list)
    current_state: str = "FREE_CHAT"
    last_agent_response_at: datetime | None = None


class DualStateAgentService:
    """AB agent router.

    A = companion black-box agent.
    B = RAG/tool-state level agent.
    AB keeps both child sessions synchronized and returns the selected branch.
    """

    def __init__(
        self,
        companion_service: CompanionDialogueService | None = None,
        tool_service: DialogueService | None = None,
        trust_model: TrustModel | None = None,
    ):
        self.companion_service = companion_service or CompanionDialogueService()
        self.tool_service = tool_service or DialogueService()
        self.trust_model = trust_model or TrustModel()
        self.sessions: dict[str, DualStateSession] = {}
        self.logger = SessionLogger(log_dir=BASE_DIR / "data" / "logs" / "sessions")

    def start_level(
        self,
        case_id: str = "opossum_play_dead_001",
        child_initial_belief: str = "unknown",
        anonymous_user_id: str = "anonymous",
    ) -> dict[str, Any]:
        session_id = str(uuid4())
        a_start = self.companion_service.start_level(
            case_id=case_id,
            child_initial_belief=child_initial_belief,
            anonymous_user_id=anonymous_user_id,
        )
        b_start = self.tool_service.start_level(
            case_id=case_id,
            child_initial_belief=child_initial_belief,
            anonymous_user_id=anonymous_user_id,
        )
        log_record = self.logger.create_session(
            session_id=session_id,
            anonymous_user_id=anonymous_user_id,
            case_id=case_id,
            child_initial_belief=child_initial_belief,
        )
        log_record["agent_type"] = "AB"
        log_record["agent_condition"] = "trust_routed_dual_state"
        log_record["child_sessions"] = {
            "A": a_start["session_id"],
            "B": b_start["session_id"],
        }
        self.logger.save(log_record)

        session = DualStateSession(
            session_id=session_id,
            anonymous_user_id=anonymous_user_id,
            case_id=case_id,
            a_session_id=a_start["session_id"],
            b_session_id=b_start["session_id"],
            log_record=log_record,
        )
        self.sessions[session_id] = session

        selected = self._wrap_response(
            session=session,
            child_response=a_start,
            selected_agent="A",
            switch_reason="initial_companion_entry",
        )
        self._record_router_event(session, selected, a_response=a_start, b_response=b_start)
        return selected

    def send_message(
        self,
        session_id: str,
        reply_format: MessageType,
        content: str,
    ) -> dict[str, Any]:
        if reply_format == "chat":
            return self.send_chat(session_id=session_id, content=content)
        if reply_format == "choice":
            return self.send_choice(session_id=session_id, choice_number=content)
        raise ValueError(f"Unsupported reply_format: {reply_format}")

    def send_chat(self, session_id: str, content: str) -> dict[str, Any]:
        session = self._get_session(session_id)
        latency = self._latency_seconds(session)
        trust_update = self.trust_model.update(
            state=session.trust_state,
            text=content,
            reply_format="chat",
            choice=None,
            latency_seconds=latency,
            dialogue_state=session.current_state,
        )

        a_response = self.companion_service.send_chat(session.a_session_id, content)
        b_response = self.tool_service.send_chat(session.b_session_id, content)
        selected_agent, switch_reason = self._select_after_child_turn(session, b_response)
        selected_child_response = a_response if selected_agent == "A" else b_response
        response = self._wrap_response(
            session=session,
            child_response=selected_child_response,
            selected_agent=selected_agent,
            switch_reason=switch_reason,
            trust_update=self.trust_model.to_dict(trust_update),
        )
        self._record_router_event(session, response, a_response=a_response, b_response=b_response)
        return response

    def send_choice(self, session_id: str, choice_number: int | str) -> dict[str, Any]:
        session = self._get_session(session_id)
        latency = self._latency_seconds(session)
        trust_update = self.trust_model.update(
            state=session.trust_state,
            text=str(choice_number),
            reply_format="choice",
            choice=choice_number,
            latency_seconds=latency,
            dialogue_state=session.current_state,
        )

        a_response = self.companion_service.send_choice(session.a_session_id, choice_number)
        b_response = self.tool_service.send_choice(session.b_session_id, choice_number)
        selected_agent, switch_reason = self._select_after_child_turn(session, b_response)
        selected_child_response = a_response if selected_agent == "A" else b_response
        response = self._wrap_response(
            session=session,
            child_response=selected_child_response,
            selected_agent=selected_agent,
            switch_reason=switch_reason,
            trust_update=self.trust_model.to_dict(trust_update),
        )
        self._record_router_event(session, response, a_response=a_response, b_response=b_response)
        return response

    def get_session(self, session_id: str) -> dict[str, Any]:
        session = self._get_session(session_id)
        return {
            "session_id": session.session_id,
            "anonymous_user_id": session.anonymous_user_id,
            "agent_type": "AB",
            "case_id": session.case_id,
            "state": session.current_state,
            "selected_agent": session.trust_state.current_agent,
            "trust_score": session.trust_state.trust_score,
            "dependency_score": session.trust_state.dependency_score,
            "child_sessions": {
                "A": session.a_session_id,
                "B": session.b_session_id,
            },
            "history": session.history,
        }

    def _select_after_child_turn(
        self,
        session: DualStateSession,
        b_response: dict[str, Any],
    ) -> tuple[AgentId, str]:
        response_state = b_response["state"]
        return self.trust_model.select_agent(session.trust_state, response_state)

    def _wrap_response(
        self,
        session: DualStateSession,
        child_response: dict[str, Any],
        selected_agent: AgentId,
        switch_reason: str,
        trust_update: dict[str, Any] | None = None,
    ) -> dict[str, Any]:
        session.current_state = child_response["state"]
        session.last_agent_response_at = self._now()
        response = {
            "session_id": session.session_id,
            "agent_type": "AB",
            "selected_agent": selected_agent,
            "switch_reason": switch_reason,
            "trust_score": session.trust_state.trust_score,
            "dependency_score": session.trust_state.dependency_score,
            "send_format": child_response["send_format"],
            "state": child_response["state"],
            "view": child_response["view"],
            "content": child_response["content"],
        }
        if trust_update:
            response["trust_update"] = trust_update
        if child_response.get("debug"):
            response["debug"] = self._json_safe_value(child_response["debug"])
        return response

    def _record_router_event(
        self,
        session: DualStateSession,
        response: dict[str, Any],
        a_response: dict[str, Any],
        b_response: dict[str, Any],
    ) -> None:
        event = {
            "role": "router",
            "agent_type": "AB",
            "selected_agent": response["selected_agent"],
            "switch_reason": response["switch_reason"],
            "trust_score": response["trust_score"],
            "dependency_score": response["dependency_score"],
            "state_after": response["state"],
            "send_format": response["send_format"],
            "content": response["content"],
            "child_branch_state": {
                "A": a_response["state"],
                "B": b_response["state"],
            },
        }
        if response.get("trust_update"):
            event["trust_update"] = response["trust_update"]
        if response.get("debug"):
            event["debug"] = response["debug"]
        session.history.append(event)
        self.logger.append_event(session.log_record, event)

    def _json_safe_value(self, value: Any) -> Any:
        if is_dataclass(value) and not isinstance(value, type):
            return asdict(value)
        if isinstance(value, dict):
            return {key: self._json_safe_value(item) for key, item in value.items()}
        if isinstance(value, list):
            return [self._json_safe_value(item) for item in value]
        return value

    def _latency_seconds(self, session: DualStateSession) -> float | None:
        if session.last_agent_response_at is None:
            return None
        return max(0.0, (self._now() - session.last_agent_response_at).total_seconds())

    def _now(self) -> datetime:
        return datetime.now(timezone.utc)

    def _get_session(self, session_id: str) -> DualStateSession:
        try:
            return self.sessions[session_id]
        except KeyError as exc:
            raise KeyError(f"Unknown session_id: {session_id}") from exc
