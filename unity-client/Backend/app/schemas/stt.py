from pydantic import BaseModel


class SpeechToTextRequest(BaseModel):
    sessionId: str = ""
    audioBase64: str
    audioFormat: str = "wav"
    sampleRate: int = 16000
    language: str = "zh"


class SpeechToTextResponse(BaseModel):
    configured: bool = False
    text: str = ""
    message: str = ""
