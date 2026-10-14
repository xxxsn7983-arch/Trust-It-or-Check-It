from __future__ import annotations

from dataclasses import dataclass
from typing import Any


@dataclass
class EvaluationResult:
    reply_quality: str
    matched_concepts: list[str]
    matched_evidence: list[str]
    misconception: str | None
    confidence: float


class DialogueEvaluator:
    """Understands child free replies; the state machine decides transitions."""

    def evaluate_direct_reply(self, child_message: str, case_package: dict[str, Any]) -> EvaluationResult:
        content = case_package["teaching_content"]
        message = child_message.strip()

        if not message:
            return EvaluationResult("insufficient", [], [], None, 0.0)

        misconception = self._match_any(message, content.get("misconception_patterns", []))
        if misconception:
            return EvaluationResult("misconception", [], [], misconception, 0.85)

        matched_concepts = [
            concept["id"]
            for concept in content["concepts"]
            if self._matches_alias(message, concept.get("aliases", []), concept["text"])
        ]
        matched_evidence = [
            evidence["id"]
            for evidence in content["evidence_cards"]
            if self._matches_alias(message, evidence.get("aliases", []), evidence["content"])
        ]

        if matched_concepts or matched_evidence:
            confidence = 0.75 + min(0.2, 0.05 * (len(matched_concepts) + len(matched_evidence)))
            return EvaluationResult("valid", matched_concepts, matched_evidence, confidence=confidence, misconception=None)

        if len(message) <= 8 or any(word in message for word in ["不知道", "感觉", "就是错", "不对"]):
            return EvaluationResult("insufficient", [], [], None, 0.65)

        return EvaluationResult("off_topic", [], [], None, 0.55)

    def validate_evidence(self, evidence_ids: list[str], case_package: dict[str, Any]) -> EvaluationResult:
        cards = case_package["teaching_content"]["evidence_cards"]
        valid_ids = {card["id"] for card in cards if card.get("refutes_wrong_claims")}
        matched = [evidence_id for evidence_id in evidence_ids if evidence_id in valid_ids]
        if matched:
            concepts = sorted(
                {
                    concept
                    for card in cards
                    if card["id"] in matched
                    for concept in card.get("supports_concepts", [])
                }
            )
            return EvaluationResult("valid", concepts, matched, None, 0.95)
        return EvaluationResult("insufficient", [], [], None, 0.7)

    def _matches_alias(self, message: str, aliases: list[str], text: str) -> bool:
        terms = aliases + [text]
        return bool(self._match_any(message, terms))

    def _match_any(self, message: str, patterns: list[str]) -> str | None:
        for pattern in patterns:
            if pattern and pattern in message:
                return pattern
        return None

