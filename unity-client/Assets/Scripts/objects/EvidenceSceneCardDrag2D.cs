using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class EvidenceSceneCardDrag2D : MonoBehaviour
{
    [Header("Card")]
    [SerializeField] private string cardId = "card_1";
    [SerializeField] private bool isCorrect;

    [Header("Drag")]
    [SerializeField] private EvidenceDropRange2D dropRange;
    [SerializeField] private bool returnOutsideRange = true;
    [SerializeField] private float returnDuration = 0.16f;
    [SerializeField] private int dragSortingOrder = 50;

    private Camera mainCamera;
    private Collider2D cardCollider;
    private SpriteRenderer spriteRenderer;
    private Vector3 homePosition;
    private Quaternion homeRotation;
    private int homeSortingOrder;
    private Vector3 dragOffset;
    private bool dragging;
    private bool locked;
    private int draggingFingerId = -1;
    private Coroutine returnRoutine;

    public string CardId => cardId;

    public void Configure(string newCardId, bool correct, EvidenceDropRange2D range)
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

        FitColliderToSprite();
        CaptureHome();
    }

    private void Awake()
    {
        mainCamera = Camera.main;
        cardCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        FitColliderToSprite();
        CaptureHome();

        if (dropRange == null)
        {
            dropRange = FindObjectOfType<EvidenceDropRange2D>();
        }
    }

    private void Update()
    {
        HandleTouchInput();
    }

    private void OnMouseDown()
    {
        BeginDrag(Input.mousePosition);
    }

    private void OnMouseDrag()
    {
        DragTo(Input.mousePosition);
    }

    private void OnMouseUp()
    {
        EndDrag();
    }

    public void CaptureHome()
    {
        homePosition = transform.position;
        homeRotation = transform.rotation;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        homeSortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 0;
    }

    public bool IsCorrectFor(string correctCardId)
    {
        return isCorrect || (!string.IsNullOrEmpty(correctCardId) && cardId == correctCardId);
    }

    public void ReturnToHome()
    {
        locked = false;

        if (returnRoutine != null)
        {
            StopCoroutine(returnRoutine);
        }

        returnRoutine = StartCoroutine(ReturnToHomeRoutine());
    }

    public void LockAt(Vector3 worldPosition)
    {
        locked = true;
        dragging = false;
        draggingFingerId = -1;
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z);

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = dragSortingOrder;
        }
    }

    private void BeginDrag(Vector3 screenPosition)
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

        Vector3 worldPosition = GetWorldPosition(screenPosition);
        dragOffset = transform.position - new Vector3(worldPosition.x, worldPosition.y, transform.position.z);
        dragging = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = dragSortingOrder;
        }
    }

    private void DragTo(Vector3 screenPosition)
    {
        if (!dragging || locked)
        {
            return;
        }

        Vector3 worldPosition = GetWorldPosition(screenPosition);
        transform.position = new Vector3(worldPosition.x, worldPosition.y, transform.position.z) + dragOffset;
    }

    private void EndDrag()
    {
        if (!dragging || locked)
        {
            return;
        }

        dragging = false;
        draggingFingerId = -1;

        bool handled = dropRange != null && dropRange.TryDrop(this);
        if (!handled && returnOutsideRange)
        {
            ReturnToHome();
        }
    }

    private void HandleTouchInput()
    {
        if (Input.touchCount == 0 || locked)
        {
            return;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Began)
            {
                TryBeginTouchDrag(touch);
            }
            else if ((touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                && dragging
                && touch.fingerId == draggingFingerId)
            {
                DragTo(touch.position);
            }
            else if ((touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                && dragging
                && touch.fingerId == draggingFingerId)
            {
                EndDrag();
            }
        }
    }

    private void TryBeginTouchDrag(Touch touch)
    {
        Vector3 worldPosition = GetWorldPosition(touch.position);
        RaycastHit2D hit = Physics2D.Raycast(worldPosition, Vector2.zero);

        if (hit.collider == cardCollider)
        {
            draggingFingerId = touch.fingerId;
            BeginDrag(touch.position);
        }
    }

    private IEnumerator ReturnToHomeRoutine()
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float elapsed = 0f;

        while (elapsed < returnDuration)
        {
            elapsed += Time.deltaTime;
            float t = returnDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / returnDuration);
            transform.position = Vector3.Lerp(startPosition, homePosition, t);
            transform.rotation = Quaternion.Slerp(startRotation, homeRotation, t);
            yield return null;
        }

        transform.position = homePosition;
        transform.rotation = homeRotation;

        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = homeSortingOrder;
        }

        returnRoutine = null;
    }

    private Vector3 GetWorldPosition(Vector3 screenPosition)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return transform.position;
        }

        Vector3 worldPosition = mainCamera.ScreenToWorldPoint(screenPosition);
        worldPosition.z = transform.position.z;
        return worldPosition;
    }

    private void FitColliderToSprite()
    {
        if (cardCollider == null)
        {
            cardCollider = GetComponent<Collider2D>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        BoxCollider2D boxCollider = cardCollider as BoxCollider2D;
        if (boxCollider == null || spriteRenderer == null || spriteRenderer.sprite == null)
        {
            return;
        }

        boxCollider.size = spriteRenderer.sprite.bounds.size;
        boxCollider.offset = spriteRenderer.sprite.bounds.center;
    }
}
