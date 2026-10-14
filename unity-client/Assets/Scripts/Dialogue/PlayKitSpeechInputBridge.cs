using Cysharp.Threading.Tasks;
using PlayKit_SDK;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayKitSpeechInputBridge : MonoBehaviour
{
    [Header("PlayKit")]
    [Tooltip("Drag the PlayKit_Transcribe component here.")]
    [SerializeField] private PlayKit_Transcribe transcriber;

    [Tooltip("Language hint for transcription. Use zh for Chinese.")]
    [SerializeField] private string language = "zh";

    [Min(1f)]
    [Tooltip("How long to wait for PlayKit_Transcribe to finish initializing.")]
    [SerializeField] private float transcriberReadyTimeoutSeconds = 10f;

    [Header("UI")]
    [Tooltip("Drag InputBar/VoiceButton here.")]
    [SerializeField] private Button voiceButton;

    [Tooltip("Optional label on the voice button. It will show recording/processing state.")]
    [SerializeField] private TMP_Text voiceButtonLabel;

    [Tooltip("Optional image on the voice button. If empty, the script uses Voice Button's Image component.")]
    [SerializeField] private Image voiceButtonImage;

    [Tooltip("Button sprite when it is not recording.")]
    [SerializeField] private Sprite idleSprite;

    [Tooltip("Button sprite while recording.")]
    [SerializeField] private Sprite recordingSprite;

    [Tooltip("Optional button sprite while PlayKit is transcribing.")]
    [SerializeField] private Sprite processingSprite;

    [Tooltip("Drag ChatPanel's ChatUIController here.")]
    [SerializeField] private ChatUIController chatUIController;

    [Header("Debug")]
    [Tooltip("Print warnings when PlayKit transcription cannot run.")]
    [SerializeField] private bool logWarnings = true;

    private string originalButtonLabel = "";
    private bool isRecording;
    private bool isProcessing;
    private bool suppressStoppedTranscription;

    private void Awake()
    {
        if (transcriber == null)
        {
            transcriber = GetComponent<PlayKit_Transcribe>();
        }

        if (transcriber == null)
        {
            transcriber = GetComponentInParent<PlayKit_Transcribe>();
        }

        if (voiceButton == null)
        {
            voiceButton = GetComponent<Button>();
        }

        if (voiceButtonImage == null && voiceButton != null)
        {
            voiceButtonImage = voiceButton.GetComponent<Image>();
        }

        if (idleSprite == null && voiceButtonImage != null)
        {
            idleSprite = voiceButtonImage.sprite;
        }

        if (chatUIController == null)
        {
            chatUIController = GetComponentInParent<ChatUIController>();
        }

        if (chatUIController == null)
        {
            chatUIController = FindObjectOfType<ChatUIController>();
        }

        if (voiceButtonLabel != null)
        {
            originalButtonLabel = voiceButtonLabel.text;
        }
    }

    private void OnEnable()
    {
        if (voiceButton != null)
        {
            voiceButton.onClick.AddListener(ToggleRecording);
        }

        SubscribeRecorderEvents();
    }

    private void OnDisable()
    {
        UnsubscribeRecorderEvents();

        if (voiceButton != null)
        {
            voiceButton.onClick.RemoveListener(ToggleRecording);
        }

        if (isRecording && transcriber != null && transcriber.IsRecording)
        {
            suppressStoppedTranscription = true;
            transcriber.StopRecording();
            suppressStoppedTranscription = false;
        }

        SetButtonSprite(idleSprite);
    }

    public async void ToggleRecording()
    {
        if (isProcessing) return;

        if (transcriber == null)
        {
            LogWarning("PlayKit_Transcribe is not assigned.");
            return;
        }

        if (isRecording || transcriber.IsRecording)
        {
            await StopRecordingAndTranscribe();
            return;
        }

        StartRecording();
    }

    public void StartRecording()
    {
        if (transcriber == null)
        {
            LogWarning("PlayKit_Transcribe is not assigned.");
            return;
        }

        SubscribeRecorderEvents();

        bool started = transcriber.StartRecording();
        if (!started)
        {
            LogWarning("PlayKit failed to start microphone recording.");
            SetButtonSprite(idleSprite);
            return;
        }

        isRecording = true;
        SetButtonLabel("Recording");
        SetButtonSprite(recordingSprite);
    }

    public async UniTask StopRecordingAndTranscribe()
    {
        if (transcriber == null || isProcessing) return;

        isProcessing = true;

        AudioClip recordedClip = null;
        if (transcriber.IsRecording)
        {
            recordedClip = transcriber.StopRecording();
        }

        if (recordedClip == null && transcriber.MicrophoneRecorder != null)
        {
            recordedClip = transcriber.MicrophoneRecorder.LastRecording;
        }

        isRecording = false;
        await TranscribeRecordedClip(recordedClip);
    }

    private void SubscribeRecorderEvents()
    {
        if (transcriber == null) return;

        PlayKit_MicrophoneRecorder recorder = transcriber.GetOrCreateRecorder();
        if (recorder == null) return;

        recorder.OnRecordingStopped -= HandleRecordingStopped;
        recorder.OnRecordingStopped += HandleRecordingStopped;
    }

    private void UnsubscribeRecorderEvents()
    {
        if (transcriber == null || transcriber.MicrophoneRecorder == null) return;

        transcriber.MicrophoneRecorder.OnRecordingStopped -= HandleRecordingStopped;
    }

    private async void HandleRecordingStopped(AudioClip recordedClip)
    {
        isRecording = false;

        if (suppressStoppedTranscription || isProcessing)
        {
            if (!isProcessing)
            {
                SetButtonLabel(originalButtonLabel);
                SetButtonSprite(idleSprite);
            }
            return;
        }

        if (recordedClip == null && transcriber != null && transcriber.MicrophoneRecorder != null)
        {
            recordedClip = transcriber.MicrophoneRecorder.LastRecording;
        }

        isProcessing = true;
        await TranscribeRecordedClip(recordedClip);
    }

    private async UniTask TranscribeRecordedClip(AudioClip recordedClip)
    {
        SetButtonLabel("Processing");
        SetButtonSprite(processingSprite != null ? processingSprite : recordingSprite);
        SetButtonInteractable(false);

        try
        {
            if (recordedClip == null)
            {
                LogWarning("No recorded audio clip was returned.");
                return;
            }

            bool ready = await WaitForTranscriberReady();
            if (!ready)
            {
                LogWarning("PlayKit_Transcribe was not ready before timeout.");
                return;
            }

            if (logWarnings)
            {
                Debug.Log($"PlayKitSpeechInputBridge: clip length={recordedClip.length:F2}s, samples={recordedClip.samples}, frequency={recordedClip.frequency}, channels={recordedClip.channels}, language={language}", this);
            }

            string text = await transcriber.TranscribeAsync(recordedClip, language);
            if (logWarnings)
            {
                Debug.Log("PlayKitSpeechInputBridge: transcribed text = " + text, this);
            }
            ApplyTranscribedText(text);
        }
        finally
        {
            isProcessing = false;
            SetButtonInteractable(true);
            SetButtonLabel(originalButtonLabel);
            SetButtonSprite(idleSprite);
        }
    }

    public void ApplyTranscribedText(string text)
    {
        text = text != null ? text.Trim() : "";
        if (string.IsNullOrEmpty(text)) return;

        if (chatUIController != null)
        {
            chatUIController.SendMessageText(text);
            return;
        }

        Debug.LogWarning("PlayKitSpeechInputBridge: ChatUIController not found, cannot send transcribed text.");
    }

    private async UniTask<bool> WaitForTranscriberReady()
    {
        float startTime = Time.realtimeSinceStartup;
        while (transcriber != null && !transcriber.IsReady)
        {
            if (Time.realtimeSinceStartup - startTime >= transcriberReadyTimeoutSeconds)
            {
                return false;
            }

            await UniTask.Yield();
        }

        return transcriber != null && transcriber.IsReady;
    }

    private void SetButtonLabel(string text)
    {
        if (voiceButtonLabel != null)
        {
            voiceButtonLabel.text = text;
        }
    }

    private void SetButtonSprite(Sprite sprite)
    {
        if (voiceButtonImage != null && sprite != null)
        {
            voiceButtonImage.sprite = sprite;
        }
    }

    private void SetButtonInteractable(bool interactable)
    {
        if (voiceButton != null)
        {
            voiceButton.interactable = interactable;
        }
    }

    private void LogWarning(string message)
    {
        if (logWarnings)
        {
            Debug.LogWarning("PlayKitSpeechInputBridge: " + message, this);
        }
    }
}
