using System.Collections;
using ProjectOverdrive.Controllers;
using ProjectOverdrive.Data;
using ProjectOverdrive.UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectOverdrive.Managers
{
    public enum WaveStatus
    {
        Preparation = 1,
        Spawn_Progress = 2,
        Open_Shop = 3,
        Game_Clear = 4,
        Wave_Clear = 5,
        Game_Fail = 6,
    }

    [DisallowMultipleComponent]
    public sealed class WaveManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private SpawnManager spawnManager;

        [Header("Shop UI Reference")]
        [Tooltip("씬의 UI_Shop 패널을 여기에 연결해주세요.")]
        [FormerlySerializedAs("_shopUI")]
        [SerializeField] private UI_Shop shopUI;

        [Header("Wave Clear Presentation")]
        [SerializeField] private UI_WaveClear waveClearUI;
        [SerializeField, Min(0.01f)] private float currencyCollectionDuration = 0.75f;

        [Header("Wave Sequence")]
        [Tooltip("실행할 순서대로 WaveData를 등록합니다.")]
        [SerializeField] private WaveData[] waves = new WaveData[0];
        [SerializeField, Min(0f)] private float waveStartDelay = 1f;

        private WaveStatus _currentStatus = WaveStatus.Preparation;
        private WaveData _currentWaveData;
        private PlayerController _playerController;
        private PlayerHealth _playerHealth;
        private float _statusEndTime;
        private float _nextSpawnTime;
        private Coroutine _waveClearCoroutine;

        public WaveStatus CurrentStatus => _currentStatus;
        public WaveData CurrentWaveData => _currentWaveData;
        public int CurrentWave { get; private set; }
        public bool IsWaveRunning { get; private set; }

        public float RemainingTime => _currentStatus == WaveStatus.Spawn_Progress
            ? Mathf.Max(0f, _statusEndTime - Time.time)
            : 0f;

        private void Start()
        {
            ResolveReferences();

            if (!ValidateConfiguration())
            {
                _playerController?.SetMovementEnabled(false);
                enabled = false;
                return;
            }

            _playerHealth.OnDied += FailWave;
            spawnManager.Initialize(player);
            BeginWave();
        }

        private void Update()
        {
            float currentTime = Time.time;

            switch (_currentStatus)
            {
                case WaveStatus.Preparation:
                    if (currentTime >= _statusEndTime) StartSpawnProgress(currentTime);
                    break;

                case WaveStatus.Spawn_Progress:
                    if (currentTime >= _statusEndTime)
                    {
                        BeginWaveClear();
                    }
                    else if (currentTime >= _nextSpawnTime)
                    {
                        spawnManager.SpawnBatch(_currentWaveData);
                        _nextSpawnTime = Mathf.Max(
                            _nextSpawnTime + _currentWaveData.SpawnInterval,
                            currentTime + 0.001f);
                    }
                    break;

                case WaveStatus.Open_Shop:
                case WaveStatus.Game_Clear:
                case WaveStatus.Wave_Clear:
                case WaveStatus.Game_Fail:
                    break;
            }
        }

        public void BeginWave()
        {
            if (waves == null || CurrentWave >= waves.Length)
            {
                _currentWaveData = null;
                ChangeStatus(WaveStatus.Game_Clear);
                IsWaveRunning = false;
                Debug.Log($"<color=green>[WaveManager] 모든 웨이브({CurrentWave})를 클리어했습니다.</color>", this);
                return;
            }

            _currentWaveData = waves[CurrentWave];
            CurrentWave++;
            _playerHealth.RestoreToFull();
            ChangeStatus(WaveStatus.Preparation);
            _statusEndTime = Time.time + waveStartDelay;
            IsWaveRunning = true;

            Debug.Log($"[WaveManager] Wave {CurrentWave} 시작 준비: {_currentWaveData.name}", this);
        }

        public void CloseShop()
        {
            if (_currentStatus != WaveStatus.Open_Shop) return;

            Debug.Log($"[WaveManager] 상점 종료 - Wave {CurrentWave + 1}을 시작합니다.", this);
            BeginWave();
        }

        public void FailWave()
        {
            if (_currentStatus == WaveStatus.Game_Fail || _currentStatus == WaveStatus.Game_Clear)
            {
                return;
            }

            if (_waveClearCoroutine != null)
            {
                StopCoroutine(_waveClearCoroutine);
                _waveClearCoroutine = null;
            }

            spawnManager?.CancelPendingSpawns();
            int returnedMonsterCount = spawnManager != null
                ? spawnManager.DespawnAllMonsters()
                : 0;
            ChangeStatus(WaveStatus.Game_Fail);
            IsWaveRunning = false;
            Debug.Log(
                $"[WaveManager] Wave {CurrentWave} 실패. 남은 몬스터 {returnedMonsterCount}마리를 반환했습니다.",
                this);
        }

        private void StartSpawnProgress(float currentTime)
        {
            if (!spawnManager.CanSpawn(_currentWaveData, out string reason))
            {
                Debug.LogError($"[WaveManager] Wave {CurrentWave}를 시작할 수 없습니다: {reason}", this);
                _playerController?.SetMovementEnabled(false);
                enabled = false;
                IsWaveRunning = false;
                return;
            }

            ChangeStatus(WaveStatus.Spawn_Progress);
            _statusEndTime = currentTime + _currentWaveData.Duration;
            _nextSpawnTime = currentTime;

            Debug.Log(
                $"[WaveManager] Wave {CurrentWave} 스폰 시작 " +
                $"(지속 {_currentWaveData.Duration:0.##}초, 간격 {_currentWaveData.SpawnInterval:0.##}초).",
                this);
        }

        private void BeginWaveClear()
        {
            spawnManager.CancelPendingSpawns();
            ChangeStatus(WaveStatus.Wave_Clear);
            IsWaveRunning = false;
            _waveClearCoroutine = StartCoroutine(PlayWaveClearSequence());
        }

        private IEnumerator PlayWaveClearSequence()
        {
            int removedMonsterCount = spawnManager.DespawnAllMonsters();
            Debug.Log($"[WaveManager] 남은 몬스터 {removedMonsterCount}마리를 제거했습니다.", this);

            if (player != null && player.TryGetComponent(out PlayerController playerController))
            {
                yield return CollectAllCurrency(playerController);
            }

            if (waveClearUI != null)
            {
                yield return waveClearUI.Play(CurrentWave);
            }

            OpenShop();
            _waveClearCoroutine = null;
        }

        private IEnumerator CollectAllCurrency(PlayerController playerController)
        {
            CurrencyPickup[] pickups = FindObjectsByType<CurrencyPickup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            if (pickups.Length == 0) yield break;

            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null)
                {
                    pickups[i].BeginWaveEndCollection(playerController, currencyCollectionDuration);
                }
            }

            float collectionEndTime = Time.unscaledTime + currencyCollectionDuration + 0.1f;
            while (Time.unscaledTime < collectionEndTime)
            {
                bool hasActivePickup = false;
                for (int i = 0; i < pickups.Length; i++)
                {
                    if (pickups[i] != null && pickups[i].gameObject.activeInHierarchy)
                    {
                        hasActivePickup = true;
                        break;
                    }
                }

                if (!hasActivePickup) yield break;
                yield return null;
            }

            for (int i = 0; i < pickups.Length; i++)
            {
                if (pickups[i] != null && pickups[i].gameObject.activeInHierarchy)
                {
                    pickups[i].CompleteWaveEndCollection();
                }
            }
        }

        private void OpenShop()
        {
            spawnManager.CancelPendingSpawns();
            ChangeStatus(WaveStatus.Open_Shop);
            IsWaveRunning = false;

            Debug.Log($"[WaveManager] Wave {CurrentWave} 종료. 상점을 오픈합니다.", this);

            if (player != null && player.TryGetComponent(out PlayerController playerController))
            {
                if (shopUI != null)
                {
                    shopUI.OpenShop(playerController, this);
                }
                else
                {
                    Debug.LogWarning("[WaveManager] Shop UI가 연결되지 않았습니다.", this);
                }
            }
        }

        private void ResolveReferences()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null) player = playerObject.transform;
            }

            if (spawnManager == null) TryGetComponent(out spawnManager);
            if (waveClearUI == null) TryGetComponent(out waveClearUI);
            if (player != null)
            {
                player.TryGetComponent(out _playerController);
                player.TryGetComponent(out _playerHealth);
            }
        }

        private void ChangeStatus(WaveStatus status)
        {
            _currentStatus = status;
            bool canMove = status == WaveStatus.Preparation || status == WaveStatus.Spawn_Progress;
            _playerController?.SetMovementEnabled(canMove);
        }

        private bool ValidateConfiguration()
        {
            if (player == null || _playerController == null || _playerHealth == null
                || spawnManager == null || PoolingManager.Instance == null)
            {
                Debug.LogError(
                    "[WaveManager] PlayerController, PlayerHealth, SpawnManager, PoolingManager가 필요합니다.",
                    this);
                return false;
            }

            if (waveClearUI == null)
            {
                Debug.LogError("[WaveManager] UI_WaveClear가 필요합니다.", this);
                return false;
            }

            if (waves == null || waves.Length == 0)
            {
                Debug.LogError("[WaveManager] WaveData 배열이 비어 있습니다.", this);
                return false;
            }

            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] == null)
                {
                    Debug.LogError($"[WaveManager] Wave {i + 1}의 WaveData가 비어 있습니다.", this);
                    return false;
                }

                if (!waves[i].IsValid(out string reason))
                {
                    Debug.LogError($"[WaveManager] {waves[i].name} 설정 오류: {reason}", waves[i]);
                    return false;
                }
            }

            return true;
        }

        private void OnValidate()
        {
            waveStartDelay = Mathf.Max(0f, waveStartDelay);
            currencyCollectionDuration = Mathf.Max(0.01f, currencyCollectionDuration);
            waves ??= new WaveData[0];
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDied -= FailWave;
            }
        }
    }
}
