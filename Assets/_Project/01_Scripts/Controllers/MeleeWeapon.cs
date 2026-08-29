using System.Collections;
using UnityEngine;
using ProjectOverdrive.Data;
using ProjectOverdrive.Managers;

namespace ProjectOverdrive.Controllers
{
    public class MeleeWeapon : MonoBehaviour
    {
        [Header("Attack Mixing (공격 패턴 섞기)")]
        [Tooltip("찌르기(Thrust)와 베기(Swing)를 섞어서 쓸지 여부")]
        [SerializeField] private bool _mixAttackTypes = true;

        [Tooltip("찌르기 발동 확률 (0 = 무조건 베기, 1 = 무조건 찌르기, 0.5 = 반반)")]
        [Range(0f, 1f)]
        [SerializeField] private float _thrustProbability = 0.5f;

        [Header("Settings")]
        [SerializeField] private float _orbitRadius = 1.3f;
        [SerializeField] private float _orbitHeight = 0.5f;
        [SerializeField] private LayerMask _enemyLayer;

        private WeaponData _weaponData;
        private PlayerController _owner;
        private SpriteRenderer _spriteRenderer;
        private int _weaponLevel = 1;
        private float _orbitAngleDeg;
        private float _cooldownTimer = 0f;
        private bool _isAttacking = false;

        private Vector3 _lastAttackDir = Vector3.forward;
        private Quaternion _originalSpriteRot;

        public float EffectiveDistance => (_weaponData != null && _owner != null)
            ? _weaponData.BaseAttackDistance + _owner.AdditionalRange
            : 1.5f;

        public float EffectiveArea => _weaponData != null ? _weaponData.BaseHitArea : 0.8f;

        private float LevelDmgMultiplier => 1f + (_weaponLevel - 1) * 0.5f;

        public void Initialize(WeaponData data, PlayerController owner, int level, float angleDeg, LayerMask enemyLayer, float orbitRadius)
        {
            _weaponData = data;
            _owner = owner;
            _weaponLevel = level;
            _orbitAngleDeg = angleDeg;
            _enemyLayer = enemyLayer;
            _orbitRadius = orbitRadius;

            _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_spriteRenderer != null && _weaponData != null)
            {
                _originalSpriteRot = _spriteRenderer.transform.localRotation;

                Sprite levelSprite = _weaponData.GetSpriteForLevel(_weaponLevel);
                if (levelSprite != null) _spriteRenderer.sprite = levelSprite;
            }
        }

        private void Update()
        {
            if (_owner == null || _weaponData == null) return;
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (!_isAttacking)
            {
                UpdateOrbitPosition();
                CheckAndAttackNearestEnemy();
            }
        }

        private void UpdateOrbitPosition()
        {
            float rad = _orbitAngleDeg * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _orbitRadius;
            offset.y = _orbitHeight + Mathf.Sin(Time.time * 3f + _orbitAngleDeg) * 0.1f;

            transform.position = _owner.transform.position + offset;

            if (_spriteRenderer != null && _lastAttackDir.sqrMagnitude > 0.001f)
            {
                _spriteRenderer.flipX = _lastAttackDir.x < 0f;
            }
        }

        private void CheckAndAttackNearestEnemy()
        {
            if (_cooldownTimer > 0f) return;

            Collider[] hits = Physics.OverlapSphere(_owner.transform.position, EffectiveDistance, _enemyLayer);
            if (hits.Length == 0) return;

            Collider nearestEnemy = null;
            float minDistance = float.MaxValue;

            foreach (var hit in hits)
            {
                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestEnemy = hit;
                }
            }

