using System;
using System.Collections;
using Unity.AI.Navigation;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class PicoSpatialMeshNavMeshBuilder : MonoBehaviour
{
    [Tooltip("Менеджер Pico spatial mesh, который сообщает о добавлении, обновлении и удалении mesh.")]
    [SerializeField] private PXR_SpatialMeshManager spatialMeshManager;

    [Tooltip("NavMeshSurface, который перестраивается при изменении Pico spatial mesh.")]
    [SerializeField] private NavMeshSurface navMeshSurface;

    [Tooltip("Задержка перед перестроением после изменения spatial mesh, чтобы объединять частые обновления.")]
    [SerializeField] private float rebuildDelay = 0.25f;

    [Tooltip("Запрашивает первичное перестроение NavMesh в Start.")]
    [SerializeField] private bool rebuildOnStart;
    [SerializeField] private bool verboseLogging;
    [SerializeField, Min(0f)] private float emptyNavMeshRetryDelay = 1f;
    [SerializeField, Min(0)] private int maxEmptyNavMeshRetries = 5;

    private Coroutine _rebuildRoutine;
    private int _emptyNavMeshRetryCount;

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
            Log("Rebuild skipped: component is disabled or NavMeshSurface is missing.");
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
            Log("Rebuild skipped: NavMeshSurface is missing.");
            return;
        }

        var before = NavMesh.CalculateTriangulation();
        var buildSettings = NavMesh.GetSettingsByID(navMeshSurface.agentTypeID);
        Log(
            $"BuildNavMesh requested. Before: vertices={before.vertices.Length}, indices={before.indices.Length}. " +
            $"Agent radius={buildSettings.agentRadius:0.###}, height={buildSettings.agentHeight:0.###}, " +
            $"slope={buildSettings.agentSlope:0.###}, climb={buildSettings.agentClimb:0.###}. " +
            GetSourceSummary());

        try
        {
            navMeshSurface.BuildNavMesh();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            return;
        }

        var after = NavMesh.CalculateTriangulation();
        Log($"BuildNavMesh completed. After: vertices={after.vertices.Length}, indices={after.indices.Length}.");

        if (after.vertices.Length > 0)
        {
            _emptyNavMeshRetryCount = 0;
            return;
        }

        if (emptyNavMeshRetryDelay <= 0f || _emptyNavMeshRetryCount >= maxEmptyNavMeshRetries)
        {
            Log("NavMesh is still empty after rebuild and retry limit is reached.");
            return;
        }

        _emptyNavMeshRetryCount++;
        Log($"NavMesh is empty after rebuild. Scheduling retry {_emptyNavMeshRetryCount}/{maxEmptyNavMeshRetries}.");
        RequestRebuild();
    }

    private void HandleSpatialMeshChanged(Guid _, GameObject meshObject)
    {
        Log($"Spatial mesh changed: {(meshObject != null ? meshObject.name : "null")}.");
        _emptyNavMeshRetryCount = 0;
        RequestRebuild();
    }

    private void HandleSpatialMeshRemoved(Guid _)
    {
        Log("Spatial mesh removed.");
        _emptyNavMeshRetryCount = 0;
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

    private string GetSourceSummary()
    {
        var sourceRoot = navMeshSurface != null ? navMeshSurface.transform : transform;
        var layerMask = navMeshSurface != null
            ? navMeshSurface.layerMask
            : new LayerMask { value = ~0 };
        var meshFilters = sourceRoot.GetComponentsInChildren<MeshFilter>(false);
        var meshColliders = sourceRoot.GetComponentsInChildren<MeshCollider>(false);
        var renderersOnMask = 0;
        var collidersOnMask = 0;
        var meshVertices = 0;
        var colliderVertices = 0;

        for (var i = 0; i < meshFilters.Length; i++)
        {
            var meshFilter = meshFilters[i];
            if (!IsLayerIncluded(meshFilter.gameObject.layer, layerMask) || meshFilter.sharedMesh == null)
            {
                continue;
            }

            renderersOnMask++;
            meshVertices += meshFilter.sharedMesh.vertexCount;
        }

        for (var i = 0; i < meshColliders.Length; i++)
        {
            var meshCollider = meshColliders[i];
            if (!IsLayerIncluded(meshCollider.gameObject.layer, layerMask) || meshCollider.sharedMesh == null)
            {
                continue;
            }

            collidersOnMask++;
            colliderVertices += meshCollider.sharedMesh.vertexCount;
        }

        return
            $"Sources under '{sourceRoot.name}': meshFilters={renderersOnMask}/{meshFilters.Length}, " +
            $"meshVertices={meshVertices}, meshColliders={collidersOnMask}/{meshColliders.Length}, " +
            $"colliderVertices={colliderVertices}, layerMask={layerMask.value}.";
    }

    private static bool IsLayerIncluded(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void Log(string message)
    {
        if (verboseLogging)
        {
            Debug.Log($"[PicoSpatialMeshNavMeshBuilder] {message}", this);
        }
    }
}
