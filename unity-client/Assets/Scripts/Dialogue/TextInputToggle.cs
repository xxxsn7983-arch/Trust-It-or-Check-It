using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TextInputToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private GameObject textBar;
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private ChatUIController chatUIController;

    private bool isActive;

    private void Awake()
    {
        if (toggleButton == null)
            toggleButton = GetComponent<Button>();
        if (chatUIController == null)
            chatUIController = FindObjectOfType<ChatUIController>();
    }

    private void OnEnable()
    {
        if (toggleButton != null)
            toggleButton.onClick.AddListener(OnButtonClicked);
    }

    private void OnDisable()
    {
        if (toggleButton != null)
            toggleButton.onClick.RemoveListener(OnButtonClicked);
    }

    private void OnButtonClicked()
    {
        if (!isActive)
        {
            ActivateTextBar();
        }
        else
        {
            SendAndDeactivate();
        }
    }

    private void ActivateTextBar()
    {
        isActive = true;
        if (textBar != null)
            textBar.SetActive(true);
        if (inputField != null)
            inputField.ActivateInputField();
    }

    private void SendAndDeactivate()
    {
        string text = inputField != null ? inputField.text.Trim() : "";

        if (!string.IsNullOrEmpty(text) && chatUIController != null)
            chatUIController.SendMessageText(text);

        if (inputField != null)
            inputField.text = "";

        if (textBar != null)
            textBar.SetActive(false);

        isActive = false;
    }
}
