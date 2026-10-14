using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the clue card images (Image_1 ~ Image_5) under Cards.
/// Images can start hidden or all active. Call ActivateRandomClue() when the
/// player clicks an animal on the map to randomly activate one hidden image.
/// </summary>
public class ClueCardManager : MonoBehaviour
{
    [Header("Clue Images")]
    [Tooltip("Drag Image_1 through Image_5 here.")]
    [SerializeField] private List<GameObject> clueImages = new List<GameObject>();

    [Header("Startup")]
    [Tooltip("If enabled, all clue images are active from the start without collecting clues.")]
    [SerializeField] private bool activateAllOnStart;

    [Header("Debug")]
    [SerializeField] private bool logActions = true;

    private List<int> inactiveIndices = new List<int>();

    private void Awake()
    {
        if (activateAllOnStart)
        {
            ActivateAll();
        }
        else
        {
            DeactivateAll();
        }
    }

    /// <summary>
    /// Shows all clue images and clears the inactive pool.
    /// </summary>
    public void ActivateAll()
    {
        inactiveIndices.Clear();
        for (int i = 0; i < clueImages.Count; i++)
        {
            if (clueImages[i] != null)
            {
                clueImages[i].SetActive(true);
            }
        }

        if (logActions)
        {
            Debug.Log("ClueCardManager: Activated all clue images.", this);
        }
    }

    /// <summary>
    /// Hides all clue images and resets the inactive pool.
    /// </summary>
    public void DeactivateAll()
    {
        inactiveIndices.Clear();
        for (int i = 0; i < clueImages.Count; i++)
        {
            if (clueImages[i] != null)
            {
                clueImages[i].SetActive(false);
            }
            inactiveIndices.Add(i);
        }
    }

    /// <summary>
    /// Randomly activates one clue image that hasn't been activated yet.
    /// Returns true if an image was activated, false if all are already active.
    /// </summary>
    public bool ActivateRandomClue()
    {
        if (inactiveIndices.Count == 0)
        {
            if (logActions)
                Debug.Log("ClueCardManager: All clue images are already active.", this);
            return false;
        }

        int randomIndex = Random.Range(0, inactiveIndices.Count);
        int imageIndex = inactiveIndices[randomIndex];
        inactiveIndices.RemoveAt(randomIndex);

        if (clueImages[imageIndex] != null)
        {
            clueImages[imageIndex].SetActive(true);
        }

        if (logActions)
            Debug.Log($"ClueCardManager: Activated clue image {imageIndex + 1} ({clueImages[imageIndex]?.name})", this);

        return true;
    }

    /// <summary>
    /// Returns how many clue images are still inactive.
    /// </summary>
    public int RemainingCount => inactiveIndices.Count;

    /// <summary>
    /// Returns true if all clue images have been activated.
    /// </summary>
    public bool AllActivated => inactiveIndices.Count == 0;
}
