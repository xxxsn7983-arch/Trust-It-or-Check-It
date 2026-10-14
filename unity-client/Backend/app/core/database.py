import sqlite3
from pathlib import Path
from typing import Iterator

from app.core.config import settings


def get_connection() -> sqlite3.Connection:
    db_path = settings.database_file
    db_path.parent.mkdir(parents=True, exist_ok=True)

    connection = sqlite3.connect(db_path)
    connection.row_factory = sqlite3.Row
    connection.execute("PRAGMA foreign_keys = ON")
    return connection


def connection_scope() -> Iterator[sqlite3.Connection]:
    connection = get_connection()
    try:
        yield connection
        connection.commit()
    except Exception:
        connection.rollback()
        raise
    finally:
        connection.close()


def init_database() -> None:
    db_path: Path = settings.database_file
    db_path.parent.mkdir(parents=True, exist_ok=True)

    with get_connection() as connection:
        connection.executescript(
            """
            CREATE TABLE IF NOT EXISTS sessions (
                session_id TEXT PRIMARY KEY,
                first_seen_utc TEXT NOT NULL,
                last_seen_utc TEXT NOT NULL,
                scene_name TEXT,
                inventory_json TEXT NOT NULL DEFAULT '[]',
                door_success_count INTEGER NOT NULL DEFAULT 0,
                door_failed_count INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                role TEXT NOT NULL,
                content TEXT NOT NULL,
                scene_name TEXT,
                created_utc TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(session_id)
            );

            CREATE TABLE IF NOT EXISTS game_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                event_type TEXT NOT NULL,
                target TEXT,
                scene_name TEXT,
                game_time REAL,
                detail TEXT,
                timestamp_utc TEXT,
                received_utc TEXT NOT NULL,
                raw_json TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(session_id)
            );

            CREATE TABLE IF NOT EXISTS agent_runs (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id TEXT NOT NULL,
                agent_name TEXT NOT NULL,
                input_json TEXT NOT NULL,
                output_json TEXT NOT NULL,
                created_utc TEXT NOT NULL,
                FOREIGN KEY (session_id) REFERENCES sessions(session_id)
            );

            CREATE TABLE IF NOT EXISTS jobs (
                job_id TEXT PRIMARY KEY,
                session_id TEXT,
                agent_name TEXT NOT NULL,
                status TEXT NOT NULL,
                request_json TEXT NOT NULL,
                result_json TEXT,
                created_utc TEXT NOT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS artifacts (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                job_id TEXT,
                session_id TEXT,
                artifact_type TEXT NOT NULL,
                path TEXT,
                metadata_json TEXT NOT NULL DEFAULT '{}',
                created_utc TEXT NOT NULL,
                FOREIGN KEY (job_id) REFERENCES jobs(job_id)
            );
            """
        )
