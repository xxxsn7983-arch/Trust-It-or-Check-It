from __future__ import annotations

import json
from pathlib import Path
from typing import Any

from vector_retriever import VectorRetriever


BASE_DIR = Path(__file__).resolve().parent


class KnowledgeStore:
    """Loads structured case packages and simple background RAG chunks."""

    def __init__(self, base_dir: Path = BASE_DIR, retriever: VectorRetriever | None = None):
        self.base_dir = base_dir
        self.case_dir = base_dir / "data" / "cases"
        self.rag_dir = base_dir / "data" / "rag"
        self.retriever = retriever or VectorRetriever()

    def load_case(self, case_id: str) -> dict[str, Any]:
        path = self.case_dir / f"{case_id}.json"
        with path.open("r", encoding="utf-8") as file:
            return json.load(file)

    def load_rag_chunks(self, case_id: str) -> list[dict[str, Any]]:
        chunks: list[dict[str, Any]] = []
        for path in self.rag_dir.glob("*.json"):
            with path.open("r", encoding="utf-8") as file:
                data = json.load(file)
            chunks.extend(chunk for chunk in data if chunk["case_id"] == case_id)
        return chunks

    def retrieve_background(self, case_id: str, query: str, top_k: int = 2) -> list[dict[str, Any]]:
        chunks = self.load_rag_chunks(case_id)
        return self.retriever.retrieve(query, chunks, top_k=top_k)
