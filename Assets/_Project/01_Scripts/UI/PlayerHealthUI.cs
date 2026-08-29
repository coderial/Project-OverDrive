using System.Globalization;
using ProjectOverdrive.Controllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectOverdrive.UI
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealthUI : MonoBehaviour
    {
        private static readonly Color HealthyColor = new Color(0.2f, 0.85f, 0.32f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.65f, 0.08f, 1f);
        private static readonly Color DangerColor = new Color(0.95f, 0.16f, 0.16f, 1f);

        [Header("Screen HUD")]
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private RectTransform screenFill;
        [SerializeField] private GameObject root;

        [Header("World Bar")]
        [SerializeField] private Transform worldBarTransform;
        [SerializeField, Min(0f)] private float worldHeight = 1.35f;
        [Tooltip("월드 Z축 기준 위치 오프셋입니다. 탑다운 카메라에서는 양수가 화면 위쪽입니다.")]
        [SerializeField, Range(-3f, 3f)] private float worldZOffset = 1.2f;
        [SerializeField] private RectTransform worldFill;

        private PlayerController _player;
        private Image _screenFillImage;
        private Image _worldFillImage;
        private Camera _camera;

        private void Awake()
        {
            _screenFillImage = screenFill != null ? screenFill.GetComponent<Image>() : null;
            _worldFillImage = worldFill != null ? worldFill.GetComponent<Image>() : null;
            RefreshHealth(0, 0);
            SetWorldBarVisible(false);
        }

        private void OnEnable()
        {
            BindToPlayer();
        }

        private void Update()
        {
            if (_player == null)
            {
                SetWorldBarVisible(false);
                BindToPlayer();
            }
        }

        private void LateUpdate()
        {
            if (_player == null || worldBarTransform == null)
            {
                return;
            }

            worldBarTransform.position = _player.transform.position
                + new Vector3(0f, worldHeight, worldZOffset);

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            if (_camera != null)
            {
                worldBarTransform.rotation = _camera.transform.rotation;
            }
        }

        private void OnDisable()
        {
            UnbindFromPlayer();
            SetWorldBarVisible(false);
        }

        private void BindToPlayer()
        {
            PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
            if (foundPlayer == null || foundPlayer == _player)
            {
                return;
            }

            UnbindFromPlayer();
            _player = foundPlayer;
            _player.OnHpChanged += RefreshHealth;
            SetWorldBarVisible(true);
            RefreshHealth(_player.CurrentHp, _player.MaxHp);
        }

        private void UnbindFromPlayer()
        {
            if (_player != null)
            {
                _player.OnHpChanged -= RefreshHealth;
            }

            _player = null;
        }

        private void SetWorldBarVisible(bool visible)
        {
            if (worldBarTransform != null && worldBarTransform.gameObject.activeSelf != visible)
            {
                worldBarTransform.gameObject.SetActive(visible);
            }
        }

        private void RefreshHealth(int currentHp, int maxHp)
        {
            if (currentHp == maxHp)
            {
                root.SetActive(false);
                return;
            }
            root.SetActive(true);
            int safeMaxHp = Mathf.Max(0, maxHp);
            int safeCurrentHp = Mathf.Clamp(currentHp, 0, safeMaxHp);
            float ratio = safeMaxHp > 0 ? (float)safeCurrentHp / safeMaxHp : 0f;
            Color healthColor = EvaluateHealthColor(ratio);

            if (healthText != null)
            {
                healthText.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:N0} / {1:N0}",
                    safeCurrentHp,
                    safeMaxHp);
            }

            SetFill(screenFill, ratio);
            SetFill(worldFill, ratio);

            if (_screenFillImage != null)
            {
                _screenFillImage.color = healthColor;
            }

            if (_worldFillImage != null)
            {
                _worldFillImage.color = healthColor;
            }
        }

        private static void SetFill(RectTransform fill, float ratio)
        {
            if (fill == null)
            {
                return;
            }

            Vector2 anchorMax = fill.anchorMax;
            anchorMax.x = Mathf.Clamp01(ratio);
            fill.anchorMax = anchorMax;
        }

        private static Color EvaluateHealthColor(float ratio)
        {
            float clampedRatio = Mathf.Clamp01(ratio);
            if (clampedRatio <= 0.5f)
            {
                return Color.Lerp(DangerColor, WarningColor, clampedRatio * 2f);
            }

            return Color.Lerp(WarningColor, HealthyColor, (clampedRatio - 0.5f) * 2f);
        }
    }
}
