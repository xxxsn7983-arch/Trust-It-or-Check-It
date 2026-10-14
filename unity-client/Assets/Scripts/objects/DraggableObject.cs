using UnityEngine;

/// <summary>
/// 可拖拽物体 - 统一触摸/鼠标拖拽逻辑，缓存 Camera.main
/// </summary>
public class DraggableObject : MonoBehaviour
{
    private Vector3 offset;
    private bool isDragging = false;
    private int draggingFingerId = -1;
    private Camera mainCam;

    void Awake()
    {
        mainCam = Camera.main;
    }

    void Update()
    {
        if (Input.touchCount == 0) return;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            Vector3 touchWorld = GetWorldPosition(touch.position);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    TryBeginDrag(touchWorld, touch.fingerId);
                    break;

                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (isDragging && touch.fingerId == draggingFingerId)
                    {
                        ApplyDrag(touchWorld);
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    if (isDragging && touch.fingerId == draggingFingerId)
                    {
                        EndDrag();
                    }
                    break;
            }
        }
    }

    void OnMouseDown()
    {
        Vector3 mouseWorld = GetWorldPosition(Input.mousePosition);
        offset = transform.position - new Vector3(mouseWorld.x, mouseWorld.y, transform.position.z);
        isDragging = true;
    }

    void OnMouseDrag()
    {
        if (isDragging)
        {
            ApplyDrag(GetWorldPosition(Input.mousePosition));
        }
    }

    void OnMouseUp()
    {
        EndDrag();
    }

    private void TryBeginDrag(Vector3 worldPos, int fingerId)
    {
        RaycastHit2D hit = Physics2D.Raycast(new Vector2(worldPos.x, worldPos.y), Vector2.zero);
        if (hit.collider != null && hit.collider.gameObject == gameObject)
        {
            offset = transform.position - new Vector3(worldPos.x, worldPos.y, transform.position.z);
            isDragging = true;
            draggingFingerId = fingerId;
        }
    }

    private void ApplyDrag(Vector3 worldPos)
    {
        transform.position = new Vector3(worldPos.x, worldPos.y, transform.position.z) + offset;
    }

    private void EndDrag()
    {
        isDragging = false;
        draggingFingerId = -1;
    }

    private Vector3 GetWorldPosition(Vector3 screenPos)
    {
        return mainCam.ScreenToWorldPoint(screenPos);
    }
}
