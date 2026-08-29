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
        private const float FRONT_IDLE_BLEND = 0.0f;
        private const float BACK_IDLE_BLEND = 0.2f;
        private const float SIDE_IDLE_BLEND = 0.4f;
        private const float FRONT_WALK_BLEND = 0.6f;
        private const float BACK_WALK_BLEND = 0.8f;
        private const float SIDE_WALK_BLEND = 1.0f;

        private static readonly int BlendParameterHash = Animator.StringToHash("Blend");

        private enum FacingDirection
        {
            Front,
            Side,
            Back
        }

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
        [SerializeField] private Animator _animator;
        //[SerializeField] private float _rotationSpeed = 15.0f;

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
        private int[] _weaponLevels = new int[MAX_WEAPON_SLOTS]; // 무기 레벨 추적용 배열

        private readonly List<MeleeWeapon> _spawnedWeapons = new List<MeleeWeapon>();

        private Rigidbody _rb;
        private Vector2 _moveInput;
        private Vector3 _moveDirection;
        private FacingDirection _facingDirection = FacingDirection.Front;
        private bool _isDead;

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
        public int[] WeaponLevels => _weaponLevels;

        public event Action<int, int> OnHpChanged;
        public event Action<float, float> OnExpChanged;
        public event Action<int> OnLevelUp;
        public event Action<int> OnCurrencyChanged;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            CurrencyPickup.SharedCollector = this;
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            if (_modelTransform == null) _modelTransform = transform;

            InitializeStats();
            UpdateMovementAnimation(Vector2.zero);
        }

        private void Start()
        {
            SpawnEquippedWeapons();
        }

        private void FixedUpdate()
        {
            Move();
        }

        public void InitializeStats()
        {
            if (_playerData == null)
            {
                Debug.LogWarning("[PlayerController] PlayerData가 할당되지 않았습니다. 기본값을 사용합니다.");
                _maxHp = 100; _moveSpeed = 6.0f; _attackSpeed = 1.0f;
                _dmgMulti = 1.0f; _additionalRange = 0.0f; _magnetRange = 3.0f;
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
                        if (_weaponInfo[i] != null) _weaponLevels[i] = 1; // 초기 무기 1레벨 셋팅
                    }
                }
            }

            _currentHp = _maxHp; _isDead = false; _currency = 0; _lv = 1; _exp = 0.0f;
            _maxExp = CalculateMaxExp(_lv);
            OnHpChanged?.Invoke(_currentHp, _maxHp);
        }

        public void SpawnEquippedWeapons()
        {
            foreach (var w in _spawnedWeapons) if (w != null) Destroy(w.gameObject);
            _spawnedWeapons.Clear();

            List<WeaponData> validWeapons = new List<WeaponData>();
            List<int> validLevels = new List<int>();

            for (int i = 0; i < MAX_WEAPON_SLOTS; i++)
            {
                if (_weaponInfo[i] != null)
                {
                    validWeapons.Add(_weaponInfo[i]);
                    validLevels.Add(_weaponLevels[i]);
                }
            }

            int count = validWeapons.Count;
            if (count == 0) return;

            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                WeaponData data = validWeapons[i];
                int level = validLevels[i];
                GameObject prefab = data.WeaponPrefab != null ? data.WeaponPrefab : _defaultWeaponPrefab;

                if (prefab == null) continue;

                GameObject weaponObj = Instantiate(prefab, transform.position, prefab.transform.rotation);
                if (!weaponObj.TryGetComponent<MeleeWeapon>(out var meleeComp))
                    meleeComp = weaponObj.AddComponent<MeleeWeapon>();

                float angle = i * angleStep;

                // 레벨 파라미터 전달 추가
                meleeComp.Initialize(data, this, level, angle, _enemyLayer, _weaponOrbitRadius);
                _spawnedWeapons.Add(meleeComp);
            }
        }

        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
            _moveDirection = new Vector3(_moveInput.x, 0f, _moveInput.y).normalized;

            if (_spriteRenderer != null && Mathf.Abs(_moveInput.x) > 0.01f)
                _spriteRenderer.flipX = _moveInput.x < 0f;
        }

        private void UpdateMovementAnimation(Vector2 moveInput)
        {
            bool isWalking = moveInput.sqrMagnitude > 0.001f;

            if (isWalking)
            {
                if (Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
                {
                    _facingDirection = FacingDirection.Side;

                    if (_spriteRenderer != null)
                    {
                        _spriteRenderer.flipX = moveInput.x < 0f;
                    }
                }
                else
                {
                    _facingDirection = moveInput.y > 0f
                        ? FacingDirection.Back
                        : FacingDirection.Front;

                    if (_spriteRenderer != null)
                    {
                        _spriteRenderer.flipX = false;
                    }
                }
            }

            if (_animator != null)
            {
                _animator.SetFloat(BlendParameterHash, GetMovementBlend(_facingDirection, isWalking));
            }
        }

        private static float GetMovementBlend(FacingDirection facingDirection, bool isWalking)
        {
            if (isWalking)
            {
                return facingDirection switch
                {
                    FacingDirection.Back => BACK_WALK_BLEND,
                    FacingDirection.Side => SIDE_WALK_BLEND,
                    _ => FRONT_WALK_BLEND
                };
            }

            return facingDirection switch
            {
                FacingDirection.Back => BACK_IDLE_BLEND,
                FacingDirection.Side => SIDE_IDLE_BLEND,
                _ => FRONT_IDLE_BLEND
            };
        }

        private void Move()
        {
            Vector3 targetVelocity = _moveDirection * _moveSpeed;
            targetVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = targetVelocity;
        }
        #endregion

        #region Combat & Progression

        public void TakeDamage(int damage)
        {
            if (_isDead || damage <= 0) return;
            int appliedDamage = Mathf.Min(_currentHp, damage);
            _currentHp = Mathf.Max(0, _currentHp - damage);

            if (DamageTextManager.Instance != null)
                DamageTextManager.Instance.ShowPlayerDamage(appliedDamage, transform.position);

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
            if (amount <= 0) return;
            _currency += amount;
            OnCurrencyChanged?.Invoke(_currency);
        }

        public bool SpendCurrency(int amount)
        {
            if (amount <= 0 || _currency < amount) return false;
            _currency -= amount;
            OnCurrencyChanged?.Invoke(_currency);
            return true;
        }

        private void LevelUp()
        {
            _lv++;
            _maxExp = CalculateMaxExp(_lv);
            OnLevelUp?.Invoke(_lv);
        }

        private float CalculateMaxExp(int level) => 10.0f + (level * 5.0f) * Mathf.Pow(1.1f, level - 1);

        // 신규 무기 장착 (레벨 1로 셋팅)
        public bool EquipWeapon(int slotIndex, WeaponData weapon)
        {
            if (slotIndex < 0 || slotIndex >= MAX_WEAPON_SLOTS) return false;
            _weaponInfo[slotIndex] = weapon;
            _weaponLevels[slotIndex] = 1;
            SpawnEquippedWeapons();
            return true;
        }

        // 기존 무기 레벨업
        public bool UpgradeWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_WEAPON_SLOTS) return false;
            if (_weaponInfo[slotIndex] != null && _weaponLevels[slotIndex] < 3)
            {
                _weaponLevels[slotIndex]++;
                SpawnEquippedWeapons();
                return true;
            }
            return false;
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            _rb.linearVelocity = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (CurrencyPickup.SharedCollector == this) CurrencyPickup.SharedCollector = null;
        }
    }
}