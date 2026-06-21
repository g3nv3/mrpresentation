using UnityEngine;

[DisallowMultipleComponent]
public sealed class RouteEndpointMarkerView : MonoBehaviour
{
    [Tooltip("GameObject маркера конечной точки. Объект будет переноситься в endpoint и включаться/выключаться вместе с маршрутом.")]
    [SerializeField] private GameObject markerObject;

    [Tooltip("Компонент, реализующий IRoutePointProjector. Если не задан, endpoint используется как есть.")]
    [SerializeField] private MonoBehaviour pointProjector;

    [Tooltip("Смещение маркера конечной точки.")]
    [SerializeField] private Vector3 markerOffset = new Vector3(0f, 0.08f, 0f);

    private IRoutePointProjector Projector => pointProjector as IRoutePointProjector;

    private void Awake()
    {
        Hide();
    }

    public void Show(Vector3 endpointPosition)
    {
        Show(endpointPosition, true);
    }

    public void Show(Vector3 endpointPosition, bool usePointProjector)
    {
        Show(endpointPosition, usePointProjector, true);
    }

    public void Show(Vector3 endpointPosition, bool usePointProjector, bool useMarkerOffset)
    {
        if (markerObject == null)
        {
            return;
        }

        var projector = Projector;
        var point = usePointProjector && projector != null && projector.TryProjectPoint(endpointPosition, out var projectedPoint)
            ? projectedPoint
            : endpointPosition;

        markerObject.transform.position = point + (useMarkerOffset ? markerOffset : Vector3.zero);
        markerObject.SetActive(true);
    }

    public void Hide()
    {
        if (markerObject != null)
        {
            markerObject.SetActive(false);
        }
    }

    public void SetPointProjector(MonoBehaviour projector)
    {
        pointProjector = projector;
    }

    private void OnValidate()
    {
        if (pointProjector != null && !(pointProjector is IRoutePointProjector))
        {
            pointProjector = null;
        }
    }
}
