using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class AreaActivator : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private bool deactivateOnExit = true;
    [SerializeField] private UnityEvent onEnter = new UnityEvent();
    [SerializeField] private UnityEvent onExit = new UnityEvent();

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (target != null)
            target.SetActive(true);

        onEnter?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        if (deactivateOnExit && target != null)
            target.SetActive(false);

        onExit?.Invoke();
    }
}
