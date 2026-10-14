using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class IntroPhenomenonViewController : MonoBehaviour
{
    [Serializable]
    public class PhenomenonCardBinding
    {
        [Tooltip("Optional id used only for logs, for example plant_motion_card_01.")]
        public string cardId;

        [Tooltip("Drag the clickable card Button here, for example PhenomenonImage_1.")]
        public Button cardButton;

        [Tooltip("Drag the matching selected detail object under Selected_Images here.")]
        public GameObject selectedImage;
    }

    [Header("Robot Text Sequence")]
    [Tooltip("Drag RobotText_1, RobotText_2, RobotText_3 here in display order.")]
    [SerializeField] private List<GameObject> robotTexts = new List<GameObject>();

    [Tooltip("Audio clips matching each robot text. Length should match robotTexts.")]
    [SerializeField] private List<AudioClip> robotTextAudioClips = new List<AudioClip>();

    [Tooltip("AudioSource used to play robot text audio. If empty, one will be created.")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("Extra seconds to wait after each audio clip finishes before showing the next text.")]
    [SerializeField] private float delayAfterAudio = 1f;

    [Tooltip("Fallback: how long each robot sentence stays if no audio clip is assigned.")]
    [SerializeField] private float secondsPerRobotText = 15f;

    [Tooltip("If false, only one robot sentence is visible at a time. If true, previous sentences stay visible.")]
    [SerializeField] private bool keepPreviousRobotTextsVisible = false;

    [Header("Book Pages")]
    [Tooltip("Drag PhenomenonImage/Page_1 here.")]
    [SerializeField] private GameObject page1;

    [Tooltip("Drag PhenomenonImage/Page_2 here.")]
    [SerializeField] private GameObject page2;

    [Tooltip("Drag Button_Left here. It shows Page_1.")]
    [SerializeField] private Button leftButton;

    [Tooltip("Drag Button_Right here. It shows Page_2.")]
    [SerializeField] private Button rightButton;

    [Tooltip("Show Page_1 when this view is opened.")]
    [SerializeField] private bool startOnPage1 = true;

    [Header("Card Selection")]
    [Tooltip("For now you can add only PhenomenonImage_1 and its matching selected detail.")]
    [SerializeField] private List<PhenomenonCardBinding> cardBindings = new List<PhenomenonCardBinding>();

    [Tooltip("Hide every selected detail object when this view opens.")]
    [SerializeField] private bool hideSelectedImagesOnOpen = true;

    [Tooltip("Seconds to wait after a card is selected before moving to KnowledgeCardView.")]
    [SerializeField] private float transitionDelayAfterSelection = 1f;

    [Header("Next Step")]
    [Tooltip("Drag the scene TeachingFlowManager here. If assigned, selection moves the flow to KNOWLEDGE_CARD.")]
    [SerializeField] private TeachingFlowManager teachingFlowManager;

    [Tooltip("Optional fallback. Drag IntroPhenomenonView here if TeachingFlowManager is not assigned.")]
    [SerializeField] private GameObject introPhenomenonView;

    [Tooltip("Optional fallback. Drag KnowledgeCardView here if TeachingFlowManager is not assigned.")]
    [SerializeField] private GameObject knowledgeCardView;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    private readonly List<ButtonActionBinding> runtimeBindings = new List<ButtonActionBinding>();
    private Coroutine robotTextRoutine;
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

        if (introPhenomenonView == null)
        {
            introPhenomenonView = gameObject;
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
        BindButtons();
        ResetSelectedImages();
        ShowPage(startOnPage1 ? 1 : 2);
        StartRobotTextSequence();
    }

    private void OnDisable()
    {
        StopRobotTextSequence();
        StopTransitionRoutine();
        UnbindButtons();
    }

    public void ShowPage1()
    {
        ShowPage(1);
    }

    public void ShowPage2()
    {
        ShowPage(2);
    }

    public void SelectCardByIndex(int index)
    {
        if (index < 0 || index >= cardBindings.Count) return;
        SelectCard(cardBindings[index]);
    }

    public void SelectCardById(string cardId)
    {
        for (int i = 0; i < cardBindings.Count; i++)
        {
            if (cardBindings[i] != null && cardBindings[i].cardId == cardId)
            {
                SelectCard(cardBindings[i]);
                return;
            }
        }
    }

    private void BindButtons()
    {
        UnbindButtons();

        AddButtonListener(leftButton, ShowPage1);
        AddButtonListener(rightButton, ShowPage2);

        for (int i = 0; i < cardBindings.Count; i++)
        {
            PhenomenonCardBinding binding = cardBindings[i];
            if (binding == null || binding.cardButton == null) continue;

            UnityAction action = () => SelectCard(binding);
            AddButtonListener(binding.cardButton, action);
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

    private void AddButtonListener(Button button, UnityAction action)
    {
        if (button == null || action == null) return;

        button.onClick.AddListener(action);
        runtimeBindings.Add(new ButtonActionBinding
        {
            button = button,
            action = action
        });
    }

    private void StartRobotTextSequence()
    {
        StopRobotTextSequence();
        robotTextRoutine = StartCoroutine(PlayRobotTextSequence());
    }

    private void StopRobotTextSequence()
    {
        if (robotTextRoutine != null)
        {
            StopCoroutine(robotTextRoutine);
            robotTextRoutine = null;
        }
    }

    private IEnumerator PlayRobotTextSequence()
    {
        SetRobotTextState(-1);

        for (int i = 0; i < robotTexts.Count; i++)
        {
            SetRobotTextState(i);

            AudioClip clip = i < robotTextAudioClips.Count ? robotTextAudioClips[i] : null;
            if (clip != null && audioSource != null)
            {
                audioSource.clip = clip;
                audioSource.Play();
                yield return new WaitForSeconds(clip.length + Mathf.Max(0f, delayAfterAudio));
            }
            else
            {
                yield return new WaitForSeconds(Mathf.Max(0f, secondsPerRobotText));
            }
        }
    }

    private void SetRobotTextState(int activeIndex)
    {
        for (int i = 0; i < robotTexts.Count; i++)
        {
            if (robotTexts[i] == null) continue;

            bool shouldShow = keepPreviousRobotTextsVisible
                ? i <= activeIndex
                : i == activeIndex;

            robotTexts[i].SetActive(shouldShow);
        }
    }

    private void ShowPage(int pageNumber)
    {
        if (page1 != null)
        {
            page1.SetActive(pageNumber == 1);
        }

        if (page2 != null)
        {
            page2.SetActive(pageNumber == 2);
        }

        if (logActions)
        {
            Debug.Log($"IntroPhenomenonViewController: show Page_{pageNumber}", this);
        }
    }

    private void SelectCard(PhenomenonCardBinding binding)
    {
        if (selectionLocked || binding == null) return;

        selectionLocked = true;
        ResetSelectedImages();

        if (binding.selectedImage != null)
        {
            binding.selectedImage.SetActive(true);
        }

        string cardId = !string.IsNullOrWhiteSpace(binding.cardId)
            ? binding.cardId
            : binding.cardButton != null ? binding.cardButton.name : "";

        GameLogManager.EnsureExists().RecordEvent("intro_phenomenon_card_selected", cardId);

        if (logActions)
        {
            Debug.Log($"IntroPhenomenonViewController: selected card {cardId}", this);
        }

        StopTransitionRoutine();
        transitionRoutine = StartCoroutine(TransitionToKnowledgeCardAfterDelay());
    }

    private void ResetSelectedImages()
    {
        if (!hideSelectedImagesOnOpen) return;

        for (int i = 0; i < cardBindings.Count; i++)
        {
            PhenomenonCardBinding binding = cardBindings[i];
            if (binding?.selectedImage != null)
            {
                binding.selectedImage.SetActive(false);
            }
        }
    }

    private void StopTransitionRoutine()
    {
        if (transitionRoutine != null)
        {
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
        }
    }

    private IEnumerator TransitionToKnowledgeCardAfterDelay()
    {
        yield return new WaitForSeconds(Mathf.Max(0f, transitionDelayAfterSelection));

        if (teachingFlowManager != null)
        {
            teachingFlowManager.SetStateKnowledgeCard();
            yield break;
        }

        if (introPhenomenonView != null)
        {
            introPhenomenonView.SetActive(false);
        }

        if (knowledgeCardView != null)
        {
            knowledgeCardView.SetActive(true);
        }
    }
}
