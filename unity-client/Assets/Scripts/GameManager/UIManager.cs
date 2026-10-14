using UnityEngine;

/// <summary>
/// UI 面板管理器 - 统一管理所有 UI 面板的显示/隐藏
/// 通过事件驱动，不直接被其他脚本引用
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("面板引用")]
    public GameObject dialoguePanel;

    void OnEnable()
    {
        GameEvents.OnDialogueStart += HandleDialogueStart;
        GameEvents.OnDialogueEnd += HandleDialogueEnd;
        GameEvents.OnPanelToggle += HandlePanelToggle;
    }

    void OnDisable()
    {
        GameEvents.OnDialogueStart -= HandleDialogueStart;
        GameEvents.OnDialogueEnd -= HandleDialogueEnd;
        GameEvents.OnPanelToggle -= HandlePanelToggle;
    }

    private void HandleDialogueStart(string msg)
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(true);
    }

    private void HandleDialogueEnd()
    {
        if (dialoguePanel != null) dialoguePanel.SetActive(false);
    }

    private void HandlePanelToggle(string panelName, bool show)
    {
        switch (panelName)
        {
            case "dialogue":
                if (dialoguePanel != null) dialoguePanel.SetActive(show);
                break;
        }
    }
}
