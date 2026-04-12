using System;
using Project.Scripts.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Scripts.Interaction.Interactive_Objects
{
    public class InteractiveButton : MonoBehaviour, IInteraction, IPressInteraction
    {
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform pressStart;
        [SerializeField] private Transform pressEnd;
        [SerializeField] private Transform pressVisual;
        [SerializeField, Range(0f, 1f)] private float pressThreshold = 0.9f;
        [SerializeField] private UnityEvent onPressed;

        private Action<Pose> _onInteract;
        private Transform _activeInteractor;
        private bool _firedInCurrentPress;

        public void Initialize(Action<Pose> onInteractCallback)
        {
            _onInteract = onInteractCallback;
        }

        public void BeginPress(GameObject interactor)
        {
            if (_activeInteractor != null || interactor == null)
            {
                return;
            }

            _activeInteractor = interactor.transform;
            _firedInCurrentPress = false;
            SetVisualPress(0f);
        }

        public void UpdatePress(GameObject interactor)
        {
            if (interactor == null || _activeInteractor != interactor.transform)
            {
                return;
            }

            var pressAmount = GetPressAmount(_activeInteractor.position);
            SetVisualPress(pressAmount);

            if (!_firedInCurrentPress && pressAmount >= pressThreshold)
            {
                _firedInCurrentPress = true;
                Interact(interactor);
            }
        }

        public void EndPress(GameObject interactor)
        {
            if (interactor == null || _activeInteractor != interactor.transform)
            {
                return;
            }

            _activeInteractor = null;
            _firedInCurrentPress = false;
            SetVisualPress(0f);
        }

        public void Interact(GameObject interactor)
        {
            var target = spawnPoint != null ? spawnPoint : transform;
            var pose = new Pose(target.position, target.rotation);

            _onInteract?.Invoke(pose);
            onPressed?.Invoke();
        }

        private float GetPressAmount(Vector3 interactorWorldPosition)
        {
            if (pressStart == null || pressEnd == null)
            {
                return 0f;
            }

            var path = pressEnd.position - pressStart.position;
            var pathLengthSquared = path.sqrMagnitude;
            if (pathLengthSquared <= Mathf.Epsilon)
            {
                return 0f;
            }

            var projected = Vector3.Dot(interactorWorldPosition - pressStart.position, path) / pathLengthSquared;
            return Mathf.Clamp01(projected);
        }

        private void SetVisualPress(float amount)
        {
            if (pressVisual == null || pressStart == null || pressEnd == null)
            {
                return;
            }

            pressVisual.position = Vector3.Lerp(pressStart.position, pressEnd.position, amount);
        }
    }
}
