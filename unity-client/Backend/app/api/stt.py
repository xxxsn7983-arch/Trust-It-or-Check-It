from fastapi import APIRouter

from app.schemas.stt import SpeechToTextRequest, SpeechToTextResponse
from app.services.stt import speech_to_text_service

router = APIRouter()


@router.post("/stt", response_model=SpeechToTextResponse)
async def transcribe_stt(request: SpeechToTextRequest) -> SpeechToTextResponse:
    return await speech_to_text_service.transcribe(request)
