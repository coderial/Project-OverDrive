using System.Collections;
using System.Collections.Generic;
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

            // 🎯 기준 방향을 '플레이어 -> 적'으로 깔끔하게 통일합니다.
            Vector3 targetDir = (target.position - _owner.transform.position);
            targetDir.y = 0;
            targetDir.Normalize();
            _lastAttackDir = targetDir;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = _lastAttackDir.x < 0f;
            }

            // 공격이 시작되는 깔끔한 고정 위치 (플레이어와 적 사이의 궤도 반경 위치)
            Vector3 attackStartPos = _owner.transform.position + targetDir * _orbitRadius;

            bool useThrust = _weaponData.AttackType == WeaponAttackType.Thrust;
            if (_weaponData.MixAttackTypes)
            {
                useThrust = UnityEngine.Random.value <= _weaponData.ThrustProbability;
            }

            if (useThrust)
            {
                yield return StartCoroutine(ThrustRoutine(attackStartPos, targetDir));
            }
            else
            {
                yield return StartCoroutine(SwingRoutine(attackStartPos, targetDir));
            }

            _isAttacking = false;
        }

                                private IEnumerator ThrustRoutine(Vector3 startPos, Vector3 targetDir)
        {
            Vector3 thrustTarget = _owner.transform.position + targetDir * EffectiveDistance;
            
            float targetAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg - 45f;
            Quaternion thrustRot = Quaternion.Euler(90f, 0f, targetAngle);

            if (_spriteRenderer != null) _spriteRenderer.transform.localRotation = thrustRot;

            float aimDuration = 0.1f;
            Vector3 aimPos = startPos - targetDir * 0.3f;
            float elapsed = 0f;

            while (elapsed < aimDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, aimPos, elapsed / aimDuration);
                yield return null;
            }

            float thrustDuration = 0.05f;
            elapsed = 0f;
            HashSet<Collider> hitTargets = new HashSet<Collider>();

            while (elapsed < thrustDuration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(aimPos, thrustTarget, elapsed / thrustDuration);
                
                // 찌르기: 진행 방향을 기준으로 좁은 각도(60도) 내의 적 타격
                ApplyConeDamage(targetDir, 60f, hitTargets);
                
                yield return null;
            }

            elapsed = 0f;
            float returnDuration = 0.15f;
            Vector3 currentThrustPos = transform.position;
            Quaternion currentRot = _spriteRenderer != null ? _spriteRenderer.transform.localRotation : _originalSpriteRot;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float rad = _orbitAngleDeg * Mathf.Deg2Rad;
                Vector3 currentOrbitTarget = _owner.transform.position + new Vector3(Mathf.Cos(rad), _orbitHeight, Mathf.Sin(rad)) * _orbitRadius;
                transform.position = Vector3.Lerp(currentThrustPos, currentOrbitTarget, elapsed / returnDuration);
                
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.transform.localRotation = Quaternion.Lerp(currentRot, _originalSpriteRot, elapsed / returnDuration);
                }
                yield return null;
            }

            if (_spriteRenderer != null) _spriteRenderer.transform.localRotation = _originalSpriteRot;
        }

                                private IEnumerator SwingRoutine(Vector3 fixedPos, Vector3 targetDir)
        {
            float elapsed = 0f;
            float swingDuration = 0.18f; 
            HashSet<Collider> hitTargets = new HashSet<Collider>();

            float baseAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg;
            float rotStart = 80f;
            float rotEnd = -80f;

            if (targetDir.x < 0f)
            {
                rotStart = -80f;
                rotEnd = 80f;
            }

            Quaternion baseRot = Quaternion.Euler(90f, 0f, baseAngle - 45f);

            while (elapsed < swingDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / swingDuration;

                transform.position = fixedPos;

                if (_spriteRenderer != null)
                {
                    float currentRot = Mathf.Lerp(rotStart, rotEnd, t);
                    _spriteRenderer.transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, currentRot);
                }

                // 베기: 궤적 전체를 커버하기 위해 타겟 방향 기준 넓은 부채꼴(160도) 검사
                ApplyConeDamage(targetDir, 160f, hitTargets);

                yield return null;
            }

            elapsed = 0f;
            float returnDuration = 0.12f;
            Vector3 currentPos = transform.position;
            Quaternion currentRotQ = _spriteRenderer != null ? _spriteRenderer.transform.localRotation : _originalSpriteRot;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float rad = _orbitAngleDeg * Mathf.Deg2Rad;
                Vector3 currentOrbitTarget = _owner.transform.position + new Vector3(Mathf.Cos(rad), _orbitHeight, Mathf.Sin(rad)) * _orbitRadius;
                transform.position = Vector3.Lerp(currentPos, currentOrbitTarget, elapsed / returnDuration);

                if (_spriteRenderer != null)
                {
                    _spriteRenderer.transform.localRotation = Quaternion.Lerp(currentRotQ, _originalSpriteRot, elapsed / returnDuration);
                }
                yield return null;
            }

            if (_spriteRenderer != null) _spriteRenderer.transform.localRotation = _originalSpriteRot;
        }

                private void ApplyConeDamage(Vector3 attackDir, float angleRange, HashSet<Collider> hitTargets)
        {
            float finalDamage = _weaponData.BaseDamage * LevelDmgMultiplier * _owner.DmgMulti;

            // 플레이어(중심)를 기준으로 사거리 내의 모든 타겟 검색
            Collider[] victims = Physics.OverlapSphere(_owner.transform.position, EffectiveDistance, _enemyLayer);
            
            foreach (var victim in victims)
            {
                // 이미 이번 공격에서 맞은 적은 패스 (중복 타격 방지)
                if (hitTargets.Contains(victim)) continue;

                Vector3 dirToVictim = (victim.transform.position - _owner.transform.position);
                dirToVictim.y = 0;
                dirToVictim.Normalize();

                // 부채꼴 각도 검사 (기준 방향과 몬스터 방향 사이의 각도)
                float angle = Vector3.Angle(attackDir, dirToVictim);
                if (angle <= angleRange * 0.5f)
                {
                    // 히트 판정
                    hitTargets.Add(victim);

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
        }

                private void OnDrawGizmosSelected()
        {
            if (_owner == null) return;
            
            // 1. 공격 사거리 원
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_owner.transform.position, EffectiveDistance);

            // 2. 현재 공격 방향 (기준선)
            Gizmos.color = Color.cyan;
            Vector3 forwardLine = _lastAttackDir * EffectiveDistance;
            Gizmos.DrawRay(_owner.transform.position, forwardLine);

            // 3. 베기 부채꼴 범위 (대략 160도)
            Vector3 leftLine = Quaternion.Euler(0f, -80f, 0f) * _lastAttackDir * EffectiveDistance;
            Vector3 rightLine = Quaternion.Euler(0f, 80f, 0f) * _lastAttackDir * EffectiveDistance;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_owner.transform.position, leftLine);
            Gizmos.DrawRay(_owner.transform.position, rightLine);
            Gizmos.DrawLine(_owner.transform.position + leftLine, _owner.transform.position + rightLine);
        }
    }
}