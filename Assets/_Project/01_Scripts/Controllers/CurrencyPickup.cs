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
        }

        public void OnDespawned()
        {
            _isAttracted = false;
            _isCollected = false;
            _attractionStartTime = 0f;
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
