using System;
using Unity.XR.PXR;
using UnityEngine;

public sealed partial class QrCameraRayPoseResolver
{
    private bool TryRefineMetricPose(
        CameraProjectionData context,
        in QrDetection detection,
        QrMarkerDefinition definition,
        in Pose basePose,
        out Pose refinedPose,
        out string metricPoseStatus)
    {
        refinedPose = default;

        if (!enableMetricPoseRefinement)
        {
            metricPoseStatus = "metric=disabled";
            return false;
        }

        if (definition == null || !definition.HasMetricPoseConfiguration)
        {
            metricPoseStatus = "metric=skipped(no-config)";
            return false;
        }

        if (!TryGetObservedFinderPoints(context.Intrinsics, detection, out var observedFinderPoints))
        {
            metricPoseStatus = "metric=skipped(no-finders)";
            return false;
        }

        if (!TryBuildProjectionModel(
                context.Intrinsics,
                detection.FrameWidth,
                detection.FrameHeight,
                out var projectionModel))
        {
            metricPoseStatus = "metric=skipped(no-projection)";
            return false;
        }

        var localFinderPoints = BuildFinderLocalPoints(definition, GetResultPointsMirrorX());
        if (!TryOptimizeMetricPose(
                projectionModel,
                context.CameraWorldPose,
                basePose,
                localFinderPoints,
                observedFinderPoints,
                out refinedPose,
                out var reprojectionError))
        {
            metricPoseStatus = "metric=skipped(opt-failed)";
            return false;
        }

        var translationDelta = Vector3.Distance(basePose.position, refinedPose.position);
        var rotationDelta = Quaternion.Angle(basePose.rotation, refinedPose.rotation);
        if (reprojectionError > metricPoseMaxReprojectionErrorPixels)
        {
            metricPoseStatus =
                $"metric=skipped(reproj err={reprojectionError:F2}px max={metricPoseMaxReprojectionErrorPixels:F1})";
            return false;
        }

        if (translationDelta > metricPoseMaxTranslationDelta ||
            rotationDelta > metricPoseMaxRotationDeltaDegrees)
        {
            metricPoseStatus =
                $"metric=skipped(outlier dt={translationDelta:F3} dr={rotationDelta:F1})";
            return false;
        }

        metricPoseStatus =
            $"metric=refined err={reprojectionError:F2}px dt={translationDelta:F3} dr={rotationDelta:F1} {FormatMetricDefinition(definition)}";
        return true;
    }

    private bool TryGetObservedFinderPoints(
        XrCameraIntrinsics intrinsics,
        in QrDetection detection,
        out Vector2[] observedFinderPoints)
    {
        observedFinderPoints = null;
        if (!TryGetOrderedFinderPoints(
                detection.ImageResultPoints,
                out var bottomLeftImage,
                out var topLeftImage,
                out var topRightImage))
        {
            return false;
        }

        observedFinderPoints = new Vector2[3];
        return TryAdjustAndScaleFinderPoint(
                   intrinsics,
                   bottomLeftImage,
                   detection.FrameWidth,
                   detection.FrameHeight,
                   out observedFinderPoints[0]) &&
               TryAdjustAndScaleFinderPoint(
                   intrinsics,
                   topLeftImage,
                   detection.FrameWidth,
                   detection.FrameHeight,
                   out observedFinderPoints[1]) &&
               TryAdjustAndScaleFinderPoint(
                   intrinsics,
                   topRightImage,
                   detection.FrameWidth,
                   detection.FrameHeight,
                   out observedFinderPoints[2]);
    }

    private bool TryAdjustAndScaleFinderPoint(
        XrCameraIntrinsics intrinsics,
        Vector2 imagePoint,
        int frameWidth,
        int frameHeight,
        out Vector2 adjustedFinderPoint)
    {
        adjustedFinderPoint = default;
        if (!TryAdjustImagePoint(imagePoint, frameWidth, frameHeight, GetResultPointsMirrorX(), out var adjustedImagePoint))
        {
            return false;
        }

        adjustedFinderPoint = adjustedImagePoint;
        return IsFinite(adjustedFinderPoint);
    }

    private static Vector3[] BuildFinderLocalPoints(QrMarkerDefinition definition, bool mirrorHorizontally)
    {
        var moduleCount = Mathf.Max(21, definition.QrModuleCount);
        var finderCenterSpacing = definition.QrCodeSizeMeters * (moduleCount - 7f) / moduleCount;
        var halfSpacing = finderCenterSpacing * 0.5f;
        var horizontalSign = mirrorHorizontally ? -1f : 1f;
        return new[]
        {
            new Vector3(-halfSpacing * horizontalSign, 0f, -halfSpacing),
            new Vector3(-halfSpacing * horizontalSign, 0f, halfSpacing),
            new Vector3(halfSpacing * horizontalSign, 0f, halfSpacing)
        };
    }

