import json
from datetime import datetime, timezone
from typing import Any

from app.core.database import get_connection
from app.schemas.agent import MemoryContext
from app.schemas.unity import UnityChatRequest


def utc_now() -> str:
    return datetime.now(timezone.utc).isoformat()


class MemoryStore:
    def save_request(self, request: UnityChatRequest) -> None:
        now = utc_now()
        inventory_json = json.dumps(request.inventory, ensure_ascii=False)

        with get_connection() as connection:
            connection.execute(
                """
                INSERT INTO sessions (
                    session_id,
                    first_seen_utc,
                    last_seen_utc,
                    scene_name,
                    inventory_json,
                    door_success_count,
                    door_failed_count
                )
                VALUES (?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(session_id) DO UPDATE SET
                    last_seen_utc = excluded.last_seen_utc,
                    scene_name = excluded.scene_name,
                    inventory_json = excluded.inventory_json,
                    door_success_count = excluded.door_success_count,
                    door_failed_count = excluded.door_failed_count
                """,
                (
                    request.sessionId,
                    now,
                    now,
                    request.sceneName,
                    inventory_json,
                    request.doorAttemptSuccessCount,
                    request.doorAttemptFailedCount,
                ),
            )

            if request.message:
                connection.execute(
                    """
                    INSERT INTO messages (session_id, role, content, scene_name, created_utc)
                    VALUES (?, ?, ?, ?, ?)
                    """,
                    (request.sessionId, "player", request.message, request.sceneName, now),
                )

            for event in request.recentEvents:
                connection.execute(
                    """
                    INSERT INTO game_events (
                        session_id,
                        event_type,
                        target,
                        scene_name,
                        game_time,
                        detail,
                        timestamp_utc,
                        received_utc,
                        raw_json
                    )
                    SELECT ?, ?, ?, ?, ?, ?, ?, ?, ?
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM game_events
                        WHERE session_id = ?
                          AND event_type = ?
                          AND target = ?
                          AND scene_name = ?
                          AND game_time = ?
                          AND detail = ?
                          AND timestamp_utc = ?
                    )
                    """,
                    (
                        request.sessionId,
                        event.type,
                        event.target,
                        event.scene or request.sceneName,
                        event.time,
                        event.detail,
                        event.timestampUtc,
                        now,
                        event.model_dump_json(),
                        request.sessionId,
                        event.type,
                        event.target,
                        event.scene or request.sceneName,
                        event.time,
                        event.detail,
                        event.timestampUtc,
                    ),
                )

    def save_agent_message(self, session_id: str, agent_name: str, response: str, scene_name: str) -> None:
        now = utc_now()
        with get_connection() as connection:
            connection.execute(
                """
                INSERT INTO messages (session_id, role, content, scene_name, created_utc)
                VALUES (?, ?, ?, ?, ?)
                """,
                (session_id, f"agent:{agent_name}", response, scene_name, now),
            )

    def save_agent_run(self, session_id: str, agent_name: str, input_data: dict[str, Any], output_data: dict[str, Any]) -> None:
        now = utc_now()
        with get_connection() as connection:
            connection.execute(
                """
                INSERT INTO agent_runs (session_id, agent_name, input_json, output_json, created_utc)
                VALUES (?, ?, ?, ?, ?)
                """,
                (
                    session_id,
                    agent_name,
                    json.dumps(input_data, ensure_ascii=False),
                    json.dumps(output_data, ensure_ascii=False),
                    now,
                ),
            )

    def build_context(self, request: UnityChatRequest, limit: int = 20) -> MemoryContext:
        with get_connection() as connection:
            messages = connection.execute(
                """
                SELECT role, content, scene_name, created_utc
                FROM messages
                WHERE session_id = ?
                ORDER BY id DESC
                LIMIT ?
                """,
                (request.sessionId, limit),
            ).fetchall()

            events = connection.execute(
                """
                SELECT event_type, target, scene_name, game_time, detail, timestamp_utc, received_utc
                FROM game_events
                WHERE session_id = ?
                ORDER BY id DESC
                LIMIT ?
                """,
                (request.sessionId, limit),
            ).fetchall()

            state_row = connection.execute(
                """
                SELECT scene_name, inventory_json, door_success_count, door_failed_count
                FROM sessions
                WHERE session_id = ?
                """,
                (request.sessionId,),
            ).fetchone()

        state = {
            "sceneName": request.sceneName,
            "inventory": request.inventory,
            "doorAttemptSuccessCount": request.doorAttemptSuccessCount,
            "doorAttemptFailedCount": request.doorAttemptFailedCount,
        }

        if state_row:
            state.update(
                {
                    "sceneName": state_row["scene_name"] or request.sceneName,
                    "inventory": json.loads(state_row["inventory_json"] or "[]"),
                    "doorAttemptSuccessCount": state_row["door_success_count"],
                    "doorAttemptFailedCount": state_row["door_failed_count"],
                }
            )

        return MemoryContext(
            prior_messages=[dict(row) for row in reversed(messages)],
            recent_events=[dict(row) for row in reversed(events)],
            state=state,
        )


memory_store = MemoryStore()
