from __future__ import annotations

from dataclasses import dataclass, field
from uuid import uuid4
from typing import Any, Literal

from session_logger import SessionLogger
from state_machine import CHILD_DIRECT_REPLY, FINAL_CONCLUSION, REFLECTION_CHOICE, LevelStateMachine


MessageType = Literal["chat", "choice"]
SendFormat = Literal["chat", "choice"]


@dataclass
class DialogueSession:
    session_id: str
    anonymous_user_id: str
    machine: LevelStateMachine
    log_record: dict[str, Any]
    history: list[dict[str, Any]] = field(default_factory=list)


class DialogueService:
    """Session layer between Unity HTTP calls and the teaching state machine."""

    def __init__(self):
        self.sessions: dict[str, DialogueSession] = {}
        self.logger = SessionLogger()

    def start_level(
        self,
        case_id: str,
        child_initial_belief: str = "unknown",
        anonymous_user_id: str = "anonymous",
    ) -> dict[str, Any]:
        session_id = str(uuid4())
        machine = LevelStateMachine(case_id)
        log_record = self.logger.create_session(
            session_id=session_id,
            anonymous_user_id=anonymous_user_id,
            case_id=case_id,
            child_initial_belief=child_initial_belief,
        )
        session = DialogueSession(
            session_id=session_id,
            anonymous_user_id=anonymous_user_id,
            machine=machine,
            log_record=log_record,
        )
        self.sessions[session_id] = session

        result = machine.handle_event(
            "CHILD_INITIAL_JUDGEMENT_SUBMITTED",
            {"choice": child_initial_belief},
        )
        response = self._to_frontend_response(session, result)
        self._record_agent_event(session, response, result)
        return response

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

    def send_chat(
        self,
        session_id: str,
        content: str,
    ) -> dict[str, Any]:
        session = self._get_session(session_id)
        machine = session.machine

        event, payload = self._chat_event(machine, content)

        child_event = {
            "role": "child",
            "reply_format": "chat",
            "content": content,
            "state_before": machine.memory.current_state,
        }
        session.history.append(child_event)
        self.logger.append_event(session.log_record, child_event)

        result = machine.handle_event(event, payload)
        response = self._to_frontend_response(session, result)

        self._record_agent_event(session, response, result)
        return response

    def send_choice(
        self,
        session_id: str,
        choice_number: int | str,
    ) -> dict[str, Any]:
        session = self._get_session(session_id)
        machine = session.machine
        content = str(choice_number)

        event, payload = self._choice_event(content)

        child_event = {
            "role": "child",
            "reply_format": "choice",
            "content": content,
            "state_before": machine.memory.current_state,
        }
        session.history.append(child_event)
        self.logger.append_event(session.log_record, child_event)

        result = machine.handle_event(event, payload)
        response = self._to_frontend_response(session, result)

        self._record_agent_event(session, response, result)
        return response

    def get_session(self, session_id: str) -> dict[str, Any]:
        session = self._get_session(session_id)
        memory = session.machine.memory
        return {
            "session_id": session.session_id,
            "anonymous_user_id": session.anonymous_user_id,
            "case_id": session.machine.case_id,
            "state": memory.current_state,
            "chat_round": memory.ai_chat_round_count,
            "history": session.history,
        }

    def _chat_event(self, machine: LevelStateMachine, content: str) -> tuple[str, dict[str, Any]]:
        if machine.memory.current_state == CHILD_DIRECT_REPLY:
            return "CHILD_DIRECT_REPLY_SUBMITTED", {"message": content}
        if machine.memory.current_state == FINAL_CONCLUSION:
            return "NOOP", {"message": content}
        return "CHILD_CHAT", {"message": content, "force_reveal": content.strip() == "/reveal"}

    def _choice_event(self, content: str) -> tuple[str, dict[str, Any]]:
        choice = self._choice_id_from_content(content)
        return "REFLECTION_CHOICE_SELECTED", {"choice": choice, "choice_text": content}

    def _choice_id_from_content(self, content: str) -> str:
        normalized = content.strip().lower()
        mapping = {
            "1": "agree",
            "2": "revise",
            "3": "reject",
        }
        if normalized not in mapping:
            raise ValueError("choice content must be 1, 2, or 3")
        return mapping.get(normalized, normalized)

    def _to_frontend_response(self, session: DialogueSession, result: dict[str, Any]) -> dict[str, Any]:
        send_format: SendFormat = "choice" if result["state"] == REFLECTION_CHOICE else "chat"
        content = self._content_for_reply(result, send_format)
        response = {
            "session_id": session.session_id,
            "send_format": send_format,
            "state": result["state"],
            "view": result["view"],
            "content": content,
        }
        debug = self._debug_payload(result)
        if debug:
            response["debug"] = debug
        return response

    def _record_agent_event(
        self,
        session: DialogueSession,
        response: dict[str, Any],
        result: dict[str, Any],
    ) -> None:
        agent_event = {
            "role": "agent",
            "send_format": response["send_format"],
            "content": response["content"],
            "state_after": response["state"],
            "view": response["view"],
        }
        debug = self._debug_payload(result)
        if debug:
            agent_event["debug"] = debug
        session.history.append(agent_event)
        self.logger.append_event(session.log_record, agent_event)

    def _content_for_reply(self, result: dict[str, Any], send_format: SendFormat) -> str:
        if send_format == "choice" and result.get("followup"):
            return f"{result['agent_message']}\n{result['followup']}"
        return result["agent_message"]

    def _debug_payload(self, result: dict[str, Any]) -> dict[str, Any]:
        debug_keys = ["retrieved_chunk_ids", "trigger", "intent", "evaluation"]
        return {key: result[key] for key in debug_keys if key in result}

    def _get_session(self, session_id: str) -> DialogueSession:
        try:
            return self.sessions[session_id]
        except KeyError as exc:
            raise KeyError(f"Unknown session_id: {session_id}") from exc
