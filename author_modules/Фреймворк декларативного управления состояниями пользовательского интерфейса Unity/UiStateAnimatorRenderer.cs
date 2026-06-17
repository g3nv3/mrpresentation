using System;
using UnityEngine;

public sealed class UiStateAnimatorRenderer : UiStateRenderer
{
    [SerializeField] private Animator animator;
    [SerializeField] private string stateIntegerParameter = "State";
    [SerializeField] private UiStateAnimatorBoolEntry[] bools;
    [SerializeField] private UiStateAnimatorTriggerEntry[] triggers;

    private UiControlState _previousState;
    private bool _hasPreviousState;

    private void Reset()
    {
        animator = GetComponent<Animator>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    public override void Render(UiControlState state, string message, UiControlStateStyle style)
    {
        if (animator == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(stateIntegerParameter))
        {
            animator.SetInteger(stateIntegerParameter, (int)state);
        }

        for (int i = 0; bools != null && i < bools.Length; i++)
        {
            if (bools[i] != null && !string.IsNullOrEmpty(bools[i].Parameter))
            {
                animator.SetBool(bools[i].Parameter, bools[i].State == state);
            }
        }

        if (!_hasPreviousState || _previousState != state)
        {
            for (int i = 0; triggers != null && i < triggers.Length; i++)
            {
                if (triggers[i] != null && triggers[i].State == state && !string.IsNullOrEmpty(triggers[i].Parameter))
                {
                    animator.SetTrigger(triggers[i].Parameter);
                }
            }
        }

        _previousState = state;
        _hasPreviousState = true;
    }
}

[Serializable]
public sealed class UiStateAnimatorBoolEntry
{
    [SerializeField] private UiControlState state;
    [SerializeField] private string parameter;

    public UiControlState State => state;
    public string Parameter => parameter;
}

[Serializable]
public sealed class UiStateAnimatorTriggerEntry
{
    [SerializeField] private UiControlState state;
    [SerializeField] private string parameter;

    public UiControlState State => state;
    public string Parameter => parameter;
}
