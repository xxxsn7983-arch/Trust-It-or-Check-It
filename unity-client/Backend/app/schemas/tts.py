from pydantic import BaseModel


class TTSRequest(BaseModel):
    sessionId: str = ""
    text: str
    voice: str = "agent_default"


class TTSResponse(BaseModel):
    configured: bool = False
    audioUrl: str = ""
    audioFormat: str = "mp3"
    message: str = ""
