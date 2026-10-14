import json
import re

from app.agents.base import BaseAgent
from app.schemas.agent import AgentRunInput, AgentRunResult
from app.services.llm import LlmServiceError, llm_service


class GuideAgent(BaseAgent):
    name = "guide"
    description = "Player-facing companion and puzzle hint agent."

    async def run(self, input_data: AgentRunInput) -> AgentRunResult:
        if llm_service.is_configured():
            try:
                response = await llm_service.complete_chat(self.build_messages(input_data))
                return AgentRunResult(
                    response=self.clean_response(response),
                    agent=self.name,
                    metadata={"provider": "deepseek", "fallback": False},
                )
            except LlmServiceError as exc:
                fallback = self.build_rule_fallback(input_data)
                return AgentRunResult(
                    response=fallback,
                    agent=self.name,
                    metadata={"provider": "deepseek", "fallback": True, "error": str(exc)},
                )

        return AgentRunResult(
            response=self.build_rule_fallback(input_data),
            agent=self.name,
            metadata={"provider": "rule_fallback", "fallback": True},
        )

    def build_messages(self, input_data: AgentRunInput) -> list[dict[str, str]]:
        request = input_data.request
        memory = input_data.memory

        system_prompt = (
            "你是游戏里的聊天助手，只在玩家主动发来消息后回答。\n"
            "回答要自然、简短、像普通朋友发聊天消息，不要神秘腔、不要吟诗、不要长篇解释。\n"
            "禁止角色扮演，禁止舞台指示，禁止括号旁白，禁止描写动作、声音、表情、环境或身体状态。\n"
            "不要写“他/她/它的声音”“金属”“嘶嘶声”“裂开”“垂下手”这类戏剧化内容。\n"
            "每次最多 1 到 2 句，总字数尽量控制在 50 个中文字符以内。\n"
            "只根据已知上下文回答：当前场景、背包线索、最近操作日志、历史对话。\n"
            "不要编造游戏里不存在的道具、地点、剧情或玩家行为。\n"
            "如果玩家只是打招呼，就简单回应并问他想聊什么；不要主动输出攻略。\n"
            "如果玩家问谜题或下一步，可以给一个小提示；只有玩家明确要求答案或多次失败时才更直接。\n"
            "必须使用简体中文。"
        )

        context = {
            "currentScene": request.sceneName,
            "inventory": request.inventory,
            "doorAttemptSuccessCount": request.doorAttemptSuccessCount,
            "doorAttemptFailedCount": request.doorAttemptFailedCount,
            "recentUnityEvents": [event.model_dump() for event in request.recentEvents[-8:]],
            "storedState": memory.state,
            "storedRecentEvents": memory.recent_events[-8:],
        }

        messages: list[dict[str, str]] = [
            {"role": "system", "content": system_prompt},
            {"role": "system", "content": "当前游戏上下文 JSON：\n" + json.dumps(context, ensure_ascii=False, indent=2)},
        ]

        for message in memory.prior_messages[-6:]:
            role = str(message.get("role", ""))
            content = str(message.get("content", "")).strip()
            if not content or content == request.message:
                continue

            if role == "player":
                messages.append({"role": "user", "content": content})

        messages.append({"role": "user", "content": request.message})
        return messages

    def clean_response(self, response: str) -> str:
        response = response.strip()
        if self.looks_like_roleplay(response):
            return "我在。你想问线索，还是想让我帮你整理下一步？"

        response = re.sub(r"（[^）]*）", "", response)
        response = re.sub(r"\([^)]*\)", "", response)
        response = response.replace("\n", " ").strip()

        if self.looks_like_roleplay(response):
            return "我在。你想问线索，还是想让我帮你整理下一步？"

        if len(response) <= 70:
            return response

        punctuation_positions = [
            response.find(mark, 45)
            for mark in ("。", "！", "？", "\n")
            if response.find(mark, 45) != -1
        ]
        if punctuation_positions:
            return response[: min(punctuation_positions) + 1].strip()

        return response[:70].strip()

    def looks_like_roleplay(self, response: str) -> bool:
        roleplay_markers = (
            "他的声音",
            "她的声音",
            "它的声音",
            "金属",
            "嘶嘶",
            "裂开",
            "垂下手",
            "指节",
            "门框",
            "焊接",
            "测试我的耐心",
            "没听见我刚才说",
            "没听见",
        )
        return any(marker in response for marker in roleplay_markers)

    def build_rule_fallback(self, input_data: AgentRunInput) -> str:
        request = input_data.request
        state = input_data.memory.state
        inventory = state.get("inventory", [])
        failed_count = state.get("doorAttemptFailedCount", 0)

        if failed_count >= 2:
            return "你已经试过几次门锁了。先别急，把最近收集到的线索图案重新排一遍。"

        if inventory:
            clues = "、".join(inventory[-3:])
            return f"我记得你现在有这些线索：{clues}。它们可能和当前谜题有关。"

        if request.sceneName:
            return f"现在在 {request.sceneName}。你可以先问我想查哪里，或者说说你卡在哪。"

        return "我在。你想问线索、下一步，还是只是聊一下？"
