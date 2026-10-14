using UnityEngine;

/// <summary>
/// 玩家交互 - 监听交互事件，触发对话开始/结束
/// 不再直接引用 UI 面板或 DialogueManager
/// </summary>
public class PlayerInteract : MonoBehaviour
{
    public string initialDialogMessage = "你好，开始对话...";
    [SerializeField] private ChatUIController chatUIController;
    private bool isDialogueOpen = false;

    private void Awake()
    {
        if (chatUIController == null)
            chatUIController = FindObjectOfType<ChatUIController>(true);
    }

    void OnEnable()
    {
        GameEvents.OnInteractPressed += HandleInteract;
        GameEvents.OnDialogueEnd += HandleDialogueEnd;
    }

    void OnDisable()
    {
        GameEvents.OnInteractPressed -= HandleInteract;
        GameEvents.OnDialogueEnd -= HandleDialogueEnd;
    }

    private void HandleInteract()
    {
        isDialogueOpen = !isDialogueOpen;

        if (isDialogueOpen)
        {
            GameEvents.FireDialogueStart(initialDialogMessage);

            // First Scene uses the global interact input rather than a direct UI button binding.
            // Call the chat UI explicitly so its AI prefab is instantiated under Content.
            if (chatUIController == null)
                chatUIController = FindObjectOfType<ChatUIController>(true);

            if (chatUIController != null)
                chatUIController.OpenWithFirstAgentMessage();
        }
        else
        {
            GameEvents.FireDialogueEnd();
        }
    }

    private void HandleDialogueEnd()
    {
        isDialogueOpen = false;
    }
}
