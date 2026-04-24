using UnityEngine;

[DisallowMultipleComponent]
public sealed class OscillatingRotator : MonoBehaviour
{
    [SerializeField] private Transform targetTransform;
    [SerializeField, Min(0f)] private float maxRotationSpeed = 90f;
    [SerializeField] private Vector2 frequencyRange = new Vector2(0.4f, 1.2f);
    [SerializeField] private Vector2 angleAmplitudeRange = new Vector2(8f, 25f);
    [SerializeField] private bool randomizeOnEnable = true;

    private Quaternion _baseLocalRotation;
    private Vector3 _axisFrequencies;
    private Vector3 _axisAmplitudes;
    private Vector3 _axisPhases;
    private float _time;
    private bool _isInitialized;

    public Transform TargetTransform => targetTransform;
    public float MaxRotationSpeed => maxRotationSpeed;

    public void Initialize(Transform rotationTarget, float maxSpeedDegreesPerSecond)
    {
        targetTransform = rotationTarget;
        maxRotationSpeed = Mathf.Max(0f, maxSpeedDegreesPerSecond);

        SetupTargetIfNeeded();
        CaptureBaseRotation();
        RandomizeOscillation();
    }

    public void SetMaxRotationSpeed(float maxSpeedDegreesPerSecond)
    {
        maxRotationSpeed = Mathf.Max(0f, maxSpeedDegreesPerSecond);
    }

    public void RandomizeOscillation()
    {
        _axisFrequencies = new Vector3(
            Random.Range(frequencyRange.x, frequencyRange.y),
            Random.Range(frequencyRange.x, frequencyRange.y),
            Random.Range(frequencyRange.x, frequencyRange.y));

        _axisAmplitudes = new Vector3(
            Random.Range(angleAmplitudeRange.x, angleAmplitudeRange.y),
            Random.Range(angleAmplitudeRange.x, angleAmplitudeRange.y),
            Random.Range(angleAmplitudeRange.x, angleAmplitudeRange.y));

        _axisPhases = new Vector3(
            Random.Range(0f, Mathf.PI * 2f),
            Random.Range(0f, Mathf.PI * 2f),
            Random.Range(0f, Mathf.PI * 2f));

        _time = 0f;
        _isInitialized = true;
    }

    private void Awake()
    {
        SetupTargetIfNeeded();
        CaptureBaseRotation();
    }

    private void OnEnable()
    {
        SetupTargetIfNeeded();
        CaptureBaseRotation();

        if (randomizeOnEnable || !_isInitialized)
        {
            RandomizeOscillation();
        }
    }

    private void Update()
    {
        if (targetTransform == null || !_isInitialized)
        {
            return;
        }

        _time += Time.deltaTime;

        var eulerOffset = new Vector3(
            _axisAmplitudes.x * Mathf.Sin(_time * _axisFrequencies.x + _axisPhases.x),
            _axisAmplitudes.y * Mathf.Sin(_time * _axisFrequencies.y + _axisPhases.y),
            _axisAmplitudes.z * Mathf.Sin(_time * _axisFrequencies.z + _axisPhases.z));

        var desiredRotation = _baseLocalRotation * Quaternion.Euler(eulerOffset);
        var maxStep = maxRotationSpeed * Time.deltaTime;

        if (maxStep <= 0f)
        {
            return;
        }

        targetTransform.localRotation = Quaternion.RotateTowards(
            targetTransform.localRotation,
            desiredRotation,
            maxStep);
    }

    private void OnValidate()
    {
        if (frequencyRange.x > frequencyRange.y)
        {
            frequencyRange = new Vector2(frequencyRange.y, frequencyRange.x);
        }

        if (angleAmplitudeRange.x > angleAmplitudeRange.y)
        {
            angleAmplitudeRange = new Vector2(angleAmplitudeRange.y, angleAmplitudeRange.x);
        }

        frequencyRange.x = Mathf.Max(0f, frequencyRange.x);
        frequencyRange.y = Mathf.Max(0f, frequencyRange.y);
        angleAmplitudeRange.x = Mathf.Max(0f, angleAmplitudeRange.x);
        angleAmplitudeRange.y = Mathf.Max(0f, angleAmplitudeRange.y);
        maxRotationSpeed = Mathf.Max(0f, maxRotationSpeed);
    }

    private void SetupTargetIfNeeded()
    {
        if (targetTransform == null)
        {
            targetTransform = transform;
        }
    }

    private void CaptureBaseRotation()
    {
        if (targetTransform == null)
        {
            return;
        }

        _baseLocalRotation = targetTransform.localRotation;
    }
}
