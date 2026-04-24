using UnityEngine;

[DisallowMultipleComponent]
public sealed class SimpleBouncer : MonoBehaviour
{
    [SerializeField] private Transform targetTransform;
    [SerializeField] private Vector3 bounceAxis = Vector3.up;
    [SerializeField, Min(0f)] private float amplitude = 0.05f;
    [SerializeField, Min(0f)] private float frequency = 1f;
    [SerializeField] private bool useLocalSpace = true;

    private Vector3 _basePosition;
    private float _time;

    public Transform TargetTransform => targetTransform;

    public void Initialize(Transform target, float bounceAmplitude, float bounceFrequency)
    {
        targetTransform = target;
        amplitude = Mathf.Max(0f, bounceAmplitude);
        frequency = Mathf.Max(0f, bounceFrequency);
        CaptureBasePosition();
    }

    public void SetAmplitude(float bounceAmplitude)
    {
        amplitude = Mathf.Max(0f, bounceAmplitude);
    }

    public void SetFrequency(float bounceFrequency)
    {
        frequency = Mathf.Max(0f, bounceFrequency);
    }

    public void ResetBasePosition()
    {
        CaptureBasePosition();
    }

    private void Awake()
    {
        SetupTargetIfNeeded();
        CaptureBasePosition();
    }

    private void OnEnable()
    {
        SetupTargetIfNeeded();
        CaptureBasePosition();
        _time = 0f;
    }

    private void Update()
    {
        if (targetTransform == null || amplitude <= 0f || frequency <= 0f)
        {
            return;
        }

        var axis = bounceAxis.sqrMagnitude > 0f ? bounceAxis.normalized : Vector3.up;
        _time += Time.deltaTime;
        var phase = _time * frequency * Mathf.PI * 2f;
        var offset = axis * (Mathf.Sin(phase) * amplitude);

        if (useLocalSpace)
        {
            targetTransform.localPosition = _basePosition + offset;
            return;
        }

        targetTransform.position = _basePosition + offset;
    }

    private void OnValidate()
    {
        amplitude = Mathf.Max(0f, amplitude);
        frequency = Mathf.Max(0f, frequency);
    }

    private void SetupTargetIfNeeded()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }
    }

    private void CaptureBasePosition()
    {
        if (targetTransform == null)
        {
            return;
        }

        _basePosition = useLocalSpace ? targetTransform.localPosition : targetTransform.position;
    }
}
