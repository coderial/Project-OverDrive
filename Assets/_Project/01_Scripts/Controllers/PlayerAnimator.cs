using UnityEngine;

namespace ProjectOverdrive.Controllers
{
    [DisallowMultipleComponent]
    public class PlayerAnimator : MonoBehaviour
    {
        private const float FRONT_IDLE_BLEND = 0.0f;
        private const float BACK_IDLE_BLEND = 0.2f;
        private const float SIDE_IDLE_BLEND = 0.4f;
        private const float FRONT_WALK_BLEND = 0.6f;
        private const float BACK_WALK_BLEND = 0.8f;
        private const float SIDE_WALK_BLEND = 1.0f;

        private static readonly int BlendParameterHash = Animator.StringToHash("Blend");
        private static readonly int IsDeadParameterHash = Animator.StringToHash("IsDead");

        private enum FacingDirection
        {
            Front,
            Side,
            Back
        }

        [Header("Visual Components")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private Animator _animator;

        [Header("Death Presentation")]
        [SerializeField, Min(0f)] private float _deathPresentationDuration = 0.5f;

        private FacingDirection _facingDirection = FacingDirection.Front;
        private bool _isDead;

        public float DeathPresentationDuration => _deathPresentationDuration;

        private void Awake()
        {
            if (_spriteRenderer == null) _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();

            UpdateMovement(Vector2.zero);
        }

        public void UpdateMovement(Vector2 moveInput)
        {
            if (_isDead) return;

            bool isWalking = moveInput.sqrMagnitude > 0.001f;

            if (isWalking)
            {
                UpdateFacingDirection(moveInput);
            }

            if (_animator != null)
            {
                _animator.SetFloat(BlendParameterHash, GetMovementBlend(_facingDirection, isWalking));
            }
        }

        public void PlayDeath()
        {
            if (_isDead) return;

            _isDead = true;
            if (_animator != null)
            {
                _animator.SetBool(IsDeadParameterHash, true);
            }
        }

        public void ResetDeath()
        {
            _isDead = false;
            if (_animator != null)
            {
                _animator.SetBool(IsDeadParameterHash, false);
            }
        }

        private void UpdateFacingDirection(Vector2 moveInput)
        {
            if (Mathf.Abs(moveInput.x) >= Mathf.Abs(moveInput.y))
            {
                _facingDirection = FacingDirection.Side;

                if (_spriteRenderer != null)
                {
                    _spriteRenderer.flipX = moveInput.x < 0f;
                }

                return;
            }

            _facingDirection = moveInput.y > 0f
                ? FacingDirection.Back
                : FacingDirection.Front;

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = false;
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
    }
}
