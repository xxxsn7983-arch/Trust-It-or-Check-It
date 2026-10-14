using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the TrustMessageRow prefab used in ReflectionPromptView.
/// Displays the agent's message text and handles the three choice buttons
/// (Agree/Clues/Question) by sending the corresponding choice number
/// to the LevelDialogueController.
///
/// Choice mapping:
///   1 = 接受 (Agree)
///   2 = 寻找线索 (Clues)
///   3 = 怀疑一下 (Question)
/// </summary>
public class ReflectionChoiceController : MonoBehaviour
{
    [Header("UI Bindings")]
    [Tooltip("The TMP_Text that shows the agent's message (AgentText).")]
    [SerializeField] private TMP_Text agentText;

    [Tooltip("Button for choice 1: 接受/同意")]
    [SerializeField] private Button agreeButton;

    [Tooltip("Button for choice 2: 寻找线索")]
    [SerializeField] private Button cluesButton;

    [Tooltip("Button for choice 3: 怀疑一下")]
    [SerializeField] private Button questionButton;

    [Header("References")]
    [SerializeField] private LevelDialogueController levelDialogueController;

    [Header("Settings")]
    [Tooltip("Disable buttons after a choice is made to prevent double-clicks.")]
    [SerializeField] private bool disableAfterChoice = true;

    private void Awake()
    {
        if (levelDialogueController == null)
            levelDialogueController = FindObjectOfType<LevelDialogueController>();
    }

    private void OnEnable()
    {
        if (agreeButton != null)
            agreeButton.onClick.AddListener(OnAgreeClicked);
        if (cluesButton != null)
            cluesButton.onClick.AddListener(OnCluesClicked);
        if (questionButton != null)
            questionButton.onClick.AddListener(OnQuestionClicked);

        SetButtonsInteractable(true);
    }

    private void OnDisable()
    {
        if (agreeButton != null)
            agreeButton.onClick.RemoveListener(OnAgreeClicked);
        if (cluesButton != null)
            cluesButton.onClick.RemoveListener(OnCluesClicked);
        if (questionButton != null)
            questionButton.onClick.RemoveListener(OnQuestionClicked);
    }

    public void Configure(LevelDialogueController controller)
    {
        levelDialogueController = controller;
    }

    /// <summary>
    /// Set the agent message text displayed above the choice buttons.
    /// Call this when receiving a send_format="choice" response from the backend.
    /// </summary>
    public void SetAgentMessage(string message)
    {
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (agentText != null)
            agentText.text = message;

        SetButtonsInteractable(true);
    }

    private void OnAgreeClicked()
    {
        SubmitChoice(1);
    }

    private void OnCluesClicked()
    {
        SubmitChoice(2);
    }

    private void OnQuestionClicked()
    {
        SubmitChoice(3);
    }

    private void SubmitChoice(int choiceNumber)
    {
        if (disableAfterChoice)
            SetButtonsInteractable(false);

        if (levelDialogueController == null)
            levelDialogueController = FindObjectOfType<LevelDialogueController>();

        if (levelDialogueController != null)
        {
            levelDialogueController.SendPlayerChoice(choiceNumber);
            return;
        }

        Debug.LogWarning("ReflectionChoiceController: LevelDialogueController is missing, choice was not sent to backend.", this);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (agreeButton != null) agreeButton.interactable = interactable;
        if (cluesButton != null) cluesButton.interactable = interactable;
        if (questionButton != null) questionButton.interactable = interactable;
    }
}
