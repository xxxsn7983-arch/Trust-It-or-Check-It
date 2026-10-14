from typing import Any

from pydantic import BaseModel, Field

from app.schemas.unity import UnityChatRequest


class MemoryContext(BaseModel):
    prior_messages: list[dict[str, Any]] = Field(default_factory=list)
    recent_events: list[dict[str, Any]] = Field(default_factory=list)
    state: dict[str, Any] = Field(default_factory=dict)


class AgentRunInput(BaseModel):
    request: UnityChatRequest
    memory: MemoryContext


class AgentRunResult(BaseModel):
    response: str
    agent: str
    actions: list[dict[str, Any]] = Field(default_factory=list)
    artifacts: list[dict[str, Any]] = Field(default_factory=list)
    job_id: str | None = None
    metadata: dict[str, Any] = Field(default_factory=dict)
