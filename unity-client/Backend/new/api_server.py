from __future__ import annotations

import sys
from pathlib import Path
from typing import Literal

CURRENT_DIR = Path(__file__).resolve().parent
if str(CURRENT_DIR) not in sys.path:
    sys.path.insert(0, str(CURRENT_DIR))

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from dialogue_service import DialogueService


app = FastAPI(title="Level Dialogue Prototype API")
service = DialogueService()


class StartLevelRequest(BaseModel):
    case_id: str = Field(default="opossum_play_dead_001")
    child_initial_belief: str = Field(default="unknown")


class MessageRequest(BaseModel):
    session_id: str
    reply_format: Literal["chat", "choice"]
    content: str


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/level/start")
def start_level(request: StartLevelRequest) -> dict:
    try:
        return service.start_level(
            case_id=request.case_id,
            child_initial_belief=request.child_initial_belief,
        )
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc


@app.post("/api/level/message")
def send_message(request: MessageRequest) -> dict:
    try:
        return service.send_message(
            session_id=request.session_id,
            reply_format=request.reply_format,
            content=request.content,
        )
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc


@app.get("/api/level/session/{session_id}")
def get_session(session_id: str) -> dict:
    try:
        return service.get_session(session_id)
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
