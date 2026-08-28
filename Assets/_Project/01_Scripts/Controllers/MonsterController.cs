using UnityEngine;

[DisallowMultipleComponent]
public sealed class MonsterController : MonoBehaviour, IPoolable
{
    public static Transform SharedTarget { get; set; }

    private Transform _cachedTransform;
    private Transform _target;
    private MonsterData _data;
    private float _moveSpeed;
    private float _stoppingDistance;
    private float _stoppingDistanceSquared;

    public MonsterData Data => _data;
    public float AttackPower => _data != null ? _data.AttackPower : 0f;
    public float MaxHealth => _data != null ? _data.MaxHealth : 0f;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        _cachedTransform = transform;
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
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void OnSpawned()
    {
        _target = SharedTarget;
    }

    public void OnDespawned()
    {
        _target = null;
        _data = null;
        CurrentHealth = 0f;
    }
}
