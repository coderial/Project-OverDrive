using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour, IDamageable
    {
        private const int MAX_WEAPON_SLOTS = 6;

        [Header("Data Asset")]
        [SerializeField] private PlayerData _playerData;

        [Header("Weapon Setup")]
        [Tooltip("WeaponData에 프리팹이 없을 때 사용할 기본 무기 프리팹")]
        [SerializeField] private GameObject _defaultWeaponPrefab;
        [Tooltip("적 감지용 레이어 (반드시 Enemy 레이어 지정)")]
        [SerializeField] private LayerMask _enemyLayer;
        [Tooltip("무기 공전 반경")]
        [SerializeField] private float _weaponOrbitRadius = 1.3f;

        [Header("Visual Components")]
        [SerializeField] private Transform _modelTransform;
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private float _rotationSpeed = 15.0f;

        // 런타임 스탯
        [Header("Runtime Status")]
        [SerializeField] private int _lv = 1;
        [SerializeField] private float _exp = 0.0f;
        [SerializeField] private float _maxExp = 10.0f;
        [SerializeField] private int _currentHp;
        [SerializeField] private int _maxHp;
        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _attackSpeed;
        [SerializeField] private float _dmgMulti;
        [SerializeField] private float _additionalRange;
        [SerializeField] private float _magnetRange;
        [SerializeField, Min(0)] private int _currency;

        [Header("Weapon Slots (Max 6)")]
        [SerializeField] private WeaponData[] _weaponInfo = new WeaponData[MAX_WEAPON_SLOTS];

        private readonly List<MeleeWeapon> _spawnedWeapons = new List<MeleeWeapon>();

        private Rigidbody _rb;
        private Vector2 _moveInput;
        private Vector3 _moveDirection;
        private bool _isDead;

        // 외부 프로퍼티
        public int Lv => _lv;
        public float Exp => _exp;
        public float MaxExp => _maxExp;
        public int CurrentHp => _currentHp;
        public int MaxHp => _maxHp;
        public float MoveSpeed => _moveSpeed;
        public float AttackSpeed => _attackSpeed;
        public float DmgMulti => _dmgMulti;
        public float AdditionalRange => _additionalRange;
        public float MagnetRange => _magnetRange;
        public int Currency => _currency;
        public WeaponData[] WeaponInfo => _weaponInfo;

        public event Action<int, int> OnHpChanged;
        public event Action<float, float> OnExpChanged;
        public event Action<int> OnLevelUp;
        public event Action<int> OnCurrencyChanged;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            CurrencyPickup.SharedCollector = this;
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_modelTransform == null) _modelTransform = transform;

            InitializeStats();
        }

        private void Start()
        {
            SpawnEquippedWeapons();
        }

        private void FixedUpdate()
        {
            Move();
        }

        private void Update()
        {
            RotateVisual();
        }

        public void InitializeStats()
        {
            if (_playerData == null)
            {
                Debug.LogWarning("[PlayerController] PlayerData가 할당되지 않았습니다. 기본값을 사용합니다.");
                _maxHp = 100;
                _moveSpeed = 6.0f;
                _attackSpeed = 1.0f;
                _dmgMulti = 1.0f;
                _additionalRange = 0.0f;
                _magnetRange = 3.0f;
            }
            else
            {
                _maxHp = _playerData.BaseMaxHp;
                _moveSpeed = _playerData.BaseMoveSpeed;
                _attackSpeed = _playerData.BaseAttackSpeed;
                _dmgMulti = _playerData.BaseDmgMulti;
                _additionalRange = _playerData.BaseAdditionalRange;
                _magnetRange = _playerData.BaseMagnetRange;

                for (int i = 0; i < MAX_WEAPON_SLOTS; i++)
                {
                    if (_playerData.InitialWeapons != null && i < _playerData.InitialWeapons.Length)
                    {
                        _weaponInfo[i] = _playerData.InitialWeapons[i];
                    }
                }
            }

            _currentHp = _maxHp;
            _isDead = false;
            _currency = 0;
            _lv = 1;
            _exp = 0.0f;
            _maxExp = CalculateMaxExp(_lv);
        }

        public void SpawnEquippedWeapons()
        {
            foreach (var w in _spawnedWeapons)
            {
                if (w != null) Destroy(w.gameObject);
            }
            _spawnedWeapons.Clear();

            List<WeaponData> validWeapons = new List<WeaponData>();
            for (int i = 0; i < MAX_WEAPON_SLOTS; i++)
            {
                if (_weaponInfo[i] != null) validWeapons.Add(_weaponInfo[i]);
            }

            int count = validWeapons.Count;
            if (count == 0) return;

            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                WeaponData data = validWeapons[i];
                GameObject prefab = data.WeaponPrefab != null ? data.WeaponPrefab : _defaultWeaponPrefab;

                if (prefab == null)
                {
                    Debug.LogWarning($"[PlayerController] '{data.WeaponName}'에 연결된 프리팹이 없습니다.");
                    continue;
                }

                GameObject weaponObj = Instantiate(prefab, transform.position, Quaternion.identity);
                if (!weaponObj.TryGetComponent<MeleeWeapon>(out var meleeComp))
                {
                    meleeComp = weaponObj.AddComponent<MeleeWeapon>();
                }

                float angle = i * angleStep;
                // _weaponOrbitRadius를 정상 전달
                meleeComp.Initialize(data, this, angle, _enemyLayer, _weaponOrbitRadius);
                _spawnedWeapons.Add(meleeComp);
            }
        }

        #region Input & Movement

        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
            _moveDirection = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;

            if (_spriteRenderer != null && Mathf.Abs(_moveInput.x) > 0.01f)
            {
                _spriteRenderer.flipX = _moveInput.x < 0f;
            }
        }

        private void Move()
        {
            Vector3 targetVelocity = _moveDirection * _moveSpeed;
            targetVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = targetVelocity;
        }

        private void RotateVisual()
        {
            if (_moveDirection.sqrMagnitude > 0.01f && _modelTransform != null)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
                _modelTransform.rotation = Quaternion.Slerp(_modelTransform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
        }

        #endregion

        #region Combat & Progression

        public void TakeDamage(int damage)
        {
            if (_isDead || damage <= 0) return;

            _currentHp = Mathf.Max(0, _currentHp - damage);
            OnHpChanged?.Invoke(_currentHp, _maxHp);
            if (_currentHp <= 0) Die();
        }

        public void TakeDamage(float damage, Vector3 hitDirection, float knockback)
        {
            if (_isDead || damage <= 0f) return;

            TakeDamage(Mathf.Max(1, Mathf.CeilToInt(damage)));

            if (!_isDead && knockback > 0f && hitDirection.sqrMagnitude > 0.0001f)
            {
                hitDirection.y = 0f;
                _rb.AddForce(hitDirection.normalized * knockback, ForceMode.Impulse);
            }
        }

        public void Heal(int amount)
        {
            _currentHp = Mathf.Min(_maxHp, _currentHp + amount);
            OnHpChanged?.Invoke(_currentHp, _maxHp);
        }

        public void AddExp(float amount)
        {
            _exp += amount;
            while (_exp >= _maxExp)
            {
                _exp -= _maxExp;
                LevelUp();
            }
            OnExpChanged?.Invoke(_exp, _maxExp);
        }

        public void AddCurrency(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _currency += amount;
            OnCurrencyChanged?.Invoke(_currency);
        }

        private void LevelUp()
        {
            _lv++;
            _maxExp = CalculateMaxExp(_lv);
            OnLevelUp?.Invoke(_lv);
            Debug.Log($"[PlayerController] Level Up! 레벨: {_lv}");
        }

        private float CalculateMaxExp(int level) => 10.0f + (level * 5.0f) * Mathf.Pow(1.1f, level - 1);

        public bool EquipWeapon(int slotIndex, WeaponData weapon)
        {
            if (slotIndex < 0 || slotIndex >= MAX_WEAPON_SLOTS) return false;
            _weaponInfo[slotIndex] = weapon;
            SpawnEquippedWeapons();
            return true;
        }

        private void Die()
        {
            if (_isDead) return;

            _isDead = true;
            Debug.Log("[PlayerController] 사망!");
            _rb.linearVelocity = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (CurrencyPickup.SharedCollector == this)
            {
                CurrencyPickup.SharedCollector = null;
            }
        }

        #endregion
    }
}
