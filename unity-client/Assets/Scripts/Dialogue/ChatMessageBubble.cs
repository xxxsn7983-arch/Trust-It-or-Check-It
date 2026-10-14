using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageBubble : MonoBehaviour
{
    [Header("Text Binding")]
    [Tooltip("Drag the TMP text inside this message bubble here, for example Bubble/AgentText or Bubble/PlayerText.")]
    [SerializeField] private TMP_Text messageText;

    [Header("Optional Layout")]
    [Tooltip("Optional bubble RectTransform. If empty, this object's RectTransform is used when refreshing layout.")]
    [SerializeField] private RectTransform bubbleRoot;

    public void SetMessage(string message)
    {
        if (messageText != null)
        {
            messageText.text = message;
        }

        RefreshLayout();
    }

    public void RefreshLayout()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform target = bubbleRoot != null ? bubbleRoot : transform as RectTransform;
        if (target != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        }

        RectTransform row = transform as RectTransform;
        if (row != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(row);
        }
    }

    private void Reset()
    {
        AutoBindReferences();
    }

    private void OnValidate()
    {
        if (messageText == null)
        {
            AutoBindReferences();
        }
    }

    [ContextMenu("Auto Bind References")]
    private void AutoBindReferences()
    {
        if (messageText == null)
        {
            messageText = GetComponentInChildren<TMP_Text>(true);
        }

        if (bubbleRoot == null)
        {
            Transform bubble = transform.Find("Bubble");
            if (bubble != null)
            {
                bubbleRoot = bubble as RectTransform;
            }
        }
    }
}
