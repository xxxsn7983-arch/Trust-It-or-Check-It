from typing import Any

from pydantic import BaseModel, Field


class JobResponse(BaseModel):
    jobId: str
    status: str
    agent: str
    result: dict[str, Any] | None = None
    artifacts: list[dict[str, Any]] = Field(default_factory=list)
