using UnityEngine;

public class CameraFollowAutoLimit : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 5f;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private bool snapToTargetOnStart = true;

    [Header("Bounds")]
    [SerializeField] private SpriteRenderer backgroundSprite;

    private float leftLimit;
    private float rightLimit;
    private float bottomLimit;
    private float topLimit;
    private bool hasBounds;
    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        ResolveTarget();
        CalculateBounds();

        if (snapToTargetOnStart && target != null)
        {
            transform.position = GetTargetCameraPosition();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            ResolveTarget();
        }

        if (target == null) return;

        Vector3 targetPos = GetTargetCameraPosition();
        float lerpAmount = Mathf.Clamp01(smoothSpeed * Time.deltaTime);
        transform.position = smoothSpeed <= 0f
            ? targetPos
            : Vector3.Lerp(transform.position, targetPos, lerpAmount);
    }

    private void CalculateBounds()
    {
        if (backgroundSprite == null || cam == null)
        {
            hasBounds = false;
            return;
        }

        Bounds bounds = backgroundSprite.bounds;
        float camHalfHeight = cam.orthographicSize;
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        leftLimit = bounds.min.x + camHalfWidth;
        rightLimit = bounds.max.x - camHalfWidth;
        bottomLimit = bounds.min.y + camHalfHeight;
        topLimit = bounds.max.y - camHalfHeight;

        if (leftLimit > rightLimit)
        {
            leftLimit = rightLimit = bounds.center.x;
        }

        if (bottomLimit > topLimit)
        {
            bottomLimit = topLimit = bounds.center.y;
        }

        hasBounds = true;
    }

    private Vector3 GetTargetCameraPosition()
    {
        Vector3 position = target.position + offset;

        if (hasBounds)
        {
            position.x = Mathf.Clamp(position.x, leftLimit, rightLimit);
            position.y = Mathf.Clamp(position.y, bottomLimit, topLimit);
        }

        return position;
    }

    private void ResolveTarget()
    {
        if (target != null) return;

        PlayerController player = FindObjectOfType<PlayerController>();
        if (player != null)
        {
            target = player.transform;
        }
    }
}
