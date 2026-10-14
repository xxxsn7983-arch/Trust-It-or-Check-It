from __future__ import annotations

import argparse
import json

from state_machine import (
    AI_CONTROLLED_MISTAKE,
    AI_REPAIR,
    CHILD_DIRECT_REPLY,
    FINAL_CONCLUSION,
    REFLECTION_PROMPT,
    SOCRATIC_LEAD_IN,
    LevelStateMachine,
)


def print_step(title: str, result: dict) -> None:
    print(f"\n=== {title} ===")
    print(json.dumps(result, ensure_ascii=False, indent=2))


def print_agent_result(result: dict) -> None:
    print(f"\n[STATE] {result['state']}  [VIEW] {result['view']}")
    print(f"AI: {result['agent_message']}")
    if result.get("followup"):
        print(f"AI: {result['followup']}")
    if result.get("retrieved_chunk_ids"):
        print(f"[retrieved] {', '.join(result['retrieved_chunk_ids'])}")
    if result.get("evaluation"):
        print("[evaluation]")
        print(json.dumps(result["evaluation"], ensure_ascii=False, indent=2))


def run_interactive() -> None:
    machine = LevelStateMachine("opossum_play_dead_001")

    print("\n=== Level Dialogue Interactive Demo ===")
    print("这会自动跳过 KNOWLEDGE_PREP，从 FREE_CHAT 开始测试。")
    print("输入 /quit 退出；在聊天阶段输入 /reveal 可强制引出可控错误。\n")

    result = machine.handle_event(
        "CHILD_INITIAL_JUDGEMENT_SUBMITTED",
        {"choice": "interactive_test"}
    )
    print_agent_result(result)

    while True:
        state = machine.memory.current_state

        if state in {AI_CONTROLLED_MISTAKE, SOCRATIC_LEAD_IN}:
            child_message = input("\n孩子输入> ").strip()
            if _should_quit(child_message):
                break
            force_reveal = child_message == "/reveal"
            result = machine.handle_event(
                "CHILD_CHAT",
                {
                    "message": "我想听听 AI 的判断" if force_reveal else child_message,
                    "force_reveal": force_reveal,
                },
            )
            print_agent_result(result)
            continue

        if state == REFLECTION_PROMPT:
            choice = input("\n选择 [1相信AI / 2怀疑一下 / 3寻找线索]> ").strip()
            if _should_quit(choice):
                break
            result = machine.handle_event(
                "REFLECTION_CHOICE_SELECTED",
                {"choice": _parse_reflection_choice(choice)},
            )
            print_agent_result(result)
            continue

        if state == CHILD_DIRECT_REPLY:
            child_message = input("\n孩子说明理由> ").strip()
            if _should_quit(child_message):
                break
            result = machine.handle_event(
                "CHILD_DIRECT_REPLY_SUBMITTED",
                {"message": child_message},
            )
            print_agent_result(result)
            continue

        if state == AI_REPAIR:
            command = input("\n按 Enter 进入最终结论，或输入 /quit 退出> ").strip()
            if _should_quit(command):
                break
            result = machine.handle_event("AI_REPAIR_DONE")
            print_agent_result(result)
            continue

        if state == FINAL_CONCLUSION:
            print("\n关卡结束。")
            break

        print(f"\n当前状态 {state} 暂无交互处理。")
        break


def _parse_reflection_choice(choice: str) -> str:
    mapping = {
        "1": "agree",
        "相信": "agree",
        "相信AI": "agree",
        "agree": "agree",
        "2": "reject",
        "怀疑": "reject",
        "怀疑一下": "reject",
        "reject": "reject",
        "3": "revise",
        "寻找": "revise",
        "寻找线索": "revise",
        "revise": "revise",
    }
    return mapping.get(choice, choice)


def _should_quit(text: str) -> bool:
    return text.lower() in {"/quit", "quit", "exit"}


def run_reject_path() -> None:
    machine = LevelStateMachine("opossum_play_dead_001")
    print_step("完成知识准备", machine.handle_event("CHILD_INITIAL_JUDGEMENT_SUBMITTED", {"choice": "not_sure"}))
    print_step("背景聊天 1", machine.handle_event("CHILD_CHAT", {"message": "负鼠是什么动物？"}))
    print_step("儿童问到关键问题并引出错误", machine.handle_event("CHILD_CHAT", {"message": "它为什么一动不动？"}))
    print_step("儿童选择怀疑一下", machine.handle_event("REFLECTION_CHOICE_SELECTED", {"choice": "reject"}))
    print_step(
        "儿童直接给出有效理由",
        machine.handle_event("CHILD_DIRECT_REPLY_SUBMITTED", {"message": "它可能不是晕倒，是在保护自己，危险过去后会恢复。"})
    )
    print_step("进入最终结论", machine.handle_event("AI_REPAIR_DONE"))


def run_agree_path() -> None:
    machine = LevelStateMachine("opossum_play_dead_001")
    print_step("完成知识准备", machine.handle_event("CHILD_INITIAL_JUDGEMENT_SUBMITTED", {"choice": "real_fainted"}))
    print_step("直接引出错误", machine.handle_event("CHILD_CHAT", {"message": "我觉得它真的晕了", "force_reveal": True}))
    print_step("儿童选择相信 AI", machine.handle_event("REFLECTION_CHOICE_SELECTED", {"choice": "agree"}))
    print_step("进入最终结论", machine.handle_event("AI_REPAIR_DONE"))


def run_evidence_path() -> None:
    machine = LevelStateMachine("opossum_play_dead_001")
    print_step("完成知识准备", machine.handle_event("CHILD_INITIAL_JUDGEMENT_SUBMITTED", {"choice": "not_sure"}))
    print_step("直接引出错误", machine.handle_event("CHILD_CHAT", {"message": "我想听AI判断", "force_reveal": True}))
    print_step("儿童选择寻找线索，AI引用线索修正", machine.handle_event("REFLECTION_CHOICE_SELECTED", {"choice": "revise"}))
    print_step("进入最终结论", machine.handle_event("AI_REPAIR_DONE"))


def run_socratic_path() -> None:
    machine = LevelStateMachine("opossum_play_dead_001")
    print_step("完成知识准备", machine.handle_event("CHILD_INITIAL_JUDGEMENT_SUBMITTED", {"choice": "not_sure"}))
    print_step("自由聊 1", machine.handle_event("CHILD_CHAT", {"message": "负鼠吃什么？"}))
    print_step("自由聊 2", machine.handle_event("CHILD_CHAT", {"message": "小负鼠怎么长大？"}))
    print_step("自由聊 3", machine.handle_event("CHILD_CHAT", {"message": "负鼠住在哪里？"}))
    print_step("第 4 轮苏格拉底引导", machine.handle_event("CHILD_CHAT", {"message": "它有没有朋友？"}))
    print_step("继续引到关键现象", machine.handle_event("CHILD_CHAT", {"message": "它可能会逃跑。"}))
    print_step("儿童回应后抛出可控错误", machine.handle_event("CHILD_CHAT", {"message": "它如果不跑会怎么样？"}))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Level dialogue prototype demo")
    parser.add_argument(
        "--scripted",
        action="store_true",
        help="运行旧版固定脚本 demo，而不是交互式 demo",
    )
    args = parser.parse_args()

    if args.scripted:
        run_reject_path()
        run_agree_path()
        run_evidence_path()
        run_socratic_path()
    else:
        run_interactive()
