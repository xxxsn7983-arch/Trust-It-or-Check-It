from typing import Any


class ImageGenerationService:
    async def generate(self, prompt: str, metadata: dict[str, Any] | None = None) -> dict[str, Any]:
        raise NotImplementedError("Connect your image generation provider here.")


image_generation_service = ImageGenerationService()
