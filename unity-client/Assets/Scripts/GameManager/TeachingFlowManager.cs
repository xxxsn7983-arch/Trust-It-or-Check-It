using UnityEngine;

public class TeachingFlowManager : MonoBehaviour
{
    [Header("Startup")]
    [Tooltip("Start the teaching flow automatically when this object starts.")]
    [SerializeField] private bool startFlowOnStart = false;

    [Tooltip("First state used by StartFlow.")]
    [SerializeField] private TeachingState startState = TeachingState.INTRO_PHENOMENON;

    [Header("State Views")]
    [Tooltip("Root object for INTRO_PHENOMENON.")]
    [SerializeField] private GameObject introPhenomenonView;

    [Tooltip("Root object for KNOWLEDGE_CARD.")]
    [SerializeField] private GameObject knowledgeCardView;

    [Tooltip("Root object for CHILD_INITIAL_JUDGEMENT.")]
    [SerializeField] private GameObject childInitialJudgementView;

    [Tooltip("Root object for AI_CONTROLLED_MISTAKE.")]
    [SerializeField] private GameObject aiControlledMistakeView;

    [Tooltip("Root object for REFLECTION_PROMPT.")]
    [SerializeField] private GameObject reflectionPromptView;

    [Tooltip("Root object for CHILD_DIRECT_REPLY.")]
    [SerializeField] private GameObject childDirectReplyView;

    [Tooltip("Root object for EVIDENCE_EXPLORE.")]
    [SerializeField] private GameObject evidenceExploreView;

    [Tooltip("Root object for EVIDENCE_SUBMIT.")]
    [SerializeField] private GameObject evidenceSubmitView;

    [Tooltip("Root object for AI_SELF_CORRECTION_WITH_EVIDENCE.")]
    [SerializeField] private GameObject aiSelfCorrectionWithEvidenceView;

    [Tooltip("Root object for AI_REPAIR.")]
    [SerializeField] private GameObject aiRepairView;

    [Tooltip("Root object for FINAL_CONCLUSION.")]
    [SerializeField] private GameObject finalConclusionView;

    [Header("Dialogue Agent")]
    [Tooltip("Optional DialogueManager. Used only when Request Agent On Agent States is enabled.")]
    [SerializeField] private DialogueManager dialogueManager;

    [Tooltip("Open the chat UI when entering a state that needs agent dialogue.")]
    [SerializeField] private bool openChatOnAgentStates = true;

    [Tooltip("If true, entering agent states sends a system prompt to the current dialogue agent.")]
    [SerializeField] private bool requestAgentOnAgentStates = false;

    [Tooltip("Prefix sent before the state name when requesting the dialogue agent.")]
    [SerializeField] private string agentPromptPrefix = "TEACHING_STATE_CHANGED";

    [Header("Debug")]
    [SerializeField] private bool logStateChanges = true;

    [SerializeField] private TeachingState currentState = TeachingState.NONE;

    public TeachingState CurrentState => currentState;

