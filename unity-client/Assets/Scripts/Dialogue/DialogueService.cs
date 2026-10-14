using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class DialogueService : MonoBehaviour
{
    public string backendUrl = "http://localhost:5000/chat";
    public int requestTimeoutSeconds = 40;

    [System.Serializable]
    private class DialogueRequest
    {
        public string schemaVersion;
        public string sessionId;
        public string messageRole;
        public string message;
        public string sceneName;
        public List<string> inventory;
        public List<GameLogEntry> recentEvents;
        public List<DialogueMessageLog> recentMessages;
        public TeachingContextSnapshot teachingContext;
    }

    [System.Serializable]
    private class DialogueResponse
    {
        public string response = "";
        public string reply = "";
    }

    public void SendRequest(string message, string fallbackText = null)
    {
        SendRequest(message, fallbackText, true);
    }

    public void SendSystemRequest(string message, string fallbackText = null)
    {
        SendRequest(message, fallbackText, false);
    }

    private void SendRequest(string message, string fallbackText, bool recordAsPlayerMessage)
    {
        StartCoroutine(SendDialogueRequest(message, fallbackText, recordAsPlayerMessage));
    }

    private IEnumerator SendDialogueRequest(string message, string fallbackText, bool recordAsPlayerMessage)
    {
        GameLogManager logManager = GameLogManager.EnsureExists();
        if (recordAsPlayerMessage)
        {
            logManager.RecordPlayerMessage(message);
        }
        else
        {
            logManager.RecordEvent("dialogue_system_request", "DialogueService", message);
        }

        string messageRole = recordAsPlayerMessage ? "player" : "system";
        DialogueRequest requestData = BuildRequest(message, messageRole, logManager.GetContextSnapshot());
        string jsonBody = JsonUtility.ToJson(requestData);

        using (UnityWebRequest webRequest = new UnityWebRequest(backendUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.timeout = Mathf.Max(1, requestTimeoutSeconds);
            webRequest.SetRequestHeader("Content-Type", "application/json");

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                string responseText = ExtractResponseText(webRequest.downloadHandler.text);
                PublishDialogueText(ChooseText(responseText, fallbackText, "Agent response was empty."));
            }
            else
            {
                Debug.LogError("DialogueService: " + webRequest.error);
                PublishDialogueText(ChooseText(fallbackText, null, "Agent is unavailable."));
            }
        }
    }

    private DialogueRequest BuildRequest(string message, string messageRole, GameContextSnapshot snapshot)
    {
        return new DialogueRequest
        {
            schemaVersion = "game-agent-context-v1",
            sessionId = snapshot.sessionId,
            messageRole = messageRole,
            message = message,
            sceneName = snapshot.sceneName,
            inventory = snapshot.inventory,
            recentEvents = snapshot.recentEvents,
            recentMessages = snapshot.recentMessages,
            teachingContext = snapshot.teachingContext
        };
    }

    private string ExtractResponseText(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody)) return "";

        try
        {
            DialogueResponse responseData = JsonUtility.FromJson<DialogueResponse>(responseBody);
            if (responseData != null)
            {
                if (!string.IsNullOrWhiteSpace(responseData.response)) return responseData.response;
                if (!string.IsNullOrWhiteSpace(responseData.reply)) return responseData.reply;
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("DialogueService: response was not JSON: " + ex.Message);
        }

        return responseBody;
    }

    private void PublishDialogueText(string text)
    {
        GameLogManager.EnsureExists().RecordAgentMessage(text);
        GameEvents.FireDialogueTextReady(text);
    }

    private string ChooseText(string primaryText, string fallbackText, string emptyText)
    {
        if (!string.IsNullOrWhiteSpace(primaryText)) return primaryText;
        if (!string.IsNullOrWhiteSpace(fallbackText)) return fallbackText;
        return emptyText;
    }
}
