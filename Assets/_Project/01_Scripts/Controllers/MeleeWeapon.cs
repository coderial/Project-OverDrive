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
        private int _weaponLevel = 1;
        private float _orbitAngleDeg;
        private Quaternion _facingRotationOffset;
        private float _cooldownTimer = 0f;
        private bool _isAttacking = false;

        // 마지막으로 공격한 방향을 기억 (초기값: 앞)
        private Vector3 _lastAttackDir = Vector3.forward;

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
            _facingRotationOffset = transform.rotation;
            _lastAttackDir = GetScreenUpDirection();
            transform.rotation = GetFacingRotation(_lastAttackDir);

            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null && _weaponData != null)
            {
                Sprite levelSprite = _weaponData.GetSpriteForLevel(_weaponLevel);
                if (levelSprite != null)
                {
                    sr.sprite = levelSprite;
                }
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

            // 공전 중에도 마지막으로 공격했던 방향을 계속 바라보게 유지
            if (_lastAttackDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = GetFacingRotation(_lastAttackDir);
            }
        }

        private Quaternion GetFacingRotation(Vector3 direction)
        {
            return Quaternion.LookRotation(direction, Vector3.up) * _facingRotationOffset;
        }

        private static Vector3 GetScreenUpDirection()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return Vector3.forward;
            }

            Vector3 screenUpDirection = Vector3.ProjectOnPlane(mainCamera.transform.up, Vector3.up);
            return screenUpDirection.sqrMagnitude > 0.001f
                ? screenUpDirection.normalized
                : Vector3.forward;
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

            // 이번 공격에서 찌를 타겟의 방향을 갱신하고 저장
            _lastAttackDir = targetDir;
            transform.rotation = GetFacingRotation(_lastAttackDir);

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

            transform.rotation = GetFacingRotation(_lastAttackDir);
            _isAttacking = false;
        }

        private void ApplyDamage(Vector3 attackDir)
        {
            float finalDamage = _weaponData.BaseDamage * LevelDmgMultiplier * _owner.DmgMulti;

            Collider[] victims = Physics.OverlapSphere(transform.position, EffectiveArea, _enemyLayer);
            foreach (var victim in victims)
            {
                if (victim.TryGetComponent<IDamageable>(out var damageable))
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
