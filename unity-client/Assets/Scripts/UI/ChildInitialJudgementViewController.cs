using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class ChildInitialJudgementViewController : MonoBehaviour
{
    [Serializable]
    public class ChoiceBinding
    {
        [Tooltip("Drag ChoiceButton_A/B/C here.")]
        public Button choiceButton;

        [Tooltip("Drag the Selected_Effect under this choice button here.")]
        public GameObject selectedEffect;

        [Tooltip("This text is recorded as the child's initial judgement.")]
        public string judgementText;
    }

    [Header("Choices")]
    [Tooltip("Add ChoiceButton_A, ChoiceButton_B, ChoiceButton_C in order.")]
    [SerializeField] private List<ChoiceBinding> choices = new List<ChoiceBinding>();

    [Tooltip("Seconds to keep the selected effect visible before moving to the next state.")]
    [SerializeField] private float transitionDelay = 1f;

    [Header("Audio")]
    [Tooltip("Audio clip to play when this view opens.")]
    [SerializeField] private AudioClip introAudioClip;

    [Tooltip("AudioSource used to play audio. If empty, one will be created.")]
    [SerializeField] private AudioSource audioSource;

    [Header("Flow")]
    [Tooltip("Drag TeachingFlowManager here. If empty, the script tries to find it at runtime.")]
    [SerializeField] private TeachingFlowManager teachingFlowManager;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    private readonly List<ButtonActionBinding> runtimeBindings = new List<ButtonActionBinding>();
    private Coroutine transitionRoutine;
    private bool selectionLocked;

    private struct ButtonActionBinding
    {
        public Button button;
        public UnityAction action;
    }

    private void Awake()
    {
        if (teachingFlowManager == null)
        {
            teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void OnEnable()
    {
        selectionLocked = false;
        ResetSelectedEffects();
        BindButtons();
        PlayIntroAudio();
    }

    private void OnDisable()
    {
        StopTransition();
        UnbindButtons();
    }

    public void SelectChoiceByIndex(int index)
    {
        if (index < 0 || index >= choices.Count) return;
        SelectChoice(choices[index]);
    }

    private void BindButtons()
    {
        UnbindButtons();

        for (int i = 0; i < choices.Count; i++)
        {
            ChoiceBinding choice = choices[i];
            if (choice == null || choice.choiceButton == null) continue;

            UnityAction action = () => SelectChoice(choice);
            choice.choiceButton.onClick.AddListener(action);
            runtimeBindings.Add(new ButtonActionBinding
            {
                button = choice.choiceButton,
                action = action
            });
        }
    }

    private void UnbindButtons()
    {
        for (int i = 0; i < runtimeBindings.Count; i++)
        {
            ButtonActionBinding binding = runtimeBindings[i];
            if (binding.button != null && binding.action != null)
            {
                binding.button.onClick.RemoveListener(binding.action);
            }
        }

        runtimeBindings.Clear();
    }

    private void SelectChoice(ChoiceBinding selectedChoice)
    {
        if (selectionLocked || selectedChoice == null) return;

        selectionLocked = true;
        ResetSelectedEffects();

        if (selectedChoice.selectedEffect != null)
        {
            selectedChoice.selectedEffect.SetActive(true);
        }

        if (logActions)
        {
            Debug.Log($"ChildInitialJudgementViewController: selected {selectedChoice.judgementText}", this);
        }

        StopTransition();
        transitionRoutine = StartCoroutine(SubmitChoiceAfterDelay(selectedChoice.judgementText));
    }

    private IEnumerator SubmitChoiceAfterDelay(string judgementText)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, transitionDelay));

        if (teachingFlowManager == null)
        {
            teachingFlowManager = FindObjectOfType<TeachingFlowManager>();
        }

        if (teachingFlowManager != null)
        {
            teachingFlowManager.SubmitInitialJudgement(judgementText);
        }
        else
        {
            Debug.LogWarning("ChildInitialJudgementViewController: TeachingFlowManager is missing.", this);
        }
    }

    private void ResetSelectedEffects()
    {
        for (int i = 0; i < choices.Count; i++)
        {
            if (choices[i]?.selectedEffect != null)
            {
                choices[i].selectedEffect.SetActive(false);
            }
        }
    }

    private void StopTransition()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    private void PlayIntroAudio()
    {
        if (introAudioClip != null && audioSource != null)
        {
            audioSource.clip = introAudioClip;
            audioSource.Play();
        }
    }
}
