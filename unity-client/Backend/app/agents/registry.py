from app.agents.asset_agent import AssetAgent
from app.agents.base import BaseAgent
from app.agents.guide_agent import GuideAgent
from app.agents.topic_agent import TopicAgent


class AgentRegistry:
    def __init__(self) -> None:
        self._agents: dict[str, BaseAgent] = {}

    def register(self, agent: BaseAgent) -> None:
        self._agents[agent.name] = agent

    def get(self, name: str) -> BaseAgent:
        if name not in self._agents:
            raise KeyError(f"Agent '{name}' is not registered.")
        return self._agents[name]

    def list_agents(self) -> list[dict[str, str]]:
        return [
            {"name": agent.name, "description": agent.description}
            for agent in self._agents.values()
        ]


registry = AgentRegistry()
registry.register(GuideAgent())
registry.register(TopicAgent())
registry.register(AssetAgent())
