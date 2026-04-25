using System;
using System.Collections.Generic;

using UnityEngine;

public sealed class QrMarkerRegistry : MonoBehaviour, IQrMarkerRegistry
{
    [SerializeField] private List<QrMarkerDefinition> markers = new List<QrMarkerDefinition>();

    private Dictionary<string, QrMarkerDefinition> definitionsById;

    public IReadOnlyList<QrMarkerDefinition> Markers => markers;

    public bool TryGet(string markerId, out QrMarkerDefinition definition)
    {
        EnsureLookup();
        return definitionsById.TryGetValue(markerId, out definition);
    }

    private void Awake()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    private void EnsureLookup()
    {
        if (definitionsById == null)
        {
            RebuildLookup();
        }
    }

    private void RebuildLookup()
    {
        definitionsById = new Dictionary<string, QrMarkerDefinition>(StringComparer.Ordinal);

        for (var i = 0; i < markers.Count; i++)
        {
            var definition = markers[i];
            if (definition == null || string.IsNullOrWhiteSpace(definition.MarkerId))
            {
                continue;
            }

            definitionsById[definition.MarkerId.Trim()] = definition;
        }
    }
}

[Serializable]
public sealed class QrMarkerDefinition
{
    public enum SpawnMode
    {
        Button = 0,
        DirectObject = 1
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
