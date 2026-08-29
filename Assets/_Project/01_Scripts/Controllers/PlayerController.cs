using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.Controllers
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(PlayerAnimator))]
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerController : MonoBehaviour
    {
        private const int MAX_WEAPON_SLOTS = 6;
        public const int MAX_WEAPON_LEVEL = 3;

        [Header("Data Asset")]
        [SerializeField] private PlayerData _playerData;

        [Header("Weapon Setup")]
        [Tooltip("WeaponData에 프리팹이 없을 때 사용할 기본 무기 프리팹")]
        [SerializeField] private GameObject _defaultWeaponPrefab;
        [Tooltip("적 감지용 레이어 (반드시 Enemy 레이어 지정)")]
        [SerializeField] private LayerMask _enemyLayer;
        [Tooltip("무기 공전 반경")]
        [SerializeField] private float _weaponOrbitRadius = 1.3f;

        [Header("Components")]
        [SerializeField] private PlayerAnimator _playerAnimator;
        [SerializeField] private PlayerHealth _playerHealth;

        [Header("Runtime Status")]
        [SerializeField] private int _lv = 1;
        [SerializeField] private float _exp = 0.0f;
        [SerializeField] private float _maxExp = 10.0f;
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
        private bool _isMovementEnabled = true;

        public int Lv => _lv;
        public float Exp => _exp;
        public float MaxExp => _maxExp;
        public float MoveSpeed => _moveSpeed;
        public float AttackSpeed => _attackSpeed;
        public float DmgMulti => _dmgMulti;
        public float AdditionalRange => _additionalRange;
        public float MagnetRange => _magnetRange;
        public int Currency => _currency;
        public bool IsMovementEnabled => _isMovementEnabled;

        public WeaponData[] WeaponInfo => _weaponInfo;
        public int[] WeaponLevels => _weaponLevels;

        public event Action<float, float> OnExpChanged;
        public event Action<int> OnLevelUp;
        public event Action<int> OnCurrencyChanged;

                private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            CurrencyPickup.SharedCollector = this;
            if (_playerAnimator == null) _playerAnimator = GetComponent<PlayerAnimator>();
            if (_playerAnimator == null) _playerAnimator = gameObject.AddComponent<PlayerAnimator>();
            if (_playerHealth == null) _playerHealth = GetComponent<PlayerHealth>();
            if (_playerHealth == null) _playerHealth = gameObject.AddComponent<PlayerHealth>();

            _playerHealth.OnDied += HandlePlayerDied;

            // 1. 캐릭터 데이터를 먼저 덮어씌웁니다.
            if (ProjectOverdrive.Managers.SessionManager.Instance != null)
            {
                if (ProjectOverdrive.Managers.SessionManager.Instance.SelectedPlayer != null)
                {
                    _playerData = ProjectOverdrive.Managers.SessionManager.Instance.SelectedPlayer;
                }
            }

            // 2. 캐릭터 데이터를 기반으로 스탯과 기본 무기를 세팅합니다.
            InitializeStats();

            // 3. 세팅이 끝난 후, 내가 선택한 무기를 0번 슬롯(메인 무기)에 강제로 덮어씌웁니다!
            if (ProjectOverdrive.Managers.SessionManager.Instance != null)
            {
                if (ProjectOverdrive.Managers.SessionManager.Instance.SelectedWeapon != null)
                {
                    _weaponInfo[0] = ProjectOverdrive.Managers.SessionManager.Instance.SelectedWeapon;
                    _weaponLevels[0] = 1;
                }
            }
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
            int maxHp;
            if (_playerData == null)
            {
                Debug.LogWarning("[PlayerController] PlayerData가 할당되지 않았습니다. 기본값을 사용합니다.");
                maxHp = 100; _moveSpeed = 6.0f; _attackSpeed = 1.0f;
                _dmgMulti = 1.0f; _additionalRange = 0.0f; _magnetRange = 3.0f;
            }
            else
            {
                maxHp = _playerData.BaseMaxHp;
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

            _playerHealth.Initialize(maxHp);
            _playerAnimator.ResetDeath();
            _currency = 0; _lv = 1; _exp = 0.0f;
            _maxExp = CalculateMaxExp(_lv);
            SetMovementEnabled(true);
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
            UpdateMoveDirection();
        }

                private void Move()
        {
            if (!_isMovementEnabled)
            {
                StopHorizontalMovement();
                return;
            }

            Vector3 targetVelocity = _moveDirection * _moveSpeed;
            
            // 맵 경계(절벽) 제한: 맵 크기 30x30 기준 (-15 ~ 15)
            // 플레이어 모델 크기를 고려해 -14.5 ~ 14.5로 제한
            float mapLimit = 14.5f; 
            Vector3 pos = _rb.position;
            
            if (pos.x <= -mapLimit && targetVelocity.x < 0) targetVelocity.x = 0;
            if (pos.x >= mapLimit && targetVelocity.x > 0) targetVelocity.x = 0;
            if (pos.z <= -mapLimit && targetVelocity.z < 0) targetVelocity.z = 0;
            if (pos.z >= mapLimit && targetVelocity.z > 0) targetVelocity.z = 0;

            targetVelocity.y = _rb.linearVelocity.y;
            _rb.linearVelocity = targetVelocity;

            float clampedX = Mathf.Clamp(pos.x, -mapLimit, mapLimit);
            float clampedZ = Mathf.Clamp(pos.z, -mapLimit, mapLimit);
            
            if (Mathf.Abs(pos.x - clampedX) > 0.01f || Mathf.Abs(pos.z - clampedZ) > 0.01f)
            {
                pos.x = clampedX;
                pos.z = clampedZ;
                _rb.MovePosition(pos);
            }
        }

        public void SetMovementEnabled(bool isEnabled)
        {
            if (_isMovementEnabled == isEnabled) return;

            _isMovementEnabled = isEnabled;
            UpdateMoveDirection();

            if (!isEnabled) StopHorizontalMovement();
        }

        private void UpdateMoveDirection()
        {
            Vector2 appliedInput = _isMovementEnabled ? _moveInput : Vector2.zero;
            _moveDirection = new Vector3(appliedInput.x, 0f, appliedInput.y).normalized;
            if (_playerAnimator != null) _playerAnimator.UpdateMovement(appliedInput);
        }

        private void StopHorizontalMovement()
        {
            if (_rb == null) return;

            Vector3 velocity = _rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            _rb.linearVelocity = velocity;
        }


        #region Progression & Weapons

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
            if (_weaponInfo[slotIndex] != null && _weaponLevels[slotIndex] < MAX_WEAPON_LEVEL)
            {
                _weaponLevels[slotIndex]++;
                SpawnEquippedWeapons();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 상점 무기를 구매합니다. 빈 슬롯을 우선 사용하고, 슬롯이 모두 찬 경우에만
        /// 같은 종류의 1레벨 무기를 구매 재료로 삼아 한 단계 강화합니다.
        /// </summary>
        public bool TryPurchaseWeapon(WeaponData weapon, int price)
        {
            if (weapon == null || price < 0 || _currency < price) return false;

            int emptySlotIndex = FindEmptyWeaponSlot();
            int upgradeSlotIndex = emptySlotIndex < 0
                ? FindMatchingWeaponSlot(weapon, 1)
                : -1;

            if (emptySlotIndex < 0 && upgradeSlotIndex < 0) return false;
            if (price > 0 && !SpendCurrency(price)) return false;

            if (emptySlotIndex >= 0)
            {
                _weaponInfo[emptySlotIndex] = weapon;
                _weaponLevels[emptySlotIndex] = 1;
            }
            else
            {
                _weaponLevels[upgradeSlotIndex]++;
            }

            SpawnEquippedWeapons();
            return true;
        }

        public bool CanSynthesizeWeapon(int slotIndex)
        {
            if (!IsValidEquippedSlot(slotIndex)) return false;

            int level = _weaponLevels[slotIndex];
            if (level >= MAX_WEAPON_LEVEL) return false;

            return FindMatchingWeaponSlot(_weaponInfo[slotIndex], level, slotIndex) >= 0;
        }

        /// <summary>
        /// 선택 슬롯과 같은 종류/같은 레벨의 다른 무기를 재료로 소비하고
        /// 선택 슬롯의 무기를 한 단계 강화합니다.
        /// </summary>
        public bool TrySynthesizeWeapon(int slotIndex)
        {
            if (!CanSynthesizeWeapon(slotIndex)) return false;

            int materialSlotIndex = FindMatchingWeaponSlot(
                _weaponInfo[slotIndex], _weaponLevels[slotIndex], slotIndex);

            if (materialSlotIndex < 0) return false;

            _weaponInfo[materialSlotIndex] = null;
            _weaponLevels[materialSlotIndex] = 0;
            _weaponLevels[slotIndex]++;
            SpawnEquippedWeapons();
            return true;
        }

        private int FindEmptyWeaponSlot()
        {
            for (int i = 0; i < _weaponInfo.Length; i++)
            {
                if (_weaponInfo[i] == null) return i;
            }

            return -1;
        }

        private int FindMatchingWeaponSlot(WeaponData weapon, int level, int excludedSlotIndex = -1)
        {
            for (int i = 0; i < _weaponInfo.Length; i++)
            {
                if (i == excludedSlotIndex || _weaponLevels[i] != level) continue;
                if (AreSameWeaponType(_weaponInfo[i], weapon)) return i;
            }

            return -1;
        }

        private bool IsValidEquippedSlot(int slotIndex)
        {
            return slotIndex >= 0 &&
                   slotIndex < _weaponInfo.Length &&
                   slotIndex < _weaponLevels.Length &&
                   _weaponInfo[slotIndex] != null;
        }

        private static bool AreSameWeaponType(WeaponData first, WeaponData second)
        {
            if (first == null || second == null) return false;
            return first == second || string.Equals(first.WeaponName, second.WeaponName, StringComparison.Ordinal);
        }

        // 특정 슬롯의 무기를 제거 (상점 판매용)
        public void RemoveWeapon(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= MAX_WEAPON_SLOTS) return;

            _weaponInfo[slotIndex] = null;
            _weaponLevels[slotIndex] = 0;
            SpawnEquippedWeapons(); // 무기 재배치
        }

        private void HandlePlayerDied()
        {
            SetMovementEnabled(false);
            _playerAnimator.PlayDeath();
        }

        private void OnDestroy()
        {
            if (_playerHealth != null) _playerHealth.OnDied -= HandlePlayerDied;
            if (CurrencyPickup.SharedCollector == this) CurrencyPickup.SharedCollector = null;
        }

        #endregion
    }
}
