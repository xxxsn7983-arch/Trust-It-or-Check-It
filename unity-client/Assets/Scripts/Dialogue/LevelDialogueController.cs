using UnityEngine;

/// <summary>
/// Bridges the teaching flow with the new level dialogue backend.
/// - On level start: calls /api/level/start with the child's initial belief
/// - On voice/text input: calls /api/level/chat
/// - On choice selection: calls /api/level/choice
/// - On response: displays agent message in chat UI and handles choice prompts
/// </summary>
public class LevelDialogueController : MonoBehaviour
{
    [Header("Services")]
    [SerializeField] private LevelDialogueService levelService;
    [SerializeField] private ChatUIController chatUI;
    [SerializeField] private TeachingFlowManager teachingFlowManager;

    [Header("Settings")]
    [Tooltip("Automatically start a level session when the teaching flow begins.")]
    [SerializeField] private bool autoStartOnTeachingFlow = true;

    [Tooltip("Default initial belief if none is provided.")]
    [SerializeField] private string defaultInitialBelief = "unknown";

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    private string pendingInitialBelief;

    private void Awake()
    {
        if (levelService == null)
            levelService = FindObjectOfType<LevelDialogueService>();
        if (chatUI == null)
            chatUI = FindObjectOfType<ChatUIController>();
        if (teachingFlowManager == null)
            teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
    }

    private void OnEnable()
    {
        GameEvents.OnTeachingChoiceSelected += HandleTeachingChoice;
        GameEvents.OnTeachingStateChanged += HandleStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.OnTeachingChoiceSelected -= HandleTeachingChoice;
        GameEvents.OnTeachingStateChanged -= HandleStateChanged;
    }

    /// <summary>
    /// Call this when the child submits voice/text input during dialogue.
    /// This is the main entry point for speech-to-text results.
    /// </summary>
    public void SendPlayerChat(string text)
    {
        text = text != null ? text.Trim() : "";
        if (string.IsNullOrEmpty(text)) return;

        if (!levelService.HasSession)
        {
            if (logActions)
                Debug.LogWarning("[LevelDialogueController] No session yet. Starting level first...");
            StartLevelThenSendChat(text);
            return;
        }

        if (chatUI != null)
            chatUI.AddPlayerMessage(text);

        levelService.SendChat(text, OnAgentResponse, OnError);
    }

    /// <summary>
    /// Call this when the child selects a choice (1=接受, 2=寻找线索, 3=怀疑一下).
    /// </summary>
    public void SendPlayerChoice(int choiceNumber)
    {
        if (!levelService.HasSession)
        {
            Debug.LogWarning("[LevelDialogueController] No session. Cannot send choice.");
            return;
        }

        levelService.SendChoice(choiceNumber.ToString(), OnAgentResponse, OnError);
    }

    /// <summary>
    /// Quick test: right-click this component in Inspector → "Test Start Level"
    /// </summary>
    [ContextMenu("Test Start Level")]
    public void TestStartLevel()
    {
        Debug.Log("[LevelDialogueController] === TEST: Starting level ===");
        StartLevel("我觉得负鼠是真的死了");
    }

    /// <summary>
    /// Quick test: right-click this component in Inspector → "Test Send Chat"
    /// </summary>
    [ContextMenu("Test Send Chat")]
    public void TestSendChat()
    {
        Debug.Log("[LevelDialogueController] === TEST: Sending chat ===");
        SendPlayerChat("为什么负鼠会装死？");
    }

    /// <summary>
    /// Manually start a level session.
    /// </summary>
    public void StartLevel(string initialBelief = null)
    {
        string belief = string.IsNullOrEmpty(initialBelief) ? defaultInitialBelief : initialBelief;

        if (logActions)
            Debug.Log($"[LevelDialogueController] Starting level with belief: {belief}");

        levelService.StartLevel(belief, OnLevelStarted, OnError);
    }

    private void HandleTeachingChoice(TeachingChoice choice, string detail)
    {
        if (choice == TeachingChoice.INITIAL_JUDGEMENT)
        {
            pendingInitialBelief = detail;

            if (autoStartOnTeachingFlow && !levelService.HasSession)
            {
                StartLevel(detail);
            }
        }
        // Note: TRUST_AI, FIND_EVIDENCE, DOUBT_AI choices are handled directly
        // by ReflectionChoiceController calling SendPlayerChoice(), so we don't
        // duplicate them here.
    }

    private void HandleStateChanged(TeachingState state)
    {
        // If entering AI_CONTROLLED_MISTAKE and no session yet, start one
        if (state == TeachingState.AI_CONTROLLED_MISTAKE && !levelService.HasSession)
        {
            string belief = string.IsNullOrEmpty(pendingInitialBelief) ? defaultInitialBelief : pendingInitialBelief;
            StartLevel(belief);
        }
    }

    private void StartLevelThenSendChat(string text)
    {
        string belief = string.IsNullOrEmpty(pendingInitialBelief) ? defaultInitialBelief : pendingInitialBelief;
        levelService.StartLevel(belief, (response) =>
        {
            OnLevelStarted(response);
            // Now send the chat message
            if (chatUI != null)
                chatUI.AddPlayerMessage(text);
            levelService.SendChat(text, OnAgentResponse, OnError);
        }, OnError);
    }

    private void OnLevelStarted(LevelResponse response)
    {
        if (logActions)
            Debug.Log($"[LevelDialogueController] Level started. Session: {response.session_id}, State: {response.state}");

        // Show the initial agent message if there is one
        if (!string.IsNullOrEmpty(response.content))
        {
            HandleAgentContent(response);
        }
    }

    private void OnAgentResponse(LevelResponse response)
    {
        if (logActions)
            Debug.Log($"[LevelDialogueController] Agent response: format={response.send_format}, state={response.state}");

        HandleAgentContent(response);
    }

    private void HandleAgentContent(LevelResponse response)
    {
        if (string.IsNullOrEmpty(response.content)) return;

        if (response.send_format == "choice")
        {
            if (chatUI != null)
            {
                chatUI.AddChoiceMessage(this, response.content);
                return;
            }

            Debug.LogWarning("[LevelDialogueController] Choice response received, but ChatUIController is missing.", this);
            return;
        }
        else
        {
            // Normal chat response
            if (chatUI != null)
                chatUI.AddAgentMessage(response.content);

            // Do not publish the legacy DialogueTextReady event here; ChatUI already renders this response directly.
        }
    }

    private void OnError(string error)
    {
        Debug.LogError($"[LevelDialogueController] {error}");
    }
}
