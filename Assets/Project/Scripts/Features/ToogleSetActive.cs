using UnityEngine;

[DisallowMultipleComponent]
public sealed class ToogleSetActive : MonoBehaviour
{
    [SerializeField] private GameObject targetObject;

    public GameObject TargetObject => targetObject;

    public void SetTarget(GameObject target)
    {
        targetObject = target;
    }

    public void Toggle()
    {
        var target = ResolveTarget();
        target.SetActive(!target.activeSelf);
    }

    public void Toogle()
    {
        Toggle();
    }

    public void SetActive(bool value)
    {
        ResolveTarget().SetActive(value);
    }

    public void Enable()
    {
        SetActive(true);
    }

    public void Disable()
    {
        SetActive(false);
    }

    private GameObject ResolveTarget()
    {
        return targetObject != null ? targetObject : gameObject;
    }
}
