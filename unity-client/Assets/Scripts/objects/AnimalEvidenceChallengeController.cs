using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AnimalEvidenceChallengeController : MonoBehaviour
{
    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool disableTriggerAfterIntro = true;

    [Header("Challenge Gate")]
    [Tooltip("Drag the original range detector here, for example DragEnableArea/Area_1.")]
    [SerializeField] private GameObject originalRangeDetector;
    [Tooltip("Optional. Drag the visible/drop target object here if it should stay hidden until the owl arrives.")]
    [SerializeField] private GameObject dropRangeObject;
    [SerializeField] private bool disableOriginalRangeOnStart = true;
    [SerializeField] private bool disableDropRangeOnStart = true;

    [Header("Owl Fly In")]
    [SerializeField] private GameObject owlObject;
    [SerializeField] private Animator owlAnimator;
    [SerializeField] private string owlFlyInTrigger = "FlyIn";
    [SerializeField] private string owlFlyInIdleTrigger = "FlyToIdle";
    [SerializeField] private Transform owlFlyInStart;
    [SerializeField] private Transform owlFlyInTarget;
    [SerializeField] private float owlFlyInSeconds = 1.2f;

    [Header("Owl Idle Patrol")]
    [SerializeField] private bool enableOwlIdlePatrol = true;
    [SerializeField] private float owlIdlePatrolIntervalSeconds = 20f;
    [SerializeField] private string owlPatrolFlyTrigger = "FlyIn";
    [SerializeField] private string owlPatrolIdleTrigger = "FlyToIdle";
    [SerializeField] private Transform owlPatrolPointA;
    [SerializeField] private Transform owlPatrolPointB;
    [SerializeField] private float owlPatrolSegmentSeconds = 1.2f;

    [Header("Owl Facing")]
    [SerializeField] private bool autoFlipOwlToMoveDirection = true;
    [SerializeField] private bool owlFacesRightByDefault = true;
    [SerializeField] private SpriteRenderer owlSpriteRenderer;
    [SerializeField] private float owlFlipDeadZone = 0.01f;

    [Header("Owl Fly Away")]
    [SerializeField] private string owlFlyAwayTrigger = "FlyAway";
    [SerializeField] private string owlFlyAwayIdleTrigger = "FlyToIdle";
    [SerializeField] private Transform owlFlyAwayTarget;
    [SerializeField] private float owlFlyAwaySeconds = 1.2f;
    [SerializeField] private bool hideOwlAfterFlyAway = true;

    [Header("Mouse Before Victory")]
    [SerializeField] private GameObject mouseObject;
    [SerializeField] private Animator mouseAnimator;
    [SerializeField] private string mouseLayTrigger = "Defence";
    [SerializeField] private string mouseLayStateName = "MouseLay";
    [SerializeField] private float mouseLayFallbackSeconds = 1f;

    [Header("Failure Fly To Mouse")]
    [Tooltip("When enabled, the owl flies to the mouse on failure. Disable it only if you want to use a separate eagle object.")]
    [SerializeField] private bool useOwlForFailure = true;
    [SerializeField] private GameObject eagleObject;
    [SerializeField] private Animator eagleAnimator;
    [SerializeField] private string eagleFlyToMouseTrigger = "FlyToMouse";
    [SerializeField] private string eagleFlyIdleTrigger = "FlyToIdle";
    [SerializeField] private Transform eagleFlyStart;
    [SerializeField] private Transform eagleFlyTarget;
    [SerializeField] private float eagleFlySeconds = 1.2f;
    [SerializeField] private bool showEagleOnFailure = true;

    [Header("Outcome UI")]
    [SerializeField] private GameObject successPanel;
    [SerializeField] private GameObject failurePanel;
    [SerializeField] private bool hideOutcomePanelsOnStart = true;

    [Header("Challenge End")]
    [SerializeField] private bool failOnFirstWrongDrop = true;
    [SerializeField] private GameObject cardsRoot;
    [SerializeField] private bool disableCardsOnOutcome = true;
    [SerializeField] private bool disableRangeOnOutcome = true;

    [Header("Motion")]
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private bool introPlayed;
    private bool outcomeStarted;
    private Collider2D triggerCollider;
    private Coroutine introRoutine;
    private Coroutine outcomeRoutine;
    private Coroutine owlIdlePatrolRoutine;
    private Vector3 owlRestPosition;
    private bool hasOwlRestPosition;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;

        if (owlAnimator == null && owlObject != null)
        {
            owlAnimator = owlObject.GetComponent<Animator>();
        }

        if (owlSpriteRenderer == null && owlObject != null)
        {
            owlSpriteRenderer = owlObject.GetComponent<SpriteRenderer>();

            if (owlSpriteRenderer == null)
            {
                owlSpriteRenderer = owlObject.GetComponentInChildren<SpriteRenderer>();
            }
        }

        if (eagleAnimator == null && eagleObject != null)
        {
            eagleAnimator = eagleObject.GetComponent<Animator>();
        }

        if (mouseAnimator == null && mouseObject != null)
        {
            mouseAnimator = mouseObject.GetComponent<Animator>();
        }
    }

    private void Start()
    {
        if (disableOriginalRangeOnStart && originalRangeDetector != null)
        {
            originalRangeDetector.SetActive(false);
        }

        if (disableDropRangeOnStart && dropRangeObject != null)
        {
            dropRangeObject.SetActive(false);
        }

        if (hideOutcomePanelsOnStart)
        {
            SetOutcomePanels(false, false);
        }

        if (eagleObject != null && showEagleOnFailure)
        {
            eagleObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        GameEvents.OnEvidenceCardChallengeWon += HandleChallengeWon;
        GameEvents.OnEvidenceCardChallengeLost += HandleChallengeLost;
        GameEvents.OnEvidenceCardRejected += HandleCardRejected;
    }

    private void OnDisable()
    {
        GameEvents.OnEvidenceCardChallengeWon -= HandleChallengeWon;
        GameEvents.OnEvidenceCardChallengeLost -= HandleChallengeLost;
        GameEvents.OnEvidenceCardRejected -= HandleCardRejected;
        StopOwlIdlePatrol();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(playerTag))
        {
            return;
        }

        PlayIntro();
    }

    public void PlayIntro()
    {
        if ((triggerOnce && introPlayed) || introRoutine != null)
        {
            return;
        }

        introRoutine = StartCoroutine(PlayIntroRoutine());
    }

    public void PlayVictorySequence()
    {
        if (outcomeStarted)
        {
            return;
        }

        outcomeStarted = true;
        StopOwlIdlePatrol();
        outcomeRoutine = StartCoroutine(PlayVictoryRoutine());
    }

    public void PlayFailureSequence()
    {
        if (outcomeStarted)
        {
            return;
        }

        outcomeStarted = true;
        StopOwlIdlePatrol();
        outcomeRoutine = StartCoroutine(PlayFailureRoutine());
    }

    private IEnumerator PlayIntroRoutine()
    {
        introPlayed = true;

        if (owlObject != null)
        {
            owlObject.SetActive(true);
        }

        PlayAnimatorTrigger(owlAnimator, owlFlyInTrigger);
        yield return MoveObject(owlObject, owlFlyInStart, owlFlyInTarget, owlFlyInSeconds);
        PlayAnimatorTrigger(owlAnimator, owlFlyInIdleTrigger);
        CaptureOwlRestPosition();
        StartOwlIdlePatrol();

        if (originalRangeDetector != null)
        {
            originalRangeDetector.SetActive(true);
        }

        if (dropRangeObject != null)
        {
            dropRangeObject.SetActive(true);
        }

        if (disableTriggerAfterIntro && triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        introRoutine = null;
    }

    private IEnumerator PlayVictoryRoutine()
    {
        yield return null;
        PrepareForOutcome();

        yield return PlayMouseLayRoutine();

        PlayAnimatorTrigger(owlAnimator, owlFlyAwayTrigger);
        yield return MoveObject(owlObject, null, owlFlyAwayTarget, owlFlyAwaySeconds);
        PlayAnimatorTrigger(owlAnimator, owlFlyAwayIdleTrigger);

        if (hideOwlAfterFlyAway && owlObject != null)
        {
            owlObject.SetActive(false);
        }

        SetOutcomePanels(true, false);
        outcomeRoutine = null;
    }

    private IEnumerator PlayFailureRoutine()
    {
        yield return null;
        PrepareForOutcome();

        GameObject failureFlyObject = useOwlForFailure ? owlObject : eagleObject;
        Animator failureFlyAnimator = useOwlForFailure ? owlAnimator : eagleAnimator;
        Transform failureFlyStart = useOwlForFailure ? null : eagleFlyStart;

        if (failureFlyObject != null && (showEagleOnFailure || useOwlForFailure))
        {
            failureFlyObject.SetActive(true);
        }

        PlayAnimatorTrigger(failureFlyAnimator, eagleFlyToMouseTrigger);
        yield return MoveObject(failureFlyObject, failureFlyStart, eagleFlyTarget, eagleFlySeconds);
        PlayAnimatorTrigger(failureFlyAnimator, eagleFlyIdleTrigger);

        SetOutcomePanels(false, true);
        outcomeRoutine = null;
    }

    private IEnumerator PlayMouseLayRoutine()
    {
        if (mouseObject == null && mouseAnimator == null)
        {
            yield break;
        }

        if (mouseObject != null)
        {
            mouseObject.SetActive(true);
        }

        PlayAnimatorTrigger(mouseAnimator, mouseLayTrigger);
        yield return WaitForAnimatorStateOnce(mouseAnimator, mouseLayStateName, mouseLayFallbackSeconds);
    }

    private void HandleChallengeWon()
    {
        PlayVictorySequence();
    }

    private void HandleChallengeLost()
    {
        PlayFailureSequence();
    }

    private void HandleCardRejected(string cardId, int wrongAttempts)
    {
        if (failOnFirstWrongDrop && wrongAttempts >= 1)
        {
            PlayFailureSequence();
        }
    }

    private void PrepareForOutcome()
    {
        StopOwlIdlePatrol();
        SetOutcomePanels(false, false);

        if (disableCardsOnOutcome && cardsRoot != null)
        {
            cardsRoot.SetActive(false);
        }

        if (disableRangeOnOutcome)
        {
            if (originalRangeDetector != null)
            {
                originalRangeDetector.SetActive(false);
            }

            if (dropRangeObject != null)
            {
                dropRangeObject.SetActive(false);
            }
        }
    }

    private void CaptureOwlRestPosition()
    {
        if (owlFlyInTarget != null)
        {
            owlRestPosition = owlFlyInTarget.position;
            hasOwlRestPosition = true;
            return;
        }

        if (owlObject != null)
        {
            owlRestPosition = owlObject.transform.position;
            hasOwlRestPosition = true;
        }
    }

    private void StartOwlIdlePatrol()
    {
        if (!enableOwlIdlePatrol
            || outcomeStarted
            || owlIdlePatrolRoutine != null
            || owlObject == null
            || owlPatrolPointA == null
            || owlPatrolPointB == null)
        {
            return;
        }

        owlIdlePatrolRoutine = StartCoroutine(PlayOwlIdlePatrolRoutine());
    }

    private void StopOwlIdlePatrol()
    {
        if (owlIdlePatrolRoutine == null)
        {
            return;
        }

        StopCoroutine(owlIdlePatrolRoutine);
        owlIdlePatrolRoutine = null;
    }

    private IEnumerator PlayOwlIdlePatrolRoutine()
    {
        while (!outcomeStarted)
        {
            float interval = Mathf.Max(0f, owlIdlePatrolIntervalSeconds);
            float elapsed = 0f;

            while (elapsed < interval && !outcomeStarted)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (outcomeStarted)
            {
                break;
            }

            if (owlPatrolPointA == null || owlPatrolPointB == null)
            {
                break;
            }

            if (!hasOwlRestPosition)
            {
                CaptureOwlRestPosition();
            }

            PlayAnimatorTrigger(owlAnimator, owlPatrolFlyTrigger);
            yield return MoveObject(owlObject, null, owlPatrolPointA, owlPatrolSegmentSeconds);

            if (outcomeStarted)
            {
                break;
            }

            yield return MoveObject(owlObject, null, owlPatrolPointB, owlPatrolSegmentSeconds);

            if (outcomeStarted)
            {
                break;
            }

            if (hasOwlRestPosition)
            {
                yield return MoveObjectToPosition(owlObject, owlRestPosition, owlPatrolSegmentSeconds);
            }

            if (!outcomeStarted)
            {
                PlayAnimatorTrigger(owlAnimator, owlPatrolIdleTrigger);
            }
        }

        owlIdlePatrolRoutine = null;
    }

    private IEnumerator MoveObject(GameObject targetObject, Transform start, Transform end, float seconds)
    {
        if (targetObject == null)
        {
            yield return WaitSeconds(seconds);
            yield break;
        }

        if (start != null)
        {
            targetObject.transform.position = start.position;
        }

        if (end == null)
        {
            yield return WaitSeconds(seconds);
            yield break;
        }

        yield return MoveObjectToPosition(targetObject, end.position, seconds);
    }

    private IEnumerator MoveObjectToPosition(GameObject targetObject, Vector3 targetPosition, float seconds)
    {
        if (targetObject == null)
        {
            yield return WaitSeconds(seconds);
            yield break;
        }

        Vector3 from = targetObject.transform.position;
        Vector3 to = targetPosition;
        FaceOwlForMove(targetObject, to);

        float duration = Mathf.Max(0f, seconds);

        if (duration <= 0f)
        {
            targetObject.transform.position = to;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float curvedT = moveCurve != null ? moveCurve.Evaluate(t) : t;
            targetObject.transform.position = Vector3.LerpUnclamped(from, to, curvedT);
            yield return null;
        }

        targetObject.transform.position = to;
    }

    private void FaceOwlForMove(GameObject targetObject, Vector3 targetPosition)
    {
        if (!autoFlipOwlToMoveDirection || targetObject == null || targetObject != owlObject)
        {
            return;
        }

        float deltaX = targetPosition.x - targetObject.transform.position.x;
        if (Mathf.Abs(deltaX) <= owlFlipDeadZone)
        {
            return;
        }

        bool movingRight = deltaX > 0f;
        if (owlSpriteRenderer != null)
        {
            owlSpriteRenderer.flipX = owlFacesRightByDefault ? !movingRight : movingRight;
        }
    }

    private static IEnumerator WaitSeconds(float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }
    }

    private static IEnumerator WaitForAnimatorStateOnce(Animator animator, string stateName, float fallbackSeconds)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            yield return WaitSeconds(fallbackSeconds);
            yield break;
        }

        const int layer = 0;
        float enterTimeout = Mathf.Max(0.5f, fallbackSeconds);
        float elapsed = 0f;

        while (elapsed < enterTimeout)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (stateInfo.IsName(stateName))
            {
                break;
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        AnimatorStateInfo enteredState = animator.GetCurrentAnimatorStateInfo(layer);
        if (!enteredState.IsName(stateName))
        {
            yield return WaitSeconds(fallbackSeconds);
            yield break;
        }

        while (true)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(layer);
            if (!stateInfo.IsName(stateName))
            {
                yield break;
            }

            if (stateInfo.normalizedTime >= 1f && !animator.IsInTransition(layer))
            {
                yield break;
            }

            yield return null;
        }
    }

    private static void PlayAnimatorTrigger(Animator animator, string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        animator.ResetTrigger(triggerName);
        animator.SetTrigger(triggerName);
    }

    private void SetOutcomePanels(bool success, bool failure)
    {
        if (successPanel != null)
        {
            successPanel.SetActive(success);
        }

        if (failurePanel != null)
        {
            failurePanel.SetActive(failure);
        }
    }
}
