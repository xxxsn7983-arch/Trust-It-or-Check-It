from uuid import uuid4

from app.agents.base import BaseAgent
from app.memory.store import utc_now
from app.schemas.agent import AgentRunInput, AgentRunResult
from app.services.job_queue import job_store


class AssetAgent(BaseAgent):
    name = "asset"
    description = "Creates or schedules visual asset generation tasks."

    async def run(self, input_data: AgentRunInput) -> AgentRunResult:
        job_id = f"job_{uuid4().hex}"
        request = {
            "message": input_data.request.message,
            "sceneName": input_data.request.sceneName,
            "createdUtc": utc_now(),
        }

        job_store.create_job(
            job_id=job_id,
            session_id=input_data.request.sessionId,
            agent_name=self.name,
            request=request,
        )

        return AgentRunResult(
            response="我已经记录了这个美术资产生成任务。当前版本先返回 jobId，后面可以接图片生成服务。",
            agent=self.name,
            job_id=job_id,
            metadata={"queued": True},
        )
