using System;
using UnityEngine;

namespace Project.Scripts.Interaction.Interactive_Objects
{
    public class QrObjectSpawner : MonoBehaviour
    {
        [SerializeField] private string markerId;

        private Action<string, Pose> _onSpawnRequested;

        public void Initialize(string injectedMarkerId, Action<string, Pose> onSpawnRequested)
        {
            markerId = injectedMarkerId;
            _onSpawnRequested = onSpawnRequested;
        }

        public void Spawn(Pose pose)
        {
            if (string.IsNullOrWhiteSpace(markerId))
            {
                return;
            }

            _onSpawnRequested?.Invoke(markerId, pose);
        }
    }
}
