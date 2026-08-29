using System.Collections;
using UnityEngine;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.Controllers
{
    public class MeleeWeapon : MonoBehaviour
    {
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
        private Quaternion _originalSpriteRot; // 스프라이트의 초기 방향(눕혀진 각도) 보존용

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

            // 타입에 따라 분기 처리
            if (_weaponData.AttackType == WeaponAttackType.Thrust)
            {
                yield return StartCoroutine(ThrustRoutine(startPos, targetDir));
            }
            else
            {
                yield return StartCoroutine(SwingRoutine(startPos, targetDir));
            }

            _isAttacking = false;
        }

        // 1. 찌르기 모션
        private IEnumerator ThrustRoutine(Vector3 startPos, Vector3 targetDir)
        {
            Vector3 thrustTarget = startPos + targetDir * (EffectiveDistance * 0.8f);
            float elapsed = 0f;
            float thrustDuration = 0.08f;
            bool hitApplied = false;

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

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / returnDuration;
                float rad = _orbitAngleDeg * Mathf.Deg2Rad;
                Vector3 currentOrbitTarget = _owner.transform.position + new Vector3(Mathf.Cos(rad), _orbitHeight, Mathf.Sin(rad)) * _orbitRadius;

                transform.position = Vector3.Lerp(currentThrustPos, currentOrbitTarget, t);
                yield return null;
            }
        }

        // 2. 휘두르기(휩쓰기) 모션 - 완벽한 와이퍼 형태로 개선!
        private IEnumerator SwingRoutine(Vector3 startPos, Vector3 targetDir)
        {
            float elapsed = 0f;
            float swingDuration = 0.18f; // 살짝 여유를 줘서 모션을 부드럽게
            bool hitApplied = false;

            // 플레이어를 중심으로 부채꼴 궤적의 각도 계산
            float baseAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg;
            float startAngle = baseAngle - 70f;
            float endAngle = baseAngle + 70f;

            // 무기가 실제로 와이퍼처럼 꺾이는(로컬 Z회전) 각도
            float rotStart = 70f;
            float rotEnd = -70f;

            // 왼쪽 타격 시 역방향 와이퍼 
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

                // 1. 플레이어를 중심으로 와이퍼처럼 둥글게 궤적을 그리며 이동
                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);
                float rad = currentAngle * Mathf.Deg2Rad;

                // EffectiveDistance를 기준으로 넓게 휩씁니다
                Vector3 swingOffset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * (EffectiveDistance * 0.75f);
                transform.position = _owner.transform.position + swingOffset;

                // 2. 무기 자체도 궤적에 맞춰 와이퍼처럼 꺾임 (Pivot이 Bottom일 경우 완벽하게 작동)
                if (_spriteRenderer != null)
                {
                    float currentRot = Mathf.Lerp(rotStart, rotEnd, t);
                    _spriteRenderer.transform.localRotation = _originalSpriteRot * Quaternion.Euler(0f, 0f, currentRot);
                }

                // 스윙 범위 내 적 모두 타격
                if (!hitApplied && t >= 0.4f)
                {
                    ApplyDamage(targetDir);
                    hitApplied = true;
                }
                yield return null;
            }

            // 복귀 로직
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

                // 돌아올 때 회전도 원래 궤도로 스무스하게 복구
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.transform.localRotation = Quaternion.Lerp(currentRotQ, _originalSpriteRot, t);
                }

                yield return null;
            }

            // 복귀 완료 후 혹시 모를 오차를 위해 강제 초기화
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
            // 빨간 원: 적을 감지하는 사거리
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_owner.transform.position, EffectiveDistance);

            // 노란 원: 실제로 피해가 들어가는 물리적 Hit Area 반경
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, EffectiveArea);
        }
    }
}