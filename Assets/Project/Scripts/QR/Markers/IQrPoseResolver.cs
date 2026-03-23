using UnityEngine;

public interface IQrPoseResolver
{
    bool TryResolvePose(in QrDetection detection, out Pose pose);
    void ClearResolvedPose();
}
