using System;
using System.Collections;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class SpeechInputService : MonoBehaviour
{
    [Header("Button")]
    [Tooltip("Drag the microphone button in InputBar here. Click once to start recording, click again to stop and transcribe.")]
    [SerializeField] private Button voiceButton;

    [Tooltip("Optional text on the microphone button. Used only to show Recording/Processing state.")]
    [SerializeField] private TMP_Text voiceButtonLabel;

    [Header("Backend STT")]
    [Tooltip("Backend speech-to-text endpoint. Keep this pointed at the FastAPI /stt route.")]
    [SerializeField] private string sttUrl = "http://localhost:5000/stt";

    [Tooltip("Language hint sent to the backend STT provider.")]
    [SerializeField] private string language = "zh";

    [Min(1)]
    [Tooltip("Timeout used for the STT request.")]
    [SerializeField] private int requestTimeoutSeconds = 60;

    [Header("Recording")]
    [Tooltip("Leave empty to use the system default microphone.")]
    [SerializeField] private string microphoneDeviceName = "";

    [Min(8000)]
    [Tooltip("Recording sample rate. 16000 is enough for speech and keeps uploads small.")]
    [SerializeField] private int sampleRate = 16000;

    [Min(1)]
    [Tooltip("Maximum seconds for one voice input.")]
    [SerializeField] private int maxRecordingSeconds = 12;

    [Header("Result")]
    [Tooltip("Drag ChatPanel's ChatUIController here. If empty, this script tries to find it.")]
    [SerializeField] private ChatUIController chatUIController;

    [Header("Debug")]
    [Tooltip("Print speech input warnings without interrupting text dialogue.")]
    [SerializeField] private bool logWarnings = true;

    private AudioClip recordingClip;
    private Coroutine autoStopCoroutine;
    private string originalButtonLabel = "";
    private bool isRecording;
    private bool isSending;

    private void Awake()
    {
        if (chatUIController == null)
        {
            chatUIController = GetComponentInParent<ChatUIController>();
        }

        if (chatUIController == null)
        {
            chatUIController = FindObjectOfType<ChatUIController>();
        }

        if (voiceButton == null)
        {
            voiceButton = GetComponent<Button>();
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
    }

    private void OnDisable()
    {
        if (voiceButton != null)
        {
            voiceButton.onClick.RemoveListener(ToggleRecording);
        }

        if (isRecording)
        {
            StopRecording(false);
        }
    }

    public void ToggleRecording()
    {
        if (isSending) return;

        if (isRecording)
        {
            StopRecording(true);
        }
        else
        {
            StartRecording();
        }
    }

    public void StartRecording()
    {
        if (Microphone.devices.Length == 0)
        {
            LogWarning("No microphone device was found.");
            return;
        }

        string deviceName = ActiveDeviceName();
        recordingClip = Microphone.Start(deviceName, false, Mathf.Max(1, maxRecordingSeconds), Mathf.Max(8000, sampleRate));
        isRecording = true;
        SetButtonLabel("Recording");

        if (autoStopCoroutine != null)
        {
            StopCoroutine(autoStopCoroutine);
        }

        autoStopCoroutine = StartCoroutine(AutoStopRecording());
    }

    public void StopRecording(bool sendToBackend)
    {
        if (!isRecording) return;

        string deviceName = ActiveDeviceName();
        int recordedSamples = Microphone.GetPosition(deviceName);
        Microphone.End(deviceName);

        isRecording = false;
        SetButtonLabel(originalButtonLabel);

        if (autoStopCoroutine != null)
        {
            StopCoroutine(autoStopCoroutine);
            autoStopCoroutine = null;
        }

        if (!sendToBackend) return;

        if (recordingClip == null || recordedSamples <= 0)
        {
            LogWarning("Recording was empty.");
            return;
        }

        byte[] wavBytes = EncodeWav(recordingClip, recordedSamples);
        StartCoroutine(SendSpeechToText(wavBytes));
    }

    private IEnumerator AutoStopRecording()
    {
        yield return new WaitForSeconds(Mathf.Max(1, maxRecordingSeconds));

        if (isRecording)
        {
            StopRecording(true);
        }
    }

    private IEnumerator SendSpeechToText(byte[] wavBytes)
    {
        isSending = true;
        if (voiceButton != null)
        {
            voiceButton.interactable = false;
        }

        SetButtonLabel("Processing");

        GameContextSnapshot snapshot = GameLogManager.EnsureExists().GetContextSnapshot();
        SpeechToTextRequest requestData = new SpeechToTextRequest
        {
            sessionId = snapshot.sessionId,
            audioBase64 = Convert.ToBase64String(wavBytes),
            audioFormat = "wav",
            sampleRate = Mathf.Max(8000, sampleRate),
            language = language
        };

        string requestJson = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(sttUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            isSending = false;
            if (voiceButton != null)
            {
                voiceButton.interactable = true;
            }

            SetButtonLabel(originalButtonLabel);

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogWarning("STT request failed: " + request.error);
                yield break;
            }

            SpeechToTextResponse response = JsonUtility.FromJson<SpeechToTextResponse>(request.downloadHandler.text);
            if (response == null || !response.configured)
            {
                LogWarning(response != null ? response.message : "STT backend returned an empty response.");
                yield break;
            }

            string recognizedText = response.text != null ? response.text.Trim() : "";
            if (string.IsNullOrEmpty(recognizedText))
            {
                LogWarning("STT backend returned empty text.");
                yield break;
            }

            ApplyRecognizedText(recognizedText);
        }
    }

    private void ApplyRecognizedText(string text)
    {
        if (chatUIController != null)
        {
            chatUIController.SendMessageText(text);
            return;
        }

        Debug.LogWarning("SpeechInputService: ChatUIController not found, cannot send recognized text.");
    }

    private string ActiveDeviceName()
    {
        return string.IsNullOrWhiteSpace(microphoneDeviceName) ? null : microphoneDeviceName;
    }

    private void SetButtonLabel(string text)
    {
        if (voiceButtonLabel != null)
        {
            voiceButtonLabel.text = text;
        }
    }

    private void LogWarning(string message)
    {
        if (logWarnings)
        {
            Debug.LogWarning("SpeechInputService: " + message, this);
        }
    }

    private byte[] EncodeWav(AudioClip clip, int recordedSamples)
    {
        int channels = clip.channels;
        int sampleCount = Mathf.Clamp(recordedSamples, 0, clip.samples) * channels;
        float[] samples = new float[sampleCount];
        clip.GetData(samples, 0);

        byte[] pcmBytes = new byte[sampleCount * 2];
        int byteIndex = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            short value = (short)(Mathf.Clamp(samples[i], -1f, 1f) * short.MaxValue);
            pcmBytes[byteIndex++] = (byte)(value & 0xff);
            pcmBytes[byteIndex++] = (byte)((value >> 8) & 0xff);
        }

        using (MemoryStream stream = new MemoryStream(44 + pcmBytes.Length))
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmBytes.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(Mathf.Max(8000, sampleRate));
            writer.Write(Mathf.Max(8000, sampleRate) * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmBytes.Length);
            writer.Write(pcmBytes);
            writer.Flush();
            return stream.ToArray();
        }
    }

    [Serializable]
    private class SpeechToTextRequest
    {
        public string sessionId;
        public string audioBase64;
        public string audioFormat;
        public int sampleRate;
        public string language;
    }

    [Serializable]
    private class SpeechToTextResponse
    {
        public bool configured = false;
        public string text = "";
        public string message = "";
    }
}
