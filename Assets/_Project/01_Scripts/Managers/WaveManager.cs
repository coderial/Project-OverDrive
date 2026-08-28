using UnityEngine;

public enum WaveStatus
{
    Preparation = 1,
    Start_Interval = 2,
    In_Progress = 3
}

public sealed class WaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private MonsterData monsterData;

    [Header("Test Wave")]
    [SerializeField, Min(0.1f)] private float waveDuration = 10f;
    [SerializeField, Min(0.01f)] private float spawnInterval = 0.25f;

    private PoolingManager _poolingManager;
    private float _waveEndTime;
    private float _nextSpawnTime;

    public bool IsWaveRunning { get; private set; }
    public float RemainingTime => IsWaveRunning
        ? Mathf.Max(0f, _waveEndTime - Time.time)
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

        if (player == null || monsterData == null || monsterData.Prefab == null ||
            _poolingManager == null)
        {
            Debug.LogError(
                "WaveManager requires a Player, MonsterData with a Prefab, and PoolingManager.",
                this);
            enabled = false;
            return;
        }

        MonsterController.SharedTarget = player;
        BeginWave();
    }

    private void Update()
    {
        if (!IsWaveRunning)
        {
            return;
        }

        float currentTime = Time.time;
        if (currentTime >= _waveEndTime)
        {
            IsWaveRunning = false;
            return;
        }

        if (currentTime < _nextSpawnTime)
        {
            return;
        }

        SpawnBatch();

        // Limits catch-up to one batch per frame after an unusually long frame.
        _nextSpawnTime = Mathf.Max(_nextSpawnTime + spawnInterval, currentTime + 0.001f);
    }

    public void BeginWave()
    {
        float currentTime = Time.time;
        _waveEndTime = currentTime + waveDuration;
        _nextSpawnTime = currentTime;
        IsWaveRunning = true;
    }

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
        float radius = Random.Range(
            patternData.MinimumDistanceFromPlayer,
            patternData.MaximumDistanceFromPlayer);

        SpawnMonsterAt(GetPointOnCircle(playerPosition, angle, radius));
    }

    private void SpawnCircle(Vector3 playerPosition, MonsterSpawnPatternData patternData)
    {
        int count = patternData.Count;
        float radius = Random.Range(
            patternData.MinimumDistanceFromPlayer,
            patternData.MaximumDistanceFromPlayer);
        float angleStep = Mathf.PI * 2f / count;
        float startAngle = Random.value * Mathf.PI * 2f;

        for (int i = 0; i < count; i++)
        {
            float angle = startAngle + angleStep * i;
            SpawnMonsterAt(GetPointOnCircle(playerPosition, angle, radius));
        }
    }

    private void SpawnCluster(Vector3 playerPosition, MonsterSpawnPatternData patternData)
    {
        float centerAngle = Random.value * Mathf.PI * 2f;
        float centerRadius = Random.Range(
            patternData.MinimumDistanceFromPlayer,
            patternData.MaximumDistanceFromPlayer);
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
            Debug.LogError($"{prefab.name} requires a MonsterController.", prefab);
            _poolingManager.Release(instance);
        }
    }

    private static Vector3 GetPointOnCircle(Vector3 center, float angle, float radius)
    {
        center.x += Mathf.Cos(angle) * radius;
        center.z += Mathf.Sin(angle) * radius;
        return center;
    }
}
