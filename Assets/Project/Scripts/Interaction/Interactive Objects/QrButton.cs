using System;
using Project.Scripts.Interaction;
using UnityEngine;

namespace Project.Scripts.Interaction.Interactive_Objects
{
    public class QrButton : MonoBehaviour, IInteraction, IPressInteraction
    {
        [SerializeField] private string markerId;
        [SerializeField] private Transform spawnPoint;
        [SerializeField] private Transform pressStart;
        [SerializeField] private Transform pressEnd;
        [SerializeField] private Transform pressVisual;
        [SerializeField, Range(0f, 1f)] private float pressThreshold = 0.9f;

        private Action<string, Pose> _onInteract;
        private Transform _activeInteractor;
        private bool _firedInCurrentPress;

        public void Initialize(string injectedMarkerId, Action<string, Pose> onInteractCallback)
        {
            markerId = injectedMarkerId;
            _onInteract = onInteractCallback;
        }

        public void BeginPress(GameObject interactor)
        {
            if (_activeInteractor != null)
            {
                return;
            }

            _activeInteractor = interactor.transform;
            _firedInCurrentPress = false;
            SetVisualPress(0f);
        }

        public void UpdatePress(GameObject interactor)
        {
            if (_activeInteractor != interactor.transform)
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
            if (_activeInteractor != interactor.transform)
            {
                return;
            }

            _activeInteractor = null;
            _firedInCurrentPress = false;
            SetVisualPress(0f);
        }

        public void Interact(GameObject interactor)
        {
            _onInteract?.Invoke(markerId, new Pose(spawnPoint.position, spawnPoint.rotation));
        }

        private float GetPressAmount(Vector3 interactorWorldPosition)
        {
            var path = pressEnd.position - pressStart.position;
            var pathLengthSquared = path.sqrMagnitude;
            var projected = Vector3.Dot(interactorWorldPosition - pressStart.position, path) / pathLengthSquared;
            return Mathf.Clamp01(projected);
        }

        private void SetVisualPress(float amount)
        {
            pressVisual.position = Vector3.Lerp(pressStart.position, pressEnd.position, amount);
        }
    }
}
