using System.Collections.Generic;
using UnityEngine;

public interface IQrPoseResolverView
{
    void Initialize(Transform host);
    void Show(Pose? resolvedPose, IReadOnlyList<Vector3> resultPointPositions);
    void Hide();
}
