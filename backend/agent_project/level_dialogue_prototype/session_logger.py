from __future__ import annotations

import json
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
import re


BASE_DIR = Path(__file__).resolve().parent


class SessionLogger:
    """Writes one JSON log file per dialogue session for later manual analysis."""

    def __init__(self, log_dir: Path | None = None):
        self.log_dir = log_dir or BASE_DIR / "data" / "logs" / "sessions"
        self.log_dir.mkdir(parents=True, exist_ok=True)

    def create_session(
        self,
        session_id: str,
        anonymous_user_id: str,
        case_id: str,
        child_initial_belief: str,
    ) -> dict[str, Any]:
        created_at = self._now()
        record = {
            "session_id": session_id,
            "anonymous_user_id": anonymous_user_id,
            "case_id": case_id,
            "child_initial_belief": child_initial_belief,
            "created_at": created_at,
            "updated_at": created_at,
            "log_file_name": self._build_log_file_name(
                anonymous_user_id=anonymous_user_id,
                case_id=case_id,
            ),
            "events": [],
        }
        self.save(record)
        return record

    def append_event(self, record: dict[str, Any], event: dict[str, Any]) -> None:
        event_with_time = {"time": self._now(), **event}
        record["events"].append(event_with_time)
        record["updated_at"] = self._now()
        self.save(record)

    def save(self, record: dict[str, Any]) -> None:
        file_name = record.get("log_file_name") or f"{record['session_id']}.json"
        path = self.log_dir / file_name
        with path.open("w", encoding="utf-8") as file:
            json.dump(record, file, ensure_ascii=False, indent=2)

    def _now(self) -> str:
        return datetime.now(timezone.utc).isoformat()

    def _build_log_file_name(
        self,
        anonymous_user_id: str,
        case_id: str,
    ) -> str:
        timestamp = datetime.now().astimezone().strftime("%Y-%m-%d_%H-%M-%S-%f")
        safe_case_id = self._safe_file_part(case_id)
        safe_user_id = self._safe_file_part(anonymous_user_id or "anonymous")
        return f"{timestamp}_{safe_case_id}_{safe_user_id}.json"

    def _safe_file_part(self, value: str) -> str:
        safe = re.sub(r"[^0-9A-Za-z\u4e00-\u9fff_-]+", "-", value.strip())
        safe = re.sub(r"-+", "-", safe).strip("-_")
        return safe[:40] or "unknown"
