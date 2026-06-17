using UnityEngine;

[DisallowMultipleComponent]
public sealed class UiStateBinder : MonoBehaviour
{
    [SerializeField] private MonoBehaviour stateSource;
    [SerializeField] private UiControlStateTheme theme;
    [SerializeField] private UiStateRenderer[] renderers;
    [SerializeField] private bool autoCollectRenderers = true;
    [SerializeField] private bool includeInactiveRenderers = true;

    private IUiControlStateSource _source;

    private void Awake()
    {
        CollectRenderersIfNeeded();
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

    public void SetTheme(UiControlStateTheme value)
    {
        theme = value;

        if (_source != null)
        {
            Apply(_source.CurrentState, _source.CurrentLabel);
        }
    }

    public void Refresh()
    {
        if (_source != null)
        {
            Apply(_source.CurrentState, _source.CurrentLabel);
        }
    }

    private void Bind()
    {
        CollectRenderersIfNeeded();

        if (stateSource == null)
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

    private void OnStateChanged(UiControlState state, string message)
    {
        Apply(state, message);
    }

    private void Apply(UiControlState state, string message)
    {
        CollectRenderersIfNeeded();

        var style = theme != null ? theme.GetStyle(state) : null;
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].Render(state, message, style);
            }
        }
    }

    private void CollectRenderersIfNeeded()
    {
        if (!autoCollectRenderers || renderers != null && renderers.Length > 0)
        {
            return;
        }

        renderers = GetComponentsInChildren<UiStateRenderer>(includeInactiveRenderers);
    }
}
