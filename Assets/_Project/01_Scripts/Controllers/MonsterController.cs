using UnityEngine;
using ProjectOverdrive.Controllers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MonsterController : MonoBehaviour, IPoolable, IDamageable
{
    public static Transform SharedTarget { get; set; }

    private Transform _cachedTransform;
    private Transform _target;
    private PooledObject _pooledObject;
    private PlayerController _contactPlayer;
    private MonsterData _data;
    private float _moveSpeed;
    private float _stoppingDistance;
    private float _stoppingDistanceSquared;
    private float _nextContactDamageTime;

    public MonsterData Data => _data;
    public float AttackPower => _data != null ? _data.AttackPower : 0f;
    public float MaxHealth => _data != null ? _data.MaxHealth : 0f;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        _cachedTransform = transform;
        TryGetComponent(out _pooledObject);
    }

    private void OnEnable()
    {
        if (_target == null)
        {
            _target = SharedTarget;
        }
    }

    private void Update()
    {
        if (_target == null)
        {
            return;
        }

        Vector3 currentPosition = _cachedTransform.position;
        Vector3 direction = _target.position - currentPosition;
        direction.y = 0f;

        float squaredDistance = direction.sqrMagnitude;
        if (squaredDistance <= _stoppingDistanceSquared)
        {
            return;
        }

        float distance = Mathf.Sqrt(squaredDistance);
        float moveDistance = Mathf.Min(_moveSpeed * Time.deltaTime, distance - _stoppingDistance);
        _cachedTransform.position = currentPosition + direction * (moveDistance / distance);
    }

    public void Configure(MonsterData data, Transform target)
    {
        _data = data;
        _target = target;
        _moveSpeed = data.MoveSpeed;
        _stoppingDistance = data.StoppingDistance;
        _stoppingDistanceSquared = _stoppingDistance * _stoppingDistance;
        CurrentHealth = data.MaxHealth;
        _nextContactDamageTime = 0f;
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float knockback)
    {
        if (_data == null || damage <= 0f || CurrentHealth <= 0f)
        {
            return;
        }

        Vector3 hitPosition = _cachedTransform.position;
        float appliedDamage = Mathf.Min(CurrentHealth, damage);
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowMonsterDamage(appliedDamage, hitPosition);
        }

        // A lethal hit must drop currency at the visible hit position, before knockback.
        if (CurrentHealth <= 0f)
        {
            Die(hitPosition);
            return;
        }

        if (knockback > 0f && hitDirection.sqrMagnitude > 0.0001f)
        {
            hitDirection.y = 0f;
            _cachedTransform.position += hitDirection.normalized * knockback;
        }
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void OnSpawned()
    {
        _target = SharedTarget;
        _contactPlayer = null;
        _nextContactDamageTime = 0f;
    }

    public void OnDespawned()
    {
        _target = null;
        _contactPlayer = null;
        _data = null;
        CurrentHealth = 0f;
        _nextContactDamageTime = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController player))
        {
            _contactPlayer = player;
            ApplyContactDamage();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (Time.time < _nextContactDamageTime)
        {
            return;
        }

        if (_contactPlayer != null)
        {
            if (other.gameObject != _contactPlayer.gameObject)
            {
                return;
            }
        }
        else if (!other.TryGetComponent(out _contactPlayer))
        {
            return;
        }

        ApplyContactDamage();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_contactPlayer != null && other.gameObject == _contactPlayer.gameObject)
        {
            _contactPlayer = null;
        }
    }

    private void ApplyContactDamage()
    {
        if (_data == null || _contactPlayer == null || _data.AttackPower <= 0f)
        {
            return;
        }

        Vector3 hitDirection = _contactPlayer.transform.position - _cachedTransform.position;
        _contactPlayer.TakeDamage(_data.AttackPower, hitDirection, 0f);
        _nextContactDamageTime = Time.time + _data.ContactDamageInterval;
    }

    private void Die(Vector3 deathPosition)
    {
        _contactPlayer = null;
        DropCurrency(deathPosition);

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

    private void DropCurrency(Vector3 deathPosition)
    {
        GameObject currencyPrefab = _data != null ? _data.CurrencyPrefab : null;
        PoolingManager poolingManager = PoolingManager.Instance;

        if (currencyPrefab == null || poolingManager == null)
        {
            return;
        }

        poolingManager.Get(
            currencyPrefab,
            deathPosition,
            currencyPrefab.transform.rotation);
    }
}
