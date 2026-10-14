# Game Agent Backend

FastAPI backend for Unity agent conversations and future gameplay agents.

## Run

```powershell
cd Backend
python -m venv .venv
.\.venv\Scripts\Activate.ps1
pip install -r requirements.txt
python main.py
```

Unity currently posts to:

```text
POST http://localhost:5000/chat
POST http://localhost:5000/tts
POST http://localhost:5000/stt
```

The backend stores player messages, game events, agent runs, jobs, and artifacts in SQLite.

## TTS

Unity's `TTSService` listens for agent replies, calls `/tts`, downloads the returned audio file, and plays it through an `AudioSource`.

For now `/tts` is only a provider-ready placeholder. When you choose a TTS API, add its values to `.env`, then implement the provider request in `app/services/tts.py`.

```text
TTS_PROVIDER=placeholder
TTS_API_URL=
TTS_API_KEY=your_tts_api_key_here
TTS_MODEL=
TTS_VOICE=agent_default
TTS_AUDIO_FORMAT=mp3
```

## Speech Input

Unity's `SpeechInputService` records microphone audio, posts it to `/stt`, then either sends the recognized text or places it in `PlayerInput`.

For now `/stt` is a provider-ready placeholder. When you choose a speech-to-text API, add its values to `.env`, then implement the provider request in `app/services/stt.py`.

```text
STT_PROVIDER=placeholder
STT_API_URL=
STT_API_KEY=your_stt_api_key_here
STT_MODEL=
STT_LANGUAGE=zh
```

## Add A New Agent

1. Create a file in `app/agents`, for example `my_agent.py`.
2. Implement `BaseAgent`.
3. Register it in `app/agents/registry.py`.
4. Add routing rules in `app/agents/orchestrator.py` when needed.
