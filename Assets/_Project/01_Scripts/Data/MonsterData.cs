using UnityEngine;
using UnityEngine.Serialization;

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

    public string MonsterName => monsterName;
    public GameObject Prefab => prefab;
    public GameObject CurrencyPrefab => currencyPrefab;
    public float AttackPower => attackPower;
    public float ContactDamageInterval => contactDamageInterval;
    public float MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public float StoppingDistance => stoppingDistance;

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        attackPower = Mathf.Max(0f, attackPower);
        contactDamageInterval = Mathf.Max(0.05f, contactDamageInterval);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        stoppingDistance = Mathf.Max(0f, stoppingDistance);
    }
}
