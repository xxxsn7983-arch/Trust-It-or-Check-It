using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KnowledgeCardViewController : MonoBehaviour
{
    [Header("Text Sequence")]
    [Tooltip("Drag Text/Text_1 and Text/Text_2 here in display order.")]
    [SerializeField] private List<GameObject> textObjects = new List<GameObject>();

    [Tooltip("Audio clips matching each text object. Length should match textObjects.")]
    [SerializeField] private List<AudioClip> textAudioClips = new List<AudioClip>();

    [Tooltip("AudioSource used to play text audio. If empty, one will be created.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Extra seconds to wait after each audio clip finishes before showing the next text.")]
    [SerializeField] private float delayAfterAudio = 1f;

    [Tooltip("Fallback: how long each text stays if no audio clip is assigned.")]
    [SerializeField] private float secondsPerText = 10f;

    [Tooltip("If false, only one text is visible at a time. If true, previous texts stay visible.")]
    [SerializeField] private bool keepPreviousTextsVisible = false;

    [Header("Start Button")]
    [Tooltip("Drag the start dialogue button GameObject here. It will be hidden on open and shown after the text sequence.")]
    [SerializeField] private GameObject startDialogueButton;

    [Header("Flow")]
    [Tooltip("Drag TeachingFlowManager here. If empty, the script finds it at runtime.")]
    [SerializeField] private TeachingFlowManager teachingFlowManager;

    [Tooltip("Automatically continue to the next teaching state after all text objects have been shown.")]
    [SerializeField] private bool autoContinueAfterSequence = true;

    private Coroutine sequenceRoutine;

    private void Awake()
    {
        ResolveTeachingFlowManager();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        StartSequence();
    }

    private void OnDisable()
    {
        StopSequence();
    }

    public void StartSequence()
    {
        StopSequence();
        sequenceRoutine = StartCoroutine(PlaySequence());
    }

    private void StopSequence()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }
    }

    private IEnumerator PlaySequence()
    {
        if (startDialogueButton != null)
        {
            startDialogueButton.SetActive(false);
        }

        SetTextState(-1);

        for (int i = 0; i < textObjects.Count; i++)
        {
            SetTextState(i);

            AudioClip clip = i < textAudioClips.Count ? textAudioClips[i] : null;
            if (clip != null && audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                yield return new WaitForSeconds(clip.length + Mathf.Max(0f, delayAfterAudio));
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(0f, secondsPerText));
            }
        }

        sequenceRoutine = null;

        if (autoContinueAfterSequence)
        {
            ContinueToNextState();
            yield break;
        }

        if (startDialogueButton != null)
        {
            startDialogueButton.SetActive(true);
        }
    }

    private void SetTextState(int activeIndex)
    {
        for (int i = 0; i < textObjects.Count; i++)
        {
            if (textObjects[i] == null) continue;

            bool shouldShow = keepPreviousTextsVisible
                ? i <= activeIndex
                : i == activeIndex;

            textObjects[i].SetActive(shouldShow);
        }
    }

    private void ContinueToNextState()
    {
        ResolveTeachingFlowManager();

        if (teachingFlowManager == null)
        {
            Debug.LogWarning("KnowledgeCardViewController: TeachingFlowManager is missing.", this);
            return;
        }

        teachingFlowManager.Continue();
    }

    private void ResolveTeachingFlowManager()
    {
        if (teachingFlowManager == null)
        {
            teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
        }
    }
}
