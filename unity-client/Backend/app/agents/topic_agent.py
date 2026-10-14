from app.agents.base import BaseAgent
from app.schemas.agent import AgentRunInput, AgentRunResult


class TopicAgent(BaseAgent):
    name = "topic"
    description = "Generates contextual dialogue topics and ambient story lines."

    async def run(self, input_data: AgentRunInput) -> AgentRunResult:
        scene = input_data.request.sceneName or "当前场景"
        response = f"如果想聊点别的，可以从{scene}里的图案、光线或者那些反复出现的元素开始。它们通常不是随便摆在那里的。"
        return AgentRunResult(
            response=response,
            agent=self.name,
            metadata={"topicSource": "rule_stub"},
        )
