from abc import ABC, abstractmethod

from app.schemas.agent import AgentRunInput, AgentRunResult


class BaseAgent(ABC):
    name: str
    description: str

    @abstractmethod
    async def run(self, input_data: AgentRunInput) -> AgentRunResult:
        raise NotImplementedError
