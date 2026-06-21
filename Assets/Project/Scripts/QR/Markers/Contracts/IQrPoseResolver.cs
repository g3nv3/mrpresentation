using UnityEngine;

public interface IQrPoseResolver
{
    string LastDebugStatus { get; }
    bool TryResolvePose(in QrDetection detection, QrMarkerDefinition definition, out Pose pose);
    void SetScanProgress(float normalizedProgress, bool isVisible);
    void ClearResolvedPose();
}
