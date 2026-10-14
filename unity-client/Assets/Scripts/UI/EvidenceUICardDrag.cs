using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
[RequireComponent(typeof(Image))]
public class EvidenceUICardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Card")]
    [SerializeField] private string cardId = "card_1";
    [SerializeField] private bool isCorrect;

    [Header("Drag")]
    [SerializeField] private EvidenceUIDropRange dropRange;
    [SerializeField] private bool returnOutsideRange = true;
    [SerializeField] private float returnDuration = 0.16f;
    [SerializeField] private bool bringToFrontOnDrag = true;

    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private RectTransform parentRect;
    private Vector2 homeAnchoredPosition;
    private Quaternion homeRotation;
    private int homeSiblingIndex;
    private Vector2 pointerOffset;
    private bool dragging;
    private bool locked;
    private Coroutine returnRoutine;

    public string CardId => cardId;

    public void Configure(string newCardId, bool correct, EvidenceUIDropRange range)
    {
        if (!string.IsNullOrEmpty(newCardId))
        {
            cardId = newCardId;
        }

        isCorrect = correct;

        if (range != null)
        {
            dropRange = range;
        }

        CaptureHome();
    }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        parentRect = rectTransform.parent as RectTransform;

        if (dropRange == null)
        {
            dropRange = FindObjectOfType<EvidenceUIDropRange>();
        }

        CaptureHome();
    }

    public void CaptureHome()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        homeAnchoredPosition = rectTransform.anchoredPosition;
        homeRotation = rectTransform.localRotation;
        homeSiblingIndex = transform.GetSiblingIndex();
    }

    public bool IsCorrectFor(string correctCardId)
    {
        return isCorrect || (!string.IsNullOrEmpty(correctCardId) && cardId == correctCardId);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (locked)
        {
            return;
        }

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
            returnRoutine = null;
        }

        parentRect = rectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocalPosition))
        {
            pointerOffset = rectTransform.anchoredPosition - pointerLocalPosition;
        }

        dragging = true;

        if (bringToFrontOnDrag)
        {
            transform.SetAsLastSibling();
        }

        canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || locked || parentRect == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 pointerLocalPosition))
        {
            rectTransform.anchoredPosition = pointerLocalPosition + pointerOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || locked)
        {
            return;
        }

        dragging = false;
        canvasGroup.blocksRaycasts = true;

        bool handled = dropRange != null && dropRange.TryDrop(this, eventData.position, eventData.pressEventCamera);
        if (!handled && returnOutsideRange)
        {
            ReturnToHome();
        }
    }

    public void ReturnToHome()
    {
        locked = false;
        canvasGroup.blocksRaycasts = true;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }

        returnRoutine = StartCoroutine(ReturnToHomeRoutine());
    }

    public void LockTo(RectTransform targetRect, Camera eventCamera)
    {
        locked = true;
        dragging = false;
        canvasGroup.blocksRaycasts = true;

        if (targetRect == null || parentRect == null)
        {
            return;
        }

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, targetRect.position);
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, eventCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
    }

    private IEnumerator ReturnToHomeRoutine()
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        Quaternion startRotation = rectTransform.localRotation;
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = returnDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / returnDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, homeAnchoredPosition, t);
            rectTransform.localRotation = Quaternion.Slerp(startRotation, homeRotation, t);
            yield return null;
        }

        rectTransform.anchoredPosition = homeAnchoredPosition;
        rectTransform.localRotation = homeRotation;
        transform.SetSiblingIndex(homeSiblingIndex);
        returnRoutine = null;
    }
}
