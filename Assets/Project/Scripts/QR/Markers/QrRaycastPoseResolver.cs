using System;
using UnityEngine;

public sealed class QrRaycastPoseResolver : MonoBehaviour, IQrPoseResolver
{
    private Camera raycastCamera;
    [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private bool flipHorizontal;
    [SerializeField] private bool flipVertical = true;
    [SerializeField] private Vector2 viewportOffset;
    [SerializeField, Range(0f, 1f)] private float positionSmoothing = 0.25f;
    [SerializeField, Range(0f, 1f)] private float rotationSmoothing = 0.25f;
    [SerializeField] private float snapDistance = 0.2f;
    [SerializeField] private float surfaceOffset = 0.01f;
    [SerializeField] private Transform debugMarker;

    private Vector3 filteredPosition;
    private Quaternion filteredRotation = Quaternion.identity;
    private bool hasFilteredPose;

    private void Awake()
    {
        raycastCamera = Camera.main;
        SetDebugMarkerVisible(false);
    }

    public bool TryResolvePose(in QrDetection detection, out Pose pose)
    {
        pose = default;

        if (!detection.HasImageCenter || detection.FrameWidth <= 0 || detection.FrameHeight <= 0)
        {
            return false;
        }

        var targetCamera = raycastCamera != null ? raycastCamera : Camera.main;
        if (targetCamera == null)
        {
            return false;
        }

        var viewportPoint = new Vector3(
            Mathf.Clamp01(detection.ImageCenter.x / detection.FrameWidth),
            Mathf.Clamp01(detection.ImageCenter.y / detection.FrameHeight),
            0f);

        if (flipHorizontal)
        {
            viewportPoint.x = 1f - viewportPoint.x;
        }

        if (flipVertical)
        {
            viewportPoint.y = 1f - viewportPoint.y;
        }

        viewportPoint.x = Mathf.Clamp01(viewportPoint.x + viewportOffset.x);
        viewportPoint.y = Mathf.Clamp01(viewportPoint.y + viewportOffset.y);

        var ray = targetCamera.ViewportPointToRay(viewportPoint);
        if (!Physics.Raycast(ray, out var hit, maxDistance, raycastMask))
        {
            return false;
        }

        var hitNormal = hit.normal.sqrMagnitude > 0f ? hit.normal.normalized : Vector3.up;
        var forward = Vector3.ProjectOnPlane(targetCamera.transform.forward, hitNormal);
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.Cross(hitNormal, targetCamera.transform.right);
        }

        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = Vector3.forward;
        }

        var resolvedPose = new Pose(
            hit.point + hitNormal * surfaceOffset,
            Quaternion.LookRotation(forward.normalized, hitNormal));
        pose = ApplyPoseFilter(resolvedPose);
        UpdateDebugMarker(pose.position);
        return true;
    }

    public void ClearResolvedPose()
    {
        hasFilteredPose = false;
        SetDebugMarkerVisible(false);
    }

    private Pose ApplyPoseFilter(Pose resolvedPose)
    {
        if (!hasFilteredPose ||
            (filteredPosition - resolvedPose.position).sqrMagnitude > snapDistance * snapDistance)
        {
            filteredPosition = resolvedPose.position;
            filteredRotation = resolvedPose.rotation;
            hasFilteredPose = true;
            return resolvedPose;
        }

        filteredPosition = Vector3.Lerp(filteredPosition, resolvedPose.position, positionSmoothing);
        filteredRotation = Quaternion.Slerp(filteredRotation, resolvedPose.rotation, rotationSmoothing);
        return new Pose(filteredPosition, filteredRotation);
    }

    private void UpdateDebugMarker(Vector3 position)
    {
        if (debugMarker == null)
        {
            return;
        }

        debugMarker.position = position;
        SetDebugMarkerVisible(true);
    }

    private void SetDebugMarkerVisible(bool isVisible)
    {
        if (debugMarker == null || debugMarker.gameObject.activeSelf == isVisible)
        {
            return;
        }

        debugMarker.gameObject.SetActive(isVisible);
    }
}
