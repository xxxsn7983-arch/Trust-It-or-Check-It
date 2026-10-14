import hashlib
from pathlib import Path

from app.core.config import settings
from app.schemas.tts import TTSRequest, TTSResponse


class TTSService:
    def configuration_issue(self) -> str:
        provider = settings.tts_provider.strip().lower()
        if not provider or provider == "placeholder":
            return "TTS backend is not configured yet. Keep TTS_PROVIDER=placeholder until you choose a TTS API."

        return f"TTS provider '{settings.tts_provider}' is not implemented yet. Send me the API docs or request format, then I can add its adapter here."

    def audio_directory(self) -> Path:
        path = Path(settings.tts_audio_dir)
        if not path.is_absolute():
            path = Path.cwd() / path
        path.mkdir(parents=True, exist_ok=True)
        return path

    async def synthesize(self, request: TTSRequest) -> TTSResponse:
        configuration_issue = self.configuration_issue()
        if configuration_issue:
            return TTSResponse(
                configured=False,
                audioFormat=self.audio_format(),
                message=configuration_issue,
            )

        audio_path = self.cached_audio_path(request)
        if not audio_path.exists():
            try:
                await self.generate_audio(request, audio_path)
            except NotImplementedError as exc:
                return TTSResponse(
                    configured=False,
                    audioFormat=self.audio_format(),
                    message=str(exc),
                )

        return TTSResponse(
            configured=True,
            audioUrl=f"{settings.public_base_url.rstrip('/')}/audio/{audio_path.name}",
            audioFormat=self.audio_format(),
            message="ok",
        )

    def cached_audio_path(self, request: TTSRequest) -> Path:
        cache_key = f"{settings.tts_provider}|{settings.tts_model}|{request.voice}|{self.audio_format()}|{request.text}".encode("utf-8")
        digest = hashlib.sha256(cache_key).hexdigest()[:24]
        return self.audio_directory() / f"{digest}.{self.audio_format()}"

    def audio_format(self) -> str:
        audio_format = settings.tts_audio_format.strip().lower().lstrip(".")
        if audio_format in {"mpeg", "mp3"}:
            return "mp3"
        if audio_format in {"wav", "wave"}:
            return "wav"
        if audio_format in {"ogg", "oggvorbis"}:
            return "ogg"
        return "mp3"

    async def generate_audio(self, request: TTSRequest, audio_path: Path) -> None:
        raise NotImplementedError(f"TTS provider '{settings.tts_provider}' has no adapter yet.")


tts_service = TTSService()
