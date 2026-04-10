using UnityEngine;
using System;

namespace Project.Scripts.Interaction
{
    public class QrButton : MonoBehaviour, IInteraction
    {
        [SerializeField] private string markerId;
        [SerializeField] private Transform spawnPoint;

        private Action<string, Pose> onInteract;

        public void Initialize(string injectedMarkerId, Action<string, Pose> onInteractCallback)
        {
            markerId = injectedMarkerId;
            onInteract = onInteractCallback;
        }

        public void Interact()
        {
            var target = spawnPoint != null ? spawnPoint : transform;
            onInteract?.Invoke(markerId, new Pose(target.position, target.rotation));
        }
    }
}
