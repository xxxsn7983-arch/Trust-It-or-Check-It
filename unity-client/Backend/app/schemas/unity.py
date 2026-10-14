from pydantic import BaseModel, Field


class GameLogEntry(BaseModel):
    type: str = ""
    target: str = ""
    scene: str = ""
    time: float = 0
    timestampUtc: str = ""
    detail: str = ""


class DialogueMessageLog(BaseModel):
    role: str = ""
    content: str = ""
    scene: str = ""
    time: float = 0
    timestampUtc: str = ""


class UnityChatRequest(BaseModel):
    schemaVersion: str = "game-agent-context-v1"
    sessionId: str = Field(default="default-session")
    message: str = ""
    sceneName: str = ""
    inventory: list[str] = Field(default_factory=list)
    recentEvents: list[GameLogEntry] = Field(default_factory=list)
    recentMessages: list[DialogueMessageLog] = Field(default_factory=list)
    doorAttemptSuccessCount: int = 0
    doorAttemptFailedCount: int = 0


class UnityChatResponse(BaseModel):
    response: str
    agent: str
    actions: list[dict] = Field(default_factory=list)
    artifacts: list[dict] = Field(default_factory=list)
    jobId: str | None = None
