using System.Collections;
using UnityEngine;

namespace ProjectOverdrive.Controllers
{
    [RequireComponent(typeof(Collider))]
    public class DummyEnemy : MonoBehaviour, IDamageable
    {
        [Header("Stats")]
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private float _currentHp;

        [Header("Feedback")]
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Color _flashColor = Color.red;

        private Color _originalColor;
        private Rigidbody _rb;

        private void Awake()
        {
            _currentHp = _maxHp;
            _rb = GetComponent<Rigidbody>();
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
            if (_renderer != null) _originalColor = _renderer.material.color;
        }

        public void TakeDamage(float damage, Vector3 hitDirection, float knockback)
        {
            _currentHp = Mathf.Max(0, _currentHp - damage);
            Debug.Log($"<color=orange>[DummyEnemy]</color> 피격! 데미지: {damage:F1}, 남은 체력: {_currentHp}/{_maxHp}");

            // 넉백 적용
            if (_rb != null && knockback > 0f)
            {
                _rb.AddForce(hitDirection.normalized * knockback, ForceMode.Impulse);
            }

            // 피격 시 빨간색 깜빡임
            if (_renderer != null)
            {
                StopAllCoroutines();
                StartCoroutine(FlashRoutine());
            }

            if (_currentHp <= 0)
            {
                Die();
            }
        }

        private IEnumerator FlashRoutine()
        {
            _renderer.material.color = _flashColor;
            yield return new WaitForSeconds(0.1f);
            _renderer.material.color = _originalColor;
        }

        private void Die()
        {
            Debug.Log("<color=red>[DummyEnemy]</color> 더미 적 사망!");
            Destroy(gameObject);
        }
    }
}