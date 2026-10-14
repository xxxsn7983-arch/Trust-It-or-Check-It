using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class EvidenceUIDropRange : MonoBehaviour
{
    [Header("Judge")]
    [SerializeField] private string correctCardId = "card_1";
    [SerializeField] private int maxWrongAttempts = 2;

    [Header("Visual")]
    [SerializeField] private bool flashWrongColor = true;
    [SerializeField] private Color wrongFlashColor = new Color(1f, 0.15f, 0.1f, 0.35f);

    [Header("Flow")]
    [SerializeField] private bool notifyTeachingFlowOnVictory = true;
    [SerializeField] private bool notifyTeachingFlowOnFailure = false;
    [SerializeField] private TeachingFlowManager teachingFlowManager;

    [Header("Victory Card")]
    [SerializeField] private bool hideAcceptedCardOnVictory = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onVictory = new UnityEvent();
    [SerializeField] private UnityEvent onFailure = new UnityEvent();
    [SerializeField] private UnityEvent onWrongDrop = new UnityEvent();

    private RectTransform rectTransform;
    private Image image;
    private Color normalColor;
    private int wrongAttempts;
    private bool resolved;

    public bool IsResolved => resolved;
    public string CorrectCardId => correctCardId;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();

        if (image != null)
        {
            normalColor = image.color;
        }

        if (teachingFlowManager == null)
        {
            teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
        }
    }

    public bool TryDrop(EvidenceUICardDrag card, Vector2 screenPosition, Camera eventCamera)
    {
        if (card == null || resolved)
        {
            return false;
        }

        if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera))
        {
            return false;
        }

        if (card.IsCorrectFor(correctCardId))
        {
            ResolveVictory(card, eventCamera);
            return true;
        }

        ResolveWrong(card);
        return true;
    }

    private void ResolveVictory(EvidenceUICardDrag card, Camera eventCamera)
    {
        resolved = true;
        wrongAttempts = 0;
        string acceptedCardId = card.CardId;

        if (hideAcceptedCardOnVictory)
        {
            card.gameObject.SetActive(false);
        }
        else
        {
            card.LockTo(rectTransform, eventCamera);
        }

        Debug.Log($"[EvidenceDropRange] Accepted correct card \"{acceptedCardId}\".");
        GameEvents.FireEvidenceCardAccepted(acceptedCardId);
        GameEvents.FireEvidenceCardChallengeWon();
        onVictory?.Invoke();

        if (notifyTeachingFlowOnVictory)
        {
            if (teachingFlowManager == null)
            {
                teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
            }

            if (teachingFlowManager != null)
            {
                teachingFlowManager.SubmitEvidence(acceptedCardId);
            }
        }
    }

    private void ResolveWrong(EvidenceUICardDrag card)
    {
        wrongAttempts++;
        card.ReturnToHome();
        FlashWrong();

        Debug.Log($"[EvidenceDropRange] 拖错！卡片 \"{card.CardId}\"，已错 {wrongAttempts}/{Mathf.Max(1, maxWrongAttempts)} 次");
        GameEvents.FireEvidenceCardRejected(card.CardId, wrongAttempts);
        onWrongDrop?.Invoke();

        if (wrongAttempts >= Mathf.Max(1, maxWrongAttempts))
        {
            ResolveFailure();
        }
    }

    private void ResolveFailure()
    {
        resolved = true;

        Debug.Log($"[EvidenceDropRange] 失败！错误次数已达上限 ({wrongAttempts} 次)");
        GameEvents.FireEvidenceCardChallengeLost();
        GameEvents.FireTeachingChoiceSelected(TeachingChoice.EVIDENCE_INSUFFICIENT, "evidence_card_failed");
        onFailure?.Invoke();

        if (notifyTeachingFlowOnFailure)
        {
            if (teachingFlowManager == null)
            {
                teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
            }

            if (teachingFlowManager != null)
            {
                teachingFlowManager.EvidenceInsufficient();
            }
        }
    }

    private void FlashWrong()
    {
        if (!flashWrongColor || image == null)
        {
            return;
        }

        CancelInvoke(nameof(RestoreColor));
        image.color = wrongFlashColor;
        Invoke(nameof(RestoreColor), 0.18f);
    }

    private void RestoreColor()
    {
        if (image != null)
        {
            image.color = normalColor;
        }
    }
}
