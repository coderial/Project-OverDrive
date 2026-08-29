using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProjectOverdrive.Managers
{
    [DisallowMultipleComponent]
    public sealed class SoundManager : MonoBehaviour
    {
        [Serializable]
        private sealed class SoundEntry
        {
            [SerializeField] private string id;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0f, 1f)] private float volume = 1f;

            public string Id => id;
            public AudioClip Clip => clip;
            public float Volume => volume;

            public void ClampVolume()
            {
                volume = Mathf.Clamp01(volume);
            }
        }

        private const string MasterVolumeKey = "Sound.MasterVolume";
        private const string BgmVolumeKey = "Sound.BgmVolume";
        private const string SfxVolumeKey = "Sound.SfxVolume";
        private const string MutedKey = "Sound.Muted";

        public static SoundManager Instance { get; private set; }

        [Header("Audio Sources")]
        [Tooltip("비워 두면 SoundManager가 실행 시 자동으로 생성합니다.")]
        [SerializeField] private AudioSource bgmSourceA;
        [Tooltip("BGM 크로스페이드에 사용하는 두 번째 AudioSource입니다.")]
        [SerializeField] private AudioSource bgmSourceB;
        [Tooltip("여러 SFX는 이 AudioSource의 PlayOneShot으로 중첩 재생됩니다.")]
        [SerializeField] private AudioSource sfxSource;

        [Header("Sound Library")]
        [SerializeField] private SoundEntry[] bgmClips = Array.Empty<SoundEntry>();
        [SerializeField] private SoundEntry[] sfxClips = Array.Empty<SoundEntry>();

        [Header("BGM")]
        [SerializeField, Min(0f)] private float defaultFadeDuration = 0.5f;

        private readonly Dictionary<string, SoundEntry> _bgmLibrary =
            new Dictionary<string, SoundEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, SoundEntry> _sfxLibrary =
            new Dictionary<string, SoundEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly float[] _bgmFadeLevels = new float[2];

        private AudioSource[] _bgmSources;
        private Coroutine _bgmFadeCoroutine;
        private string _currentBgmId;

        public float MasterVolume { get; private set; } = 1f;
        public float BgmVolume { get; private set; } = 1f;
        public float SfxVolume { get; private set; } = 1f;
        public bool IsMuted { get; private set; }
        public string CurrentBgmId => _currentBgmId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateMissingAudioSources();
            ConfigureAudioSources();
            BuildLibrary(bgmClips, _bgmLibrary, "BGM");
            BuildLibrary(sfxClips, _sfxLibrary, "SFX");
            LoadSettings();
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// 등록된 ID의 BGM을 재생합니다. 같은 BGM이 재생 중이면 다시 시작하지 않습니다.
        /// </summary>
        public void PlayBgm(string id)
        {
            PlayBgm(id, defaultFadeDuration, false);
        }

        /// <summary>
        /// 등록된 ID의 BGM을 지정한 시간 동안 크로스페이드하여 재생합니다.
        /// </summary>
        public void PlayBgm(string id, float fadeDuration, bool restart = false)
        {
            if (!TryGetEntry(_bgmLibrary, id, "BGM", out SoundEntry entry))
            {
                return;
            }

            if (!restart && string.Equals(_currentBgmId, entry.Id, StringComparison.OrdinalIgnoreCase)
                && IsAnyBgmPlaying())
            {
                return;
            }

            StopBgmFade();

            int fromIndex = GetLouderBgmSourceIndex();
            int toIndex = 1 - fromIndex;
            AudioSource fromSource = _bgmSources[fromIndex];
            AudioSource toSource = _bgmSources[toIndex];

            toSource.Stop();
            toSource.clip = entry.Clip;
            toSource.loop = true;
            toSource.Play();

            _currentBgmId = entry.Id;
            _bgmFadeLevels[toIndex] = 0f;

            float duration = Mathf.Max(0f, fadeDuration);
            if (duration <= 0f)
            {
                StopAndClearBgmSource(fromIndex);
                _bgmFadeLevels[toIndex] = entry.Volume;
                ApplyBgmVolumes();
                return;
            }

            _bgmFadeCoroutine = StartCoroutine(CrossfadeBgm(
                fromIndex,
                toIndex,
                _bgmFadeLevels[fromIndex],
                entry.Volume,
                duration));
        }

        /// <summary>
        /// 현재 BGM을 기본 페이드 시간 동안 정지합니다.
        /// </summary>
        public void StopBgm()
        {
            StopBgm(defaultFadeDuration);
        }

        public void StopBgm(float fadeDuration)
        {
            StopBgmFade();
            _currentBgmId = null;

            float duration = Mathf.Max(0f, fadeDuration);
            if (duration <= 0f)
            {
                StopAndClearBgmSource(0);
                StopAndClearBgmSource(1);
                ApplyBgmVolumes();
                return;
            }

            _bgmFadeCoroutine = StartCoroutine(FadeOutBgm(duration));
        }

        public void PauseBgm()
        {
            for (int i = 0; i < _bgmSources.Length; i++)
            {
                _bgmSources[i].Pause();
            }
        }

        public void ResumeBgm()
        {
            for (int i = 0; i < _bgmSources.Length; i++)
            {
                _bgmSources[i].UnPause();
            }
        }

        /// <summary>
        /// 등록된 ID의 SFX를 재생합니다. PlayOneShot을 사용하므로 효과음이 서로 겹칠 수 있습니다.
        /// </summary>
        public void PlaySfx(string id, float volumeScale = 1f)
        {
            if (!TryGetEntry(_sfxLibrary, id, "SFX", out SoundEntry entry))
            {
                return;
            }

            PlaySfx(entry.Clip, entry.Volume * volumeScale);
        }

        /// <summary>
        /// 라이브러리 등록 없이 AudioClip을 직접 재생합니다.
        /// </summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                Debug.LogWarning("[SoundManager] 재생할 SFX AudioClip이 없습니다.", this);
                return;
            }

            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        public void StopAllSfx()
        {
            sfxSource.Stop();
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            ApplyVolumes();
        }

        public void SetBgmVolume(float volume)
        {
            BgmVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(BgmVolumeKey, BgmVolume);
            ApplyBgmVolumes();
        }

        public void SetSfxVolume(float volume)
        {
            SfxVolume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            ApplySfxVolume();
        }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            ApplyVolumes();
        }

        /// <summary>
        /// 현재 볼륨 설정을 즉시 디스크에 저장합니다.
        /// </summary>
        public void SaveSettings()
        {
            PlayerPrefs.Save();
        }

        private IEnumerator CrossfadeBgm(
            int fromIndex,
            int toIndex,
            float fromStartLevel,
            float toTargetLevel,
            float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                _bgmFadeLevels[fromIndex] = Mathf.Lerp(fromStartLevel, 0f, progress);
                _bgmFadeLevels[toIndex] = Mathf.Lerp(0f, toTargetLevel, progress);
                ApplyBgmVolumes();
                yield return null;
            }

            StopAndClearBgmSource(fromIndex);
            _bgmFadeLevels[toIndex] = toTargetLevel;
            ApplyBgmVolumes();
            _bgmFadeCoroutine = null;
        }

        private IEnumerator FadeOutBgm(float duration)
        {
            float sourceAStartLevel = _bgmFadeLevels[0];
            float sourceBStartLevel = _bgmFadeLevels[1];
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                _bgmFadeLevels[0] = Mathf.Lerp(sourceAStartLevel, 0f, progress);
                _bgmFadeLevels[1] = Mathf.Lerp(sourceBStartLevel, 0f, progress);
                ApplyBgmVolumes();
                yield return null;
            }

            StopAndClearBgmSource(0);
            StopAndClearBgmSource(1);
            ApplyBgmVolumes();
            _bgmFadeCoroutine = null;
        }

        private void CreateMissingAudioSources()
        {
            if (bgmSourceA == null)
            {
                bgmSourceA = gameObject.AddComponent<AudioSource>();
            }

            if (bgmSourceB == null || bgmSourceB == bgmSourceA)
            {
                bgmSourceB = gameObject.AddComponent<AudioSource>();
            }

            if (sfxSource == null || sfxSource == bgmSourceA || sfxSource == bgmSourceB)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
            }

            _bgmSources = new[] { bgmSourceA, bgmSourceB };
        }

        private void ConfigureAudioSources()
        {
            for (int i = 0; i < _bgmSources.Length; i++)
            {
                AudioSource source = _bgmSources[i];
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
            }

            sfxSource.playOnAwake = false;
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0f;
        }

        private void LoadSettings()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 1f));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            IsMuted = PlayerPrefs.GetInt(MutedKey, 0) != 0;
        }

        private void ApplyVolumes()
        {
            ApplyBgmVolumes();
            ApplySfxVolume();
        }

        private void ApplyBgmVolumes()
        {
            float outputVolume = IsMuted ? 0f : MasterVolume * BgmVolume;
            for (int i = 0; i < _bgmSources.Length; i++)
            {
                _bgmSources[i].volume = outputVolume * _bgmFadeLevels[i];
            }
        }

        private void ApplySfxVolume()
        {
            sfxSource.volume = IsMuted ? 0f : MasterVolume * SfxVolume;
        }

        private static void BuildLibrary(
            SoundEntry[] entries,
            Dictionary<string, SoundEntry> library,
            string category)
        {
            library.Clear();

            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                SoundEntry entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id) || entry.Clip == null)
                {
                    continue;
                }

                if (!library.TryAdd(entry.Id.Trim(), entry))
                {
                    Debug.LogWarning($"[SoundManager] 중복된 {category} ID를 무시합니다: {entry.Id}");
                }
            }
        }

        private bool TryGetEntry(
            Dictionary<string, SoundEntry> library,
            string id,
            string category,
            out SoundEntry entry)
        {
            if (!string.IsNullOrWhiteSpace(id) && library.TryGetValue(id.Trim(), out entry))
            {
                return true;
            }

            Debug.LogWarning($"[SoundManager] 등록되지 않은 {category} ID입니다: {id}", this);
            entry = null;
            return false;
        }

        private bool IsAnyBgmPlaying()
        {
            return _bgmSources[0].isPlaying || _bgmSources[1].isPlaying;
        }

        private int GetLouderBgmSourceIndex()
        {
            return _bgmFadeLevels[1] > _bgmFadeLevels[0] ? 1 : 0;
        }

        private void StopAndClearBgmSource(int index)
        {
            AudioSource source = _bgmSources[index];
            source.Stop();
            source.clip = null;
            _bgmFadeLevels[index] = 0f;
        }

        private void StopBgmFade()
        {
            if (_bgmFadeCoroutine == null)
            {
                return;
            }

            StopCoroutine(_bgmFadeCoroutine);
            _bgmFadeCoroutine = null;
        }

        private void OnValidate()
        {
            defaultFadeDuration = Mathf.Max(0f, defaultFadeDuration);
            ClampEntryVolumes(bgmClips);
            ClampEntryVolumes(sfxClips);
        }

        private static void ClampEntryVolumes(SoundEntry[] entries)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                entries[i]?.ClampVolume();
            }
        }
    }
}
