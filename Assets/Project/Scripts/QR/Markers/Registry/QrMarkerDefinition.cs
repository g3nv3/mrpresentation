using System;

using UnityEngine;

[Serializable]
public sealed class QrMarkerDefinition
{
    public enum SpawnMode
    {
        Button = 0,
        DirectObject = 1,
        DirectObjectWithInstallNotification = 2
    }

    [SerializeField] private string markerId;
    [SerializeField] private GameObject prefab;
    [SerializeField] private SpawnMode spawnMode = SpawnMode.Button;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset;
    [SerializeField, Min(0f)] private float qrCodeSizeMeters;
    [SerializeField, Min(21)] private int qrModuleCount = 21;
    [SerializeField] private bool enableMetricPoseRefinement = true;

    public string MarkerId => markerId;
    public GameObject Prefab => prefab;
    public SpawnMode MarkerSpawnMode => spawnMode;
    public bool SpawnsDirectObject =>
        spawnMode == SpawnMode.DirectObject ||
        spawnMode == SpawnMode.DirectObjectWithInstallNotification;
    public bool ShowsInstallNotification =>
        spawnMode == SpawnMode.DirectObjectWithInstallNotification;
    public Vector3 PositionOffset => positionOffset;
    public Vector3 RotationOffset => rotationOffset;
    public float QrCodeSizeMeters => qrCodeSizeMeters;
    public int QrModuleCount => qrModuleCount;
    public bool EnableMetricPoseRefinement => enableMetricPoseRefinement;
    public bool HasMetricPoseConfiguration =>
        enableMetricPoseRefinement &&
        qrCodeSizeMeters > 0f &&
        qrModuleCount >= 21;
}
