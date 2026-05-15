using UnityEngine;

public readonly struct AnchorObservationResult
{
    public enum ObservationState
    {
        Invalid = 0,
        Collecting = 1,
        Unstable = 2,
        Created = 3,
        AlreadyAnchored = 4
    }

    public readonly ObservationState State;
    public readonly int SampleCount;
    public readonly int RequiredSampleCount;
    public readonly Pose Pose;
    public readonly float MaxPositionSpreadMeters;
    public readonly float MaxRotationSpreadDegrees;

    private AnchorObservationResult(
        ObservationState state,
        int sampleCount,
        int requiredSampleCount,
        in Pose pose,
        float maxPositionSpreadMeters,
        float maxRotationSpreadDegrees)
    {
        State = state;
        SampleCount = sampleCount;
        RequiredSampleCount = requiredSampleCount;
        Pose = pose;
        MaxPositionSpreadMeters = maxPositionSpreadMeters;
        MaxRotationSpreadDegrees = maxRotationSpreadDegrees;
    }

    public static AnchorObservationResult Invalid =>
        new AnchorObservationResult(ObservationState.Invalid, 0, 0, default, 0f, 0f);

    public static AnchorObservationResult Collecting(
        int sampleCount,
        int requiredSampleCount,
        in Pose pose,
        float maxPositionSpreadMeters,
        float maxRotationSpreadDegrees)
    {
        return new AnchorObservationResult(
            ObservationState.Collecting,
            sampleCount,
            requiredSampleCount,
            pose,
            maxPositionSpreadMeters,
            maxRotationSpreadDegrees);
    }

    public static AnchorObservationResult Unstable(
        int sampleCount,
        int requiredSampleCount,
        in Pose pose,
        float maxPositionSpreadMeters,
        float maxRotationSpreadDegrees)
    {
        return new AnchorObservationResult(
            ObservationState.Unstable,
            sampleCount,
            requiredSampleCount,
            pose,
            maxPositionSpreadMeters,
            maxRotationSpreadDegrees);
    }

    public static AnchorObservationResult Created(
        int requiredSampleCount,
        in Pose pose,
        float maxPositionSpreadMeters,
        float maxRotationSpreadDegrees)
    {
        return new AnchorObservationResult(
            ObservationState.Created,
            requiredSampleCount,
            requiredSampleCount,
            pose,
            maxPositionSpreadMeters,
            maxRotationSpreadDegrees);
    }

    public static AnchorObservationResult AlreadyAnchored(int requiredSampleCount, in Pose pose)
    {
        return new AnchorObservationResult(
            ObservationState.AlreadyAnchored,
            requiredSampleCount,
            requiredSampleCount,
            pose,
            0f,
            0f);
    }
}
