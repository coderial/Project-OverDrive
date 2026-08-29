using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectOverdrive.Data;
using ProjectOverdrive.Managers;
using ProjectOverdrive.Controllers;
using ProjectOverdrive.Managers;

[RequireComponent(typeof(Collider))]
public sealed class MonsterController : MonoBehaviour, IPoolable, IDamageable
{
    [Header("Home Run Settings")]
    [Tooltip("홈런 날아가는 속도")]
    [SerializeField] private float _homeRunSpeed = 25f;
    [Tooltip("홈런 시 치솟는 높이 (숫자가 클수록 포물선이 높아집니다)")]
    [SerializeField] private float _homeRunHeight = 15f;
    [Tooltip("홈런 시 회전 속도")]
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

    private static GameObject _ghostPrefab;
    private GameObject _currentShadowObj;
    private readonly List<GameObject> _activeGhosts = new List<GameObject>();

    public MonsterData Data => _data;
    public float CurrentHealth { get; private set; }

    private void Awake()
    {
        _cachedTransform = transform;
        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (_spriteRenderer != null)
        {
            _originalSpriteRot = _spriteRenderer.transform.localRotation;
            _originalColor = _spriteRenderer.color;
            _originalSpriteScale = _spriteRenderer.transform.localScale;
        }

        TryGetComponent(out _pooledObject);
    }

    private static void EnsureGhostPrefab()
    {
        if (_ghostPrefab != null) return;
        
        _ghostPrefab = new GameObject("MonsterGhostPrefab");
        _ghostPrefab.SetActive(false);
        Object.DontDestroyOnLoad(_ghostPrefab);
        
        _ghostPrefab.AddComponent<SpriteRenderer>();
        _ghostPrefab.AddComponent<GhostFader>();
    }

    private void OnDisable()
    {
        if (_currentShadowObj != null && PoolingManager.Instance != null)
        {
            PoolingManager.Instance.Release(_currentShadowObj);
            _currentShadowObj = null;
        }

        if (PoolingManager.Instance != null)
        {
            for (int i = 0; i < _activeGhosts.Count; i++)
            {
                if (_activeGhosts[i] != null && _activeGhosts[i].activeInHierarchy)
                {
                    PoolingManager.Instance.Release(_activeGhosts[i]);
                }
            }
        }
        _activeGhosts.Clear();
    }

    public void Configure(MonsterData data, Transform target)
    {
        if (data == null)
        {
            Debug.LogError("MonsterData가 Null입니다.", this);
            return;
        }

        _data = data;
        _moveSpeed = data.MoveSpeed;
        CurrentHealth = data.MaxHealth;
        _stoppingDistance = data.StoppingDistance;
        _stoppingDistanceSquared = _stoppingDistance * _stoppingDistance;

        _target = target;
        SharedTarget = target;
    }

    private void Update()
    {
        if (_target == null || _isDead) return;

        Vector3 currentPosition = _cachedTransform.position;
        Vector3 direction = _target.position - currentPosition;
        direction.y = 0f;

        float squaredDistance = direction.sqrMagnitude;
        if (squaredDistance <= _stoppingDistanceSquared) return;

        Vector3 moveVelocity = direction.normalized * _moveSpeed;
        _cachedTransform.position += moveVelocity * Time.deltaTime;

        if (_spriteRenderer != null && moveVelocity.sqrMagnitude > 0.001f)
        {
            _spriteRenderer.flipX = moveVelocity.x < 0f;
        }
    }

    public void TakeDamage(float damage, Vector3 hitDirection, float knockback = 0f)
    {
        TakeDamageInternal(damage, hitDirection, knockback, 1);
    }

    public void TakeWeaponDamage(float damage, Vector3 hitDirection, float knockback, int weaponLevel)
    {
        TakeDamageInternal(damage, hitDirection, knockback, weaponLevel);
    }

