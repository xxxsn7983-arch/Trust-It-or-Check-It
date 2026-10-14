using UnityEngine;
using TMPro;

public class DialogueManager : MonoBehaviour
{
    [Header("Text Display")]
    public TMP_Text dialogueText;

    [Header("Settings")]
    public bool useLocalJsonText = false;
    public bool preferBackendAgent = true;
    [Tooltip("If false, opening the dialogue UI will not send the initial interact message to the backend agent.")]
    public bool sendInitialMessageToAgent = false;
    [Tooltip("If true, opening the dialogue UI can show the initial/local text without contacting the backend.")]
    public bool showInitialMessageLocally = false;

    [SerializeField]
    private DialogueService dialogueService;

    void OnEnable()
    {
        GameEvents.OnDialogueStart += HandleDialogueStart;
        GameEvents.OnDialogueTextReady += DisplayText;
    }

    void OnDisable()
    {
        GameEvents.OnDialogueStart -= HandleDialogueStart;
        GameEvents.OnDialogueTextReady -= DisplayText;
    }

    private void HandleDialogueStart(string initialMessage)
    {
        if (!sendInitialMessageToAgent && !showInitialMessageLocally)
        {
            return;
        }

        string localText = useLocalJsonText ? DialogueLoader.LoadDefaultDialogue() : null;

        if (sendInitialMessageToAgent && preferBackendAgent && dialogueService != null)
        {
            dialogueService.SendRequest(initialMessage, localText);
            return;
        }

        if (showInitialMessageLocally && !string.IsNullOrEmpty(localText))
        {
            DisplayText(localText);
            return;
        }

        if (sendInitialMessageToAgent && dialogueService != null)
        {
            dialogueService.SendRequest(initialMessage);
            return;
        }

        if (showInitialMessageLocally)
        {
            DisplayText(initialMessage);
        }
    }

    private void DisplayText(string text)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }
    }

    public void SendPlayerInput(string playerInput)
    {
        if (dialogueService != null)
        {
            string localText = useLocalJsonText ? DialogueLoader.LoadDefaultDialogue() : null;
            dialogueService.SendRequest(playerInput, localText);
        }
    }

    public void SendSystemPrompt(string prompt)
    {
        if (dialogueService != null)
        {
            string localText = useLocalJsonText ? DialogueLoader.LoadDefaultDialogue() : null;
            dialogueService.SendSystemRequest(prompt, localText);
        }
    }
}
