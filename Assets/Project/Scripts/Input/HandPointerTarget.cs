using UnityEngine;

public readonly struct HandPointerTarget
{
    public Ray AimRay { get; }
    public bool HasHit { get; }
    public RaycastHit Hit { get; }

    public HandPointerTarget(Ray aimRay, bool hasHit, in RaycastHit hit)
    {
        AimRay = aimRay;
        HasHit = hasHit;
        Hit = hit;
    }

    public bool HasTag(string tag)
    {
        return HasHit && Hit.collider != null && Hit.collider.CompareTag(tag);
    }

    public bool TryGetSurfacePose(float surfaceOffset, out Pose pose)
    {
        pose = default;
        if (!HasHit)
        {
            return false;
        }

        var normal = Hit.normal.sqrMagnitude > 0f ? Hit.normal.normalized : Vector3.up;
        pose = new Pose(
            Hit.point + normal * surfaceOffset,
            Quaternion.LookRotation(-normal));
        return true;
    }

    public bool TryGetSurfacePose(string requiredTag, float surfaceOffset, out Pose pose)
    {
        pose = default;
        if (!HasTag(requiredTag))
        {
            return false;
        }

        return TryGetSurfacePose(surfaceOffset, out pose);
    }

    public bool TryGetComponentInParent<T>(out T component)
        where T : Component
    {
        component = null;
        if (!HasHit || Hit.collider == null)
        {
            return false;
        }

        component = Hit.collider.GetComponentInParent<T>();
        return component != null;
    }
}

public readonly struct HandContactTarget
{
    public bool HasHit { get; }
    public Collider Collider { get; }
    public Vector3 Point { get; }

    public HandContactTarget(bool hasHit, Collider collider, Vector3 point)
    {
        HasHit = hasHit;
        Collider = collider;
        Point = point;
    }

    public bool HasTag(string tag)
    {
        return HasHit && Collider != null && Collider.CompareTag(tag);
    }

    public bool TryGetComponentInParent<T>(out T component)
        where T : Component
    {
        component = null;
        if (!HasHit || Collider == null)
        {
            return false;
        }

        component = Collider.GetComponentInParent<T>();
        return component != null;
    }
}
