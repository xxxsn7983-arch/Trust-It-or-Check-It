# 科学探究游戏中的双模式对话智能体

Unity 交互前端与 Python 智能体后端的研究原型，用于探索陪伴对话、知识检索及引导用户质疑与寻找证据的交互流程。

## 项目结构

- `backend/agent_project/dual_state_agent`：A/B 智能体路由，使用规则估计信任与依赖评分。
- `backend/agent_project/companion_agent`：A 陪伴对话模式。
- `backend/agent_project/level_dialogue_prototype`：B 科学探究状态机、知识检索与回答评价。
- `backend/agent_project/two_state_rag_agent`：早期实验，非默认运行入口。
- `unity-client`：Unity 前端，保留 Assets、Packages 与 ProjectSettings。
- `unity-client/Backend`：另一个辅助服务原型，包含语音接口占位实现，与双模式智能体服务分开。

## 启动默认智能体后端

在仓库根目录执行（macOS/Linux）：

```bash
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -r backend/agent_project/requirements.txt
export PYTHONPATH="$PWD/backend"
read -s "QWEN_API_KEY?输入 Qwen API 密钥："  # zsh
export QWEN_API_KEY
echo
python -m uvicorn agent_project.dual_state_agent.api_server:app --host 127.0.0.1 --port 8030
```

密钥通过环境变量传入；不要将真实密钥写进源码或提交到 Git。默认智能体入口没有自动加载 `.env`，仅复制示例文件不会完成配置。未配置模型密钥时，部分策略会使用固定回退回复，这不能证明真实模型调用成功。

服务启动后，可访问 `http://127.0.0.1:8030/docs` 查看接口，或访问 `/health` 检查服务。

## Unity 前端

使用 Unity Hub 打开 `unity-client`。原项目版本为 `2022.3.42f1c1`，依赖信息见 Packages。聊天客户端默认连接 `http://127.0.0.1:8030`。

语音客户端另有 `http://localhost:5000/stt` 等地址，需要配置对应辅助服务；该服务的 STT/TTS 目前包含占位实现。跨设备运行时需修改服务地址。当前公开副本尚未执行 Unity 导入及完整端到端验证。

## 设计与限制

AB 模式每轮依次执行 A、B 分支并返回选定分支。信任与依赖评分来自规则，未证明能准确测量用户真实心理状态。会话主要存储在进程内存中，服务重启后不会自动恢复。当前流程面向儿童科学探究，包含可控错误与纠正环节，通用聊天需要进一步抽象。

真实会话、用户日志、数据库、录音输出、密钥、Unity 本地缓存和云项目标识未随此公开副本提供。仓库中的美术与音频资源在公开发布前仍需核实个人内容及再分发授权。
