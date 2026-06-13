using UnityEngine;

public interface IRoutePointProjector
{
    bool TryProjectPoint(Vector3 sourcePosition, out Vector3 projectedPosition);
}