            if (nearestEnemy != null)
            {
                StartCoroutine(AttackRoutine(nearestEnemy.transform));
            }
        }

        private IEnumerator AttackRoutine(Transform target)
        {
            _isAttacking = true;

            float attackSpeedFactor = _owner.AttackSpeed * _weaponData.BaseAttackSpeed;
            float totalCooldown = 1.0f / Mathf.Max(0.1f, attackSpeedFactor);
            _cooldownTimer = totalCooldown;

            Vector3 startPos = transform.position;
            Vector3 targetDir = (target.position - transform.position);
            targetDir.y = 0;
            targetDir.Normalize();

            _lastAttackDir = targetDir;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = _lastAttackDir.x < 0f;
            }

            // 🎯 혼합 공격 분기 로직
            bool useThrust = _weaponData.AttackType == WeaponAttackType.Thrust;
            if (_mixAttackTypes)
            {
                // 인스펙터에 설정한 확률(0~1)에 따라 공격 방식 결정
                useThrust = UnityEngine.Random.value <= _thrustProbability;
            }

            if (useThrust)
            {
                yield return StartCoroutine(ThrustRoutine(startPos, targetDir));
            }
            else
            {
                yield return StartCoroutine(SwingRoutine(startPos, targetDir));
            }

            _isAttacking = false;
        }

        // 1. 찌르기 모션 (날이 적을 향하게 개선됨)
        private IEnumerator ThrustRoutine(Vector3 startPos, Vector3 targetDir)
        {
            Vector3 thrustTarget = startPos + targetDir * (EffectiveDistance * 0.8f);
            float elapsed = 0f;
            float thrustDuration = 0.08f;
            bool hitApplied = false;

            // 🎯 날이 적을 향하도록 Z축 회전 각도 계산 (-90도는 칼이 위를 보고 있을 때의 보정값)
            float targetAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg - 90f;
            Quaternion thrustRot = _originalSpriteRot * Quaternion.Euler(0f, 0f, targetAngle);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.transform.localRotation = thrustRot;
            }

            while (elapsed < thrustDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / thrustDuration;
                transform.position = Vector3.Lerp(startPos, thrustTarget, t);

                if (!hitApplied && t >= 0.5f)
                {
                    ApplyDamage(targetDir);
                    hitApplied = true;
                }
                yield return null;
            }

            elapsed = 0f;
            float returnDuration = 0.12f;
            Vector3 currentThrustPos = transform.position;
            Quaternion currentRot = _spriteRenderer != null ? _spriteRenderer.transform.localRotation : _originalSpriteRot;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                float rad = _orbitAngleDeg * Mathf.Deg2Rad;
                Vector3 currentOrbitTarget = _owner.transform.position + new Vector3(Mathf.Cos(rad), _orbitHeight, Mathf.Sin(rad)) * _orbitRadius;

                transform.position = Vector3.Lerp(currentThrustPos, currentOrbitTarget, t);

                // 자연스럽게 원래 각도로 복구
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.transform.localRotation = Quaternion.Lerp(currentRot, _originalSpriteRot, t);
                }

                yield return null;
            }

            if (_spriteRenderer != null) _spriteRenderer.transform.localRotation = _originalSpriteRot;
        }

        // 2. 휘두르기 모션
        private IEnumerator SwingRoutine(Vector3 startPos, Vector3 targetDir)
        {
            float elapsed = 0f;
            float swingDuration = 0.18f;
            bool hitApplied = false;

            float baseAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg;
            float startAngle = baseAngle - 70f;
            float endAngle = baseAngle + 70f;

            float rotStart = 70f;
            float rotEnd = -70f;

            if (targetDir.x < 0f)
            {
                startAngle = baseAngle + 70f;
                endAngle = baseAngle - 70f;
                rotStart = -70f;
                rotEnd = 70f;
            }

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;

                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
                float rad = currentAngle * Mathf.Deg2Rad;

                Vector3 swingOffset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * (EffectiveDistance * 0.75f);
                transform.position = _owner.transform.position + swingOffset;

                if (_spriteRenderer != null)
                {
                    float currentRot = Mathf.Lerp(rotStart, rotEnd, t);
                    _spriteRenderer.transform.localRotation = _originalSpriteRot * Quaternion.Euler(0f, 0f, currentRot);
                }

                if (!hitApplied && t >= 0.4f)
                {
                    ApplyDamage(targetDir);
                    hitApplied = true;
                }
                yield return null;
            }

            elapsed = 0f;
            float returnDuration = 0.12f;
            Vector3 currentPos = transform.position;
            Quaternion currentRotQ = _spriteRenderer != null ? _spriteRenderer.transform.localRotation : _originalSpriteRot;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;

                float rad = _orbitAngleDeg * Mathf.Deg2Rad;
                Vector3 currentOrbitTarget = _owner.transform.position + new Vector3(Mathf.Cos(rad), _orbitHeight, Mathf.Sin(rad)) * _orbitRadius;
                transform.position = Vector3.Lerp(currentPos, currentOrbitTarget, t);

                if (_spriteRenderer != null)
                {
                    _spriteRenderer.transform.localRotation = Quaternion.Lerp(currentRotQ, _originalSpriteRot, t);
                }

                yield return null;
            }

            if (_spriteRenderer != null) _spriteRenderer.transform.localRotation = _originalSpriteRot;
        }

        private void ApplyDamage(Vector3 attackDir)
        {
            float finalDamage = _weaponData.BaseDamage * LevelDmgMultiplier * _owner.DmgMulti;

            Collider[] victims = Physics.OverlapSphere(transform.position, EffectiveArea, _enemyLayer);
            foreach (var victim in victims)
            {
                if (victim.TryGetComponent<MonsterController>(out var monster))
                {
                    monster.TakeWeaponDamage(finalDamage, attackDir, _weaponData.BaseKnockback, _weaponLevel);
                }
                else if (victim.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable.TakeDamage(finalDamage, attackDir, _weaponData.BaseKnockback);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_owner == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_owner.transform.position, EffectiveDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, EffectiveArea);
        }
    }
}