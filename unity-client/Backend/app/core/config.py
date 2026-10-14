import os
from functools import lru_cache
from pathlib import Path


def load_dotenv(path: Path) -> None:
    if not path.exists():
        return

    for raw_line in path.read_text(encoding="utf-8").splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or "=" not in line:
            continue

        key, value = line.split("=", 1)
        os.environ.setdefault(key.strip(), value.strip().strip("\"'"))


class Settings:
    def __init__(self) -> None:
        load_dotenv(Path.cwd() / ".env")

        self.app_name = os.getenv("APP_NAME", "Game Agent Backend")
        self.host = os.getenv("HOST", "127.0.0.1")
        self.port = int(os.getenv("PORT", "5000"))
        self.database_path = os.getenv("DATABASE_PATH", "data/game_agent.db")
        self.deepseek_api_key = os.getenv("DEEPSEEK_API_KEY", "")
        self.deepseek_base_url = os.getenv("DEEPSEEK_BASE_URL", "https://api.deepseek.com")
        self.deepseek_model = os.getenv("DEEPSEEK_MODEL", "deepseek-v4-flash")
        self.deepseek_timeout_seconds = int(os.getenv("DEEPSEEK_TIMEOUT_SECONDS", "30"))
        self.public_base_url = os.getenv("PUBLIC_BASE_URL", f"http://{self.host}:{self.port}")
        self.tts_provider = os.getenv("TTS_PROVIDER", "placeholder")
        self.tts_api_url = os.getenv("TTS_API_URL", "")
        self.tts_api_key = os.getenv("TTS_API_KEY", "")
        self.tts_model = os.getenv("TTS_MODEL", "")
        self.tts_voice = os.getenv("TTS_VOICE", "agent_default")
        self.tts_audio_format = os.getenv("TTS_AUDIO_FORMAT", "mp3")
        self.tts_audio_dir = os.getenv("TTS_AUDIO_DIR", "data/audio")
        self.tts_timeout_seconds = int(os.getenv("TTS_TIMEOUT_SECONDS", "60"))
        self.stt_provider = os.getenv("STT_PROVIDER", "placeholder")
        self.stt_api_url = os.getenv("STT_API_URL", "")
        self.stt_api_key = os.getenv("STT_API_KEY", "")
        self.stt_model = os.getenv("STT_MODEL", "")
        self.stt_language = os.getenv("STT_LANGUAGE", "zh")
        self.stt_timeout_seconds = int(os.getenv("STT_TIMEOUT_SECONDS", "60"))

    @property
    def database_file(self) -> Path:
        path = Path(self.database_path)
        if not path.is_absolute():
            path = Path.cwd() / path
        return path


@lru_cache(maxsize=1)
def get_settings() -> Settings:
    return Settings()


settings = get_settings()
