using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public enum TTSAudioFormat
{
    Auto,
    Mp3,
    Wav,
    OggVorbis
}

public class TTSService : MonoBehaviour
{
    [Header("Backend")]
    [Tooltip("Enable this to speak every agent reply published by DialogueService.")]
    public bool enableTTS = true;

    [Tooltip("Backend TTS endpoint. Keep this pointed at the FastAPI /tts route.")]
    public string ttsUrl = "http://localhost:5000/tts";

    [Tooltip("Voice id sent to the backend. The real meaning depends on the TTS provider you choose later.")]
    public string voice = "agent_default";

    [Min(1)]
    [Tooltip("Timeout used for both the TTS request and the audio file download.")]
    public int requestTimeoutSeconds = 60;

    [Header("Playback")]
    [Tooltip("AudioSource used to play generated speech. One is added automatically if this is empty.")]
    public AudioSource audioSource;

    [Tooltip("Stop the previous speech/request when a newer agent reply arrives.")]
    public bool stopPreviousSpeech = true;

    [Tooltip("Auto uses the backend audioFormat field, then falls back to the audio URL extension.")]
    public TTSAudioFormat audioFormat = TTSAudioFormat.Auto;

    [Tooltip("Print TTS warnings without interrupting dialogue playback.")]
    public bool logTtsWarnings = true;

    private Coroutine speechCoroutine;
    private int speechRequestVersion;

    [System.Serializable]
    private class TTSRequest
    {
        public string sessionId;
        public string text;
        public string voice;
    }

    [System.Serializable]
    private class TTSResponse
    {
        public bool configured = false;
        public string audioUrl = "";
        public string audioFormat = "mp3";
        public string message = "";
    }

    void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    void OnEnable()
    {
        GameEvents.OnDialogueTextReady += HandleSpeak;
    }

    void OnDisable()
    {
        GameEvents.OnDialogueTextReady -= HandleSpeak;

        if (speechCoroutine != null)
        {
            StopCoroutine(speechCoroutine);
            speechCoroutine = null;
        }
    }

    private void HandleSpeak(string text)
    {
        if (!enableTTS || string.IsNullOrWhiteSpace(text)) return;

        int requestVersion = ++speechRequestVersion;
        if (stopPreviousSpeech)
        {
            if (speechCoroutine != null)
            {
                StopCoroutine(speechCoroutine);
                speechCoroutine = null;
            }

            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }
        }

        speechCoroutine = StartCoroutine(RequestAndPlaySpeech(text, requestVersion));
    }

    private IEnumerator RequestAndPlaySpeech(string text, int requestVersion)
    {
        GameContextSnapshot snapshot = GameLogManager.EnsureExists().GetContextSnapshot();
        TTSRequest requestData = new TTSRequest
        {
            sessionId = snapshot.sessionId,
            text = text,
            voice = voice
        };

        string requestJson = JsonUtility.ToJson(requestData);

        using (UnityWebRequest request = new UnityWebRequest(ttsUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestJson));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (stopPreviousSpeech && requestVersion != speechRequestVersion)
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogWarning("TTS request failed: " + request.error);
                yield break;
            }

            TTSResponse response = JsonUtility.FromJson<TTSResponse>(request.downloadHandler.text);
            if (response == null || !response.configured)
            {
                LogWarning(response != null ? response.message : "TTS backend returned an empty response.");
                yield break;
            }

            if (string.IsNullOrWhiteSpace(response.audioUrl))
            {
                LogWarning("TTS backend did not return audioUrl.");
                yield break;
            }

            yield return DownloadAndPlay(response.audioUrl, response.audioFormat, requestVersion);
        }
    }

    private IEnumerator DownloadAndPlay(string audioUrl, string responseAudioFormat, int requestVersion)
    {
        AudioType resolvedAudioType = ResolveAudioType(audioUrl, responseAudioFormat);
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(audioUrl, resolvedAudioType))
        {
            request.timeout = Mathf.Max(1, requestTimeoutSeconds);
            yield return request.SendWebRequest();

            if (stopPreviousSpeech && requestVersion != speechRequestVersion)
            {
                yield break;
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                LogWarning("TTS audio download failed: " + request.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            if (clip == null)
            {
                LogWarning("TTS audio clip was empty.");
                yield break;
            }

            if (stopPreviousSpeech && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            audioSource.clip = clip;
            audioSource.Play();
        }
    }

    private AudioType ResolveAudioType(string audioUrl, string responseAudioFormat)
    {
        if (audioFormat != TTSAudioFormat.Auto)
        {
            return ToUnityAudioType(audioFormat);
        }

        string formatHint = !string.IsNullOrWhiteSpace(responseAudioFormat)
            ? responseAudioFormat.Trim().ToLowerInvariant()
            : audioUrl.ToLowerInvariant();

        if (formatHint.Contains("wav")) return AudioType.WAV;
        if (formatHint.Contains("ogg")) return AudioType.OGGVORBIS;
        return AudioType.MPEG;
    }

    private AudioType ToUnityAudioType(TTSAudioFormat format)
    {
        switch (format)
        {
            case TTSAudioFormat.Wav:
                return AudioType.WAV;
            case TTSAudioFormat.OggVorbis:
                return AudioType.OGGVORBIS;
            default:
                return AudioType.MPEG;
        }
    }

    private void LogWarning(string message)
    {
        if (logTtsWarnings)
        {
            Debug.LogWarning("TTSService: " + message);
        }
    }
}
