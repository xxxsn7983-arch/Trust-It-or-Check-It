import json
from typing import Any

from app.core.database import get_connection
from app.memory.store import utc_now


class JobStore:
    def create_job(self, job_id: str, session_id: str, agent_name: str, request: dict[str, Any]) -> None:
        now = utc_now()
        with get_connection() as connection:
            connection.execute(
                """
                INSERT INTO jobs (
                    job_id,
                    session_id,
                    agent_name,
                    status,
                    request_json,
                    result_json,
                    created_utc,
                    updated_utc
                )
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    job_id,
                    session_id,
                    agent_name,
                    "queued",
                    json.dumps(request, ensure_ascii=False),
                    None,
                    now,
                    now,
                ),
            )

    def get_job(self, job_id: str) -> dict[str, Any] | None:
        with get_connection() as connection:
            row = connection.execute(
                """
                SELECT job_id, session_id, agent_name, status, request_json, result_json, created_utc, updated_utc
                FROM jobs
                WHERE job_id = ?
                """,
                (job_id,),
            ).fetchone()

            artifact_rows = connection.execute(
                """
                SELECT artifact_type, path, metadata_json, created_utc
                FROM artifacts
                WHERE job_id = ?
                ORDER BY id ASC
                """,
                (job_id,),
            ).fetchall()

        if row is None:
            return None

        return {
            "jobId": row["job_id"],
            "sessionId": row["session_id"],
            "agent": row["agent_name"],
            "status": row["status"],
            "request": json.loads(row["request_json"]),
            "result": json.loads(row["result_json"]) if row["result_json"] else None,
            "createdUtc": row["created_utc"],
            "updatedUtc": row["updated_utc"],
            "artifacts": [
                {
                    "type": artifact["artifact_type"],
                    "path": artifact["path"],
                    "metadata": json.loads(artifact["metadata_json"] or "{}"),
                    "createdUtc": artifact["created_utc"],
                }
                for artifact in artifact_rows
            ],
        }


job_store = JobStore()
