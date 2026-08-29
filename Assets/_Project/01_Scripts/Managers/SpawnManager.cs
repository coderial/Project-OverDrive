using System.Collections;
using System.Collections.Generic;
using ProjectOverdrive.Data;
using UnityEngine;

namespace ProjectOverdrive.Managers
{
    [DisallowMultipleComponent]
    public sealed class SpawnManager : MonoBehaviour
    {
        private const float FullCircle = 360f;
        private const int SpawnSignalBlinkCount = 2;

        [Header("References")]
        [SerializeField] private Transform player;

        [Tooltip("맵에서 스폰 가능한 영역을 나타내는 Trigger BoxCollider들입니다.")]
        [SerializeField] private BoxCollider[] spawnAreas = new BoxCollider[0];

        [Header("Spawn Signal")]
        [SerializeField] private GameObject spawnSignalPrefab;
        [SerializeField, Min(0.01f)] private float signalVisibleDuration = 0.2f;
        [SerializeField, Min(0.01f)] private float signalHiddenDuration = 0.15f;

        [Header("Placement Validation")]
        [Tooltip("벽, 장애물 등 스폰을 막을 레이어입니다. Ground와 SpawnArea Trigger는 제외하세요.")]
        [SerializeField] private LayerMask blockingLayers;

        [Tooltip("0이 아니면 위에서 아래로 Raycast하여 지면 높이에 맞춥니다.")]
        [SerializeField] private LayerMask groundLayers;

        [SerializeField, Min(0f)] private float obstacleClearanceRadius = 0.4f;
        [SerializeField, Min(0f)] private float spawnHeightOffset;
        [SerializeField, Min(1)] private int maxPlacementAttempts = 24;
        [SerializeField, Min(0f)] private float groundProbeHeight = 10f;
        [SerializeField, Min(0f)] private float groundProbeDistance = 30f;

        [Header("Player Reaction Safety")]
        [Tooltip("선택된 몬스터가 이 시간보다 빨리 플레이어에게 닿는 거리에는 생성되지 않습니다.")]
        [SerializeField, Min(0f)] private float minimumReactionTime = 1.5f;

        private readonly List<Vector3> _positions = new List<Vector3>(32);
        private readonly List<GameObject> _activeSignals = new List<GameObject>(64);
        private PoolingManager _poolingManager;

        public void Initialize(Transform targetPlayer)
        {
            if (targetPlayer != null) player = targetPlayer;
            _poolingManager = PoolingManager.Instance;
            MonsterController.SharedTarget = player;
        }

