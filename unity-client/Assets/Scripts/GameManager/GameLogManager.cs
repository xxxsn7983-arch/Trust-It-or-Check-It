using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public class GameLogEntry
{
    public string type;
    public string target;
    public string scene;
    public float time;
    public string timestampUtc;
    public string detail;
}

[Serializable]
public class DialogueMessageLog
{
    public string role;
    public string content;
    public string scene;
    public float time;
    public string timestampUtc;
}

[Serializable]
public class GameContextSnapshot
{
    public string sessionId;
    public string sceneName;
    public List<string> inventory;
    public List<GameLogEntry> recentEvents;
    public List<DialogueMessageLog> recentMessages;
    public TeachingContextSnapshot teachingContext;
}

public class GameLogManager : MonoBehaviour
{
    private const string SessionPrefsKey = "agent_session_id";

    public static GameLogManager Instance { get; private set; }

    [Header("Context limits")]
    public int maxRecentEvents = 20;
    public int maxRecentMessages = 12;

    private readonly List<GameLogEntry> recentEvents = new List<GameLogEntry>();
    private readonly List<DialogueMessageLog> recentMessages = new List<DialogueMessageLog>();
    private readonly List<string> inventory = new List<string>();
    private readonly List<string> discoveredEvidenceIds = new List<string>();
    private readonly List<string> submittedEvidenceIds = new List<string>();

    private string sessionId;
    private string currentSceneName;
    private string currentTeachingState = nameof(TeachingState.NONE);
    private string lastTeachingChoice = nameof(TeachingChoice.NONE);
    private string initialJudgement = "";
    private string currentKnowledgePointId = "";
    private string currentEvidenceId = "";
    private string aiMistakeId = "";
    private string lastChoiceDetail = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static GameLogManager EnsureExists()
    {
        if (Instance != null) return Instance;

        GameLogManager existing = FindObjectOfType<GameLogManager>();
        if (existing != null) return existing;

        GameObject managerObject = new GameObject("GameLogManager");
        return managerObject.AddComponent<GameLogManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        sessionId = LoadOrCreateSessionId();
        currentSceneName = SceneManager.GetActiveScene().name;
    }

