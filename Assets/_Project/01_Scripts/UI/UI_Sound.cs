using ProjectOverdrive.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectOverdrive.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_Sound : MonoBehaviour
    {
        [Header("Volume Sliders")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _bgmVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("Panel")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _closeButton;

        private bool _listenersRegistered;
        private bool _missingManagerWarningShown;

        private void Awake()
        {
            RegisterListeners();
        }

        private void OnEnable()
        {
            SyncSlidersWithCurrentVolumes();
        }

        private void Start()
        {
            // 다른 오브젝트의 Awake에서 SoundManager가 초기화되는 경우까지 반영합니다.
            SyncSlidersWithCurrentVolumes();
        }

        private void OnDestroy()
        {
            UnregisterListeners();
        }

        public void Open()
        {
            GetPanelRoot().SetActive(true);
            SyncSlidersWithCurrentVolumes();
        }

        public void Close()
        {
            SoundManager.Instance?.SaveSettings();
            GetPanelRoot().SetActive(false);
        }

        private void RegisterListeners()
        {
            if (_listenersRegistered)
            {
                return;
            }

            _masterVolumeSlider?.onValueChanged.AddListener(OnMasterVolumeChanged);
            _bgmVolumeSlider?.onValueChanged.AddListener(OnBgmVolumeChanged);
            _sfxVolumeSlider?.onValueChanged.AddListener(OnSfxVolumeChanged);
            _closeButton?.onClick.AddListener(Close);
            _listenersRegistered = true;
        }

        private void UnregisterListeners()
        {
            if (!_listenersRegistered)
            {
                return;
            }

            _masterVolumeSlider?.onValueChanged.RemoveListener(OnMasterVolumeChanged);
            _bgmVolumeSlider?.onValueChanged.RemoveListener(OnBgmVolumeChanged);
            _sfxVolumeSlider?.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            _closeButton?.onClick.RemoveListener(Close);
            _listenersRegistered = false;
        }

        private void SyncSlidersWithCurrentVolumes()
        {
            SoundManager soundManager = SoundManager.Instance;
            if (soundManager == null)
            {
                if (!_missingManagerWarningShown)
                {
                    Debug.LogWarning("[UI_Sound] 씬에서 SoundManager를 찾을 수 없습니다.", this);
                    _missingManagerWarningShown = true;
                }

                return;
            }

            _missingManagerWarningShown = false;
            SetSliderValueWithoutNotify(_masterVolumeSlider, soundManager.MasterVolume);
            SetSliderValueWithoutNotify(_bgmVolumeSlider, soundManager.BgmVolume);
            SetSliderValueWithoutNotify(_sfxVolumeSlider, soundManager.SfxVolume);
        }

        private void OnMasterVolumeChanged(float volume)
        {
            SoundManager.Instance?.SetMasterVolume(GetNormalizedValue(_masterVolumeSlider, volume));
        }

        private void OnBgmVolumeChanged(float volume)
        {
            SoundManager.Instance?.SetBgmVolume(GetNormalizedValue(_bgmVolumeSlider, volume));
        }

        private void OnSfxVolumeChanged(float volume)
        {
            SoundManager.Instance?.SetSfxVolume(GetNormalizedValue(_sfxVolumeSlider, volume));
        }

        private GameObject GetPanelRoot()
        {
            return _panelRoot != null ? _panelRoot : gameObject;
        }

        private static void SetSliderValueWithoutNotify(Slider slider, float normalizedVolume)
        {
            if (slider == null)
            {
                return;
            }

            float value = Mathf.Lerp(slider.minValue, slider.maxValue, Mathf.Clamp01(normalizedVolume));
            slider.SetValueWithoutNotify(value);
        }

        private static float GetNormalizedValue(Slider slider, float value)
        {
            return slider != null
                ? Mathf.InverseLerp(slider.minValue, slider.maxValue, value)
                : Mathf.Clamp01(value);
        }
    }
}
