using UnityEngine;

namespace Project.Scripts.UI
{
    public interface IUiWindowManager
    {
        GameObject CurrentWindow { get; }
        string CurrentWindowId { get; }

        void Open(string windowId);
        void Open(GameObject window);
        void CloseCurrent();
        bool IsOpen(string windowId);
        bool TryGetGeneratedButtonParent(UiGeneratedButtonWindow window, out Transform parent);
    }
}
