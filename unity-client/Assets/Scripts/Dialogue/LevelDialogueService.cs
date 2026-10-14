using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Text;

public class LevelDialogueService : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField] private string backendBaseUrl = "http://127.0.0.1:8030";
    [SerializeField] private int requestTimeoutSeconds = 30;

    [Header("Level")]
    [SerializeField] private string caseId = "opossum_play_dead_001";

    [Header("Debug")]
    [SerializeField] private bool logRequests = true;

    private string sessionId;

    public string SessionId => sessionId;
    public bool HasSession => !string.IsNullOrEmpty(sessionId);

    public void StartLevel(string childInitialBelief, Action<LevelResponse> onSuccess = null, Action<string> onError = null)
    {
        StartCoroutine(StartLevelRequest(childInitialBelief, onSuccess, onError));
    }

    public void SendMessage(string content, string replyFormat = "chat", Action<LevelResponse> onSuccess = null, Action<string> onError = null)
    {
        if (replyFormat == "choice")
        {
            SendChoice(content, onSuccess, onError);
            return;
        }

        SendChat(content, onSuccess, onError);
    }

    public void SendChat(string content, Action<LevelResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!HasSession)
        {
            Debug.LogWarning("LevelDialogueService: No session. Call StartLevel first.");
            onError?.Invoke("No session");
            return;
        }

        StartCoroutine(SendChatRequest(content, onSuccess, onError));
    }

    public void SendChoice(string choice, Action<LevelResponse> onSuccess = null, Action<string> onError = null)
    {
        if (!HasSession)
        {
            Debug.LogWarning("LevelDialogueService: No session. Call StartLevel first.");
            onError?.Invoke("No session");
            return;
        }

        StartCoroutine(SendChoiceRequest(choice, onSuccess, onError));
    }

    private IEnumerator StartLevelRequest(string childInitialBelief, Action<LevelResponse> onSuccess, Action<string> onError)
    {
        var requestData = new StartLevelRequestData
        {
            case_id = caseId,
            child_initial_belief = childInitialBelief
        };

        string url = $"{backendBaseUrl}/api/dual-state/start";
        string json = JsonUtility.ToJson(requestData);

        if (logRequests) Debug.Log($"[LevelDialogue] POST {url}\n{json}");

        using (UnityWebRequest request = CreatePostRequest(url, json))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"StartLevel failed: {request.error}";
                Debug.LogError($"[LevelDialogue] {error}");
                onError?.Invoke(error);
                yield break;
            }

            string responseBody = request.downloadHandler.text;
            if (logRequests) Debug.Log($"[LevelDialogue] Response:\n{responseBody}");

            LevelResponse response = JsonUtility.FromJson<LevelResponse>(responseBody);
            sessionId = response.session_id;

            onSuccess?.Invoke(response);
        }
    }

    private IEnumerator SendChatRequest(string content, Action<LevelResponse> onSuccess, Action<string> onError)
    {
        var requestData = new ChatRequestData
        {
            session_id = sessionId,
            content = content
        };

        string url = $"{backendBaseUrl}/api/dual-state/chat";
        string json = JsonUtility.ToJson(requestData);
        yield return SendPostRequest(url, json, "SendChat", onSuccess, onError);
    }

    private IEnumerator SendChoiceRequest(string choice, Action<LevelResponse> onSuccess, Action<string> onError)
    {
        if (!int.TryParse(choice, out int choiceNumber))
        {
            string error = $"SendChoice failed: invalid choice '{choice}'";
            Debug.LogError($"[LevelDialogue] {error}");
            onError?.Invoke(error);
            yield break;
        }

        var requestData = new ChoiceRequestData
        {
            session_id = sessionId,
            choice = choiceNumber
        };

        string url = $"{backendBaseUrl}/api/dual-state/choice";
        string json = JsonUtility.ToJson(requestData);
        yield return SendPostRequest(url, json, "SendChoice", onSuccess, onError);
    }

    private IEnumerator SendPostRequest(string url, string json, string label, Action<LevelResponse> onSuccess, Action<string> onError)
    {
        if (logRequests) Debug.Log($"[LevelDialogue] POST {url}\n{json}");

        using (UnityWebRequest request = CreatePostRequest(url, json))
        {
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                string error = $"{label} failed: {request.error}";
                Debug.LogError($"[LevelDialogue] {error}");
                onError?.Invoke(error);
                yield break;
            }

            string responseBody = request.downloadHandler.text;
            if (logRequests) Debug.Log($"[LevelDialogue] Response:\n{responseBody}");

            LevelResponse response = JsonUtility.FromJson<LevelResponse>(responseBody);
            onSuccess?.Invoke(response);
        }
    }

    private UnityWebRequest CreatePostRequest(string url, string json)
    {
        var request = new UnityWebRequest(url, "POST");
        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        request.downloadHandler = new DownloadHandlerBuffer();
        request.timeout = Mathf.Max(1, requestTimeoutSeconds);
        request.SetRequestHeader("Content-Type", "application/json");
        return request;
    }

    [Serializable]
    private class StartLevelRequestData
    {
        public string case_id;
        public string child_initial_belief;
    }

    [Serializable]
    private class ChatRequestData
    {
        public string session_id;
        public string content;
    }

    [Serializable]
    private class ChoiceRequestData
    {
        public string session_id;
        public int choice;
    }
}

[Serializable]
public class LevelResponse
{
    public string session_id = "";
    public string send_format = "";
    public string state = "";
    public string view = "";
    public string content = "";
}
