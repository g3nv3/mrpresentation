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
    [SerializeField] private string markerId;
    [SerializeField] private GameObject prefab;
    [SerializeField] private Vector3 positionOffset;
    [SerializeField] private Vector3 rotationOffset;

    public string MarkerId => markerId;
    public GameObject Prefab => prefab;
    public Vector3 PositionOffset => positionOffset;
    public Vector3 RotationOffset => rotationOffset;
}
