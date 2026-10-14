using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class CardGroupToggleButton : MonoBehaviour
{
    [SerializeField] private GameObject targetGroup;
    [SerializeField] private string targetGroupName = "Cards";
    [SerializeField] private bool hideOnStart = true;
    [SerializeField] private bool toggleOnClick = true;

    private Button button;
    private bool isOpen;

    private void Awake()
    {
        button = GetComponent<Button>();
        ResolveTargetGroup();
    }

    private void Start()
    {
        if (hideOnStart)
        {
            SetOpen(false);
        }
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(HandleClick);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        SetOpen(toggleOnClick ? !isOpen : true);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        ResolveTargetGroup();

        if (targetGroup != null)
        {
            targetGroup.SetActive(isOpen);
        }
        else
        {
            Debug.LogWarning($"CardGroupToggleButton: target group '{targetGroupName}' was not found.", this);
        }
    }

    private void ResolveTargetGroup()
    {
        if (targetGroup != null || string.IsNullOrEmpty(targetGroupName))
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform searchRoot = canvas != null ? canvas.transform : transform.root;
        Transform found = FindChildByName(searchRoot, targetGroupName);

        if (found != null)
        {
            targetGroup = found.gameObject;
        }
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }

            Transform nested = FindChildByName(child, childName);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }
}
