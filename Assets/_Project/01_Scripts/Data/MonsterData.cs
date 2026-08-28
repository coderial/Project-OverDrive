using System;
using UnityEngine;
using UnityEngine.Serialization;

public enum MonsterSpawnPattern
{
    Single = 0,
    Circle_Around_Player = 1,
    Cluster = 2,
}

[Serializable]
public sealed class MonsterSpawnPatternData
{
    [SerializeField] private MonsterSpawnPattern pattern = MonsterSpawnPattern.Single;
    [SerializeField, Min(1)] private int count = 6;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 6f;
    [SerializeField, Min(0f)] private float maximumDistanceFromPlayer = 8f;
    [SerializeField, Min(0f)] private float clusterRadius = 1.5f;

    public MonsterSpawnPattern Pattern => pattern;
    public int Count => pattern == MonsterSpawnPattern.Single ? 1 : Mathf.Max(1, count);
    public float MinimumDistanceFromPlayer => minimumDistanceFromPlayer;
    public float MaximumDistanceFromPlayer => maximumDistanceFromPlayer;
    public float ClusterRadius => clusterRadius;

    internal void Validate()
    {
        count = Mathf.Max(1, count);
        minimumDistanceFromPlayer = Mathf.Max(0f, minimumDistanceFromPlayer);
        maximumDistanceFromPlayer = Mathf.Max(minimumDistanceFromPlayer, maximumDistanceFromPlayer);
        clusterRadius = Mathf.Max(0f, clusterRadius);
    }
}

[CreateAssetMenu(fileName = "MonsterData", menuName = "Project Overdrive/Monster Data")]
public sealed class MonsterData : ScriptableObject
{
    [Header("Identity")]
    [FormerlySerializedAs("Name")]
    [SerializeField] private string monsterName = "Monster";

    [FormerlySerializedAs("Prefab")]
    [SerializeField] private GameObject prefab;

    [Header("Drop")]
    [SerializeField] private GameObject currencyPrefab;

    [Header("Stats")]
    [FormerlySerializedAs("AttackPoint")]
    [SerializeField, Min(0f)] private float attackPower = 10f;

    [SerializeField, Min(0.05f)] private float contactDamageInterval = 0.5f;

    [FormerlySerializedAs("MaxHP")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;

    [FormerlySerializedAs("MoveSpeed")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;

    [SerializeField, Min(0f)] private float stoppingDistance = 0.1f;

    [Header("Spawn")]
    [SerializeField] private MonsterSpawnPatternData spawnPattern = new MonsterSpawnPatternData();

    public string MonsterName => monsterName;
    public GameObject Prefab => prefab;
    public GameObject CurrencyPrefab => currencyPrefab;
    public float AttackPower => attackPower;
    public float ContactDamageInterval => contactDamageInterval;
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float StoppingDistance => stoppingDistance;
    public MonsterSpawnPatternData SpawnPattern => spawnPattern;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        attackPower = Mathf.Max(0f, attackPower);
        contactDamageInterval = Mathf.Max(0.05f, contactDamageInterval);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);

        if (spawnPattern == null)
        {
            spawnPattern = new MonsterSpawnPatternData();
        }

        spawnPattern.Validate();
    }
}