    private bool TryOptimizeMetricPose(
        in CameraProjectionModel projectionModel,
        in Pose cameraWorldPose,
        in Pose basePose,
        Vector3[] localFinderPoints,
        Vector2[] observedFinderPoints,
        out Pose refinedPose,
        out float reprojectionErrorPixels)
    {
        refinedPose = default;
        reprojectionErrorPixels = 0f;

        var cameraFromWorld = InvertPose(cameraWorldPose);
        var markerInCameraPose = TransformPose(cameraFromWorld, basePose);
        if (!TryComputeReprojectionError(
                projectionModel,
                markerInCameraPose,
                localFinderPoints,
                observedFinderPoints,
                out var residuals,
                out var currentCost))
        {
            return false;
        }

        var damping = Mathf.Max(metricPoseInitialDamping, 0.000001f);
        for (var iteration = 0; iteration < Mathf.Max(1, metricPoseRefinementIterations); iteration++)
        {
            if (!TryBuildNormalEquations(
                    projectionModel,
                    markerInCameraPose,
                    localFinderPoints,
                    observedFinderPoints,
                    residuals,
                    damping,
                    out var normalMatrix,
                    out var gradient))
            {
                break;
            }

            if (!TrySolveLinearSystem6x6(normalMatrix, gradient, out var delta))
            {
                break;
            }

            var candidatePose = ApplyPoseDelta(markerInCameraPose, delta);
            if (!TryComputeReprojectionError(
                    projectionModel,
                    candidatePose,
                    localFinderPoints,
                    observedFinderPoints,
                    out var candidateResiduals,
                    out var candidateCost))
            {
                damping *= 4f;
                continue;
            }

            if (candidateCost + 0.0001f < currentCost)
            {
                markerInCameraPose = candidatePose;
                residuals = candidateResiduals;
                currentCost = candidateCost;
                damping = Mathf.Max(damping * 0.5f, 0.000001f);

                if (GetDeltaMagnitude(delta) <= 0.0001f)
                {
                    break;
                }
            }
            else
            {
                damping *= 4f;
            }
        }

        refinedPose = TransformPose(cameraWorldPose, markerInCameraPose);
        reprojectionErrorPixels = Mathf.Sqrt(currentCost / Mathf.Max(1, observedFinderPoints.Length * 2));
        return true;
    }

    private bool TryBuildNormalEquations(
        in CameraProjectionModel projectionModel,
        in Pose pose,
        Vector3[] localFinderPoints,
        Vector2[] observedFinderPoints,
        float[] residuals,
        float damping,
        out float[,] normalMatrix,
        out float[] gradient)
    {
        const int parameterCount = 6;
        normalMatrix = new float[parameterCount, parameterCount];
        gradient = new float[parameterCount];
        var jacobianColumns = new float[parameterCount][];

        for (var parameterIndex = 0; parameterIndex < parameterCount; parameterIndex++)
        {
            var step = parameterIndex < 3
                ? metricPoseRotationStepRadians
                : metricPoseTranslationStepMeters;
            var perturbedPose = ApplyPoseDelta(pose, CreateUnitDelta(parameterIndex, step));

            if (!TryComputeReprojectionError(
                    projectionModel,
                    perturbedPose,
                    localFinderPoints,
                    observedFinderPoints,
                    out var perturbedResiduals,
                    out _))
            {
                return false;
            }

            var jacobianColumn = new float[residuals.Length];
            jacobianColumns[parameterIndex] = jacobianColumn;
            for (var residualIndex = 0; residualIndex < residuals.Length; residualIndex++)
            {
                jacobianColumn[residualIndex] = (perturbedResiduals[residualIndex] - residuals[residualIndex]) / step;
            }
        }

        for (var row = 0; row < parameterCount; row++)
        {
            var rowJacobian = jacobianColumns[row];
            for (var residualIndex = 0; residualIndex < residuals.Length; residualIndex++)
            {
                gradient[row] += rowJacobian[residualIndex] * residuals[residualIndex];
            }

            for (var column = row; column < parameterCount; column++)
            {
                var value = 0f;
                var columnJacobian = jacobianColumns[column];
                for (var residualIndex = 0; residualIndex < residuals.Length; residualIndex++)
                {
                    value += rowJacobian[residualIndex] * columnJacobian[residualIndex];
                }

                normalMatrix[row, column] = value;
                normalMatrix[column, row] = value;
            }
        }

        for (var i = 0; i < parameterCount; i++)
        {
            normalMatrix[i, i] += Mathf.Max(damping, 0.000001f);
            gradient[i] = -gradient[i];
        }

        return true;
    }

    private static float[] CreateUnitDelta(int parameterIndex, float value)
    {
        var delta = new float[6];
        delta[parameterIndex] = value;
        return delta;
    }

