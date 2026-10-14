from fastapi import FastAPI
from fastapi.staticfiles import StaticFiles
from pathlib import Path

from app.api import agents, chat, jobs, stt, tts
from app.core.config import settings
from app.core.database import init_database


def create_app() -> FastAPI:
    app = FastAPI(title=settings.app_name)

    @app.on_event("startup")
    async def startup() -> None:
        init_database()

    @app.get("/health")
    async def health() -> dict[str, str]:
        return {"status": "ok"}

    app.include_router(chat.router)
    app.include_router(agents.router)
    app.include_router(jobs.router)
    app.include_router(tts.router)
    app.include_router(stt.router)

    audio_dir = Path(settings.tts_audio_dir)
    if not audio_dir.is_absolute():
        audio_dir = Path.cwd() / audio_dir
    audio_dir.mkdir(parents=True, exist_ok=True)
    app.mount("/audio", StaticFiles(directory=audio_dir), name="audio")
    return app
