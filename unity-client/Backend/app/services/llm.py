import json
import urllib.error
import urllib.request
from typing import Any

from app.core.config import settings


class LlmServiceError(RuntimeError):
    pass


class LlmService:
    def is_configured(self) -> bool:
        return bool(settings.deepseek_api_key)

    async def complete_chat(self, messages: list[dict[str, str]], temperature: float = 0.5, max_tokens: int = 160) -> str:
        if not self.is_configured():
            raise LlmServiceError("DEEPSEEK_API_KEY is not configured.")

        endpoint = settings.deepseek_base_url.rstrip("/") + "/chat/completions"
        payload: dict[str, Any] = {
            "model": settings.deepseek_model,
            "messages": messages,
            "temperature": temperature,
            "max_tokens": max_tokens,
        }

        request = urllib.request.Request(
            endpoint,
            data=json.dumps(payload, ensure_ascii=False).encode("utf-8"),
            headers={
                "Authorization": f"Bearer {settings.deepseek_api_key}",
                "Content-Type": "application/json",
            },
            method="POST",
        )

        try:
            with urllib.request.urlopen(request, timeout=settings.deepseek_timeout_seconds) as response:
                response_body = response.read().decode("utf-8")
        except urllib.error.HTTPError as exc:
            error_body = exc.read().decode("utf-8", errors="replace")
            raise LlmServiceError(f"DeepSeek API HTTP {exc.code}: {error_body}") from exc
        except urllib.error.URLError as exc:
            raise LlmServiceError(f"DeepSeek API connection failed: {exc}") from exc

        data = json.loads(response_body)
        choices = data.get("choices") or []
        if not choices:
            raise LlmServiceError("DeepSeek API response contained no choices.")

        message = choices[0].get("message") or {}
        content = message.get("content", "").strip()
        if not content:
            raise LlmServiceError("DeepSeek API response content was empty.")

        return content


llm_service = LlmService()
