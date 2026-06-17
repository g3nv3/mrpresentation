using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatefulButtonBinder : MonoBehaviour
{
    [SerializeField] private StatefulButton button;
    [SerializeField] private MonoBehaviour stateSource;

    private IUiControlStateSource _source;

    private void Reset()
    {
        button = GetComponent<StatefulButton>();
    }

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<StatefulButton>();
        }
    }

    private void OnEnable()
    {
        Bind();
    }

    private void OnDisable()
    {
        Unbind();
    }

    public void SetSource(MonoBehaviour source)
    {
        Unbind();
        stateSource = source;
        Bind();
    }

    private void Bind()
    {
        if (button == null || stateSource == null)
        {
            return;
        }

        _source = stateSource as IUiControlStateSource;
        if (_source == null)
        {
            Debug.LogWarning(stateSource.GetType().Name + " does not implement IUiControlStateSource.", this);
            return;
        }

        _source.StateChanged += OnStateChanged;
        Apply(_source.CurrentState, _source.CurrentLabel);
    }

    private void Unbind()
    {
        if (_source != null)
        {
            _source.StateChanged -= OnStateChanged;
            _source = null;
        }
    }

    private void OnStateChanged(UiControlState state, string label)
    {
        Apply(state, label);
    }

    private void Apply(UiControlState state, string label)
    {
        if (string.IsNullOrEmpty(label))
        {
            button.SetState(state);
        }
        else
        {
            button.SetState(state, label);
        }
    }
}
