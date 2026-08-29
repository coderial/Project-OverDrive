using UnityEngine;

namespace ProjectOverdrive.UI
{
    [DisallowMultipleComponent]
    public sealed class DamageText : MonoBehaviour, IPoolable
    {
        [SerializeField, Min(0.1f)] private float lifetime = 0.8f;
        [SerializeField, Min(0f)] private float floatDistance = 0.8f;
        [SerializeField, Min(1)] private int fontSize = 64;
        [SerializeField, Min(0.001f)] private float characterSize = 0.05f;

        private Transform _cachedTransform;
        private TextMesh _textMesh;
        private PooledObject _pooledObject;
        private Camera _camera;
        private Color _baseColor;
        private float _startTime;
        private float _endTime;

        private void Awake()
        {
            _cachedTransform = transform;
            TryGetComponent(out _pooledObject);
            CreateTextMesh();
            _camera = Camera.main;
        }

        private void Update()
        {
            float currentTime = Time.time;
            if (currentTime >= _endTime)
            {
                Release();
                return;
            }

            float duration = Mathf.Max(0.0001f, _endTime - _startTime);
            float normalizedTime = Mathf.Clamp01((currentTime - _startTime) / duration);

            Vector3 floatDirection = _camera != null ? _camera.transform.up : Vector3.forward;
            _cachedTransform.position += floatDirection * (floatDistance / duration * Time.deltaTime);

            if (_camera != null)
            {
                _cachedTransform.rotation = _camera.transform.rotation;
            }

            Color color = _baseColor;
            color.a = 1f - normalizedTime;
            _textMesh.color = color;
        }

        public void Show(float damage, Color color)
        {
            int displayedDamage = Mathf.Max(1, Mathf.CeilToInt(damage));
            _textMesh.text = displayedDamage.ToString();
            _baseColor = color;
            _textMesh.color = color;
            _startTime = Time.time;
            _endTime = _startTime + lifetime;

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                _cachedTransform.rotation = _camera.transform.rotation;
            }
        }

        public void OnSpawned()
        {
            _startTime = Time.time;
            _endTime = _startTime + lifetime;
        }

        public void OnDespawned()
        {
            if (_textMesh != null)
            {
                _textMesh.text = string.Empty;
            }
        }

        private void CreateTextMesh()
        {
            if (!TryGetComponent(out _textMesh))
            {
                _textMesh = gameObject.AddComponent<TextMesh>();
            }

            _textMesh.fontSize = fontSize;
            _textMesh.characterSize = characterSize;
            _textMesh.anchor = TextAnchor.MiddleCenter;
            _textMesh.alignment = TextAlignment.Center;
            _textMesh.richText = false;

            MeshRenderer renderer = _textMesh.GetComponent<MeshRenderer>();
            
            if (renderer != null)
            {
                renderer.sortingOrder = 100;
            }
        }

        private void Release()
        {
            if (_pooledObject == null)
            {
                TryGetComponent(out _pooledObject);
            }

            if (_pooledObject != null)
            {
                _pooledObject.Release();
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
