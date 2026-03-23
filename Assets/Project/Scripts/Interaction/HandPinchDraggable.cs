using System;
using UnityEngine;

public sealed class HandPinchDraggable : MonoBehaviour
{
    private Outline _outline;
    
    private void Awake()
    {
        _outline = GetComponent<Outline>();
    }

    public void ShowGrabEffect()
    {
        if (_outline != null)
        {
            _outline.enabled = true;
        }
    }

    public void HideGrabEffect()
    {
        if (_outline != null)
        {
            _outline.enabled = false;
        }
    }
}
