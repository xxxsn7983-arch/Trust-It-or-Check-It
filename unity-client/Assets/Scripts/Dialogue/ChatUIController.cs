using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ChatUIController : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject chatPanelRoot;
    [SerializeField] private bool showPanelOnDialogue = true;

    [Header("Close")]
    [SerializeField] private bool closeOnOutsideClick = true;
    [SerializeField] private RectTransform outsideClickBounds;
    [SerializeField] private bool closeOnEscape = true;

    [Header("Message List")]
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private Transform contentRoot;

    [Header("Message Prefabs")]
    [SerializeField] private ChatMessageBubble agentMessagePrefab;
    [SerializeField] private ChatMessageBubble playerMessagePrefab;
    [SerializeField] private ReflectionChoiceController choiceMessagePrefab;

    [Header("Dialogue")]
    [SerializeField] private DialogueManager dialogueManager;
    [Tooltip("If assigned, voice/text input is sent to the new level dialogue backend instead of the old DialogueManager.")]
    [SerializeField] private LevelDialogueController levelDialogueController;
    [SerializeField] private bool showDialogueStartAsPlayerMessage = false;

    [Header("First Open Prompt")]
    [Tooltip("Show one preset agent message the first time this dialogue panel is opened.")]
    [SerializeField] private bool showFirstOpenAgentMessage = false;
    [TextArea(2, 4)]
    [SerializeField] private string firstOpenAgentMessage = "";

    [Header("Debug")]
    [SerializeField] private bool warnMissingReferences = true;

    private bool hasShownFirstOpenAgentMessage;

    private void Awake()
    {
        if (levelDialogueController == null)
            levelDialogueController = FindObjectOfType<LevelDialogueController>();

        if (dialogueManager == null)
            dialogueManager = FindObjectOfType<DialogueManager>();

        if (chatPanelRoot == null)
            chatPanelRoot = gameObject;

        // ChatUIController can live on the panel that is hidden with SetActive(false).
        // Keep event subscriptions alive so a later dialogue-start event can reopen it.
        GameEvents.OnDialogueStart += HandleDialogueStart;
        GameEvents.OnDialogueEnd += HandleDialogueEnd;
        GameEvents.OnDialogueTextReady += HandleDialogueTextReady;
    }

    private void OnDestroy()
    {
        GameEvents.OnDialogueStart -= HandleDialogueStart;
        GameEvents.OnDialogueEnd -= HandleDialogueEnd;
        GameEvents.OnDialogueTextReady -= HandleDialogueTextReady;
    }

    private void Update()
    {
        if (chatPanelRoot == null || !chatPanelRoot.activeInHierarchy) return;

        if (closeOnEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseChatPanel();
            return;
        }

        if (closeOnOutsideClick && WasOutsidePointerPressed())
        {
            CloseChatPanel();
        }
    }

    public void SendMessageText(string message)
    {
        message = message != null ? message.Trim() : "";
        if (string.IsNullOrEmpty(message)) return;

        if (levelDialogueController == null)
            levelDialogueController = FindObjectOfType<LevelDialogueController>();

        // The level dialogue backend is the primary route for this scene.
        // This keeps voice/text input from falling back to the old local JSON agent path.
        if (levelDialogueController != null)
        {
            levelDialogueController.SendPlayerChat(message);
            return;
        }

        Debug.LogWarning("ChatUIController: LevelDialogueController is missing. Message was not sent to the old DialogueManager.");
    }

    public void CloseChatPanel()
    {
        GameEvents.FireDialogueEnd();
    }

    public void OpenWithFirstAgentMessage()
    {
        if (showPanelOnDialogue && chatPanelRoot != null)
            chatPanelRoot.SetActive(true);

        if (showFirstOpenAgentMessage && !hasShownFirstOpenAgentMessage)
            ShowFirstOpenAgentMessage();
    }

    public void AddAgentMessage(string message)
    {
        AddMessage(agentMessagePrefab, message);
    }

    public void AddPlayerMessage(string message)
    {
        AddMessage(playerMessagePrefab, message);
    }

    public ReflectionChoiceController AddChoiceMessage(LevelDialogueController controller, string message)
    {
        if (choiceMessagePrefab == null || contentRoot == null) return null;

        if (showPanelOnDialogue && chatPanelRoot != null && !chatPanelRoot.activeSelf)
            chatPanelRoot.SetActive(true);

        ReflectionChoiceController choice = Instantiate(choiceMessagePrefab, contentRoot);
        choice.Configure(controller);
        choice.SetAgentMessage(message);

        StartCoroutine(ScrollToBottomNextFrame());
        return choice;
    }

    private void AddMessage(ChatMessageBubble prefab, string message)
    {
        if (prefab == null || contentRoot == null) return;

        if (showPanelOnDialogue && chatPanelRoot != null && !chatPanelRoot.activeSelf)
            chatPanelRoot.SetActive(true);

        ChatMessageBubble bubble = Instantiate(prefab, contentRoot);
        bubble.gameObject.SetActive(true);
        bubble.SetMessage(message);

        StartCoroutine(ScrollToBottomNextFrame());
    }

    private IEnumerator ScrollToBottomNextFrame()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (contentRoot is RectTransform contentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        if (scrollRect != null)
            scrollRect.verticalNormalizedPosition = 0f;
    }

    private void HandleDialogueStart(string message)
    {
        OpenWithFirstAgentMessage();

        if (showDialogueStartAsPlayerMessage)
            AddPlayerMessage(message);
    }

    private void ShowFirstOpenAgentMessage()
    {
        if (agentMessagePrefab == null || contentRoot == null)
        {
            if (warnMissingReferences)
                Debug.LogWarning("ChatUIController: Cannot show first-open AI message because the AI message prefab or content root is missing.", this);
            return;
        }

        if (string.IsNullOrWhiteSpace(firstOpenAgentMessage))
            return;

        // Create the preset text with the same AI bubble prefab used by backend replies.
        AddAgentMessage(firstOpenAgentMessage);
        hasShownFirstOpenAgentMessage = true;
    }

    private void HandleDialogueEnd()
    {
        if (chatPanelRoot != null && chatPanelRoot != gameObject)
            chatPanelRoot.SetActive(false);
    }

    private void HandleDialogueTextReady(string message)
    {
        // LevelDialogueController writes agent bubbles directly.
        // Ignore this legacy event when the new backend controller exists to avoid duplicate/local fallback bubbles.
        if (levelDialogueController != null) return;

        AddAgentMessage(message);
    }

    private bool WasOutsidePointerPressed()
    {
        if (Input.GetMouseButtonDown(0))
            return !IsScreenPointInsideChat(Input.mousePosition);

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.phase == TouchPhase.Began && !IsScreenPointInsideChat(touch.position))
                return true;
        }

        return false;
    }

    private bool IsScreenPointInsideChat(Vector2 screenPosition)
    {
        RectTransform bounds = outsideClickBounds;
        if (bounds == null && chatPanelRoot != null)
            bounds = chatPanelRoot.GetComponent<RectTransform>();

        if (bounds == null) return true;

        Canvas canvas = bounds.GetComponentInParent<Canvas>();
        Camera eventCamera = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            eventCamera = canvas.worldCamera;

        return RectTransformUtility.RectangleContainsScreenPoint(bounds, screenPosition, eventCamera);
    }

    private void ReportMissingReferences()
    {
        if (!warnMissingReferences) return;
        if (scrollRect == null) Debug.LogWarning("ChatUIController: Scroll Rect is not assigned.", this);
        if (contentRoot == null) Debug.LogWarning("ChatUIController: Content Root is not assigned.", this);
        if (agentMessagePrefab == null) Debug.LogWarning("ChatUIController: Agent Message Prefab is not assigned.", this);
        if (playerMessagePrefab == null) Debug.LogWarning("ChatUIController: Player Message Prefab is not assigned.", this);
        if (choiceMessagePrefab == null) Debug.LogWarning("ChatUIController: Choice Message Prefab is not assigned.", this);
        if (dialogueManager == null) Debug.LogWarning("ChatUIController: Dialogue Manager is not assigned.", this);
    }

    private void Start()
    {
        ReportMissingReferences();
    }
}