    private void TakeDamageInternal(float damage, Vector3 hitDirection, float knockback, int weaponLevel)
    {
        if (_isDead) return;

        Vector3 hitPosition = _cachedTransform.position;

        if (DamageTextManager.Instance != null)
        {
            DamageTextManager.Instance.ShowMonsterDamage(damage, hitPosition);
        }

        CurrentHealth -= damage;

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
            if (!_isDead)
            {
                _spriteRenderer.color = _originalColor;
            }
        }
    }

    private IEnumerator HomeRunRoutine(Vector3 hitDirection)
    {
        Time.timeScale = 0.05f;
        Camera mainCam = Camera.main;
        Vector3 origCamPos = mainCam != null ? mainCam.transform.position : Vector3.zero;

        if (mainCam != null)
        {
            mainCam.transform.position += hitDirection.normalized * 0.5f;
        }

        yield return new WaitForSecondsRealtime(0.05f); 

        Time.timeScale = 1f;
        if (mainCam != null) mainCam.transform.position = origCamPos;

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        float elapsed = 0f;
        float duration = 1.0f; 

        Vector3 startPos = _cachedTransform.position;
        Vector3 flyDirection = hitDirection.normalized;

        EnsureGhostPrefab();
        _currentShadowObj = PoolingManager.Instance.Get(_ghostPrefab, startPos, _originalSpriteRot);
        if (_currentShadowObj.TryGetComponent<GhostFader>(out var sFader)) sFader.enabled = false;

        SpriteRenderer shadowSr = _currentShadowObj.GetComponent<SpriteRenderer>();
        shadowSr.sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
        shadowSr.color = new Color(0f, 0f, 0f, 0.4f);
        _currentShadowObj.transform.localScale = _originalSpriteScale;

        float ghostTimer = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float easeOutT = 1f - Mathf.Pow(1f - t, 3f);
            Vector3 currentGroundPos = startPos + flyDirection * (_homeRunSpeed * easeOutT);

            if (_currentShadowObj != null)
            {
                _currentShadowObj.transform.position = currentGroundPos;
                _currentShadowObj.transform.localScale = _originalSpriteScale * (1f - (t * 0.8f));
            }

            float height = Mathf.Sin(t * Mathf.PI) * _homeRunHeight;
            _cachedTransform.position = currentGroundPos + new Vector3(0, height, 0);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.transform.Rotate(0f, 0f, _homeRunSpinSpeed * Time.deltaTime, Space.Self);
                float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f;
                scaleMultiplier -= t * 0.8f;
                _spriteRenderer.transform.localScale = _originalSpriteScale * Mathf.Max(0f, scaleMultiplier);
            }

            ghostTimer += Time.deltaTime;
            if (ghostTimer >= 0.04f)
            {
                ghostTimer = 0f;
                SpawnGhostTrail();
            }

            yield return null;
        }

        if (_currentShadowObj != null && PoolingManager.Instance != null)
        {
            PoolingManager.Instance.Release(_currentShadowObj);
            _currentShadowObj = null;
        }

        ReleaseToPool();
    }

    private void SpawnGhostTrail()
    {
        if (_spriteRenderer == null || _spriteRenderer.sprite == null || PoolingManager.Instance == null) return;

        EnsureGhostPrefab();
        GameObject ghost = PoolingManager.Instance.Get(_ghostPrefab, _spriteRenderer.transform.position, _spriteRenderer.transform.rotation);
        ghost.transform.localScale = _spriteRenderer.transform.localScale;

        SpriteRenderer ghostSr = ghost.GetComponent<SpriteRenderer>();
        ghostSr.sprite = _spriteRenderer.sprite;
        ghostSr.color = new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.5f);

        if (ghost.TryGetComponent<GhostFader>(out var fader))
        {
            fader.enabled = true;
            fader.Setup(ghostSr, 0.3f);
        }

        _activeGhosts.Add(ghost);
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
            _spriteRenderer.transform.localScale = _originalSpriteScale; 
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
            if (PoolingManager.Instance != null)
                PoolingManager.Instance.Release(gameObject);
            else
                Destroy(gameObject);
        }
    }
}