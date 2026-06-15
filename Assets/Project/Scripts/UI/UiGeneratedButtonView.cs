using TMPro;
using UnityEngine;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class UiGeneratedButtonView : MonoBehaviour
    {
        [SerializeField] private UiPressButton pressButton;
        [SerializeField] private TMP_Text label;
        [SerializeField] private UiToggleStateIcon stateIcon;

        public UiPressButton PressButton
        {
            get
            {
                if (pressButton == null)
                {
                    pressButton = GetComponentInChildren<UiPressButton>(true);
                }

                return pressButton;
            }
        }

        public UiToggleStateIcon StateIcon
        {
            get
            {
                if (stateIcon == null)
                {
                    stateIcon = GetComponentInChildren<UiToggleStateIcon>(true);
                }

                return stateIcon;
            }
        }

        public void SetLabel(string text)
        {
            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }

            if (label != null)
            {
                label.text = text;
            }
        }

        private void OnValidate()
        {
            if (pressButton == null)
            {
                pressButton = GetComponentInChildren<UiPressButton>(true);
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>(true);
            }

            if (stateIcon == null)
            {
                stateIcon = GetComponentInChildren<UiToggleStateIcon>(true);
            }
        }
    }
}
