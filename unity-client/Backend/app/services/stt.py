import base64
import binascii

from app.core.config import settings
from app.schemas.stt import SpeechToTextRequest, SpeechToTextResponse


class SpeechToTextService:
    def configuration_issue(self) -> str:
        provider = settings.stt_provider.strip().lower()
        if not provider or provider == "placeholder":
            return "STT backend is not configured yet. Keep STT_PROVIDER=placeholder until you choose a speech-to-text API."

        return f"STT provider '{settings.stt_provider}' is not implemented yet. Send me the API docs or request format, then I can add its adapter here."

    async def transcribe(self, request: SpeechToTextRequest) -> SpeechToTextResponse:
        configuration_issue = self.configuration_issue()
        if configuration_issue:
            return SpeechToTextResponse(
                configured=False,
                message=configuration_issue,
            )

        try:
            audio_bytes = base64.b64decode(request.audioBase64, validate=True)
        except (binascii.Error, ValueError):
            return SpeechToTextResponse(
                configured=False,
                message="STT request audioBase64 was invalid.",
            )

        try:
            text = await self.generate_text(request, audio_bytes)
        except NotImplementedError as exc:
            return SpeechToTextResponse(
                configured=False,
                message=str(exc),
            )

        return SpeechToTextResponse(
            configured=True,
            text=text.strip(),
            message="ok",
        )

    async def generate_text(self, request: SpeechToTextRequest, audio_bytes: bytes) -> str:
        raise NotImplementedError(f"STT provider '{settings.stt_provider}' has no adapter yet.")


speech_to_text_service = SpeechToTextService()