    private void Awake()
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindObjectOfType<DialogueManager>();
        }
    }

    private void Start()
    {
        if (startFlowOnStart)
        {
            StartFlow();
        }
    }

    [ContextMenu("Start Flow")]
    public void StartFlow()
    {
        SetState(startState);
    }

    [ContextMenu("Restart Flow")]
    public void RestartFlow()
    {
        SetState(startState);
    }

    public void Continue()
    {
        switch (currentState)
        {
            case TeachingState.NONE:
                SetState(startState);
                break;
            case TeachingState.INTRO_PHENOMENON:
                SetState(TeachingState.KNOWLEDGE_CARD);
                break;
            case TeachingState.KNOWLEDGE_CARD:
                SetState(TeachingState.CHILD_INITIAL_JUDGEMENT);
                break;
            case TeachingState.AI_CONTROLLED_MISTAKE:
                SetState(TeachingState.REFLECTION_PROMPT);
                break;
            case TeachingState.CHILD_DIRECT_REPLY:
                SetState(TeachingState.AI_REPAIR);
                break;
            case TeachingState.AI_SELF_CORRECTION_WITH_EVIDENCE:
                SetState(TeachingState.AI_REPAIR);
                break;
            case TeachingState.AI_REPAIR:
                SetState(TeachingState.FINAL_CONCLUSION);
                break;
        }
    }

    public void SubmitInitialJudgement(string judgement)
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.INITIAL_JUDGEMENT, judgement);
        SetState(TeachingState.AI_CONTROLLED_MISTAKE);
    }

    public void ChooseTrustAI()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.TRUST_AI, "");
        SetState(TeachingState.AI_SELF_CORRECTION_WITH_EVIDENCE);
    }

    public void ChooseDoubtAI()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.DOUBT_AI, "");
        SetState(TeachingState.CHILD_DIRECT_REPLY);
    }

    public void ChooseFindEvidence()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.FIND_EVIDENCE, "");
        SetState(TeachingState.EVIDENCE_EXPLORE);
    }

    public void AcceptDirectReply()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.DIRECT_REPLY, "accepted");
        SetState(TeachingState.AI_REPAIR);
    }

    public void DirectReplyNeedsEvidence()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.EVIDENCE_INSUFFICIENT, "direct_reply_needs_evidence");
        SetState(TeachingState.EVIDENCE_EXPLORE);
    }

    public void DiscoverEvidence(string evidenceId)
    {
        if (string.IsNullOrWhiteSpace(evidenceId)) return;

        GameLogManager.EnsureExists().SetCurrentEvidence(evidenceId);
        GameEvents.FireTeachingEvidenceDiscovered(evidenceId);
    }

    public void OpenEvidenceSubmit(string evidenceId)
    {
        if (!string.IsNullOrWhiteSpace(evidenceId))
        {
            DiscoverEvidence(evidenceId);
        }

        SetState(TeachingState.EVIDENCE_SUBMIT);
    }

    public void SubmitEvidence(string evidenceId)
    {
        if (!string.IsNullOrWhiteSpace(evidenceId))
        {
            GameLogManager.EnsureExists().SetCurrentEvidence(evidenceId);
            GameEvents.FireTeachingEvidenceSubmitted(evidenceId);
        }

        GameEvents.FireTeachingChoiceSelected(TeachingChoice.EVIDENCE_SUBMITTED, evidenceId);
        SetState(TeachingState.AI_REPAIR);
    }

    public void EvidenceInsufficient()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.EVIDENCE_INSUFFICIENT, "");
        SetState(TeachingState.EVIDENCE_EXPLORE);
    }

    public void EvidenceValid()
    {
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.EVIDENCE_VALID, "");
        SetState(TeachingState.AI_REPAIR);
    }

    public void FinishAiSelfCorrection()
    {
        SetState(TeachingState.AI_REPAIR);
    }

    public void FinishAiRepair()
    {
        SetState(TeachingState.FINAL_CONCLUSION);
    }

    public void SetCurrentKnowledgePoint(string knowledgePointId)
    {
        GameLogManager.EnsureExists().SetCurrentKnowledgePoint(knowledgePointId);
    }

    public void SetAiMistake(string mistakeId)
    {
        GameLogManager.EnsureExists().SetAiMistake(mistakeId);
    }

    public void SetStateIntroPhenomenon() => SetState(TeachingState.INTRO_PHENOMENON);
    public void SetStateKnowledgeCard() => SetState(TeachingState.KNOWLEDGE_CARD);
    public void SetStateChildInitialJudgement() => SetState(TeachingState.CHILD_INITIAL_JUDGEMENT);
    public void SetStateAiControlledMistake() => SetState(TeachingState.AI_CONTROLLED_MISTAKE);
    public void SetStateReflectionPrompt() => SetState(TeachingState.REFLECTION_PROMPT);
    public void SetStateChildDirectReply() => SetState(TeachingState.CHILD_DIRECT_REPLY);
    public void SetStateEvidenceExplore() => SetState(TeachingState.EVIDENCE_EXPLORE);
    public void SetStateEvidenceSubmit() => SetState(TeachingState.EVIDENCE_SUBMIT);
    public void SetStateAiSelfCorrectionWithEvidence() => SetState(TeachingState.AI_SELF_CORRECTION_WITH_EVIDENCE);
    public void SetStateAiRepair() => SetState(TeachingState.AI_REPAIR);
    public void SetStateFinalConclusion() => SetState(TeachingState.FINAL_CONCLUSION);

    public void SetState(TeachingState nextState)
    {
        currentState = nextState;
        ApplyStateView(nextState);
        GameEvents.FireTeachingStateChanged(nextState);

        if (logStateChanges)
        {
            Debug.Log($"TeachingFlowManager: state changed to {nextState}", this);
        }

        if (ShouldOpenDialogue(nextState))
        {
            GameEvents.FireDialogueStart(nextState.ToString());
        }

        if (requestAgentOnAgentStates && ShouldRequestAgent(nextState))
        {
            RequestAgentForState(nextState);
        }
    }

    private void ApplyStateView(TeachingState state)
    {
        HideAllViews();

        GameObject activeView = GetViewForState(state);
        if (activeView != null)
        {
            activeView.SetActive(true);
        }
    }

    private void HideAllViews()
    {
        SetViewActive(introPhenomenonView, false);
        SetViewActive(knowledgeCardView, false);
        SetViewActive(childInitialJudgementView, false);
        SetViewActive(aiControlledMistakeView, false);
        SetViewActive(reflectionPromptView, false);
        SetViewActive(childDirectReplyView, false);
        SetViewActive(evidenceExploreView, false);
        SetViewActive(evidenceSubmitView, false);
        SetViewActive(aiSelfCorrectionWithEvidenceView, false);
        SetViewActive(aiRepairView, false);
        SetViewActive(finalConclusionView, false);
    }

    private GameObject GetViewForState(TeachingState state)
    {
        switch (state)
        {
            case TeachingState.INTRO_PHENOMENON:
                return introPhenomenonView;
            case TeachingState.KNOWLEDGE_CARD:
                return knowledgeCardView;
            case TeachingState.CHILD_INITIAL_JUDGEMENT:
                return childInitialJudgementView;
            case TeachingState.AI_CONTROLLED_MISTAKE:
                return aiControlledMistakeView;
            case TeachingState.REFLECTION_PROMPT:
                return reflectionPromptView;
            case TeachingState.CHILD_DIRECT_REPLY:
                return childDirectReplyView;
            case TeachingState.EVIDENCE_EXPLORE:
                return evidenceExploreView;
            case TeachingState.EVIDENCE_SUBMIT:
                return evidenceSubmitView;
            case TeachingState.AI_SELF_CORRECTION_WITH_EVIDENCE:
                return aiSelfCorrectionWithEvidenceView;
            case TeachingState.AI_REPAIR:
                return aiRepairView;
            case TeachingState.FINAL_CONCLUSION:
                return finalConclusionView;
            default:
                return null;
        }
    }

    private bool ShouldOpenDialogue(TeachingState state)
    {
        if (!openChatOnAgentStates) return false;

        return state == TeachingState.AI_CONTROLLED_MISTAKE
            || state == TeachingState.REFLECTION_PROMPT
            || state == TeachingState.CHILD_DIRECT_REPLY
            || state == TeachingState.EVIDENCE_EXPLORE
            || state == TeachingState.EVIDENCE_SUBMIT
            || state == TeachingState.AI_SELF_CORRECTION_WITH_EVIDENCE
            || state == TeachingState.AI_REPAIR
            || state == TeachingState.FINAL_CONCLUSION;
    }

    private bool ShouldRequestAgent(TeachingState state)
    {
        return ShouldOpenDialogue(state);
    }

    private void RequestAgentForState(TeachingState state)
    {
        if (dialogueManager == null)
        {
            dialogueManager = FindObjectOfType<DialogueManager>();
        }

        if (dialogueManager == null)
        {
            Debug.LogWarning("TeachingFlowManager: DialogueManager is missing.", this);
            return;
        }

        dialogueManager.SendSystemPrompt($"{agentPromptPrefix}: {state}");
    }

    private static void SetViewActive(GameObject view, bool active)
    {
        if (view != null)
        {
            view.SetActive(active);
        }
    }
}
