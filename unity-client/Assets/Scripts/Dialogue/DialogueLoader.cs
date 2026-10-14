using UnityEngine;

/// <summary>
/// 本地对话文本加载器 - 从 Resources 文件夹加载 JSON
/// </summary>
public static class DialogueLoader
{
    [System.Serializable]
    private class LocalDialogueData
    {
        public string defaultDialogue;
    }

    public static string LoadDefaultDialogue()
    {
        TextAsset asset = Resources.Load<TextAsset>("dialogue_text");
        if (asset == null)
        {
            Debug.LogWarning("DialogueLoader: Resources/dialogue_text.json 未找到");
            return null;
        }

        LocalDialogueData data = JsonUtility.FromJson<LocalDialogueData>(asset.text);
        return data?.defaultDialogue;
    }
}
