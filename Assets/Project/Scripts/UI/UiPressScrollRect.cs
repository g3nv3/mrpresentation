using Project.Scripts.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ScrollRect), typeof(BoxCollider))]
    [AddComponentMenu("Project/UI/UI Press Scroll Rect")]
    public sealed class UiPressScrollRect : MonoBehaviour, IPressInteraction
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RectTransform content;
        [SerializeField, Min(0f)] private float dragSensitivity = 1f;
        [SerializeField] private bool invertHorizontal;
        [SerializeField] private bool invertVertical;

        [Header("Collider")]
        [SerializeField] private bool syncColliderToViewport = true;
        [SerializeField, Min(0.001f)] private float colliderDepth = 0.02f;

        private Transform activeInteractor;
        private Vector2 startInteractorLocalPosition;
        private Vector2 startNormalizedPosition;

        private void Awake()
        {
            EnsureReferences();
            SyncCollider();
        }

        private void OnEnable()
        {
            activeInteractor = null;
        }

        private void OnDisable()
        {
            activeInteractor = null;
        }

        private void OnValidate()
        {
            EnsureReferences();
            SyncCollider();
        }

        public void BeginPress(GameObject interactor)
        {
            if (activeInteractor != null || interactor == null || !EnsureReferences())
            {
                return;
            }

            if (!TryGetInteractorLocalPosition(interactor.transform, out startInteractorLocalPosition))
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();

            activeInteractor = interactor.transform;
            startNormalizedPosition = scrollRect.normalizedPosition;
        }

        public void UpdatePress(GameObject interactor)
        {
            if (activeInteractor == null || interactor == null || activeInteractor != interactor.transform || !EnsureReferences())
            {
                return;
            }

            if (!TryGetInteractorLocalPosition(activeInteractor, out var currentInteractorLocalPosition))
            {
                return;
            }

            var delta = (currentInteractorLocalPosition - startInteractorLocalPosition) * dragSensitivity;
            var targetNormalizedPosition = startNormalizedPosition;

            if (scrollRect.horizontal)
            {
                targetNormalizedPosition.x = CalculateHorizontalPosition(delta.x);
            }

            if (scrollRect.vertical)
            {
                targetNormalizedPosition.y = CalculateVerticalPosition(delta.y);
            }

            scrollRect.normalizedPosition = targetNormalizedPosition;
            scrollRect.velocity = Vector2.zero;
        }

        public void EndPress(GameObject interactor)
        {
            if (interactor == null || activeInteractor != interactor.transform)
            {
                return;
            }

            activeInteractor = null;
        }

        private float CalculateHorizontalPosition(float deltaX)
        {
            var scrollableWidth = GetScrollableWidth();
            if (scrollableWidth <= Mathf.Epsilon)
            {
                return startNormalizedPosition.x;
            }

            var direction = invertHorizontal ? 1f : -1f;
            return Mathf.Clamp01(startNormalizedPosition.x + direction * deltaX / scrollableWidth);
        }

        private float CalculateVerticalPosition(float deltaY)
        {
            var scrollableHeight = GetScrollableHeight();
            if (scrollableHeight <= Mathf.Epsilon)
            {
                return startNormalizedPosition.y;
            }

            var direction = invertVertical ? 1f : -1f;
            return Mathf.Clamp01(startNormalizedPosition.y + direction * deltaY / scrollableHeight);
        }

        private float GetScrollableWidth()
        {
            return Mathf.Max(0f, content.rect.width - viewport.rect.width);
        }

        private float GetScrollableHeight()
        {
            return Mathf.Max(0f, content.rect.height - viewport.rect.height);
        }

        private bool TryGetInteractorLocalPosition(Transform interactor, out Vector2 localPosition)
        {
            localPosition = default;
            if (interactor == null || viewport == null)
            {
                return false;
            }

            var localPoint = viewport.InverseTransformPoint(interactor.position);
            localPosition = new Vector2(localPoint.x, localPoint.y);
            return true;
        }

        private bool EnsureReferences()
        {
            if (scrollRect == null)
            {
                scrollRect = GetComponent<ScrollRect>();
            }

            if (scrollRect == null)
            {
                return false;
            }

            if (viewport == null)
            {
                viewport = scrollRect.viewport != null
                    ? scrollRect.viewport
                    : scrollRect.GetComponent<RectTransform>();
            }

            if (content == null)
            {
                content = scrollRect.content;
            }

            return viewport != null && content != null;
        }

        private void SyncCollider()
        {
            if (!syncColliderToViewport || viewport == null || !TryGetComponent(out BoxCollider boxCollider))
            {
                return;
            }

            boxCollider.isTrigger = true;

            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);

            var bounds = new Bounds(transform.InverseTransformPoint(corners[0]), Vector3.zero);
            for (var i = 1; i < corners.Length; i++)
            {
                bounds.Encapsulate(transform.InverseTransformPoint(corners[i]));
            }

            var size = bounds.size;
            size.z = Mathf.Max(size.z, colliderDepth);
            boxCollider.center = bounds.center;
            boxCollider.size = size;
        }
    }
}
