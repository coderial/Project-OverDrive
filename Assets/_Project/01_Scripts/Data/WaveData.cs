using System;
using UnityEngine;

namespace ProjectOverdrive.Data
{
    public enum SpawnFormation
    {
        Single = 0,
        Cluster = 1,
        Ring = 2,
    }

    public enum SpawnOrigin
    {
        AnywhereOnMap = 0,
        AroundPlayer = 1,
    }

    [Serializable]
    public sealed class WaveMonsterEntry
    {
        [SerializeField] private MonsterData monster;
        [SerializeField, Min(0f)] private float spawnWeight = 1f;

        [Header("Spawn Rule")]
        [SerializeField] private WaveSpawnSettings spawnSettings = new WaveSpawnSettings();

        public MonsterData Monster => monster;
        public float SpawnWeight => Mathf.Max(0f, spawnWeight);
        public WaveSpawnSettings SpawnSettings => spawnSettings;

        internal void Validate()
        {
            spawnWeight = Mathf.Max(0f, spawnWeight);
            spawnSettings ??= new WaveSpawnSettings();
            spawnSettings.Validate();
        }
    }

    [Serializable]
    public sealed class WaveSpawnSettings
    {
        [Header("Placement")]
        [SerializeField] private SpawnOrigin origin = SpawnOrigin.AnywhereOnMap;
        [SerializeField] private SpawnFormation formation = SpawnFormation.Single;

        [Tooltip("Single은 항상 1마리, 나머지 대형은 한 번에 이 수만큼 배치합니다.")]
        [SerializeField, Min(1)] private int countPerBatch = 1;

        [Tooltip("Cluster 반경 또는 맵 임의 지점 중심 Ring 반경입니다.")]
        [SerializeField, Min(0f)] private float formationRadius = 2f;

        [Header("Player Safety")]
        [Tooltip("어떤 스폰 방식에서도 지켜야 하는 플레이어와의 최소 거리입니다.")]
        [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 6f;

        [Tooltip("AroundPlayer에서 후보 지점을 찾을 최대 거리입니다.")]
        [SerializeField, Min(0f)] private float maximumDistanceFromPlayer = 12f;

        [Tooltip("Ring이 플레이어를 중심으로 생성될 때 비워 둘 탈출 방향의 각도입니다.")]
        [SerializeField, Range(0f, 180f)] private float escapeArcDegrees = 90f;

        [Tooltip("같은 배치에서 몬스터끼리 확보할 최소 간격입니다.")]
        [SerializeField, Min(0f)] private float minimumMonsterSpacing = 0.75f;

        public SpawnOrigin Origin => origin;
        public SpawnFormation Formation => formation;
        public int CountPerBatch => formation == SpawnFormation.Single ? 1 : Mathf.Max(1, countPerBatch);
        public float FormationRadius => Mathf.Max(0f, formationRadius);
        public float MinimumDistanceFromPlayer => Mathf.Max(0f, minimumDistanceFromPlayer);
        public float MaximumDistanceFromPlayer => Mathf.Max(MinimumDistanceFromPlayer, maximumDistanceFromPlayer);
        public float EscapeArcDegrees => Mathf.Clamp(escapeArcDegrees, 0f, 180f);
        public float MinimumMonsterSpacing => Mathf.Max(0f, minimumMonsterSpacing);

        internal void Validate()
        {
            countPerBatch = Mathf.Max(1, countPerBatch);
            formationRadius = Mathf.Max(0f, formationRadius);
            minimumDistanceFromPlayer = Mathf.Max(0f, minimumDistanceFromPlayer);
            maximumDistanceFromPlayer = Mathf.Max(minimumDistanceFromPlayer, maximumDistanceFromPlayer);
            escapeArcDegrees = Mathf.Clamp(escapeArcDegrees, 0f, 180f);
            minimumMonsterSpacing = Mathf.Max(0f, minimumMonsterSpacing);
        }
    }

    [CreateAssetMenu(fileName = "WaveData", menuName = "Project Overdrive/Wave Data")]
    public sealed class WaveData : ScriptableObject
    {
        [Header("Wave")]
        [SerializeField, Min(0.1f)] private float duration = 30f;
        [SerializeField, Min(0.01f)] private float spawnInterval = 0.5f;

        [Header("Monster Pool")]
        [Tooltip("각 몬스터 항목이 가중치와 고유한 Spawn Rule을 가집니다.")]
        [SerializeField] private WaveMonsterEntry[] monsters = Array.Empty<WaveMonsterEntry>();

        public float Duration => Mathf.Max(0.1f, duration);
        public float SpawnInterval => Mathf.Max(0.01f, spawnInterval);
        public int MonsterEntryCount => monsters?.Length ?? 0;

        public WaveMonsterEntry GetMonsterEntry(int index)
        {
            return monsters != null && index >= 0 && index < monsters.Length ? monsters[index] : null;
        }

        public bool TryGetRandomMonsterEntry(out WaveMonsterEntry selectedEntry)
        {
            selectedEntry = null;
            if (monsters == null || monsters.Length == 0) return false;

            float totalWeight = 0f;
            for (int i = 0; i < monsters.Length; i++)
            {
                WaveMonsterEntry entry = monsters[i];
                if (entry?.Monster != null && entry.Monster.Prefab != null) totalWeight += entry.SpawnWeight;
            }

            if (totalWeight <= 0f) return false;

            float selection = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < monsters.Length; i++)
            {
                WaveMonsterEntry entry = monsters[i];
                if (entry?.Monster == null || entry.Monster.Prefab == null || entry.SpawnWeight <= 0f) continue;

                selection -= entry.SpawnWeight;
                if (selection <= 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            for (int i = monsters.Length - 1; i >= 0; i--)
            {
                WaveMonsterEntry entry = monsters[i];
                if (entry?.Monster != null && entry.Monster.Prefab != null && entry.SpawnWeight > 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            return false;
        }

        public bool IsValid(out string reason)
        {
            if (monsters == null || monsters.Length == 0)
            {
                reason = "몬스터 목록이 비어 있습니다.";
                return false;
            }

            bool hasSelectableMonster = false;
            for (int i = 0; i < monsters.Length; i++)
            {
                WaveMonsterEntry entry = monsters[i];
                if (entry == null || entry.SpawnWeight <= 0f) continue;

                if (entry.Monster == null)
                {
                    reason = $"몬스터 항목 {i + 1}의 MonsterData가 없습니다.";
                    return false;
                }

                if (entry.Monster.Prefab == null)
                {
                    reason = $"{entry.Monster.name}의 프리팹이 없습니다.";
                    return false;
                }

                if (entry.SpawnSettings == null)
                {
                    reason = $"{entry.Monster.name}의 Spawn Settings가 없습니다.";
                    return false;
                }

                hasSelectableMonster = true;
            }

            reason = hasSelectableMonster ? null : "양수 가중치를 가진 몬스터가 없습니다.";
            return hasSelectableMonster;
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.1f, duration);
            spawnInterval = Mathf.Max(0.01f, spawnInterval);

            if (monsters == null) monsters = Array.Empty<WaveMonsterEntry>();
            for (int i = 0; i < monsters.Length; i++) monsters[i]?.Validate();
        }
    }
}
