using UnityEngine;

public class HandInteractFinder : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out IInteraction interaction))
        {
            interaction.Interact();
        }
    }
}