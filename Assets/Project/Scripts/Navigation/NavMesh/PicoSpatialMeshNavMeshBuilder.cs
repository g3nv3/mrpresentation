using System;
using System.Collections;
using Unity.AI.Navigation;
using Unity.XR.PXR;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PicoSpatialMeshNavMeshBuilder : MonoBehaviour
{
    [SerializeField] private PXR_SpatialMeshManager spatialMeshManager;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private float rebuildDelay = 0.25f;
    [SerializeField] private bool rebuildOnStart;

    private Coroutine _rebuildRoutine;

    private void Awake()
    {
        if (spatialMeshManager == null)
        {
            spatialMeshManager = GetComponent<PXR_SpatialMeshManager>();
        }

        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
        }
    }

    private void OnEnable()
    {
        if (spatialMeshManager == null)
        {
            return;
        }

        spatialMeshManager.OnSpatialMeshAdded.AddListener(HandleSpatialMeshChanged);
        spatialMeshManager.OnSpatialMeshUpdated.AddListener(HandleSpatialMeshChanged);
        spatialMeshManager.OnSpatialMeshRemoved.AddListener(HandleSpatialMeshRemoved);
    }

    private void Start()
    {
        if (rebuildOnStart)
        {
            RequestRebuild();
        }
    }

    private void OnDisable()
    {
        if (spatialMeshManager == null)
        {
            return;
        }

        spatialMeshManager.OnSpatialMeshAdded.RemoveListener(HandleSpatialMeshChanged);
        spatialMeshManager.OnSpatialMeshUpdated.RemoveListener(HandleSpatialMeshChanged);
        spatialMeshManager.OnSpatialMeshRemoved.RemoveListener(HandleSpatialMeshRemoved);
    }

    public void RequestRebuild()
    {
        if (!isActiveAndEnabled || navMeshSurface == null)
        {
            return;
        }

        if (_rebuildRoutine != null)
        {
            StopCoroutine(_rebuildRoutine);
        }

        _rebuildRoutine = StartCoroutine(RebuildAfterDelay());
    }

    public void RebuildNow()
    {
        if (navMeshSurface == null)
        {
            return;
        }

        navMeshSurface.BuildNavMesh();
    }

    private void HandleSpatialMeshChanged(Guid _, GameObject __)
    {
        RequestRebuild();
    }

    private void HandleSpatialMeshRemoved(Guid _)
    {
        RequestRebuild();
    }

    private IEnumerator RebuildAfterDelay()
    {
        var delay = Mathf.Max(0f, rebuildDelay);
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }
        else
        {
            yield return null;
        }

        _rebuildRoutine = null;
        RebuildNow();
    }
}
