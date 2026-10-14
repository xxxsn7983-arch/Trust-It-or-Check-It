from fastapi import APIRouter, HTTPException

from app.agents.registry import registry
from app.schemas.agent import AgentRunInput, AgentRunResult

router = APIRouter(prefix="/agents")


@router.get("")
async def list_agents() -> list[dict[str, str]]:
    return registry.list_agents()


@router.post("/{agent_name}/run", response_model=AgentRunResult)
async def run_agent(agent_name: str, input_data: AgentRunInput) -> AgentRunResult:
    try:
        agent = registry.get(agent_name)
    except KeyError as exc:
        raise HTTPException(status_code=404, detail=str(exc)) from exc

    return await agent.run(input_data)