        public bool CanSpawn(WaveData waveData, out string reason)
        {
            if (player == null)
            {
                reason = "Player가 연결되지 않았습니다.";
                return false;
            }

            if (_poolingManager == null) _poolingManager = PoolingManager.Instance;
            if (_poolingManager == null)
            {
                reason = "PoolingManager가 없습니다.";
                return false;
            }

            if (spawnSignalPrefab == null)
            {
                reason = "SpawnSignal 프리팹이 연결되지 않았습니다.";
                return false;
            }

            if (waveData == null)
            {
                reason = "WaveData가 없습니다.";
                return false;
            }

            if (!waveData.IsValid(out reason)) return false;

            for (int i = 0; i < waveData.MonsterEntryCount; i++)
            {
                WaveMonsterEntry entry = waveData.GetMonsterEntry(i);
                if (entry == null || entry.SpawnWeight <= 0f) continue;

                WaveSpawnSettings settings = entry.SpawnSettings;
                if (settings.Origin == SpawnOrigin.AnywhereOnMap && !HasUsableSpawnArea())
                {
                    reason = $"{entry.Monster.name}: AnywhereOnMap 스폰에는 활성화된 Spawn Area가 필요합니다.";
                    return false;
                }

                float requiredSafetyDistance = GetRequiredSafetyDistance(entry);
                if (settings.Origin == SpawnOrigin.AroundPlayer &&
                    settings.MaximumDistanceFromPlayer < requiredSafetyDistance)
                {
                    reason =
                        $"{entry.Monster.name}: 최대 스폰 거리({settings.MaximumDistanceFromPlayer:0.##})가 " +
                        $"안전 거리({requiredSafetyDistance:0.##})보다 작습니다.";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public int SpawnBatch(WaveData waveData)
        {
            if (!CanSpawn(waveData, out string reason))
            {
                Debug.LogWarning($"[SpawnManager] 스폰을 건너뜁니다: {reason}", this);
                return 0;
            }

            if (!waveData.TryGetRandomMonsterEntry(out WaveMonsterEntry selectedEntry))
            {
                Debug.LogWarning("[SpawnManager] 선택 가능한 몬스터 항목이 없습니다.", waveData);
                return 0;
            }

            _positions.Clear();
            WaveSpawnSettings settings = selectedEntry.SpawnSettings;
            float safetyDistance = GetRequiredSafetyDistance(selectedEntry);

            switch (settings.Formation)
            {
                case SpawnFormation.Cluster:
                    BuildCluster(settings, safetyDistance);
                    break;
                case SpawnFormation.Ring:
                    BuildRing(settings, safetyDistance);
                    break;
                default:
                    BuildSingle(settings, safetyDistance);
                    break;
            }

            if (_positions.Count == 0) return 0;

            Vector3[] reservedPositions = _positions.ToArray();
            StartCoroutine(SpawnAfterSignal(selectedEntry.Monster, reservedPositions));
            return reservedPositions.Length;
        }

        public void CancelPendingSpawns()
        {
            StopAllCoroutines();

            for (int i = _activeSignals.Count - 1; i >= 0; i--)
            {
                GameObject signal = _activeSignals[i];
                if (signal != null && _poolingManager != null) _poolingManager.Release(signal);
            }

            _activeSignals.Clear();
        }

        public int DespawnAllMonsters()
        {
            MonsterController[] monsters = FindObjectsByType<MonsterController>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            int despawnedCount = 0;
            for (int i = 0; i < monsters.Length; i++)
            {
                MonsterController monster = monsters[i];
                if (monster == null || !monster.gameObject.activeInHierarchy) continue;

                monster.ForceDespawn();
                despawnedCount++;
            }

            return despawnedCount;
        }

        private IEnumerator SpawnAfterSignal(MonsterData monsterData, Vector3[] positions)
        {
            GameObject[] signals = new GameObject[positions.Length];
            for (int i = 0; i < positions.Length; i++)
            {
                GameObject signal = _poolingManager.Get(
                    spawnSignalPrefab,
                    positions[i],
                    spawnSignalPrefab.transform.rotation);

                signals[i] = signal;
                if (signal != null) _activeSignals.Add(signal);
            }

            var visibleWait = new WaitForSeconds(signalVisibleDuration);
            var hiddenWait = new WaitForSeconds(signalHiddenDuration);

            for (int blink = 0; blink < SpawnSignalBlinkCount; blink++)
            {
                SetSignalsVisible(signals, true);
                yield return visibleWait;
                SetSignalsVisible(signals, false);
                yield return hiddenWait;
            }

            ReleaseSignals(signals);

            for (int i = 0; i < positions.Length; i++)
            {
                SpawnMonster(monsterData, positions[i]);
            }
        }

        private static void SetSignalsVisible(GameObject[] signals, bool isVisible)
        {
            for (int i = 0; i < signals.Length; i++)
            {
                GameObject signal = signals[i];
                if (signal == null) continue;

                SpriteRenderer[] renderers = signal.GetComponentsInChildren<SpriteRenderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    renderers[rendererIndex].enabled = isVisible;
                }
            }
        }

        private void ReleaseSignals(GameObject[] signals)
        {
            for (int i = 0; i < signals.Length; i++)
            {
                GameObject signal = signals[i];
                if (signal == null) continue;

                _activeSignals.Remove(signal);
                _poolingManager.Release(signal);
                signals[i] = null;
            }
        }

        private void BuildSingle(WaveSpawnSettings settings, float safetyDistance)
        {
            if (TryFindIndependentPosition(settings, safetyDistance, out Vector3 position))
            {
                _positions.Add(position);
            }
        }

        private void BuildCluster(WaveSpawnSettings settings, float safetyDistance)
        {
            if (!TryFindIndependentPosition(settings, safetyDistance, out Vector3 center)) return;

            _positions.Add(center);
            for (int i = 1; i < settings.CountPerBatch; i++)
            {
                for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
                {
                    float angle = Random.value * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(Random.value) * settings.FormationRadius;
                    Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                    if (TryFinalizeCandidate(candidate, safetyDistance, out candidate) &&
                        HasBatchSpacing(candidate, settings.MinimumMonsterSpacing))
                    {
                        _positions.Add(candidate);
                        break;
                    }
                }
            }
        }

        private void BuildRing(WaveSpawnSettings settings, float safetyDistance)
        {
            Vector3 center;
            float radius;

            if (settings.Origin == SpawnOrigin.AroundPlayer)
            {
                center = player.position;
                float minimumRadius = Mathf.Max(settings.MinimumDistanceFromPlayer, safetyDistance);
                radius = Random.Range(minimumRadius, settings.MaximumDistanceFromPlayer);
            }
            else
            {
                if (!TryFindMapPosition(out center)) return;
                radius = settings.FormationRadius;
            }

            int count = settings.CountPerBatch;
            float gap = settings.Origin == SpawnOrigin.AroundPlayer ? settings.EscapeArcDegrees : 0f;
            float usableArc = FullCircle - gap;
            float gapCenter = Random.Range(0f, FullCircle);
            float startAngle = gapCenter + gap * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float angleDegrees = startAngle + usableArc * ((i + 0.5f) / count);
                float angle = angleDegrees * Mathf.Deg2Rad;
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                if (TryFinalizeCandidate(candidate, safetyDistance, out candidate) &&
                    HasBatchSpacing(candidate, settings.MinimumMonsterSpacing))
                {
                    _positions.Add(candidate);
                }
            }
        }

        private bool TryFindIndependentPosition(
            WaveSpawnSettings settings,
            float safetyDistance,
            out Vector3 position)
        {
            for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
            {
                Vector3 candidate;
                if (settings.Origin == SpawnOrigin.AnywhereOnMap)
                {
                    if (!TryFindMapPosition(out candidate)) break;
                }
                else
                {
                    float minimumRadius = Mathf.Max(settings.MinimumDistanceFromPlayer, safetyDistance);
                    float angle = Random.value * Mathf.PI * 2f;
                    float radius = Mathf.Sqrt(Random.Range(
                        minimumRadius * minimumRadius,
                        settings.MaximumDistanceFromPlayer * settings.MaximumDistanceFromPlayer));
                    candidate = player.position + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                }

                if (TryFinalizeCandidate(candidate, safetyDistance, out position) &&
                    HasBatchSpacing(position, settings.MinimumMonsterSpacing))
                {
                    return true;
                }
            }

            position = default;
            return false;
        }

        private bool TryFindMapPosition(out Vector3 position)
        {
            float totalArea = 0f;
            for (int i = 0; i < spawnAreas.Length; i++) totalArea += GetAreaWeight(spawnAreas[i]);

            if (totalArea <= 0f)
            {
                position = default;
                return false;
            }

            float selection = Random.value * totalArea;
            BoxCollider selectedArea = null;
            for (int i = 0; i < spawnAreas.Length; i++)
            {
                BoxCollider area = spawnAreas[i];
                selection -= GetAreaWeight(area);
                if (selection <= 0f && GetAreaWeight(area) > 0f)
                {
                    selectedArea = area;
                    break;
                }
            }

            if (selectedArea == null)
            {
                for (int i = spawnAreas.Length - 1; i >= 0; i--)
                {
                    if (GetAreaWeight(spawnAreas[i]) > 0f)
                    {
                        selectedArea = spawnAreas[i];
                        break;
                    }
                }
            }

            if (selectedArea == null)
            {
                position = default;
                return false;
            }

            Vector3 halfSize = selectedArea.size * 0.5f;
            Vector3 localPoint = selectedArea.center + new Vector3(
                Random.Range(-halfSize.x, halfSize.x),
                0f,
                Random.Range(-halfSize.z, halfSize.z));
            position = selectedArea.transform.TransformPoint(localPoint);
            return true;
        }

        private bool TryFinalizeCandidate(Vector3 candidate, float safetyDistance, out Vector3 position)
        {
            position = candidate;

            if (HasUsableSpawnArea() && !IsInsideSpawnArea(position)) return false;

            Vector3 playerOffset = position - player.position;
            playerOffset.y = 0f;
            if (playerOffset.sqrMagnitude < safetyDistance * safetyDistance) return false;

            if (groundLayers.value != 0)
            {
                Vector3 rayOrigin = position + Vector3.up * groundProbeHeight;
                float rayDistance = groundProbeHeight + groundProbeDistance;
                if (!Physics.Raycast(
                        rayOrigin,
                        Vector3.down,
                        out RaycastHit hit,
                        rayDistance,
                        groundLayers,
                        QueryTriggerInteraction.Ignore))
                {
                    return false;
                }

                position = hit.point;
            }

            position.y += spawnHeightOffset;

            return blockingLayers.value == 0 ||
                   !Physics.CheckSphere(
                       position,
                       obstacleClearanceRadius,
                       blockingLayers,
                       QueryTriggerInteraction.Ignore);
        }

        private bool IsInsideSpawnArea(Vector3 worldPosition)
        {
            for (int i = 0; i < spawnAreas.Length; i++)
            {
                BoxCollider area = spawnAreas[i];
                if (area == null || !area.enabled || !area.gameObject.activeInHierarchy) continue;

                Vector3 localPoint = area.transform.InverseTransformPoint(worldPosition) - area.center;
                Vector3 halfSize = area.size * 0.5f;
                if (Mathf.Abs(localPoint.x) <= halfSize.x && Mathf.Abs(localPoint.z) <= halfSize.z)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasBatchSpacing(Vector3 candidate, float minimumSpacing)
        {
            if (minimumSpacing <= 0f) return true;

            float minimumSpacingSquared = minimumSpacing * minimumSpacing;
            for (int i = 0; i < _positions.Count; i++)
            {
                Vector3 offset = candidate - _positions[i];
                offset.y = 0f;
                if (offset.sqrMagnitude < minimumSpacingSquared) return false;
            }

            return true;
        }

        private bool SpawnMonster(MonsterData monsterData, Vector3 position)
        {
            if (monsterData == null || monsterData.Prefab == null) return false;

            GameObject prefab = monsterData.Prefab;
            GameObject instance = _poolingManager.Get(prefab, position, prefab.transform.rotation);
            if (instance == null) return false;

            if (instance.TryGetComponent(out MonsterController monster))
            {
                monster.Configure(monsterData, player);
                return true;
            }

            Debug.LogError($"{prefab.name}에 MonsterController가 없습니다.", prefab);
            _poolingManager.Release(instance);
            return false;
        }

        private float GetRequiredSafetyDistance(WaveMonsterEntry entry)
        {
            return Mathf.Max(
                entry.SpawnSettings.MinimumDistanceFromPlayer,
                entry.Monster.MoveSpeed * minimumReactionTime);
        }

        private bool HasUsableSpawnArea()
        {
            if (spawnAreas == null) return false;
            for (int i = 0; i < spawnAreas.Length; i++)
            {
                if (GetAreaWeight(spawnAreas[i]) > 0f) return true;
            }

            return false;
        }

        private static float GetAreaWeight(BoxCollider area)
        {
            if (area == null || !area.enabled || !area.gameObject.activeInHierarchy) return 0f;

            Vector3 scale = area.transform.lossyScale;
            return Mathf.Abs(area.size.x * scale.x * area.size.z * scale.z);
        }

        private void OnValidate()
        {
            obstacleClearanceRadius = Mathf.Max(0f, obstacleClearanceRadius);
            maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
            groundProbeHeight = Mathf.Max(0f, groundProbeHeight);
            groundProbeDistance = Mathf.Max(0f, groundProbeDistance);
            minimumReactionTime = Mathf.Max(0f, minimumReactionTime);
            signalVisibleDuration = Mathf.Max(0.01f, signalVisibleDuration);
            signalHiddenDuration = Mathf.Max(0.01f, signalHiddenDuration);
            spawnAreas ??= new BoxCollider[0];
        }

        private void OnDisable()
        {
            CancelPendingSpawns();
        }
    }
}
