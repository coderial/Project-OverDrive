using ProjectOverdrive.Managers;
using System;
using System.Collections;
using UnityEngine;

namespace ProjectOverdrive.Controllers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Hit Reaction")]
        [SerializeField, Min(0f)] private float _invincibilityDuration = 0.5f;
        [SerializeField, Min(0.01f)] private float _blinkInterval = 0.1f;
        [SerializeField] private SpriteRenderer[] _spriteRenderers;

        private Rigidbody _rigidbody;
        private bool[] _rendererEnabledStates;
        private Coroutine _invincibilityCoroutine;

        public int CurrentHp { get; private set; }
        public int MaxHp { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsInvincible => _invincibilityCoroutine != null;

        public event Action<int, int> OnHpChanged;
        public event Action OnDied;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();

            if (_spriteRenderers == null || _spriteRenderers.Length == 0)
            {
                _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            }

            CacheRendererStates();
        }

        public void Initialize(int maxHp)
        {
            StopInvincibility();
            MaxHp = Mathf.Max(1, maxHp);
            CurrentHp = MaxHp;
            IsDead = false;
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }

        public void TakeDamage(float damage, Vector3 hitDirection, float knockback)
        {
            if (IsDead || IsInvincible || damage <= 0f)
            {
                return;
            }
            SoundManager.Instance.PlaySfx("Hurt");
            int roundedDamage = Mathf.Max(1, Mathf.CeilToInt(damage));
            int appliedDamage = Mathf.Min(CurrentHp, roundedDamage);
            CurrentHp = Mathf.Max(0, CurrentHp - roundedDamage);

            if (DamageTextManager.Instance != null)
            {
                DamageTextManager.Instance.ShowPlayerDamage(appliedDamage, transform.position);
            }

            OnHpChanged?.Invoke(CurrentHp, MaxHp);

            if (CurrentHp <= 0)
            {
                Die();
                return;
            }

            ApplyKnockback(hitDirection, knockback);
            if (_invincibilityDuration > 0f)
            {
                _invincibilityCoroutine = StartCoroutine(InvincibilityRoutine());
            }
        }

        public void Heal(int amount)
        {
            if (IsDead || amount <= 0)
            {
                return;
            }

            int healedHp = Mathf.Min(MaxHp, CurrentHp + amount);
            if (healedHp == CurrentHp)
            {
                return;
            }

            CurrentHp = healedHp;
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }

        public void RestoreToFull()
        {
            if (IsDead || CurrentHp == MaxHp)
            {
                return;
            }

            CurrentHp = MaxHp;
            OnHpChanged?.Invoke(CurrentHp, MaxHp);
        }

        private void ApplyKnockback(Vector3 hitDirection, float knockback)
        {
            if (_rigidbody == null || knockback <= 0f || hitDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            hitDirection.y = 0f;
            _rigidbody.AddForce(hitDirection.normalized * knockback, ForceMode.Impulse);
        }

        private IEnumerator InvincibilityRoutine()
        {
            float endTime = Time.time + _invincibilityDuration;
            bool isVisible = true;

            while (Time.time < endTime)
            {
                isVisible = !isVisible;
                SetRenderersVisible(isVisible);

                float remainingTime = endTime - Time.time;
                yield return new WaitForSeconds(Mathf.Min(_blinkInterval, remainingTime));
            }

            RestoreRendererStates();
            _invincibilityCoroutine = null;
        }

        private void Die()
        {
            if (IsDead)
            {
                return;
            }

            SoundManager.Instance.PlaySfx("Lose");
            IsDead = true;
            if (_rigidbody != null)
            {
                _rigidbody.linearVelocity = Vector3.zero;
            }

            OnDied?.Invoke();
        }

        private void CacheRendererStates()
        {
            _rendererEnabledStates = new bool[_spriteRenderers.Length];
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    _rendererEnabledStates[i] = _spriteRenderers[i].enabled;
                }
            }
        }

        private void SetRenderersVisible(bool isVisible)
        {
            for (int i = 0; i < _spriteRenderers.Length; i++)
            {
                if (_spriteRenderers[i] != null && _rendererEnabledStates[i])
                {
                    _spriteRenderers[i].enabled = isVisible;
                }
            }
        }

        private void RestoreRendererStates()
        {
            if (_spriteRenderers == null || _rendererEnabledStates == null)
            {
                return;
            }

            int count = Mathf.Min(_spriteRenderers.Length, _rendererEnabledStates.Length);
            for (int i = 0; i < count; i++)
            {
                if (_spriteRenderers[i] != null)
                {
                    _spriteRenderers[i].enabled = _rendererEnabledStates[i];
                }
            }
        }

        private void StopInvincibility()
        {
            if (_invincibilityCoroutine != null)
            {
                StopCoroutine(_invincibilityCoroutine);
                _invincibilityCoroutine = null;
            }

            RestoreRendererStates();
        }

        private void OnDisable()
        {
            StopInvincibility();
        }
    }
}
