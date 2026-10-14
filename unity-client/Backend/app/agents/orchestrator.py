from app.agents.registry import registry
from app.memory.store import memory_store
from app.schemas.agent import AgentRunInput, AgentRunResult
from app.schemas.unity import UnityChatRequest


class AgentOrchestrator:
    async def run_for_chat(self, request: UnityChatRequest) -> AgentRunResult:
        memory = memory_store.build_context(request)
        agent_name = self.choose_agent(request)
        agent = registry.get(agent_name)

        input_data = AgentRunInput(request=request, memory=memory)
        result = await agent.run(input_data)

        memory_store.save_agent_run(
            session_id=request.sessionId,
            agent_name=result.agent,
            input_data=input_data.model_dump(),
            output_data=result.model_dump(),
        )
        memory_store.save_agent_message(
            session_id=request.sessionId,
            agent_name=result.agent,
            response=result.response,
            scene_name=request.sceneName,
        )

        return result

    def choose_agent(self, request: UnityChatRequest) -> str:
        message = request.message.lower()

        asset_keywords = ("asset", "image", "art", "sprite", "生成素材", "生成图片", "美术", "贴图", "立绘")
        topic_keywords = ("话题", "聊点", "随机对话", "闲聊", "topic")

        if any(keyword in message for keyword in asset_keywords):
            return "asset"
        if any(keyword in message for keyword in topic_keywords):
            return "topic"
        return "guide"


orchestrator = AgentOrchestrator()
