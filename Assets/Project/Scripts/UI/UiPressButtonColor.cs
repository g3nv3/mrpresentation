using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UiPressButton))]
    [AddComponentMenu("Project/UI/UI Press Button Color")]
    public sealed class UiPressButtonColor : MonoBehaviour
    {
        private enum ColorChangeMode
        {
            HoldWhilePressed,
            ToggleOnPress,
            SetPressedOnPress
        }

        [SerializeField] private UiPressButton pressButton;
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private ColorChangeMode colorChangeMode = ColorChangeMode.HoldWhilePressed;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color pressedColor = new(0.36f, 0.65f, 1f, 1f);
        [SerializeField, Min(0f)] private float transitionDuration = 0.08f;
        [SerializeField] private bool applyColorOnEnable = true;
        [SerializeField] private bool resetColorOnDisable = true;

        private bool pressed;
        private Tween colorTween;

        public bool Pressed => pressed;

        private void Reset()
        {
            EnsureReferences();

            if (targetGraphic != null)
            {
                normalColor = targetGraphic.color;
            }
        }

        private void OnEnable()
        {
            EnsureReferences();

            if (pressButton != null)
            {
                pressButton.OnPressBegan.AddListener(HandlePressBegan);
                pressButton.OnPressed.AddListener(HandlePressed);
                pressButton.OnPressEnded.AddListener(HandlePressEnded);
            }

            if (applyColorOnEnable)
            {
                ApplyCurrentColor(true);
            }
        }

        private void OnDisable()
        {
            KillColorTween();

            if (pressButton != null)
            {
                pressButton.OnPressBegan.RemoveListener(HandlePressBegan);
                pressButton.OnPressed.RemoveListener(HandlePressed);
                pressButton.OnPressEnded.RemoveListener(HandlePressEnded);
            }

            if (resetColorOnDisable)
            {
                pressed = false;
                ApplyColor(normalColor, true);
            }
        }

        private void OnValidate()
        {
            EnsureReferences();
        }

        public void SetPressed(bool value)
        {
            if (pressed == value)
            {
                return;
            }

            pressed = value;
            ApplyCurrentColor(false);
        }

        public void TogglePressed()
        {
            SetPressed(!pressed);
        }

        public void ApplyNormalColor()
        {
            SetPressed(false);
        }

        public void ApplyPressedColor()
        {
            SetPressed(true);
        }

        private void HandlePressBegan()
        {
            if (colorChangeMode == ColorChangeMode.HoldWhilePressed)
            {
                SetPressed(true);
            }
        }

        private void HandlePressed()
        {
            switch (colorChangeMode)
            {
                case ColorChangeMode.ToggleOnPress:
                    TogglePressed();
                    break;
                case ColorChangeMode.SetPressedOnPress:
                    SetPressed(true);
                    break;
            }
        }

        private void HandlePressEnded()
        {
            if (colorChangeMode == ColorChangeMode.HoldWhilePressed)
            {
                SetPressed(false);
            }
        }

        private void ApplyCurrentColor(bool immediate)
        {
            ApplyColor(pressed ? pressedColor : normalColor, immediate);
        }

        private void ApplyColor(Color color, bool immediate)
        {
            if (targetGraphic == null)
            {
                return;
            }

            if (immediate || transitionDuration <= 0f || !Application.isPlaying)
            {
                KillColorTween();
                targetGraphic.color = color;
                return;
            }

            KillColorTween();
            colorTween = targetGraphic
                .DOColor(color, transitionDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetTarget(this);
        }

        private void KillColorTween()
        {
            if (colorTween == null)
            {
                return;
            }

            if (colorTween.IsActive())
            {
                colorTween.Kill(false);
            }

            colorTween = null;
        }

        private void EnsureReferences()
        {
            if (pressButton == null)
            {
                pressButton = GetComponent<UiPressButton>();
            }

            if (targetGraphic == null)
            {
                targetGraphic = GetComponent<Graphic>();
            }
        }
    }
}
