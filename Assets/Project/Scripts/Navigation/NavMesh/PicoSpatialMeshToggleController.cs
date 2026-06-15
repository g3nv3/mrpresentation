using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Project.Scripts.UI;
using Unity.AI.Navigation;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.Android;

[DisallowMultipleComponent]
public sealed class PicoSpatialMeshToggleController : MonoBehaviour, IUiToggleState
{
    private const string SpatialDataPermission = "com.picovr.permission.SPATIAL_DATA";
    private const BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    [SerializeField] private PXR_SpatialMeshManager spatialMeshManager;
    [SerializeField] private PicoSpatialMeshNavMeshBuilder navMeshBuilder;
    [SerializeField] private NavMeshSurface navMeshSurface;
    [SerializeField] private Transform spatialMeshRoot;
    [SerializeField] private string spatialMeshTag = "MRMesh";
    [SerializeField] private bool clearSpatialMeshOnDisable = true;
    [SerializeField] private bool clearNavMeshOnDisable = true;
    [SerializeField] private bool enableOnStart = true;
    [SerializeField] private bool requestSpatialDataPermission = true;
    [SerializeField] private bool rebuildNavMeshOnEnable = true;

    public bool IsSpatialMeshEnabled { get; private set; }
    public bool IsOn => IsSpatialMeshEnabled;
    public event Action<bool> Changed;

    private bool waitingForSpatialDataPermission;

    private void Awake()
    {
        if (spatialMeshManager == null)
        {
            spatialMeshManager = GetComponent<PXR_SpatialMeshManager>();
        }

        if (navMeshBuilder == null)
        {
            navMeshBuilder = GetComponent<PicoSpatialMeshNavMeshBuilder>();
        }

        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
        }

