using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Scripts.Interaction.Interactive_Objects
{
    public sealed class QrCarousel : MonoBehaviour, IPressInteraction
    {
        private enum CarouselAxis
        {
            X,
            Y,
            Z
        }

        [Serializable]
        public sealed class IntEvent : UnityEvent<int>
        {
        }

        [Header("Layout")]
        [SerializeField] private Transform wheelRoot;
        [SerializeField] private CarouselAxis rotationAxis = CarouselAxis.Y;
        [SerializeField, Min(0f)] private float radius = 0.2f;
        [SerializeField] private float startAngleDegrees;
        [SerializeField] private bool reparentItemsToWheelRoot = true;
        [SerializeField] private bool layoutOnStart = true;
        [SerializeField] private List<Transform> items = new List<Transform>();
        [SerializeField, Min(0)] private int initialIndex;
        [SerializeField] private bool orientItemsToMainCamera = true;

        [Header("Gesture")]
        [SerializeField, Range(0f, 1f)] private float followFactor = 0.25f;
        [SerializeField, Min(0.1f)] private float commitThresholdDegrees = 24f;
        [SerializeField, Min(0f)] private float maxFollowDegrees = 12f;
        [SerializeField] private bool invertGestureDirection;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float snapDuration = 0.2f;
        [SerializeField] private Ease snapEase = Ease.OutCubic;
        [SerializeField, Min(0f)] private float returnDuration = 0.12f;
        [SerializeField] private Ease returnEase = Ease.OutQuad;

        [Header("Events")]
        [SerializeField] private IntEvent onIndexChanged = new IntEvent();

        private readonly List<Transform> _runtimeItems = new List<Transform>();

        private Transform _activeInteractor;
        private bool _stepCommittedInCurrentPress;
        private bool _hasStartDirection;
        private Vector3 _startDirectionOnPlane;
        private float _pressStartAngle;

        private Quaternion _initialLocalRotation;
        private float _currentSnappedAngle;
        private float _renderAngle;
        private int _currentIndex;
        private Tween _rotationTween;

        public int CurrentIndex => _currentIndex;
        public int ItemCount => _runtimeItems.Count;

        private void Awake()
        {
            if (wheelRoot == null)
            {
                wheelRoot = transform;
            }

            _initialLocalRotation = wheelRoot.localRotation;
            RebuildItemCache();

            if (layoutOnStart)
            {
                LayoutItems();
            }

            _currentIndex = WrapIndex(initialIndex, _runtimeItems.Count);
            _currentSnappedAngle = _currentIndex * GetStepAngle();
            _renderAngle = _currentSnappedAngle;
            ApplyAngleImmediate(_renderAngle);
        }

        private void OnDisable()
        {
            KillRotationTween();
            _activeInteractor = null;
            _hasStartDirection = false;
            _stepCommittedInCurrentPress = false;
        }

        private void OnValidate()
        {
            if (wheelRoot == null)
            {
                wheelRoot = transform;
            }

            if (Application.isPlaying)
            {
                return;
            }

            RebuildItemCache();
            if (layoutOnStart)
            {
                LayoutItems();
            }
        }

        private void LateUpdate()
        {
            if (!orientItemsToMainCamera)
            {
                return;
            }

            OrientItemsToMainCamera();
        }

        [ContextMenu("Layout Items")]
        public void LayoutItems()
        {
            if (wheelRoot == null)
            {
                wheelRoot = transform;
            }

            RebuildItemCache();
            if (_runtimeItems.Count == 0)
            {
                return;
            }

            var stepAngle = GetStepAngle();
            GetLayoutBasis(out var axisA, out var axisB);

            for (var i = 0; i < _runtimeItems.Count; i++)
            {
                var item = _runtimeItems[i];
                if (item == null)
                {
                    continue;
                }

                if (reparentItemsToWheelRoot && item.parent != wheelRoot)
                {
                    item.SetParent(wheelRoot, true);
                }

                var angleRad = (startAngleDegrees + stepAngle * i) * Mathf.Deg2Rad;
                var localDirection = axisA * Mathf.Cos(angleRad) + axisB * Mathf.Sin(angleRad);
                var localPosition = localDirection * radius;

                if (item.parent == wheelRoot)
                {
                    item.localPosition = localPosition;
                }
                else
                {
                    item.position = wheelRoot.TransformPoint(localPosition);
                }
            }
        }

        public void BeginPress(GameObject interactor)
        {
            if (_activeInteractor != null || interactor == null || _runtimeItems.Count < 2)
            {
                return;
            }

            _activeInteractor = interactor.transform;
            _stepCommittedInCurrentPress = false;
            _pressStartAngle = _currentSnappedAngle;

            KillRotationTween();
            _renderAngle = _currentSnappedAngle;
            ApplyAngleImmediate(_renderAngle);

            _hasStartDirection = TryGetDirectionOnPlane(_activeInteractor.position, out _startDirectionOnPlane);
        }

        public void UpdatePress(GameObject interactor)
        {
            if (_activeInteractor != interactor.transform || _stepCommittedInCurrentPress || _runtimeItems.Count < 2)
            {
                return;
            }

            if (!TryGetDirectionOnPlane(_activeInteractor.position, out var currentDirection))
            {
                return;
            }

            if (!_hasStartDirection)
            {
                _startDirectionOnPlane = currentDirection;
                _hasStartDirection = true;
                return;
            }

            var signedDelta = Vector3.SignedAngle(
                _startDirectionOnPlane,
                currentDirection,
                GetRotationAxisWorld());

            var followDelta = Mathf.Clamp(signedDelta, -maxFollowDegrees, maxFollowDegrees) * followFactor;
            _renderAngle = _pressStartAngle + followDelta;
            ApplyAngleImmediate(_renderAngle);

            if (Mathf.Abs(signedDelta) < commitThresholdDegrees)
            {
                return;
            }

            var stepDirection = signedDelta > 0f ? 1 : -1;
            if (invertGestureDirection)
            {
                stepDirection *= -1;
            }

            CommitStep(stepDirection);
            _stepCommittedInCurrentPress = true;
        }

        public void EndPress(GameObject interactor)
        {
            if (_activeInteractor != interactor.transform)
            {
                return;
            }

            _activeInteractor = null;
            _hasStartDirection = false;

            if (!_stepCommittedInCurrentPress)
            {
                AnimateToAngle(_currentSnappedAngle, returnDuration, returnEase);
            }

            _stepCommittedInCurrentPress = false;
        }

        public void SetItems(IReadOnlyList<Transform> newItems, bool relayout = true)
        {
            items.Clear();
            if (newItems != null)
            {
                for (var i = 0; i < newItems.Count; i++)
                {
                    items.Add(newItems[i]);
                }
            }

            RebuildItemCache();
            _currentIndex = WrapIndex(0, _runtimeItems.Count);
            _currentSnappedAngle = _currentIndex * GetStepAngle();
            _renderAngle = _currentSnappedAngle;
            ApplyAngleImmediate(_renderAngle);

            if (relayout)
            {
                LayoutItems();
            }
        }

        public void SnapToIndex(int index, bool animated = true)
        {
            if (_runtimeItems.Count == 0)
            {
                return;
            }

            var previousIndex = _currentIndex;
            _currentIndex = WrapIndex(index, _runtimeItems.Count);

            var stepAngle = GetStepAngle();
            var forwardSteps = (_currentIndex - previousIndex + _runtimeItems.Count) % _runtimeItems.Count;
            var backwardSteps = (previousIndex - _currentIndex + _runtimeItems.Count) % _runtimeItems.Count;
            var deltaSteps = forwardSteps <= backwardSteps ? forwardSteps : -backwardSteps;
            _currentSnappedAngle += deltaSteps * stepAngle;

            if (animated)
            {
                AnimateToAngle(_currentSnappedAngle, snapDuration, snapEase);
            }
            else
            {
                KillRotationTween();
                _renderAngle = _currentSnappedAngle;
                ApplyAngleImmediate(_renderAngle);
            }

            onIndexChanged?.Invoke(_currentIndex);
        }

        private void CommitStep(int stepDirection)
        {
            if (_runtimeItems.Count < 2 || stepDirection == 0)
            {
                return;
            }

            _currentIndex = WrapIndex(_currentIndex + stepDirection, _runtimeItems.Count);
            _currentSnappedAngle += stepDirection * GetStepAngle();
            AnimateToAngle(_currentSnappedAngle, snapDuration, snapEase);
            onIndexChanged?.Invoke(_currentIndex);
        }

        private void AnimateToAngle(float targetAngle, float duration, Ease ease)
        {
            KillRotationTween();

            if (duration <= 0f)
            {
                _renderAngle = targetAngle;
                ApplyAngleImmediate(_renderAngle);
                return;
            }

            var tweenStart = _renderAngle;
            _rotationTween = DOTween.To(
                    () => tweenStart,
                    value =>
                    {
                        tweenStart = value;
                        _renderAngle = value;
                        ApplyAngleImmediate(_renderAngle);
                    },
                    targetAngle,
                    duration)
                .SetEase(ease)
                .SetTarget(this);
        }

        private void KillRotationTween()
        {
            if (_rotationTween == null)
            {
                return;
            }

            if (_rotationTween.IsActive())
            {
                _rotationTween.Kill(false);
            }

            _rotationTween = null;
        }

        private void RebuildItemCache()
        {
            _runtimeItems.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || _runtimeItems.Contains(item))
                {
                    continue;
                }

                _runtimeItems.Add(item);
            }
        }

        private float GetStepAngle()
        {
            return _runtimeItems.Count > 0 ? 360f / _runtimeItems.Count : 0f;
        }

        private void ApplyAngleImmediate(float angle)
        {
            if (wheelRoot == null)
            {
                return;
            }

            wheelRoot.localRotation = _initialLocalRotation * Quaternion.AngleAxis(angle, GetRotationAxisLocal());
        }

        private bool TryGetDirectionOnPlane(Vector3 worldPosition, out Vector3 directionOnPlane)
        {
            if (wheelRoot == null)
            {
                directionOnPlane = default;
                return false;
            }

            var radial = worldPosition - wheelRoot.position;
            var projected = Vector3.ProjectOnPlane(radial, GetRotationAxisWorld());
            if (projected.sqrMagnitude < 0.000001f)
            {
                directionOnPlane = default;
                return false;
            }

            directionOnPlane = projected.normalized;
            return true;
        }

        private void OrientItemsToMainCamera()
        {
            if (_runtimeItems.Count == 0)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            var axis = GetRotationAxisWorld();
            for (var i = 0; i < _runtimeItems.Count; i++)
            {
                var item = _runtimeItems[i];
                if (item == null)
                {
                    continue;
                }

                var toCamera = mainCamera.transform.position - item.position;
                var planarToCamera = Vector3.ProjectOnPlane(toCamera, axis);
                if (planarToCamera.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                item.rotation = Quaternion.LookRotation(planarToCamera.normalized, axis);
            }
        }

        private void GetLayoutBasis(out Vector3 axisA, out Vector3 axisB)
        {
            var axis = GetRotationAxisLocal();
            var reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up;

            axisA = Vector3.Cross(axis, reference).normalized;
            axisB = Vector3.Cross(axis, axisA).normalized;
        }

        private Vector3 GetRotationAxisLocal()
        {
            switch (rotationAxis)
            {
                case CarouselAxis.X:
                    return Vector3.right;
                case CarouselAxis.Y:
                    return Vector3.up;
                default:
                    return Vector3.forward;
            }
        }

        private Vector3 GetRotationAxisWorld()
        {
            return wheelRoot.TransformDirection(GetRotationAxisLocal()).normalized;
        }

        private static int WrapIndex(int index, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }
    }
}
