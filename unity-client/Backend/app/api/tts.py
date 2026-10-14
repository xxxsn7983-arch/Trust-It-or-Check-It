from fastapi import APIRouter

from app.schemas.tts import TTSRequest, TTSResponse
from app.services.tts import tts_service

router = APIRouter()


@router.post("/tts", response_model=TTSResponse)
async def synthesize_tts(request: TTSRequest) -> TTSResponse:
    return await tts_service.synthesize(request)
