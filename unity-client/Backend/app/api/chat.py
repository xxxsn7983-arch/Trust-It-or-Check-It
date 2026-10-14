from fastapi import APIRouter

from app.agents.orchestrator import orchestrator
from app.memory.store import memory_store
from app.schemas.unity import UnityChatRequest, UnityChatResponse

router = APIRouter()


@router.post("/chat", response_model=UnityChatResponse)
async def chat(request: UnityChatRequest) -> UnityChatResponse:
    memory_store.save_request(request)
    result = await orchestrator.run_for_chat(request)

    return UnityChatResponse(
        response=result.response,
        agent=result.agent,
        actions=result.actions,
        artifacts=result.artifacts,
        jobId=result.job_id,
    )
