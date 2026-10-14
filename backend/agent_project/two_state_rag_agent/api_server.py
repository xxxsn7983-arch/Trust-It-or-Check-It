from __future__ import annotations

import sys
from pathlib import Path

CURRENT_DIR = Path(__file__).resolve().parent
if str(CURRENT_DIR) not in sys.path:
    sys.path.insert(0, str(CURRENT_DIR))

from fastapi import FastAPI, HTTPException
from pydantic import BaseModel, Field

from agent import TwoStateRagAgent


app = FastAPI(title="Two-State RAG Agent API")
agent = TwoStateRagAgent()


class StartRequest(BaseModel):
    creature_id: str = Field(default="orchid_mantis")


class ChatRequest(BaseModel):
    session_id: str
    content: str


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/api/two-state/start")
def start(request: StartRequest) -> dict:
    try:
        return agent.start_session(creature_id=request.creature_id)
    except FileNotFoundError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc


@app.post("/api/two-state/chat")
def chat(request: ChatRequest) -> dict:
    try:
        return agent.chat(session_id=request.session_id, message=request.content)
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except Exception as exc:
        raise HTTPException(status_code=500, detail=str(exc)) from exc


@app.get("/api/two-state/session/{session_id}")
def get_session(session_id: str) -> dict:
    try:
        return agent.get_session(session_id)
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc
