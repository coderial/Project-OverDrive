using System.Collections;
using UnityEngine;
using ProjectOverdrive.Controllers;
using ProjectOverdrive.Managers;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class MonsterController : MonoBehaviour, IPoolable, IDamageable
{
    [Header("Home Run Settings")]
    [Tooltip("홈런 시 날아가는 속도")]
    [SerializeField] private float _homeRunSpeed = 25f;
    [Tooltip("홈런 시 위로 치솟는 높이 (값이 클수록 가파르게 솟아오릅니다)")]
    [SerializeField] private float _homeRunHeight = 15f;
    [Tooltip("홈런 시 빙글빙글 도는 회전 속도")]
    [SerializeField] private float _homeRunSpinSpeed = 1500f;

    public static Transform SharedTarget { get; set; }

    private Transform _cachedTransform;
    private SpriteRenderer _spriteRenderer;
    private Transform _target;
    private PooledObject _pooledObject;
    private PlayerHealth _contactPlayer;
    private MonsterData _data;
    private float _moveSpeed;
    private float _stoppingDistance;
    private float _stoppingDistanceSquared;
    private float _nextContactDamageTime;

    private bool _isDead;
    private Quaternion _originalSpriteRot;
    private Color _originalColor = Color.white;
    private Vector3 _originalSpriteScale = Vector3.one;

    public MonsterData Data => _data;
    public float AttackPower => _data != null ? _data.AttackPower : 0f;
    public float MaxHealth => _data != null ? _data.MaxHealth : 0f;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        _cachedTransform = transform;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        TryGetComponent(out _pooledObject);

        if (_spriteRenderer != null)
        {
            _originalSpriteRot = _spriteRenderer.transform.localRotation;
            _originalColor = _spriteRenderer.color;
            _originalSpriteScale = _spriteRenderer.transform.localScale;
        }
    }

    private void OnEnable()
    {
        if (_target == null) _target = SharedTarget;
    }

    private void Update()
    {
        if (_target == null || _isDead) return;

        Vector3 currentPosition = _cachedTransform.position;
        Vector3 direction = _target.position - currentPosition;
        direction.y = 0f;

        float squaredDistance = direction.sqrMagnitude;
        if (squaredDistance <= _stoppingDistanceSquared) return;

        float distance = Mathf.Sqrt(squaredDistance);
        float moveDistance = Mathf.Min(_moveSpeed * Time.deltaTime, distance - _stoppingDistance);
        UpdateSpriteFlip(direction.x);
        _cachedTransform.position = currentPosition + direction * (moveDistance / distance);
    }

    private void UpdateSpriteFlip(float horizontalDirection)
    {
        if (_spriteRenderer == null || Mathf.Abs(horizontalDirection) <= 0.001f) return;
        _spriteRenderer.flipX = horizontalDirection < 0f;
    }

    public void Configure(MonsterData data, Transform target)
    {
        _data = data;
        _target = target;
        _moveSpeed = data.MoveSpeed;
        _stoppingDistance = data.StoppingDistance;
        _stoppingDistanceSquared = _stoppingDistance * _stoppingDistance;
        CurrentHealth = data.MaxHealth;
        _nextContactDamageTime = 0f;

        if (_target != null)
        {
            UpdateSpriteFlip(_target.position.x - _cachedTransform.position.x);
        }
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float knockback)
    {
        TakeDamageInternal(damage, hitDirection, knockback, 0);
    }

    public void TakeWeaponDamage(float damage, Vector3 hitDirection, float knockback, int weaponLevel)
    {
        TakeDamageInternal(damage, hitDirection, knockback, weaponLevel);
    }

    private void TakeDamageInternal(float damage, Vector3 hitDirection, float knockback, int weaponLevel)
    {
        if (_data == null || damage <= 0f || _isDead) return;

        Vector3 hitPosition = _cachedTransform.position;
        // float appliedDamage = Mathf.Min(CurrentHealth, damage);
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);

        if (DamageTextManager.Instance != null)
            DamageTextManager.Instance.ShowMonsterDamage(damage, hitPosition);

        if (CurrentHealth <= 0f)
        {
            _isDead = true;
            _contactPlayer = null;
            DropCurrency(hitPosition);

            float homeRunChance = weaponLevel * 15f;
            bool isHomeRun = Random.Range(0f, 100f) <= homeRunChance;

            if (isHomeRun)
            {
                SoundManager.Instance.PlaySfx("Home-Run");
                StartCoroutine(HomeRunRoutine(hitDirection));
            }
            else
            {
                SoundManager.Instance.PlaySfx("Hurt");
                ReleaseToPool();
            }
            return;
        }
        else
        {
            SoundManager.Instance.PlaySfx("Hurt");
            StartCoroutine(FlashRoutine());
        }

        if (knockback > 0f && hitDirection.sqrMagnitude > 0.0001f)
        {
            hitDirection.y = 0f;
            _cachedTransform.position += hitDirection.normalized * knockback;
        }
    }

    private IEnumerator FlashRoutine()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = Color.red;
            yield return new WaitForSeconds(0.1f);
            if (_spriteRenderer != null && !_isDead)
            {
                _spriteRenderer.color = _originalColor;
            }
        }
    }

    private IEnumerator HomeRunRoutine(Vector3 hitDirection)
    {
        // 1. 역경직(Hit Stop) 및 카메라 킥 (0.05초간 게임 정지)
        Time.timeScale = 0.05f;
        Camera mainCam = Camera.main;
        Vector3 origCamPos = mainCam != null ? mainCam.transform.position : Vector3.zero;

        if (mainCam != null)
        {
            // 타격 방향으로 카메라를 살짝 밀침
            mainCam.transform.position += hitDirection.normalized * 0.5f;
        }

        yield return new WaitForSecondsRealtime(0.05f); // 0.05초 체공 시간

        // 정지 풀림 및 카메라 복구
        Time.timeScale = 1f;
        if (mainCam != null) mainCam.transform.position = origCamPos;

        // 2. 본격적인 홈런 세팅
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        float elapsed = 0f;
        float duration = 1.0f; // 타격감 극대화를 위해 1초로 셋팅

        Vector3 startPos = _cachedTransform.position;
        Vector3 flyDirection = hitDirection.normalized;

        // [핵심] 몬스터 그림자 임시 생성 (코드로 즉석 생성하여 바닥에 붙여둠)
        GameObject shadowObj = new GameObject("HomeRunShadow");
        shadowObj.transform.position = startPos;
        shadowObj.transform.rotation = _originalSpriteRot;
        SpriteRenderer shadowSr = shadowObj.AddComponent<SpriteRenderer>();
        shadowSr.sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
        shadowSr.color = new Color(0f, 0f, 0f, 0.4f);

        float ghostTimer = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // X, Z 축 이동 (처음엔 빠르고 갈수록 감속하는 Ease-Out 연출)
            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 currentGroundPos = startPos + flyDirection * (_homeRunSpeed * easeOutT);

            if (shadowObj != null)
            {
                shadowObj.transform.position = currentGroundPos;
                // 멀어질수록 그림자가 콩알만해짐
                shadowObj.transform.localScale = _originalSpriteScale * (1f - (t * 0.8f));
            }

            // 본체는 Y축(가상 Z축)을 더해 위로 솟구치게 만듦
            float height = Mathf.Sin(t * Mathf.PI) * _homeRunHeight;
            _cachedTransform.position = currentGroundPos + new Vector3(0, height, 0);

            if (_spriteRenderer != null)
            {
                // 풍차 돌리기
                _spriteRenderer.transform.Rotate(0f, 0f, _homeRunSpinSpeed * Time.deltaTime, Space.Self);

                // 원근감 스케일 (하늘 꼭대기에 있을 때 1.5배 커졌다가, 바닥에 꽂힐 때 0으로 수렴)
                float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f;
                scaleMultiplier -= t * 0.8f;
                _spriteRenderer.transform.localScale = _originalSpriteScale * Mathf.Max(0f, scaleMultiplier);
            }

            // 잔상(Ghost) 궤적 생성
            ghostTimer += Time.deltaTime;
            if (ghostTimer >= 0.04f)
            {
                ghostTimer = 0f;
                SpawnGhostTrail();
            }

            yield return null;
        }

        // 비행이 끝나면 임시 그림자 삭제
        if (shadowObj != null) Destroy(shadowObj);

        ReleaseToPool();
    }

    private void SpawnGhostTrail()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite == null) return;

        // 잔상 오브젝트 동적 생성
        GameObject ghost = new GameObject("GhostTrail");
        ghost.transform.position = _spriteRenderer.transform.position;
        ghost.transform.rotation = _spriteRenderer.transform.rotation;
        ghost.transform.localScale = _spriteRenderer.transform.localScale;

        SpriteRenderer ghostSr = ghost.AddComponent<SpriteRenderer>();
        ghostSr.sprite = _spriteRenderer.sprite;
        ghostSr.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.5f);

        // 아래에 만든 GhostFader 부착 (알아서 투명해지다가 소멸함)
        GhostFader fader = ghost.AddComponent<GhostFader>();
        fader.Setup(ghostSr, 0.3f); // 0.3초간 유지
    }

    public void SetTarget(Transform target)
    {
        _target = target;
    }

    public void ForceDespawn()
    {
        if (!gameObject.activeInHierarchy) return;

        StopAllCoroutines();
        _isDead = true;
        _contactPlayer = null;
        ReleaseToPool();
    }

    public void OnSpawned()
    {
        _target = SharedTarget;
        _contactPlayer = null;
        _nextContactDamageTime = 0f;
        _isDead = false;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
            _spriteRenderer.transform.localRotation = _originalSpriteRot;
            _spriteRenderer.transform.localScale = _originalSpriteScale; // 스케일 원상복구
            _spriteRenderer.flipX = false;
        }
    }

    public void OnDespawned()
    {
        _target = null;
        _contactPlayer = null;
        _data = null;
        CurrentHealth = 0f;
        _nextContactDamageTime = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isDead) return;
        if (other.TryGetComponent(out PlayerHealth player))
        {
            _contactPlayer = player;
            ApplyContactDamage();
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (_isDead) return;
        if (Time.time < _nextContactDamageTime) return;

        if (_contactPlayer != null)
        {
            if (other.gameObject != _contactPlayer.gameObject) return;
        }
        else if (!other.TryGetComponent(out _contactPlayer)) return;

        ApplyContactDamage();
    }

    private void OnTriggerExit(Collider other)
    {
        if (_contactPlayer != null && other.gameObject == _contactPlayer.gameObject)
        {
            _contactPlayer = null;
        }
    }

    private void ApplyContactDamage()
    {
        if (_data == null || _contactPlayer == null || _data.AttackPower <= 0f) return;

        Vector3 hitDirection = _contactPlayer.transform.position - _cachedTransform.position;
        _contactPlayer.TakeDamage(_data.AttackPower, hitDirection, 0f);
        _nextContactDamageTime = Time.time + _data.ContactDamageInterval;
    }

    private void DropCurrency(Vector3 deathPosition)
    {
        GameObject currencyPrefab = _data != null ? _data.CurrencyPrefab : null;
        PoolingManager poolingManager = PoolingManager.Instance;

        if (currencyPrefab == null || poolingManager == null) return;

        poolingManager.Get(currencyPrefab, deathPosition, currencyPrefab.transform.rotation);
    }

    private void ReleaseToPool()
    {
        if (_pooledObject == null) TryGetComponent(out _pooledObject);
        if (_pooledObject != null) _pooledObject.Release();
        else gameObject.SetActive(false);
    }
}

/// <summary>
/// 홈런 잔상(Ghost)을 서서히 투명하게 만들고 스스로 삭제하는 가벼운 유틸 컴포넌트
/// 몬스터가 죽어 풀(Pool)로 반환되어도 잔상은 화면에 남아 스무스하게 사라집니다.
/// </summary>
public class GhostFader : MonoBehaviour
{
    private SpriteRenderer _sr;
    private float _duration;
    private float _elapsed;
    private float _startAlpha;

    public void Setup(SpriteRenderer sr, float duration)
    {
        _sr = sr;
        _duration = duration;
        _elapsed = 0f;
        _startAlpha = sr.color.a;
    }

    private void Update()
    {
        _elapsed += Time.deltaTime;
        if (_sr != null)
        {
            Color c = _sr.color;
            c.a = Mathf.Lerp(_startAlpha, 0f, _elapsed / _duration);
            _sr.color = c;
        }

        if (_elapsed >= _duration)
        {
            Destroy(gameObject);
        }
    }
}
