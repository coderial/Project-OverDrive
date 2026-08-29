using ProjectOverdrive.Controllers;
using ProjectOverdrive.UI;
using UnityEngine;

public enum WaveStatus
{
    Preparation = 1,
    Spawn_Progress = 2,
    Open_Shop = 3,
}

public sealed class WaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private MonsterData monsterData;

    [Header("Shop UI Reference")]
    [Tooltip("씬의 UI_Shop 패널을 여기에 연결해주세요")]
    [SerializeField] private UI_Shop _shopUI;

    [Header("Test Wave Settings")]
    [SerializeField, Min(0f)] private float waveStartDelay = 1f;
    [SerializeField, Min(0.1f)] private float waveDuration = 10f;
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.25f;

    private PoolingManager _poolingManager;
    private WaveStatus _currentStatus = WaveStatus.Preparation;
    private float _statusEndTime;
    private float _nextSpawnTime;

    public WaveStatus CurrentStatus => _currentStatus;
    public int CurrentWave { get; private set; }
    public bool IsWaveRunning { get; private set; }
    public float RemainingTime => _currentStatus == WaveStatus.Spawn_Progress
        ? Mathf.Max(0f, _statusEndTime - Time.time)
        : 0f;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
            }
        }

        _poolingManager = PoolingManager.Instance;

        if (player == null || monsterData == null || monsterData.Prefab == null || _poolingManager == null)
        {
            Debug.LogError("WaveManager: Player, MonsterData, PoolingManager가 필요합니다.", this);
            enabled = false;
            return;
        }

        MonsterController.SharedTarget = player;
        BeginWave();
    }

    private void Update()
    {
        float currentTime = Time.time;

        switch (_currentStatus)
        {
            case WaveStatus.Preparation:
                if (currentTime >= _statusEndTime)
                {
                    StartSpawnProgress(currentTime);
                }
                break;

            case WaveStatus.Spawn_Progress:
                if (currentTime >= _statusEndTime)
                {
                    OpenShop(currentTime);
                    break;
                }

                if (currentTime >= _nextSpawnTime)
                {
                    SpawnBatch();
                    _nextSpawnTime = Mathf.Max(_nextSpawnTime + spawnInterval, currentTime + 0.001f);
                }
                break;

            case WaveStatus.Open_Shop:
                // 상점 상태에서는 자동 종료 타이머를 돌리지 않고 플레이어의 '다음 웨이브' 버튼 입력을 대기합니다.
                break;
        }
    }

    public void BeginWave()
    {
        float currentTime = Time.time;
        CurrentWave++;
        _currentStatus = WaveStatus.Preparation;
        _statusEndTime = currentTime + waveStartDelay;
        IsWaveRunning = true;

        Debug.Log($"[WaveManager] Wave {CurrentWave} 시작 - {waveStartDelay:0.##}초 대기 후 몬스터 스폰.", this);
    }

    /// <summary>
    /// UI_Shop의 '다음 웨이브 시작' 버튼에서 호출
    /// </summary>
    public void CloseShop()
    {
        if (_currentStatus != WaveStatus.Open_Shop) return;

        Debug.Log($"[WaveManager] 상점 종료 - Wave {CurrentWave + 1}을 시작합니다.", this);
        BeginWave();
    }

    private void StartSpawnProgress(float currentTime)
    {
        _currentStatus = WaveStatus.Spawn_Progress;
        _statusEndTime = currentTime + waveDuration;
        _nextSpawnTime = currentTime;

        Debug.Log($"[WaveManager] Wave {CurrentWave} 몬스터 스폰 시작 (지속 시간: {waveDuration}초).", this);
    }

    private void OpenShop(float currentTime)
    {
        _currentStatus = WaveStatus.Open_Shop;
        IsWaveRunning = false;

        Debug.Log($"[WaveManager] Wave {CurrentWave} 클리어! 상점을 오픈합니다.", this);

        if (player != null && player.TryGetComponent<PlayerController>(out var playerController))
        {
            if (_shopUI != null)
            {
                _shopUI.OpenShop(playerController, this);
            }
            else
            {
                Debug.LogWarning("[WaveManager] _shopUI가 연결되지 않았습니다. 인스펙터에서 UI_Shop을 연결해주세요.");
            }
        }
    }

    #region Monster Spawning Logic

    private void SpawnBatch()
    {
        MonsterSpawnPatternData patternData = monsterData.SpawnPattern;
        Vector3 playerPosition = player.position;

        switch (patternData.Pattern)
        {
            case MonsterSpawnPattern.Single:
                SpawnSingle(playerPosition, patternData);
                break;
            case MonsterSpawnPattern.Circle_Around_Player:
                SpawnCircle(playerPosition, patternData);
                break;
            case MonsterSpawnPattern.Cluster:
                SpawnCluster(playerPosition, patternData);
                break;
            default:
                SpawnSingle(playerPosition, patternData);
                break;
        }
    }

    private void SpawnSingle(Vector3 playerPosition, MonsterSpawnPatternData patternData)
    {
        float angle = Random.value * Mathf.PI * 2f;
        float radius = Random.Range(patternData.MinimumDistanceFromPlayer, patternData.MaximumDistanceFromPlayer);
        SpawnMonsterAt(GetPointOnCircle(playerPosition, angle, radius));
    }

    private void SpawnCircle(Vector3 playerPosition, MonsterSpawnPatternData patternData)
    {
        int count = patternData.Count;
        float radius = Random.Range(patternData.MinimumDistanceFromPlayer, patternData.MaximumDistanceFromPlayer);
        float angleStep = 360f / count;

        for (int i = 0; i < count; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            SpawnMonsterAt(GetPointOnCircle(playerPosition, angle, radius));
        }
    }

    private void SpawnCluster(Vector3 playerPosition, MonsterSpawnPatternData patternData)
    {
        float centerAngle = Random.value * Mathf.PI * 2f;
        float centerRadius = Random.Range(patternData.MinimumDistanceFromPlayer, patternData.MaximumDistanceFromPlayer);
        Vector3 clusterCenter = GetPointOnCircle(playerPosition, centerAngle, centerRadius);

        for (int i = 0; i < patternData.Count; i++)
        {
            float localAngle = Random.value * Mathf.PI * 2f;
            float localRadius = Mathf.Sqrt(Random.value) * patternData.ClusterRadius;
            SpawnMonsterAt(GetPointOnCircle(clusterCenter, localAngle, localRadius));
        }
    }

    private void SpawnMonsterAt(Vector3 position)
    {
        GameObject prefab = monsterData.Prefab;
        GameObject instance = _poolingManager.Get(prefab, position, prefab.transform.rotation);

        if (instance.TryGetComponent(out MonsterController monster))
        {
            monster.Configure(monsterData, player);
        }
        else
        {
            Debug.LogError($"{prefab.name}에 MonsterController가 없습니다.", prefab);
            _poolingManager.Release(instance);
        }
    }

    private static Vector3 GetPointOnCircle(Vector3 center, float angle, float radius)
    {
        center.x += Mathf.Cos(angle) * radius;
        center.z += Mathf.Sin(angle) * radius;
        return center;
    }

    #endregion
}