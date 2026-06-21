using UnityEngine;

public sealed partial class QrCameraRayPoseResolver
{
    private void UpdateView(Pose? resolvedPose)
    {
        var view = GetActiveView();
        view?.Show(resolvedPose, debugResultPointPositions);
        statusBarView?.Show(resolvedPose, debugResultPointPositions);
    }

    private void InitializeView()
    {
        var view = GetActiveView();
        view?.Initialize(transform);
        statusBarView?.Initialize(transform);
    }

    private void HideView()
    {
        var view = GetActiveView();
        view?.Hide();
        statusBarView?.Hide();
    }

    public void SetScanProgress(float normalizedProgress, bool isVisible)
    {
        statusBarView?.SetProgress(normalizedProgress, isVisible);
    }

    private IQrPoseResolverView GetActiveView()
    {
        if (activeView != null)
        {
            return activeView;
        }

        activeView = viewStrategy switch
        {
            PoseResolverViewStrategyType.Debug => debugView ??= new QrPoseResolverDebugView(),
            PoseResolverViewStrategyType.CenterTransform => centerTransformView ??= new QrPoseResolverCenterTransformView(),
            _ => null
        };

        return activeView;
    }
}
