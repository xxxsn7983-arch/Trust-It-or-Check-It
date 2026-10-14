using UnityEngine;

public class AnimalCluePickup : MonoBehaviour
{
    [Tooltip("Drag the ClueCardManager here. If empty, it will be found at runtime.")]
    [SerializeField] private ClueCardManager clueCardManager;

    [Tooltip("Disable this object after it's been clicked.")]
    [SerializeField] private bool disableAfterPickup = true;

    private void Awake()
    {
        if (clueCardManager == null)
            clueCardManager = FindObjectOfType<ClueCardManager>();
    }

    private void OnMouseDown()
    {
        if (clueCardManager == null)
        {
            clueCardManager = FindObjectOfType<ClueCardManager>();
            if (clueCardManager == null)
            {
                Debug.LogWarning("AnimalCluePickup: ClueCardManager not found.", this);
                return;
            }
        }

        clueCardManager.ActivateRandomClue();

        if (disableAfterPickup)
            gameObject.SetActive(false);
    }
}
