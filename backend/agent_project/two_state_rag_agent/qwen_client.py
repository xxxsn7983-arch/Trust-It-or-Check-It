from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass
class QwenConfig:
    model: str = "qwen-plus"
    base_url: str = "https://dashscope.aliyuncs.com/compatible-mode/v1"
    temperature: float = 0.4
    max_tokens: int = 260
    enable_thinking: bool = False


class QwenClient:
    """Qwen OpenAI-compatible client used by the standalone two-state agent."""

    def __init__(self, config: QwenConfig | None = None, api_key: str | None = None):
        self.config = config or QwenConfig(
            model=os.getenv("QWEN_MODEL", "qwen-plus"),
            base_url=os.getenv(
                "QWEN_BASE_URL",
                os.getenv("DASHSCOPE_BASE_URL", "https://dashscope.aliyuncs.com/compatible-mode/v1"),
            ),
            temperature=float(os.getenv("QWEN_TEMPERATURE", "0.4")),
            max_tokens=int(os.getenv("QWEN_MAX_TOKENS", "260")),
            enable_thinking=os.getenv("QWEN_ENABLE_THINKING", "false").lower() == "true",
        )
        self.api_key = api_key or os.getenv("DASHSCOPE_API_KEY") or os.getenv("QWEN_API_KEY")
        self._client = None

    @property
    def available(self) -> bool:
        return bool(self.api_key) and os.getenv("QWEN_DISABLED", "false").lower() != "true"

    def chat(self, system_prompt: str, user_prompt: str) -> str:
        if not self.available:
            raise RuntimeError("Qwen API key is not configured.")

        client = self._get_client()
        kwargs = {
            "model": self.config.model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            "temperature": self.config.temperature,
            "max_tokens": self.config.max_tokens,
        }
        try:
            response = client.chat.completions.create(
                **kwargs,
                extra_body={"enable_thinking": self.config.enable_thinking},
            )
        except TypeError:
            response = client.chat.completions.create(**kwargs)
        return response.choices[0].message.content.strip()

    def embed(self, texts: list[str]) -> list[list[float]]:
        if not self.available:
            raise RuntimeError("Qwen API key is not configured.")

        client = self._get_client()
        model = os.getenv("QWEN_EMBEDDING_MODEL", "text-embedding-v4")
        response = client.embeddings.create(model=model, input=texts)
        return [item.embedding for item in response.data]

    def _get_client(self):
        if self._client is not None:
            return self._client

        try:
            from openai import OpenAI
        except ImportError as exc:
            raise RuntimeError("openai package is required for Qwen compatible API calls.") from exc

        self._client = OpenAI(api_key=self.api_key, base_url=self.config.base_url)
        return self._client
