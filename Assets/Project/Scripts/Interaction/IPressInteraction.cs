using UnityEngine;

namespace Project.Scripts.Interaction
{
    public interface IPressInteraction
    {
        void BeginPress(GameObject interactor);
        void UpdatePress(GameObject interactor);
        void EndPress(GameObject interactor);
    }
}
