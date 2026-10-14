using UnityEngine;

public class AiControlledMistakeViewController : MonoBehaviour
{
    [Header("External Objects to Activate")]
    [Tooltip("Drag Robot_4 here.")]
    [SerializeField] private GameObject robot4;

    [Tooltip("Drag AITalk here.")]
    [SerializeField] private GameObject aiTalk;

    private void OnEnable()
    {
        if (robot4 != null) robot4.SetActive(true);
        if (aiTalk != null) aiTalk.SetActive(true);
    }

}