        if (spatialMeshRoot == null && spatialMeshManager != null)
        {
            spatialMeshRoot = spatialMeshManager.transform;
        }
    }

    private void Start()
    {
        SetSpatialMeshActive(enableOnStart);
    }

    private void Update()
    {
        if (!waitingForSpatialDataPermission ||
            !Permission.HasUserAuthorizedPermission(SpatialDataPermission))
        {
            return;
        }

        waitingForSpatialDataPermission = false;
        SetSpatialMeshActive(true);
    }

    public void ToggleSpatialMesh()
    {
        SetSpatialMeshActive(!IsSpatialMeshEnabled);
    }

    public void Toggle()
    {
        ToggleSpatialMesh();
    }

    public void SetOn(bool value)
    {
        SetSpatialMeshActive(value);
    }

    public void StartSpatialMesh()
    {
        SetSpatialMeshActive(true);
    }

    public void StopSpatialMesh()
    {
        SetSpatialMeshActive(false);
    }

    public void SetSpatialMeshActive(bool isActive)
    {
        var wasEnabled = IsSpatialMeshEnabled;

        if (!isActive)
        {
            waitingForSpatialDataPermission = false;
        }

        if (isActive && requestSpatialDataPermission && !EnsureSpatialDataPermission())
        {
            waitingForSpatialDataPermission = true;
            return;
        }

        IsSpatialMeshEnabled = isActive;

        if (navMeshBuilder != null)
        {
            navMeshBuilder.enabled = isActive;
        }

        if (spatialMeshManager != null)
        {
            spatialMeshManager.enabled = isActive;
        }

        if (!isActive)
        {
            if (clearSpatialMeshOnDisable)
            {
                ClearSpatialMeshObjects();
            }

            if (clearNavMeshOnDisable)
            {
                navMeshSurface?.RemoveData();
            }
        }
        else if (navMeshBuilder != null && rebuildNavMeshOnEnable)
        {
            navMeshBuilder.RequestRebuild();
        }

        if (wasEnabled != IsSpatialMeshEnabled)
        {
            Changed?.Invoke(IsSpatialMeshEnabled);
        }
    }

    private void ClearSpatialMeshObjects()
    {
        if (TryClearPicoSpatialMeshManagerState())
        {
            return;
        }

        var root = spatialMeshRoot != null ? spatialMeshRoot : transform;
        var meshFilters = root.GetComponentsInChildren<MeshFilter>(true);
        var meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
        var objectsToDestroy = new HashSet<GameObject>();

        AddSpatialMeshObjects(meshFilters, objectsToDestroy, root);
        AddSpatialMeshObjects(meshColliders, objectsToDestroy, root);

        foreach (var meshObject in objectsToDestroy)
        {
            if (Application.isPlaying)
            {
                Destroy(meshObject);
            }
            else
            {
                DestroyImmediate(meshObject);
            }
        }
    }

    private bool TryClearPicoSpatialMeshManagerState()
    {
        if (spatialMeshManager == null)
        {
            return false;
        }

        try
        {
            var managerType = spatialMeshManager.GetType();
            var meshMap = GetPrivateFieldValue(managerType, spatialMeshManager, "meshIDToGameobject");
            var needingDraw = GetPrivateFieldValue(managerType, spatialMeshManager, "spatialMeshNeedingDraw");
            var meshPool = GetPrivateFieldValue(managerType, spatialMeshManager, "meshObjectsPool");
            var meshObjects = ExtractGameObjectsFromDictionaryValues(meshMap);

            for (var i = 0; i < meshObjects.Count; i++)
            {
                var meshObject = meshObjects[i];
                if (meshObject == null)
                {
                    continue;
                }

                meshObject.SetActive(false);
                EnqueueGameObject(meshPool, meshObject);
            }

            ClearCollection(meshMap);
            ClearCollection(needingDraw);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[{nameof(PicoSpatialMeshToggleController)}] Failed to clear {nameof(PXR_SpatialMeshManager)} state. Spatial mesh objects were left intact to avoid breaking the manager pool. {exception.Message}",
                this);
            return true;
        }
    }

    private static object GetPrivateFieldValue(Type type, object target, string fieldName)
    {
        var field = type.GetField(fieldName, PrivateInstanceFlags);
        return field?.GetValue(target);
    }

    private static List<GameObject> ExtractGameObjectsFromDictionaryValues(object dictionary)
    {
        var gameObjects = new List<GameObject>();
        if (dictionary == null)
        {
            return gameObjects;
        }

        var valuesProperty = dictionary.GetType().GetProperty("Values");
        if (valuesProperty?.GetValue(dictionary) is not IEnumerable values)
        {
            return gameObjects;
        }

        foreach (var value in values)
        {
            if (value is GameObject gameObject)
            {
                gameObjects.Add(gameObject);
            }
        }

        return gameObjects;
    }

    private static void EnqueueGameObject(object queue, GameObject gameObject)
    {
        if (queue == null || gameObject == null)
        {
            return;
        }

        if (QueueContains(queue, gameObject))
        {
            return;
        }

        var enqueueMethod = queue.GetType().GetMethod("Enqueue", new[] { typeof(GameObject) });
        enqueueMethod?.Invoke(queue, new object[] { gameObject });
    }

    private static bool QueueContains(object queue, GameObject gameObject)
    {
        if (queue is not IEnumerable values)
        {
            return false;
        }

        foreach (var value in values)
        {
            if (ReferenceEquals(value, gameObject))
            {
                return true;
            }
        }

        return false;
    }

    private static void ClearCollection(object collection)
    {
        var clearMethod = collection?.GetType().GetMethod("Clear", Type.EmptyTypes);
        clearMethod?.Invoke(collection, null);
    }

    private void AddSpatialMeshObjects<T>(T[] components, HashSet<GameObject> objectsToDestroy, Transform root)
        where T : Component
    {
        if (components == null)
        {
            return;
        }

        for (var i = 0; i < components.Length; i++)
        {
            var component = components[i];
            if (component == null ||
                component.transform == root ||
                !IsSpatialMeshObject(component.gameObject))
            {
                continue;
            }

            objectsToDestroy.Add(component.gameObject);
        }
    }

    private bool IsSpatialMeshObject(GameObject candidate)
    {
        if (string.IsNullOrWhiteSpace(spatialMeshTag))
        {
            return true;
        }

        try
        {
            return candidate.CompareTag(spatialMeshTag);
        }
        catch (UnityException)
        {
            return true;
        }
    }

    private static bool EnsureSpatialDataPermission()
    {
        if (Permission.HasUserAuthorizedPermission(SpatialDataPermission))
        {
            return true;
        }

        Permission.RequestUserPermission(SpatialDataPermission);
        return false;
    }
}
