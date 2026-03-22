
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.Android;

public class EnablePicoPassthrough : MonoBehaviour
{
    private const string SpatialDataPermission = "com.picovr.permission.SPATIAL_DATA";
    void Start()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);

        if (!Permission.HasUserAuthorizedPermission(SpatialDataPermission))
            Permission.RequestUserPermission(SpatialDataPermission);
    }
}