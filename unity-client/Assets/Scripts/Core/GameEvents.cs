using System;
using UnityEngine;

/// <summary>
/// Global event hub. Scripts should subscribe in OnEnable and unsubscribe in OnDisable.
/// </summary>
public static class GameEvents
{
    // Input
    public static event Action<float> OnMoveInput;
    public static event Action OnJumpPressed;
    public static event Action OnInteractPressed;

    // Dialogue
    public static event Action<string> OnDialogueStart;
    public static event Action OnDialogueEnd;
    public static event Action<string> OnDialogueTextReady;

    // Teaching flow
    public static event Action<TeachingState> OnTeachingStateChanged;
    public static event Action<TeachingChoice, string> OnTeachingChoiceSelected;
    public static event Action<string> OnTeachingEvidenceDiscovered;
    public static event Action<string> OnTeachingEvidenceSubmitted;

    // UI
    public static event Action<string, bool> OnPanelToggle;

    // Item popup
    public static event Action<ItemData> OnItemPopupOpen;
    public static event Action OnItemPopupClose;

    // Clues
    public static event Action<ClueInfo> OnClueCollected;
    public static event Action<ClueInfo> OnClueExamine;
    public static event Action OnClueExamineClose;

    // Evidence card challenge
    public static event Action<string> OnEvidenceCardAccepted;
    public static event Action<string, int> OnEvidenceCardRejected;
    public static event Action OnEvidenceCardChallengeWon;
    public static event Action OnEvidenceCardChallengeLost;

    public static void FireMoveInput(float x) => OnMoveInput?.Invoke(x);
    public static void FireJumpPressed() => OnJumpPressed?.Invoke();
    public static void FireInteractPressed() => OnInteractPressed?.Invoke();

    public static void FireDialogueStart(string msg) => OnDialogueStart?.Invoke(msg);
    public static void FireDialogueEnd() => OnDialogueEnd?.Invoke();
    public static void FireDialogueTextReady(string text) => OnDialogueTextReady?.Invoke(text);

    public static void FireTeachingStateChanged(TeachingState state) => OnTeachingStateChanged?.Invoke(state);
    public static void FireTeachingChoiceSelected(TeachingChoice choice, string detail = "") => OnTeachingChoiceSelected?.Invoke(choice, detail);
    public static void FireTeachingEvidenceDiscovered(string evidenceId) => OnTeachingEvidenceDiscovered?.Invoke(evidenceId);
    public static void FireTeachingEvidenceSubmitted(string evidenceId) => OnTeachingEvidenceSubmitted?.Invoke(evidenceId);

    public static void FirePanelToggle(string name, bool show) => OnPanelToggle?.Invoke(name, show);
    public static void FireItemPopupOpen(ItemData data) => OnItemPopupOpen?.Invoke(data);
    public static void FireItemPopupClose() => OnItemPopupClose?.Invoke();
    public static void FireClueCollected(ClueInfo info) => OnClueCollected?.Invoke(info);
    public static void FireClueExamine(ClueInfo info) => OnClueExamine?.Invoke(info);
    public static void FireClueExamineClose() => OnClueExamineClose?.Invoke();
    public static void FireEvidenceCardAccepted(string cardId) => OnEvidenceCardAccepted?.Invoke(cardId);
    public static void FireEvidenceCardRejected(string cardId, int wrongAttempts) => OnEvidenceCardRejected?.Invoke(cardId, wrongAttempts);
    public static void FireEvidenceCardChallengeWon() => OnEvidenceCardChallengeWon?.Invoke();
    public static void FireEvidenceCardChallengeLost() => OnEvidenceCardChallengeLost?.Invoke();
}
