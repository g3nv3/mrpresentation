using System;
using UnityEngine;

public sealed class QrRaycastPoseResolver : MonoBehaviour, IQrPoseResolver
{
    private Camera raycastCamera;
    [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private bool flipHorizontal;
    [SerializeField] private bool flipVertical = true;
    [SerializeField] private float surfaceOffset = 0.01f;

    private void Awake()
    {
        raycastCamera = Camera.main;
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

        pose = new Pose(
            hit.point + hitNormal * surfaceOffset,
            Quaternion.LookRotation(forward.normalized, hitNormal));
        return true;
    }
}