    private bool TryComputeReprojectionError(
        in CameraProjectionModel projectionModel,
        in Pose pose,
        Vector3[] localFinderPoints,
        Vector2[] observedFinderPoints,
        out float[] residuals,
        out float cost)
    {
        residuals = new float[localFinderPoints.Length * 2];
        cost = 0f;

        for (var i = 0; i < localFinderPoints.Length; i++)
        {
            var cameraPoint = pose.position + pose.rotation * localFinderPoints[i];
            if (!TryProjectCameraPoint(projectionModel, cameraPoint, out var projectedPoint))
            {
                residuals = null;
                cost = float.PositiveInfinity;
                return false;
            }

            var residualX = projectedPoint.x - observedFinderPoints[i].x;
            var residualY = projectedPoint.y - observedFinderPoints[i].y;
            var residualIndex = i * 2;
            residuals[residualIndex] = residualX;
            residuals[residualIndex + 1] = residualY;
            cost += (residualX * residualX) + (residualY * residualY);
        }

        return true;
    }

    private bool TryProjectCameraPoint(
        in CameraProjectionModel projectionModel,
        Vector3 cameraPoint,
        out Vector2 projectedPoint)
    {
        projectedPoint = default;

        var depth = invertCameraSpaceForward ? -cameraPoint.z : cameraPoint.z;
        if (depth <= MinAxisMagnitude)
        {
            return false;
        }

        var fx = Mathf.Max(projectionModel.Fx, MinAxisMagnitude);
        var fy = Mathf.Max(projectionModel.Fy, MinAxisMagnitude);
        projectedPoint = new Vector2(
            (fx * cameraPoint.x / depth) + projectionModel.Cx,
            projectionModel.Cy - (fy * cameraPoint.y / depth));
        return IsFinite(projectedPoint);
    }

    private void PopulateDebugPointsFromPose(QrMarkerDefinition definition, in Pose markerPose)
    {
        debugResultPointPositions.Clear();
        if (definition == null || !definition.HasMetricPoseConfiguration)
        {
            return;
        }

        var localFinderPoints = BuildFinderLocalPoints(definition, GetResultPointsMirrorX());
        for (var i = 0; i < localFinderPoints.Length; i++)
        {
            debugResultPointPositions.Add(markerPose.position + markerPose.rotation * localFinderPoints[i]);
        }
    }

    private static Pose ApplyPoseDelta(in Pose pose, float[] delta)
    {
        var rotationDelta = QuaternionFromRotationVector(new Vector3(delta[0], delta[1], delta[2]));
        return new Pose(
            pose.position + new Vector3(delta[3], delta[4], delta[5]),
            NormalizeQuaternion(rotationDelta * pose.rotation));
    }

    private static Quaternion QuaternionFromRotationVector(Vector3 rotationVector)
    {
        var angle = rotationVector.magnitude;
        if (angle <= MinAxisMagnitude)
        {
            return Quaternion.identity;
        }

        return Quaternion.AngleAxis(angle * Mathf.Rad2Deg, rotationVector / angle);
    }

    private static float GetDeltaMagnitude(float[] delta)
    {
        var magnitude = 0f;
        for (var i = 0; i < delta.Length; i++)
        {
            magnitude += delta[i] * delta[i];
        }

        return Mathf.Sqrt(magnitude);
    }

    private static bool TrySolveLinearSystem6x6(float[,] matrix, float[] vector, out float[] solution)
    {
        const int size = 6;
        solution = new float[size];
        var augmented = new double[size, size + 1];

        for (var row = 0; row < size; row++)
        {
            for (var column = 0; column < size; column++)
            {
                augmented[row, column] = matrix[row, column];
            }

            augmented[row, size] = vector[row];
        }

        for (var pivotIndex = 0; pivotIndex < size; pivotIndex++)
        {
            var pivotRow = pivotIndex;
            var pivotMagnitude = Math.Abs(augmented[pivotRow, pivotIndex]);
            for (var row = pivotIndex + 1; row < size; row++)
            {
                var magnitude = Math.Abs(augmented[row, pivotIndex]);
                if (magnitude > pivotMagnitude)
                {
                    pivotMagnitude = magnitude;
                    pivotRow = row;
                }
            }

            if (pivotMagnitude <= 0.000000001d)
            {
                return false;
            }

            if (pivotRow != pivotIndex)
            {
                for (var column = pivotIndex; column <= size; column++)
                {
                    var temp = augmented[pivotIndex, column];
                    augmented[pivotIndex, column] = augmented[pivotRow, column];
                    augmented[pivotRow, column] = temp;
                }
            }

            var pivot = augmented[pivotIndex, pivotIndex];
            for (var row = pivotIndex + 1; row < size; row++)
            {
                var factor = augmented[row, pivotIndex] / pivot;
                if (Math.Abs(factor) <= 0d)
                {
                    continue;
                }

                for (var column = pivotIndex; column <= size; column++)
                {
                    augmented[row, column] -= factor * augmented[pivotIndex, column];
                }
            }
        }

        for (var row = size - 1; row >= 0; row--)
        {
            var value = augmented[row, size];
            for (var column = row + 1; column < size; column++)
            {
                value -= augmented[row, column] * solution[column];
            }

            var diagonal = augmented[row, row];
            if (Math.Abs(diagonal) <= 0.000000001d)
            {
                return false;
            }

            solution[row] = (float)(value / diagonal);
        }

        return true;
    }
}
