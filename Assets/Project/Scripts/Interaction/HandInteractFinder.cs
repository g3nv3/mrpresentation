using UnityEngine;

namespace Project.Scripts.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public class HandInteractFinder : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out IPressInteraction pressInteraction))
            {
                pressInteraction.BeginPress(gameObject);
                return;
            }

            if (other.TryGetComponent(out IInteraction interaction))
            {
                interaction.Interact(gameObject);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (other.TryGetComponent(out IPressInteraction pressInteraction))
            {
                pressInteraction.UpdatePress(gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out IPressInteraction pressInteraction))
            {
                pressInteraction.EndPress(gameObject);
            }
        }
    }
}