    private void OnEnable()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded += HandleSceneLoaded;
        GameEvents.OnItemPopupOpen += HandleItemPopupOpen;
        GameEvents.OnClueCollected += HandleClueCollected;
        GameEvents.OnClueExamine += HandleClueExamine;
        GameEvents.OnTeachingStateChanged += HandleTeachingStateChanged;
        GameEvents.OnTeachingChoiceSelected += HandleTeachingChoiceSelected;
        GameEvents.OnTeachingEvidenceDiscovered += HandleTeachingEvidenceDiscovered;
        GameEvents.OnTeachingEvidenceSubmitted += HandleTeachingEvidenceSubmitted;
    }

    private void OnDisable()
    {
        if (Instance != this) return;

        SceneManager.sceneLoaded -= HandleSceneLoaded;
        GameEvents.OnItemPopupOpen -= HandleItemPopupOpen;
        GameEvents.OnClueCollected -= HandleClueCollected;
        GameEvents.OnClueExamine -= HandleClueExamine;
        GameEvents.OnTeachingStateChanged -= HandleTeachingStateChanged;
        GameEvents.OnTeachingChoiceSelected -= HandleTeachingChoiceSelected;
        GameEvents.OnTeachingEvidenceDiscovered -= HandleTeachingEvidenceDiscovered;
        GameEvents.OnTeachingEvidenceSubmitted -= HandleTeachingEvidenceSubmitted;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public GameContextSnapshot GetContextSnapshot()
    {
        return new GameContextSnapshot
        {
            sessionId = sessionId,
            sceneName = GetCurrentSceneName(),
            inventory = new List<string>(inventory),
            recentEvents = new List<GameLogEntry>(recentEvents),
            recentMessages = new List<DialogueMessageLog>(recentMessages),
            teachingContext = GetTeachingContextSnapshot()
        };
    }

    public TeachingContextSnapshot GetTeachingContextSnapshot()
    {
        return new TeachingContextSnapshot
        {
            currentState = string.IsNullOrEmpty(currentTeachingState) ? nameof(TeachingState.NONE) : currentTeachingState,
            lastChoice = string.IsNullOrEmpty(lastTeachingChoice) ? nameof(TeachingChoice.NONE) : lastTeachingChoice,
            initialJudgement = initialJudgement,
            currentKnowledgePointId = currentKnowledgePointId,
            currentEvidenceId = currentEvidenceId,
            aiMistakeId = aiMistakeId,
            lastChoiceDetail = lastChoiceDetail,
            discoveredEvidenceIds = new List<string>(discoveredEvidenceIds),
            submittedEvidenceIds = new List<string>(submittedEvidenceIds)
        };
    }

    public void RecordPlayerMessage(string content)
    {
        RecordMessage("player", content);
    }

    public void RecordAgentMessage(string content)
    {
        RecordMessage("agent", content);
    }

    public void RecordEvent(string type, string target = "", string detail = "")
    {
        recentEvents.Add(new GameLogEntry
        {
            type = type,
            target = target,
            scene = GetCurrentSceneName(),
            time = Time.realtimeSinceStartup,
            timestampUtc = DateTime.UtcNow.ToString("o"),
            detail = detail
        });

        TrimList(recentEvents, Mathf.Max(1, maxRecentEvents));
    }

    public void SetCurrentKnowledgePoint(string knowledgePointId)
    {
        currentKnowledgePointId = knowledgePointId ?? "";
        RecordEvent("teaching_knowledge_point_set", currentKnowledgePointId);
    }

    public void SetCurrentEvidence(string evidenceId)
    {
        currentEvidenceId = evidenceId ?? "";
        RecordEvent("teaching_current_evidence_set", currentEvidenceId);
    }

    public void SetAiMistake(string mistakeId)
    {
        aiMistakeId = mistakeId ?? "";
        RecordEvent("teaching_ai_mistake_set", aiMistakeId);
    }

    private void RecordMessage(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        recentMessages.Add(new DialogueMessageLog
        {
            role = role,
            content = content,
            scene = GetCurrentSceneName(),
            time = Time.realtimeSinceStartup,
            timestampUtc = DateTime.UtcNow.ToString("o")
        });

        TrimList(recentMessages, Mathf.Max(1, maxRecentMessages));
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentSceneName = scene.name;
        RecordEvent("scene_loaded", scene.name, mode.ToString());
    }

    private void HandleItemPopupOpen(ItemData data)
    {
        string target = data != null ? data.itemName : "";
        RecordEvent("item_popup_opened", target);
    }

    private void HandleClueCollected(ClueInfo info)
    {
        if (info == null) return;

        string clueId = !string.IsNullOrEmpty(info.clueId) ? info.clueId : info.clueName;
        if (!string.IsNullOrEmpty(clueId) && !inventory.Contains(clueId))
        {
            inventory.Add(clueId);
        }

        RecordEvent("clue_collected", clueId, info.clueName);
    }

    private void HandleClueExamine(ClueInfo info)
    {
        if (info == null) return;

        string clueId = !string.IsNullOrEmpty(info.clueId) ? info.clueId : info.clueName;
        RecordEvent("clue_examined", clueId, info.clueName);
    }

    private void HandleTeachingStateChanged(TeachingState state)
    {
        currentTeachingState = state.ToString();
        RecordEvent("teaching_state_changed", currentTeachingState);
    }

    private void HandleTeachingChoiceSelected(TeachingChoice choice, string detail)
    {
        lastTeachingChoice = choice.ToString();
        lastChoiceDetail = detail ?? "";

        if (choice == TeachingChoice.INITIAL_JUDGEMENT)
        {
            initialJudgement = lastChoiceDetail;
        }

        RecordEvent("teaching_choice_selected", lastTeachingChoice, lastChoiceDetail);
    }

    private void HandleTeachingEvidenceDiscovered(string evidenceId)
    {
        evidenceId = evidenceId ?? "";
        if (!string.IsNullOrEmpty(evidenceId) && !discoveredEvidenceIds.Contains(evidenceId))
        {
            discoveredEvidenceIds.Add(evidenceId);
        }

        currentEvidenceId = evidenceId;
        RecordEvent("teaching_evidence_discovered", evidenceId);
    }

    private void HandleTeachingEvidenceSubmitted(string evidenceId)
    {
        evidenceId = evidenceId ?? "";
        if (!string.IsNullOrEmpty(evidenceId) && !submittedEvidenceIds.Contains(evidenceId))
        {
            submittedEvidenceIds.Add(evidenceId);
        }

        currentEvidenceId = evidenceId;
        RecordEvent("teaching_evidence_submitted", evidenceId);
    }

    private string GetCurrentSceneName()
    {
        if (string.IsNullOrEmpty(currentSceneName))
        {
            currentSceneName = SceneManager.GetActiveScene().name;
        }

        return currentSceneName;
    }

    private string LoadOrCreateSessionId()
    {
        string savedSessionId = PlayerPrefs.GetString(SessionPrefsKey, "");
        if (!string.IsNullOrEmpty(savedSessionId))
        {
            return savedSessionId;
        }

        string newSessionId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(SessionPrefsKey, newSessionId);
        PlayerPrefs.Save();
        return newSessionId;
    }

    private static void TrimList<T>(List<T> list, int maxCount)
    {
        while (list.Count > maxCount)
        {
            list.RemoveAt(0);
        }
    }
}
