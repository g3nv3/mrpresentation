using UnityEngine;

public sealed class QrManualAnchorVisualOptions
{
    public GameObject AnchorPrefab { get; }
    public QrManualAnchorVisualOptions(GameObject anchorPrefab) => AnchorPrefab = anchorPrefab;
}