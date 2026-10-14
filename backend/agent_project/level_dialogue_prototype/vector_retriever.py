from __future__ import annotations

import math
import os
import re
from collections import Counter
from typing import Any

from qwen_client import QwenClient


class VectorRetriever:
    """Vector-style retriever with Qwen embeddings and local TF-IDF fallback."""

    def __init__(self, qwen_client: QwenClient | None = None):
        self.qwen = qwen_client or QwenClient()
        self._embedding_cache: dict[str, list[float]] = {}

    def retrieve(self, query: str, chunks: list[dict[str, Any]], top_k: int = 3) -> list[dict[str, Any]]:
        if not chunks:
            return []

        if self._should_use_qwen_embeddings():
            try:
                return self._retrieve_with_qwen_embeddings(query, chunks, top_k)
            except Exception:
                pass

        return self._retrieve_with_local_vectors(query, chunks, top_k)

    def _should_use_qwen_embeddings(self) -> bool:
        mode = os.getenv("RAG_RETRIEVAL_MODE", "auto").lower()
        if mode == "local":
            return False
        if mode == "qwen":
            return True
        return self.qwen.available

    def _retrieve_with_qwen_embeddings(
        self,
        query: str,
        chunks: list[dict[str, Any]],
        top_k: int,
    ) -> list[dict[str, Any]]:
        query_vector = self.qwen.embed([query])[0]
        scored = []
        for chunk in chunks:
            key = chunk["chunk_id"]
            if key not in self._embedding_cache:
                self._embedding_cache[key] = self.qwen.embed([self._chunk_text(chunk)])[0]
            scored.append((self._cosine(query_vector, self._embedding_cache[key]), chunk))
        scored.sort(key=lambda item: item[0], reverse=True)
        return [chunk for score, chunk in scored[:top_k] if score > 0] or chunks[:top_k]

    def _retrieve_with_local_vectors(
        self,
        query: str,
        chunks: list[dict[str, Any]],
        top_k: int,
    ) -> list[dict[str, Any]]:
        expanded_query = self._expand_query(query)
        documents = [self._chunk_text(chunk) for chunk in chunks]
        all_terms = [self._terms(document) for document in documents]
        doc_freq = Counter(term for terms in all_terms for term in set(terms))
        query_vector = self._tfidf_vector(self._terms(expanded_query), doc_freq, len(documents))
        scored = []
        for terms, chunk in zip(all_terms, chunks):
            chunk_vector = self._tfidf_vector(terms, doc_freq, len(documents))
            scored.append((self._cosine_sparse(query_vector, chunk_vector), chunk))
        scored.sort(key=lambda item: item[0], reverse=True)
        return [chunk for score, chunk in scored[:top_k] if score > 0] or chunks[:top_k]

    def _chunk_text(self, chunk: dict[str, Any]) -> str:
        return f"{chunk.get('topic', '')} {chunk.get('chunk_type', '')} {chunk['text']}"

    def _expand_query(self, query: str) -> str:
        expansions = []
        rules = [
            (["吃", "食物", "食", "饭"], "食物 杂食 昆虫 蠕虫 果实 鸟蛋 腐肉"),
            (["住", "哪里", "生活", "家"], "生活 树林 草地 农场 城市 夜晚 树洞 藏身处"),
            (["怕", "天敌", "敌人", "危险", "追"], "天敌 猫头鹰 狐狸 郊狼 狗 猫 捕食者 危险"),
            (["宝宝", "小负鼠", "出生", "妈妈", "繁育", "育儿袋"], "小负鼠 妈妈 育儿袋 出生 长大 背上"),
            (["不动", "装死", "死", "晕", "吓", "僵"], "一动不动 身体变僵 张嘴 危险 恢复 判断"),
            (["尾巴", "爪子", "牙", "身体", "长什么"], "身体 口鼻 尾巴 爪子 牙齿 爬树"),
        ]
        for triggers, expansion in rules:
            if any(trigger in query for trigger in triggers):
                expansions.append(expansion)
        return " ".join([query] + expansions)

    def _terms(self, text: str) -> list[str]:
        compact = re.sub(r"\s+", "", text.lower())
        chinese_chars = re.findall(r"[\u4e00-\u9fff]", compact)
        chinese_unigrams = chinese_chars
        chinese_bigrams = [
            "".join(chinese_chars[index : index + 2])
            for index in range(max(len(chinese_chars) - 1, 0))
        ]
        chinese_trigrams = [
            "".join(chinese_chars[index : index + 3])
            for index in range(max(len(chinese_chars) - 2, 0))
        ]
        latin_terms = re.findall(r"[a-z0-9]+", text.lower())
        return chinese_unigrams + chinese_bigrams + chinese_trigrams + latin_terms

    def _tfidf_vector(
        self,
        terms: list[str],
        doc_freq: Counter,
        document_count: int,
    ) -> dict[str, float]:
        term_counts = Counter(terms)
        vector = {}
        for term, count in term_counts.items():
            idf = math.log((document_count + 1) / (doc_freq[term] + 1)) + 1
            vector[term] = count * idf
        return vector

    def _cosine(self, left: list[float], right: list[float]) -> float:
        if not left or not right:
            return 0.0
        dot = sum(a * b for a, b in zip(left, right))
        left_norm = math.sqrt(sum(value * value for value in left))
        right_norm = math.sqrt(sum(value * value for value in right))
        if left_norm == 0 or right_norm == 0:
            return 0.0
        return dot / (left_norm * right_norm)

    def _cosine_sparse(self, left: dict[str, float], right: dict[str, float]) -> float:
        if not left or not right:
            return 0.0
        shared = set(left) & set(right)
        dot = sum(left[term] * right[term] for term in shared)
        left_norm = math.sqrt(sum(value * value for value in left.values()))
        right_norm = math.sqrt(sum(value * value for value in right.values()))
        if left_norm == 0 or right_norm == 0:
            return 0.0
        return dot / (left_norm * right_norm)
