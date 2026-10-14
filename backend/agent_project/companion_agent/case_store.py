from __future__ import annotations

import json
from pathlib import Path
from typing import Any


BASE_DIR = Path(__file__).resolve().parents[1]
DEFAULT_CASE_DIR = BASE_DIR / "level_dialogue_prototype" / "data" / "cases"


class CaseStore:
    """Loads the same structured case packages used by the tool-state level agent."""

    def __init__(self, case_dir: Path = DEFAULT_CASE_DIR):
        self.case_dir = case_dir

    def load_case(self, case_id: str) -> dict[str, Any]:
        path = self.case_dir / f"{case_id}.json"
        with path.open("r", encoding="utf-8") as file:
            return json.load(file)
