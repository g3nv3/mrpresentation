using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class OcclusionOverlayMaterialController : MonoBehaviour
{
    public enum DisableMode
    {
        UseDisabledStateMaterial = 0,
        RestoreCapturedMaterials = 1
    }

    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool useTaggedSceneRenderers = true;
    [SerializeField] private string targetTag = "MRMesh";
    [SerializeField] private bool skipRenderersWithoutMesh = true;
    [SerializeField] private Material overlayMaterial;
    [SerializeField] private Material disabledStateMaterial;
    [SerializeField] private DisableMode disableMode = DisableMode.UseDisabledStateMaterial;
    [SerializeField] private bool enableOverlayOnStart;

    private Material[][] _originalSharedMaterials;
    private bool _isOverlayEnabled;
    private bool _isCached;

    public bool IsOverlayEnabled => _isOverlayEnabled;

    private void Awake()
    {
        CacheOriginalMaterials();

        if (enableOverlayOnStart)
            EnableOverlay();
    }

    private void OnDestroy()
    {
        if (_isOverlayEnabled)
            DisableOverlay();
    }

    public void EnableOverlay()
    {
        if (_isOverlayEnabled)
            return;

        ResolveTargetsForApply();

        if (overlayMaterial == null)
        {
            Debug.LogWarning($"{nameof(OcclusionOverlayMaterialController)} requires an overlay material.", this);
            return;
        }

        CacheOriginalMaterials();

        ApplySingleMaterialToTargets(overlayMaterial);

        _isOverlayEnabled = true;
    }

    public void DisableOverlay()
    {
        ResolveTargetsForApply();

        if (!_isOverlayEnabled)
            return;

        if (disableMode == DisableMode.UseDisabledStateMaterial && disabledStateMaterial != null)
            ApplySingleMaterialToTargets(disabledStateMaterial);
        else
            RestoreCapturedMaterials();

        _isOverlayEnabled = false;
    }

    public void ToggleOverlay()
    {
        SetOverlayEnabled(!_isOverlayEnabled);
    }

    public void SetOverlayEnabled(bool enabled)
    {
        if (enabled)
            EnableOverlay();
        else
            DisableOverlay();
    }

    public void RefreshTargetsFromSceneByTag()
    {
        if (string.IsNullOrWhiteSpace(targetTag))
            return;

        var allRenderers = FindObjectsOfType<Renderer>(true);
        var found = new List<Renderer>(allRenderers.Length);

        for (var i = 0; i < allRenderers.Length; i++)
        {
            var renderer = allRenderers[i];
            if (renderer == null || !renderer.CompareTag(targetTag))
                continue;

            if (skipRenderersWithoutMesh && !HasUsableMesh(renderer))
                continue;

            found.Add(renderer);
        }

        var refreshedTargets = found.ToArray();
        if (IsSameRendererSet(targetRenderers, refreshedTargets))
            return;

        targetRenderers = refreshedTargets;
        _isCached = false;
    }

    private void CacheOriginalMaterials()
    {
        ResolveTargetsForApply();

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);

        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            _originalSharedMaterials = Array.Empty<Material[]>();
            _isCached = true;
            return;
        }

        if (_isCached && _originalSharedMaterials != null && _originalSharedMaterials.Length == targetRenderers.Length)
            return;

        _originalSharedMaterials = new Material[targetRenderers.Length][];

        for (var i = 0; i < targetRenderers.Length; i++)
            _originalSharedMaterials[i] = targetRenderers[i] != null ? targetRenderers[i].sharedMaterials : Array.Empty<Material>();

        _isCached = true;
    }

    private void ResolveTargetsForApply()
    {
        if (useTaggedSceneRenderers)
        {
            RefreshTargetsFromSceneByTag();
            return;
        }

        if (targetRenderers == null || targetRenderers.Length == 0)
            targetRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private static bool HasUsableMesh(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            return skinnedMeshRenderer.sharedMesh != null;

        var meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null)
            return meshFilter.sharedMesh != null;

        return true;
    }

    private static bool IsSameRendererSet(Renderer[] left, Renderer[] right)
    {
        if (ReferenceEquals(left, right))
            return true;

        if (left == null || right == null || left.Length != right.Length)
            return false;

        for (var i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
                return false;
        }

        return true;
    }

    private void RestoreCapturedMaterials()
    {
        if (!_isCached || _originalSharedMaterials == null)
            return;

        for (var i = 0; i < targetRenderers.Length; i++)
        {
            var targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
                continue;

            if (i >= _originalSharedMaterials.Length)
                continue;

            targetRenderer.sharedMaterials = _originalSharedMaterials[i];
        }
    }

    private void ApplySingleMaterialToTargets(Material material)
    {
        for (var i = 0; i < targetRenderers.Length; i++)
        {
            var targetRenderer = targetRenderers[i];
            if (targetRenderer == null)
                continue;

            var slotCount = GetSlotCount(i, targetRenderer);
            var materials = new Material[slotCount];

            for (var slot = 0; slot < slotCount; slot++)
                materials[slot] = material;

            targetRenderer.sharedMaterials = materials;
        }
    }

    private int GetSlotCount(int rendererIndex, Renderer renderer)
    {
        if (_originalSharedMaterials != null && rendererIndex < _originalSharedMaterials.Length)
        {
            var original = _originalSharedMaterials[rendererIndex];
            if (original != null && original.Length > 0)
                return original.Length;
        }

        var current = renderer.sharedMaterials;
        return current != null && current.Length > 0 ? current.Length : 1;
    }
}
