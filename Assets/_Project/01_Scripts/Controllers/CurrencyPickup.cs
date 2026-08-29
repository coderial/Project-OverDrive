using UnityEngine;

namespace ProjectOverdrive.Controllers
{
    [DisallowMultipleComponent]
    public sealed class CurrencyPickup : MonoBehaviour, IPoolable
    {
        public const int CurrencyAmount = 1;

        public static PlayerController SharedCollector { get; set; }

        [SerializeField, Min(0.1f)] private float attractionSpeed = 10f;
        [SerializeField, Min(0.01f)] private float collectionDistance = 0.25f;
        [SerializeField, Min(0f)] private float attractionDelay = 0.1f;

        private Transform _cachedTransform;
        private PooledObject _pooledObject;
        private bool _isAttracted;
        private bool _isCollected;
        private float _collectionDistanceSquared;
        private float _attractionStartTime;
        private bool _isWaveEndCollection;
        private float _waveEndCollectionStartTime;
        private float _waveEndCollectionDuration;
        private Vector3 _waveEndStartPosition;
        private PlayerController _waveEndCollector;

        private void Awake()
        {
            _cachedTransform = transform;
            _collectionDistanceSquared = collectionDistance * collectionDistance;
            TryGetComponent(out _pooledObject);
        }

        private void Update()
        {
            PlayerController collector = SharedCollector;
            if (_isCollected || collector == null || Time.time < _attractionStartTime)
            {
                return;
            }

            if (_isWaveEndCollection)
            {
                UpdateWaveEndCollection();
                return;
            }

            Vector3 currentPosition = _cachedTransform.position;
            Vector3 collectorPosition = collector.transform.position;
            Vector3 difference = collectorPosition - currentPosition;
            difference.y = 0f;
            float squaredDistance = difference.sqrMagnitude;

            if (!_isAttracted)
            {
                float magnetRange = collector.MagnetRange;
                if (squaredDistance > magnetRange * magnetRange)
                {
                    return;
                }

                _isAttracted = true;
            }

            if (squaredDistance <= _collectionDistanceSquared)
            {
                Collect(collector);
                return;
            }

            collectorPosition.y = currentPosition.y;
            _cachedTransform.position = Vector3.MoveTowards(
                currentPosition,
                collectorPosition,
                attractionSpeed * Time.deltaTime);
        }

        public void OnSpawned()
        {
            _isAttracted = false;
            _isCollected = false;
            _attractionStartTime = Time.time + attractionDelay;
            _isWaveEndCollection = false;
            _waveEndCollector = null;
        }

        public void OnDespawned()
        {
            _isAttracted = false;
            _isCollected = false;
            _attractionStartTime = 0f;
            _isWaveEndCollection = false;
            _waveEndCollector = null;
        }

        public void BeginWaveEndCollection(PlayerController collector, float duration)
        {
            if (_isCollected || collector == null) return;

            _waveEndCollector = collector;
            _waveEndStartPosition = _cachedTransform.position;
            _waveEndCollectionStartTime = Time.unscaledTime;
            _waveEndCollectionDuration = Mathf.Max(0.01f, duration);
            _isWaveEndCollection = true;
            _isAttracted = true;
            _attractionStartTime = 0f;
        }

        public void CompleteWaveEndCollection()
        {
            if (!_isCollected && _waveEndCollector != null) Collect(_waveEndCollector);
        }

        private void UpdateWaveEndCollection()
        {
            if (_waveEndCollector == null)
            {
                _isWaveEndCollection = false;
                return;
            }

            float t = Mathf.Clamp01(
                (Time.unscaledTime - _waveEndCollectionStartTime) / _waveEndCollectionDuration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 targetPosition = _waveEndCollector.transform.position;
            targetPosition.y = _waveEndStartPosition.y;
            _cachedTransform.position = Vector3.LerpUnclamped(_waveEndStartPosition, targetPosition, easedT);

            if (t >= 1f) Collect(_waveEndCollector);
        }

        private void Collect(PlayerController collector)
        {
            if (_isCollected)
            {
                return;
            }

            _isCollected = true;
            collector.AddCurrency(CurrencyAmount);

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

        private void OnValidate()
        {
            _collectionDistanceSquared = collectionDistance * collectionDistance;
        }
    }
}
