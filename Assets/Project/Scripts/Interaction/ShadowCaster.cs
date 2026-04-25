using System;
using UnityEngine;

namespace Project.Scripts.Interaction
{
    public class ShadowCaster : MonoBehaviour
    {
        public GameObject shadowPrefab;
        public LayerMask groundLayerMask;
        public float shadowScaleAdd;
        public float maxVisibleHeight = 1f;
        public float maxScaleMultiplier = 1.5f;
        
        private Transform _shadow;
        private SpriteRenderer _shadowRenderer;
        private Color _startColor;
        private Vector3 _startScale;
        private Color _endColor = new Color(0f, 0f, 0f, 0f);
        private void Start()
        {
            _shadow = Instantiate(shadowPrefab).transform;
            _shadowRenderer = _shadow.GetComponent<SpriteRenderer>();
            _startColor = _shadowRenderer.color;
            _shadow.localScale = transform.localScale + Vector3.one * shadowScaleAdd;
            _startScale = _shadow.localScale;
        }

        private void Update()
        {
            DropShadow();
        }
        
        private void DropShadow()
        {
            if (Physics.Raycast(transform.position, -Vector3.up, out var hit, Mathf.Infinity, groundLayerMask))
            {
                _shadow.position = hit.point + Vector3.up * 0.01f;
                _shadowRenderer.color = Color.Lerp(_startColor, _endColor, hit.distance / maxVisibleHeight);
                _shadow.localScale = Vector3.Lerp(_startScale, _startScale * maxScaleMultiplier, hit.distance / maxVisibleHeight);
            }
        }
    }
}