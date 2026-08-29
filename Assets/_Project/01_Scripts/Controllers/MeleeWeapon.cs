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
        private float _orbitAngleDeg;
        private float _fixedXRotationDeg;
        private float _fixedYRotationDeg;
        private float _cooldownTimer = 0f;
        private bool _isAttacking = false;

        public float EffectiveRange => (_weaponData != null && _owner != null)
            ? _weaponData.BaseAttackRange + _owner.AdditionalRange
            : 1.5f;

        /// <summary>
        /// 무기 생성 시 초기화 (공전 반경 포함)
        /// </summary>
        public void Initialize(WeaponData data, PlayerController owner, float angleDeg, LayerMask enemyLayer, float orbitRadius)
        {
            _weaponData = data;
            _owner = owner;
            _orbitAngleDeg = angleDeg;
            _enemyLayer = enemyLayer;
            _orbitRadius = orbitRadius;
            _fixedXRotationDeg = transform.eulerAngles.x;
            _fixedYRotationDeg = transform.eulerAngles.y;
            transform.rotation = GetFacingRotation(GetScreenUpDirection());
        }

        private void Update()
        {
            if (_owner == null || _weaponData == null) return;

            if (_cooldownTimer > 0f)
            {
                _cooldownTimer -= Time.deltaTime;
            }

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
        }

        private Quaternion GetFacingRotation(Vector3 direction)
        {
            return Quaternion.LookRotation(direction, Vector3.up)
                * Quaternion.Euler(_fixedXRotationDeg, _fixedYRotationDeg, 0f);
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

            Collider[] hits = Physics.OverlapSphere(_owner.transform.position, EffectiveRange, _enemyLayer);
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

            // 프로퍼티 정상 참조 (BaseAttackSpeed)
            float attackSpeedFactor = _owner.AttackSpeed * _weaponData.BaseAttackSpeed;
            float totalCooldown = 1.0f / Mathf.Max(0.1f, attackSpeedFactor);
            _cooldownTimer = totalCooldown;

            Vector3 startPos = transform.position;
            Vector3 targetDir = (target.position - transform.position);
            targetDir.y = 0;
            targetDir.Normalize();

            transform.rotation = GetFacingRotation(targetDir);

            Vector3 thrustTarget = startPos + targetDir * (EffectiveRange * 0.8f);
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

            transform.rotation = GetFacingRotation(targetDir);
            _isAttacking = false;
        }

        private void ApplyDamage(Vector3 attackDir)
        {
            float finalDamage = _weaponData.BaseDamage * _owner.DmgMulti;
            float hitRadius = 0.8f;

            Collider[] victims = Physics.OverlapSphere(transform.position, hitRadius, _enemyLayer);
            foreach (var victim in victims)
            {
                if (victim.TryGetComponent<IDamageable>(out var damageable))
                {
                    // 프로퍼티 정상 참조 (BaseKnockback)
                    damageable.TakeDamage(finalDamage, attackDir, _weaponData.BaseKnockback);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, EffectiveRange);
        }
    }
}
