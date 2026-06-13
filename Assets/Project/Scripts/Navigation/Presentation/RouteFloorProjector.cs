using UnityEngine;

[DisallowMultipleComponent]
public sealed class RouteFloorProjector : MonoBehaviour, IRoutePointProjector
{
    [Tooltip("Если выключено, возвращает исходную точку без raycast-проекции.")]
    [SerializeField] private bool useFloorRaycast = true;

    [Tooltip("Маска слоев для raycast-проекции на пол.")]
    [SerializeField] private LayerMask floorRaycastMask = ~0;

    [Tooltip("Высота над исходной точкой, откуда начинается raycast вниз для поиска пола.")]
    [SerializeField] private float floorRaycastStartHeight = 1.5f;

    [Tooltip("Дополнительная дистанция ниже исходной точки, проверяемая raycast-ом пола.")]
    [SerializeField] private float floorRaycastDistance = 3f;

    [Tooltip("Максимальный угол между нормалью поверхности и направлением вверх, при котором поверхность считается полом.")]
    [SerializeField, Range(0f, 90f)] private float maxFloorAngle = 45f;

    public bool TryProjectPoint(Vector3 sourcePosition, out Vector3 projectedPosition)
    {
        if (!useFloorRaycast)
        {
            projectedPosition = sourcePosition;
            return true;
        }

        var rayOrigin = sourcePosition + Vector3.up * floorRaycastStartHeight;
        var rayDistance = floorRaycastStartHeight + floorRaycastDistance;

        if (Physics.Raycast(rayOrigin, Vector3.down, out var hit, rayDistance, floorRaycastMask, QueryTriggerInteraction.Ignore) &&
            IsFloorNormal(hit.normal))
        {
            projectedPosition = hit.point;
            return true;
        }

        projectedPosition = default;
        return false;
    }

    private bool IsFloorNormal(Vector3 normal)
    {
        var minDot = Mathf.Cos(maxFloorAngle * Mathf.Deg2Rad);
        return Vector3.Dot(normal.normalized, Vector3.up) >= minDot;
    }
}
