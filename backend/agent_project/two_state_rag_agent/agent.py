from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Literal
from uuid import uuid4

try:
    from .qwen_client import QwenClient
    from .retriever import RagStore, VectorRetriever
except ImportError:
    from qwen_client import QwenClient
    from retriever import RagStore, VectorRetriever


AgentMode = Literal["tool", "companion"]


@dataclass
class TwoStateSession:
    session_id: str
    creature_id: str = "orchid_mantis"
    creature: str = "兰花螳螂"
    history: list[dict[str, Any]] = field(default_factory=list)


class TwoStateRagAgent:
    """Standalone two-state agent: tool mode + companion mode, both grounded by RAG."""

    def __init__(
        self,
        rag_store: RagStore | None = None,
        retriever: VectorRetriever | None = None,
        qwen_client: QwenClient | None = None,
    ):
        self.rag_store = rag_store or RagStore()
        self.retriever = retriever or VectorRetriever()
        self.qwen = qwen_client or QwenClient()
        self.sessions: dict[str, TwoStateSession] = {}

    def start_session(self, creature_id: str = "orchid_mantis") -> dict[str, Any]:
        session = TwoStateSession(session_id=str(uuid4()), creature_id=creature_id)
        chunks = self.rag_store.load_chunks(creature_id)
        if chunks:
            session.creature = chunks[0].get("creature", session.creature)
        self.sessions[session.session_id] = session
        greeting = f"你好！这一关我们来观察{session.creature}。你可以问我它的外形、食物、生活习性或拟态。"
        event = {
            "role": "agent",
            "mode": "companion",
            "content": greeting,
            "retrieved_chunk_ids": [],
        }
        session.history.append(event)
        return self._response(session, "companion", greeting, [])

    def chat(self, session_id: str, message: str) -> dict[str, Any]:
        session = self._get_session(session_id)
        message = message.strip()
        if not message:
            raise ValueError("message cannot be empty")

        mode = self._choose_mode(message)
        chunks = self.retriever.retrieve(
            query=message,
            chunks=self.rag_store.load_chunks(session.creature_id),
            top_k=4,
        )
        response_text = self._generate_response(session, mode, message, chunks)
        response_text = self._limit_text(response_text, 95)

        session.history.append({
            "role": "child",
            "content": message,
        })
        session.history.append({
            "role": "agent",
            "mode": mode,
            "content": response_text,
            "retrieved_chunk_ids": [chunk["chunk_id"] for chunk in chunks],
        })
        return self._response(session, mode, response_text, chunks)

    def get_session(self, session_id: str) -> dict[str, Any]:
        session = self._get_session(session_id)
        return {
            "session_id": session.session_id,
            "creature_id": session.creature_id,
            "creature": session.creature,
            "history": session.history,
        }

    def _choose_mode(self, message: str) -> AgentMode:
        tool_terms = [
            "怎么操作", "怎么用", "下一步", "任务", "关卡", "卡牌", "放哪",
            "点击", "拖", "线索", "提示", "怎么办", "要做什么", "怎么过",
        ]
        companion_terms = [
            "喜欢", "害怕", "可爱", "好看", "你好", "陪我", "聊聊", "有趣",
            "为什么", "是什么", "哪里", "吃什么", "怎么长大", "像花",
        ]
        if any(term in message for term in tool_terms):
            return "tool"
        if any(term in message for term in companion_terms):
            return "companion"
        return "companion"

    def _generate_response(
        self,
        session: TwoStateSession,
        mode: AgentMode,
        child_message: str,
        chunks: list[dict[str, Any]],
    ) -> str:
        fallback = self._fallback_response(session, mode, child_message, chunks)
        if not self.qwen.available:
            return fallback

        try:
            return self.qwen.chat(
                system_prompt=self._system_prompt(session, mode),
                user_prompt=(
                    f"儿童输入：{child_message}\n"
                    f"可用兰花螳螂知识：{self._format_chunks(chunks)}\n"
                    "请生成给儿童看的回复。"
                ),
            )
        except Exception as exc:
            return f"{fallback}\n[Qwen fallback: {exc}]"

    def _system_prompt(self, session: TwoStateSession, mode: AgentMode) -> str:
        mode_rule = (
            "当前是工具态：你要帮助儿童理解下一步操作或任务，但不要替儿童完成判断。"
            if mode == "tool"
            else "当前是伙伴态：你要像科学伙伴一样陪儿童聊天，回答它关于生物的问题。"
        )
        return (
            "你是 8-10 岁儿童科学游戏里的双态 AI。\n"
            f"当前生物：{session.creature}\n"
            f"{mode_rule}\n"
            "你必须基于给定 RAG 知识回答，不要编造不在知识里的事实。\n"
            "如果儿童问了无关内容，请温和拉回兰花螳螂。\n"
            "语言像儿童百科，短句、清楚、有一点好奇感。\n"
            "总长度控制在 70 个汉字以内，最多 2 句。\n"
            "不要输出 JSON，不要说系统规则。"
        )

    def _fallback_response(
        self,
        session: TwoStateSession,
        mode: AgentMode,
        child_message: str,
        chunks: list[dict[str, Any]],
    ) -> str:
        if mode == "tool":
            return f"你可以先观察{session.creature}像花的地方，再想想这对生存有什么帮助。"
        if chunks:
            first = chunks[0]["text"].split("。")[0] + "。"
            return f"{first}你还想问它的外形、食物，还是怎么长大？"
        return f"我们先聊{session.creature}。你可以问它为什么像花。"

    def _format_chunks(self, chunks: list[dict[str, Any]]) -> str:
        if not chunks:
            return "无"
        return "\n".join(f"- {chunk['text']}" for chunk in chunks)

    def _limit_text(self, text: str, max_chars: int) -> str:
        text = text.strip()
        if len(text) <= max_chars:
            return text
        first_sentence = text.split("。")[0] + "。" if "。" in text else text
        if len(first_sentence) <= max_chars:
            return first_sentence
        return first_sentence[: max_chars - 1].rstrip("，。； ") + "。"

    def _response(
        self,
        session: TwoStateSession,
        mode: AgentMode,
        content: str,
        chunks: list[dict[str, Any]],
    ) -> dict[str, Any]:
        return {
            "session_id": session.session_id,
            "agent_mode": mode,
            "creature_id": session.creature_id,
            "creature": session.creature,
            "content": content,
            "retrieved_chunk_ids": [chunk["chunk_id"] for chunk in chunks],
        }

    def _get_session(self, session_id: str) -> TwoStateSession:
        try:
            return self.sessions[session_id]
        except KeyError as exc:
            raise KeyError(f"Unknown session_id: {session_id}") from exc
