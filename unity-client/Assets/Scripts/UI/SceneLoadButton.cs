using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class SceneLoadButton : MonoBehaviour
{
    [Header("Target Scene")]
    [Tooltip("Scene name in Build Settings. Your first gameplay scene is currently named 'First Scene'.")]
    [SerializeField] private string targetSceneName = "First Scene";

    [Tooltip("When enabled, load by build index instead of scene name.")]
    [SerializeField] private bool useBuildIndex = false;

    [SerializeField] private int targetBuildIndex = 2;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
    }

    private void OnEnable()
    {
        if (button != null)
        {
            button.onClick.AddListener(LoadTargetScene);
        }
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(LoadTargetScene);
        }
    }

    public void LoadTargetScene()
    {
        if (useBuildIndex)
        {
            SceneManager.LoadScene(targetBuildIndex);
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("SceneLoadButton: Target Scene Name is empty.", this);
            return;
        }

        SceneManager.LoadScene(targetSceneName);
    }
}
