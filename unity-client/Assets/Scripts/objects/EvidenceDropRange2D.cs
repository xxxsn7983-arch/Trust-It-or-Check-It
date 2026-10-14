using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(BoxCollider2D))]
public class EvidenceDropRange2D : MonoBehaviour
{
    [Header("Judge")]
    [SerializeField] private string correctCardId = "card_1";
    [SerializeField] private int maxWrongAttempts = 2;

    [Header("Range")]
    [SerializeField] private Vector2 rangeSize = new Vector2(5f, 2.6f);
    [SerializeField] private bool showRangeVisual = true;
    [SerializeField] private Color rangeColor = new Color(0.25f, 0.85f, 0.35f, 0.22f);
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

    private BoxCollider2D rangeCollider;
    private SpriteRenderer visualRenderer;
    private int wrongAttempts;
    private bool resolved;

    public bool IsResolved => resolved;
    public string CorrectCardId => correctCardId;

    private void Awake()
    {
        rangeCollider = GetComponent<BoxCollider2D>();
        rangeCollider.isTrigger = true;
        rangeCollider.size = rangeSize;

        if (teachingFlowManager == null)
        {
            teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
        }

        BuildVisual();
    }

    private void OnValidate()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            collider.isTrigger = true;
            collider.size = rangeSize;
        }

        if (visualRenderer != null)
        {
            visualRenderer.transform.localScale = new Vector3(rangeSize.x, rangeSize.y, 1f);
            visualRenderer.color = rangeColor;
            visualRenderer.gameObject.SetActive(showRangeVisual);
        }
    }

    public bool TryDrop(EvidenceSceneCardDrag2D card)
    {
        if (card == null || resolved)
        {
            return false;
        }

        if (!Contains(card.transform.position))
        {
            return false;
        }

        if (card.IsCorrectFor(correctCardId))
        {
            ResolveVictory(card);
            return true;
        }

        ResolveWrong(card);
        return true;
    }

    private bool Contains(Vector3 worldPosition)
    {
        if (rangeCollider == null)
        {
            rangeCollider = GetComponent<BoxCollider2D>();
        }

        return rangeCollider != null && rangeCollider.OverlapPoint(worldPosition);
    }

    private void ResolveVictory(EvidenceSceneCardDrag2D card)
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
            card.LockAt(transform.position);
        }

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

    private void ResolveWrong(EvidenceSceneCardDrag2D card)
    {
        wrongAttempts++;
        card.ReturnToHome();
        FlashWrong();

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

    private void BuildVisual()
    {
        if (!showRangeVisual)
        {
            return;
        }

        Transform existingVisual = transform.Find("RangeVisual");
        GameObject visualObject = existingVisual != null
            ? existingVisual.gameObject
            : new GameObject("RangeVisual");

        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = Vector3.zero;
        visualObject.transform.localRotation = Quaternion.identity;
        visualObject.transform.localScale = new Vector3(rangeSize.x, rangeSize.y, 1f);

        visualRenderer = visualObject.GetComponent<SpriteRenderer>();
        if (visualRenderer == null)
        {
            visualRenderer = visualObject.AddComponent<SpriteRenderer>();
        }

        visualRenderer.sprite = GetSolidSprite();
        visualRenderer.color = rangeColor;
        visualRenderer.sortingOrder = 20;
        visualObject.SetActive(true);
    }

    private void FlashWrong()
    {
        if (visualRenderer == null)
        {
            return;
        }

        CancelInvoke(nameof(RestoreRangeColor));
        visualRenderer.color = wrongFlashColor;
        Invoke(nameof(RestoreRangeColor), 0.18f);
    }

    private void RestoreRangeColor()
    {
        if (visualRenderer != null)
        {
            visualRenderer.color = rangeColor;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = resolved ? Color.gray : Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(rangeSize.x, rangeSize.y, 0.1f));
    }

    private static Sprite solidSprite;

    private static Sprite GetSolidSprite()
    {
        if (solidSprite != null)
        {
            return solidSprite;
        }

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        solidSprite.name = "EvidenceDropRangeSolid";
        return solidSprite;
    }
}
