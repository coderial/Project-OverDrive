using System.Collections;
using ProjectOverdrive.Controllers;
using ProjectOverdrive.Managers;
using TMPro;
using UnityEngine;

namespace ProjectOverdrive.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_GameOver : MonoBehaviour
    {
        [Header("Wave Fail Presentation")]
        [SerializeField] private TextMeshProUGUI waveFailText;
        [SerializeField] private CanvasGroup waveFailCanvasGroup;
        [SerializeField] private string waveFailMessage = "WAVE FAIL";
        [SerializeField, Min(0.01f)] private float appearDuration = 0.3f;
        [SerializeField, Min(0f)] private float holdDuration = 0.65f;
        [SerializeField, Min(0.01f)] private float disappearDuration = 0.3f;

        [Header("Result Panel")]
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI finalWaveText;
        [SerializeField] private string finalWaveFormat = "FINAL WAVE\n{0}";

        private PlayerHealth _playerHealth;
        private PlayerAnimator _playerAnimator;
        private WaveManager _waveManager;
        private Coroutine _gameOverCoroutine;
        private Vector2 _waveFailBasePosition;

        private void Awake()
        {
            if (waveFailText != null)
            {
                _waveFailBasePosition = waveFailText.rectTransform.anchoredPosition;
                waveFailText.gameObject.SetActive(false);
            }

            if (waveFailCanvasGroup != null)
            {
                waveFailCanvasGroup.alpha = 0f;
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(false);
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (_playerHealth == null)
            {
                ResolveReferences();
            }
        }

        private void ResolveReferences()
        {
            if (_waveManager == null)
            {
                _waveManager = FindFirstObjectByType<WaveManager>();
            }

            PlayerHealth foundHealth = FindFirstObjectByType<PlayerHealth>();
            if (foundHealth == null || foundHealth == _playerHealth)
            {
                return;
            }

            UnbindPlayer();
            _playerHealth = foundHealth;
            _playerAnimator = foundHealth.GetComponent<PlayerAnimator>();
            _playerHealth.OnDied += HandlePlayerDied;
        }

        private void HandlePlayerDied()
        {
            if (_gameOverCoroutine == null)
            {
                _gameOverCoroutine = StartCoroutine(PlayGameOverSequence());
            }
        }

        private IEnumerator PlayGameOverSequence()
        {
            int finalWave = _waveManager != null ? _waveManager.CurrentWave : 0;
            float deathAnimationDuration = _playerAnimator != null
                ? _playerAnimator.DeathPresentationDuration
                : 0f;
            float presentationStartTime = Time.unscaledTime;

            yield return PlayWaveFailText();

            float remainingAnimationTime = deathAnimationDuration
                - (Time.unscaledTime - presentationStartTime);
            if (remainingAnimationTime > 0f)
            {
                yield return new WaitForSecondsRealtime(remainingAnimationTime);
            }

            if (finalWaveText != null)
            {
                finalWaveText.text = string.Format(finalWaveFormat, finalWave);
            }

            if (resultPanel != null)
            {
                resultPanel.SetActive(true);
            }

            _gameOverCoroutine = null;
        }

        private IEnumerator PlayWaveFailText()
        {
            if (waveFailText == null || waveFailCanvasGroup == null)
            {
                yield break;
            }

            RectTransform textRect = waveFailText.rectTransform;
            waveFailText.text = waveFailMessage;
            waveFailText.gameObject.SetActive(true);
            waveFailCanvasGroup.alpha = 0f;
            textRect.anchoredPosition = _waveFailBasePosition;
            textRect.localScale = Vector3.one * 0.65f;

            float elapsed = 0f;
            while (elapsed < appearDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / appearDuration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                waveFailCanvasGroup.alpha = t;
                textRect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, easedT);
                yield return null;
            }

            waveFailCanvasGroup.alpha = 1f;
            textRect.localScale = Vector3.one;

            float holdEndTime = Time.unscaledTime + holdDuration;
            while (Time.unscaledTime < holdEndTime)
            {
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < disappearDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / disappearDuration);
                waveFailCanvasGroup.alpha = 1f - t;
                textRect.anchoredPosition = _waveFailBasePosition + Vector2.up * (30f * t);
                yield return null;
            }

            waveFailCanvasGroup.alpha = 0f;
            textRect.anchoredPosition = _waveFailBasePosition;
            textRect.localScale = Vector3.one;
            waveFailText.gameObject.SetActive(false);
        }

        private void UnbindPlayer()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnDied -= HandlePlayerDied;
            }

            _playerHealth = null;
            _playerAnimator = null;
        }

        private void OnDisable()
        {
            UnbindPlayer();
        }

        private void OnValidate()
        {
            appearDuration = Mathf.Max(0.01f, appearDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            disappearDuration = Mathf.Max(0.01f, disappearDuration);
        }
    }
}
