using UnityEngine;

public interface IQrPoseResolver
{
    string LastDebugStatus { get; }
    bool TryResolvePose(in QrDetection detection, out Pose pose);
    void ClearResolvedPose();
}
