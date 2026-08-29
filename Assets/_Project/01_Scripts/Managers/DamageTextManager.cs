using ProjectOverdrive.UI;
using UnityEngine;

public sealed class DamageTextManager : MonoBehaviour
{
    public static DamageTextManager Instance { get; private set; }

    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.15f, 0f);
    [SerializeField] private Color monsterDamageColor = new Color(1f, 0.85f, 0.1f, 1f);
    [SerializeField] private Color playerDamageColor = new Color(1f, 0.2f, 0.2f, 1f);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void ShowMonsterDamage(float damage, Vector3 position)
    {
        Show(damage, position, monsterDamageColor);
    }

    public void ShowPlayerDamage(float damage, Vector3 position)
    {
        Show(damage, position, playerDamageColor);
    }

    private void Show(float damage, Vector3 position, Color color)
    {
        PoolingManager poolingManager = PoolingManager.Instance;
        if (damage <= 0f || damageTextPrefab == null || poolingManager == null)
        {
            return;
        }

        GameObject instance = poolingManager.Get(
            damageTextPrefab,
            position + worldOffset,
            Quaternion.identity);

        if (instance.TryGetComponent(out DamageText damageText))
        {
            damageText.Show(damage, color);
        }
        else
        {
            poolingManager.Release(instance);
        }
    }
}
