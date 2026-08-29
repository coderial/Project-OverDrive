using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectOverdrive.Data;
using ProjectOverdrive.Managers;

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

        private void LateUpdate()
        {
            if (_owner == null || _weaponData == null) return;
            if (_cooldownTimer > 0f) _cooldownTimer -= Time.deltaTime;

            if (!_isAttacking)
            {
                _orbitAngleDeg += 120f * Time.deltaTime;
                float rad = _orbitAngleDeg * Mathf.Deg2Rad;
                Vector3 offset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * _orbitRadius;
                offset.y = _orbitHeight + Mathf.Sin(Time.time * 3f + _orbitAngleDeg) * 0.1f;

                transform.position = _owner.transform.position + offset;

                if (_spriteRenderer != null && _lastAttackDir.sqrMagnitude > 0.001f)
                {
                    float angle = Mathf.Atan2(_lastAttackDir.z, _lastAttackDir.x) * Mathf.Rad2Deg;
                    _spriteRenderer.transform.localRotation = Quaternion.Euler(90f, 0f, angle - 45f);
                }

                CheckAndAttackNearestEnemy();
            }
            else
            {
                Vector3 offset = _lastAttackDir * _orbitRadius;
                offset.y = _orbitHeight;
                transform.position = _owner.transform.position + offset;
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

            Vector3 targetDir = (target.position - _owner.transform.position);
            targetDir.y = 0;
            targetDir.Normalize();
            _lastAttackDir = targetDir;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = _lastAttackDir.x < 0f;
            }

            bool useThrust = _weaponData.AttackType == WeaponAttackType.Thrust;
            if (useThrust)
            {
                yield return StartCoroutine(ThrustRoutine(targetDir));
            }
            else
            {
                yield return StartCoroutine(SwingRoutine(targetDir));
            }

            _isAttacking = false;
        }

        private IEnumerator ThrustRoutine(Vector3 targetDir)
        {
            float targetAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg - 45f;
            Quaternion thrustRot = Quaternion.Euler(90f, 0f, targetAngle);

            if (_spriteRenderer != null) _spriteRenderer.transform.localRotation = thrustRot;

            Vector3 localStart = Vector3.zero;
            Vector3 localAim = -targetDir * 0.4f; 
            Vector3 localThrust = targetDir * 1.5f; 

            HashSet<Collider> hitTargets = new HashSet<Collider>();

            if (_spriteRenderer != null)
            {
                float elapsed = 0f;
                float duration = 0.05f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeT = 1f - (1f - t) * (1f - t); 
                    _spriteRenderer.transform.localPosition = Vector3.Lerp(localStart, localAim, easeT);
                    yield return null;
                }

                PlayAttackVFX(transform.position + targetDir * 0.5f, targetDir);

                elapsed = 0f;
                duration = 0.08f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeT = 1f - Mathf.Pow(1f - t, 3f); 
                    _spriteRenderer.transform.localPosition = Vector3.Lerp(localAim, localThrust, easeT);
                    ApplyConeDamage(targetDir, 60f, hitTargets);
                    yield return null;
                }

                yield return new WaitForSeconds(0.04f);

                elapsed = 0f;
                duration = 0.1f;
                Vector3 currentPos = _spriteRenderer.transform.localPosition;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; 
                    _spriteRenderer.transform.localPosition = Vector3.Lerp(currentPos, localStart, easeT);
                    yield return null;
                }
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.transform.localPosition = Vector3.zero;
                _spriteRenderer.transform.localRotation = _originalSpriteRot;
            }
        }

        private IEnumerator SwingRoutine(Vector3 targetDir)
        {
            float baseAngle = Mathf.Atan2(targetDir.z, targetDir.x) * Mathf.Rad2Deg;
            float rotStart = 100f;
            float rotEnd = -100f;

            if (targetDir.x < 0f)
            {
                rotStart = -100f;
                rotEnd = 100f;
            }

            Quaternion baseRot = Quaternion.Euler(90f, 0f, baseAngle - 45f);
            float windUpAngle = rotStart + (rotStart > 0 ? 20f : -20f); 
            HashSet<Collider> hitTargets = new HashSet<Collider>();

            if (_spriteRenderer != null)
            {
                float elapsed = 0f;
                float duration = 0.05f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeT = 1f - (1f - t) * (1f - t); 
                    float currentRot = Mathf.Lerp(rotStart, windUpAngle, easeT);
                    _spriteRenderer.transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, currentRot);
                    yield return null;
                }

                PlayAttackVFX(transform.position, targetDir);

                elapsed = 0f;
                duration = 0.12f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeT = 1f - Mathf.Pow(1f - t, 3f); 
                    float currentRot = Mathf.Lerp(windUpAngle, rotEnd, easeT);
                    _spriteRenderer.transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, currentRot);
                    ApplyConeDamage(targetDir, 160f, hitTargets);
                    yield return null;
                }

                yield return new WaitForSeconds(0.05f);

                elapsed = 0f;
                duration = 0.1f;
                Quaternion startRetRot = _spriteRenderer.transform.localRotation;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easeT = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; 
                    _spriteRenderer.transform.localRotation = Quaternion.Lerp(startRetRot, _originalSpriteRot, easeT);
                    yield return null;
                }
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.transform.localPosition = Vector3.zero;
                _spriteRenderer.transform.localRotation = _originalSpriteRot;
            }
        }

        private void PlayAttackVFX(Vector3 position, Vector3 direction)
        {
            if (_weaponData == null || _weaponData.AttackEffectPrefab == null) return;
            
            float angle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
            Quaternion vfxRot = Quaternion.Euler(90f, 0f, angle - 90f);
            
            GameObject vfx = Instantiate(_weaponData.AttackEffectPrefab, position, vfxRot);
            Destroy(vfx, 1.5f);
        }

        private void ApplyConeDamage(Vector3 attackDir, float angleRange, HashSet<Collider> hitTargets)
        {
            float finalDamage = _weaponData.BaseDamage * LevelDmgMultiplier * _owner.DmgMulti;

            Collider[] victims = Physics.OverlapSphere(_owner.transform.position, EffectiveDistance, _enemyLayer);
            
            foreach (var victim in victims)
            {
                if (hitTargets.Contains(victim)) continue;

                Vector3 dirToVictim = (victim.transform.position - _owner.transform.position);
                dirToVictim.y = 0;
                dirToVictim.Normalize();

                float angle = Vector3.Angle(attackDir, dirToVictim);
                if (angle <= angleRange * 0.5f)
                {
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
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_owner.transform.position, EffectiveDistance);

            Gizmos.color = Color.cyan;
            Vector3 forwardLine = _lastAttackDir * EffectiveDistance;
            Gizmos.DrawRay(_owner.transform.position, forwardLine);

            Vector3 leftLine = Quaternion.Euler(0f, -80f, 0f) * _lastAttackDir * EffectiveDistance;
            Vector3 rightLine = Quaternion.Euler(0f, 80f, 0f) * _lastAttackDir * EffectiveDistance;
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_owner.transform.position, leftLine);
            Gizmos.DrawRay(_owner.transform.position, rightLine);
            Gizmos.DrawLine(_owner.transform.position + leftLine, _owner.transform.position + rightLine);
        }
    }
}