using UnityEngine;

namespace Project.Scripts.Interaction
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class SetKinematicOnHandTouch : MonoBehaviour, IInteraction
    {
        [SerializeField, Min(0f)] private float throwGraceSeconds = 0.18f;
        [SerializeField, Min(0f)] private float minReleaseSpeedForGrace = 0.2f;
        [SerializeField] private bool clearVelocityOnStop = true;

        private Rigidbody _targetRigidbody;
        private bool _wasKinematicLastFixedUpdate;
        private float _ignoreTouchesUntil;

        private void Awake()
        {
            _targetRigidbody = GetComponent<Rigidbody>();
            _wasKinematicLastFixedUpdate = _targetRigidbody != null && _targetRigidbody.isKinematic;
        }

        private void FixedUpdate()
        {
            if (_targetRigidbody == null)
            {
                return;
            }

            // Detect release from drag (kinematic -> dynamic) and ignore brief self-touches right after throw.
            if (_wasKinematicLastFixedUpdate && !_targetRigidbody.isKinematic)
            {
                var releaseSpeed = _targetRigidbody.linearVelocity.magnitude;
                if (releaseSpeed >= minReleaseSpeedForGrace)
                {
                    _ignoreTouchesUntil = Time.time + throwGraceSeconds;
                }
            }

            _wasKinematicLastFixedUpdate = _targetRigidbody.isKinematic;
        }

        public void Interact(GameObject interactor)
        {
            if (_targetRigidbody == null || _targetRigidbody.isKinematic)
            {
                return;
            }

            if (Time.time < _ignoreTouchesUntil)
            {
                return;
            }

            _targetRigidbody.isKinematic = true;

            if (clearVelocityOnStop)
            {
                _targetRigidbody.linearVelocity = Vector3.zero;
                _targetRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }
}
