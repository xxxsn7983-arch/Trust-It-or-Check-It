using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextInputService : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button sendButton;

    [Header("Target")]
    [Tooltip("Drag ChatPanel's ChatUIController here. If empty, this script tries to find it.")]
    [SerializeField] private ChatUIController chatUIController;

    [Header("Behavior")]
    [SerializeField] private bool sendOnEnter = true;
    [SerializeField] private bool clearAfterSend = true;
    [SerializeField] private bool refocusAfterSend = true;

    [Header("Debug")]
    [SerializeField] private bool logWarnings = true;

    private void Awake()
    {
        if (inputField == null)
        {
            inputField = GetComponent<TMP_InputField>();
        }

        if (sendButton == null)
        {
            sendButton = GetComponentInChildren<Button>();
        }

        if (chatUIController == null)
        {
            chatUIController = GetComponentInParent<ChatUIController>();
        }

        if (chatUIController == null)
        {
            chatUIController = FindObjectOfType<ChatUIController>();
        }
    }

    private void OnEnable()
    {
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(SendCurrentText);
        }
    }

    private void OnDisable()
    {
        if (sendButton != null)
        {
            sendButton.onClick.RemoveListener(SendCurrentText);
        }
    }

    private void Update()
    {
        if (!sendOnEnter || inputField == null || !inputField.isFocused) return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendCurrentText();
        }
    }

    public void SendCurrentText()
    {
        string text = inputField != null ? inputField.text.Trim() : "";
        if (string.IsNullOrEmpty(text)) return;

        if (chatUIController == null)
        {
            chatUIController = FindObjectOfType<ChatUIController>();
        }

        if (chatUIController == null)
        {
            if (logWarnings)
            {
                Debug.LogWarning("TextInputService: ChatUIController not found, cannot send text.", this);
            }
            return;
        }

        chatUIController.SendMessageText(text);

        if (clearAfterSend && inputField != null)
        {
            inputField.text = "";
        }

        if (refocusAfterSend && inputField != null)
        {
            inputField.ActivateInputField();
        }
    }
}
