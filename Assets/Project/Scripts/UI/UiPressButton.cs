using System.Collections.Generic;
using Project.Scripts.Interaction;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class UiPressButton : MonoBehaviour, IPressInteraction
    {
        private enum PressInvokeMode
        {
            PressBegan,
            PressEnded
        }

        [SerializeField] private bool interactable = true;
        [SerializeField] private PressInvokeMode invokeMode = PressInvokeMode.PressBegan;
        [Tooltip("Короткая задержка снятия блокировки, если старая кнопка получила выход пальца после своего выключения.")]
        [SerializeField, Min(0f)] private float ownerExitReleaseDelaySeconds = 0.25f;
        [Tooltip("Запасной таймаут блокировки пальца, если Unity не пришлет событие выхода после переключения окна.")]
        [SerializeField, Min(0.1f)] private float releaseLockTimeoutSeconds = 3f;

        [Header("События")]
        [SerializeField] private UnityEvent onPressBegan = new UnityEvent();
        [SerializeField] private UnityEvent onPressed = new UnityEvent();
        [SerializeField] private UnityEvent onPressEnded = new UnityEvent();

        private static readonly Dictionary<int, ReleaseLock> ReleaseLockedInteractors = new Dictionary<int, ReleaseLock>();

        private Transform activeInteractor;
        private int activeInteractorId;
        private bool pressedInvoked;

        public bool Interactable
        {
            get => interactable;
            set
            {
                if (interactable == value)
                {
                    return;
                }

                interactable = value;
                if (!interactable)
                {
                    LockActiveInteractorUntilRelease();
                    ResetPressState();
                }
            }
        }

        public UnityEvent OnPressBegan => onPressBegan;
        public UnityEvent OnPressed => onPressed;
        public UnityEvent OnPressEnded => onPressEnded;

        private void OnEnable()
        {
            ResetPressState();
        }

        private void OnDisable()
        {
            LockActiveInteractorUntilRelease();
            ResetPressState();
        }

        private void OnValidate()
        {
            var triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        public void BeginPress(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            var interactorId = interactor.GetInstanceID();
            if (IsInteractorReleaseLocked(interactorId))
            {
                return;
            }

            if (!interactable || activeInteractor != null)
            {
                return;
            }

            activeInteractor = interactor.transform;
            activeInteractorId = interactorId;
            pressedInvoked = false;
            onPressBegan.Invoke();

            if (invokeMode == PressInvokeMode.PressBegan)
            {
                InvokePressed();
            }
        }

        public void UpdatePress(GameObject interactor)
        {
        }

        public void EndPress(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            var interactorId = interactor.GetInstanceID();
            ReleaseInteractorLock(interactorId, GetInstanceID(), ownerExitReleaseDelaySeconds);

            if (activeInteractor != interactor.transform)
            {
                return;
            }

            activeInteractor = null;
            activeInteractorId = 0;
            pressedInvoked = false;
            onPressEnded.Invoke();

            if (interactable && invokeMode == PressInvokeMode.PressEnded)
            {
                InvokePressed();
            }
        }

        private bool IsInteractorReleaseLocked(int interactorId)
        {
            if (!ReleaseLockedInteractors.TryGetValue(interactorId, out var releaseLock))
            {
                return false;
            }

            if (Time.unscaledTime <= releaseLock.ExpiresAt)
            {
                return true;
            }

            ReleaseLockedInteractors.Remove(interactorId);
            return false;
        }

        private void LockActiveInteractorUntilRelease()
        {
            if (activeInteractor == null || activeInteractorId == 0)
            {
                return;
            }

            ReleaseLockedInteractors[activeInteractorId] = new ReleaseLock(
                Time.unscaledTime + releaseLockTimeoutSeconds,
                GetInstanceID());
        }

        private static void ReleaseInteractorLock(int interactorId, int releaserId, float ownerExitReleaseDelaySeconds)
        {
            if (ReleaseLockedInteractors.TryGetValue(interactorId, out var releaseLock) &&
                releaseLock.OwnerId == releaserId)
            {
                ReleaseLockedInteractors[interactorId] = new ReleaseLock(
                    Time.unscaledTime + ownerExitReleaseDelaySeconds,
                    releaseLock.OwnerId);
                return;
            }

            ReleaseLockedInteractors.Remove(interactorId);
        }

        private void InvokePressed()
        {
            if (pressedInvoked)
            {
                return;
            }

            pressedInvoked = true;
            onPressed.Invoke();
        }

        private void ResetPressState()
        {
            activeInteractor = null;
            activeInteractorId = 0;
            pressedInvoked = false;
        }

        private readonly struct ReleaseLock
        {
            public float ExpiresAt { get; }
            public int OwnerId { get; }

            public ReleaseLock(float expiresAt, int ownerId)
            {
                ExpiresAt = expiresAt;
                OwnerId = ownerId;
            }
        }
    }
}
