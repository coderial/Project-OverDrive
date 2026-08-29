using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectOverdrive.UI
{
    [DisallowMultipleComponent]
    public sealed class UI_WaveClear : MonoBehaviour
    {
        [Header("Optional Scene Reference")]
        [SerializeField] private TextMeshProUGUI clearText;

        [Header("Presentation")]
        [SerializeField] private string messageFormat = "STAGE {0} CLEAR!";
        [SerializeField, Min(0.01f)] private float appearDuration = 0.3f;
        [SerializeField, Min(0f)] private float holdDuration = 0.7f;
        [SerializeField, Min(0.01f)] private float disappearDuration = 0.35f;
        [SerializeField] private Color textColor = new Color(1f, 0.9f, 0.25f, 1f);

        private CanvasGroup _canvasGroup;
        private RectTransform _textRect;
        private Vector2 _baseAnchoredPosition;
        private bool _isInitialized;

        private void Awake()
        {
            EnsureUI();
        }

        public IEnumerator Play(int waveNumber)
        {
            EnsureUI();
            if (clearText == null) yield break;

            clearText.text = string.Format(messageFormat, waveNumber);
            clearText.color = textColor;
            clearText.gameObject.SetActive(true);
            _textRect.anchoredPosition = _baseAnchoredPosition;

            float elapsed = 0f;
            while (elapsed < appearDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / appearDuration);
                float easedT = 1f - Mathf.Pow(1f - t, 3f);
                _canvasGroup.alpha = t;
                _textRect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1f, easedT);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _textRect.localScale = Vector3.one;

            float holdEndTime = Time.unscaledTime + holdDuration;
            while (Time.unscaledTime < holdEndTime) yield return null;

            elapsed = 0f;
            while (elapsed < disappearDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / disappearDuration);
                _canvasGroup.alpha = 1f - t;
                _textRect.anchoredPosition = _baseAnchoredPosition + Vector2.up * (30f * t);
                yield return null;
            }

            _canvasGroup.alpha = 0f;
            _textRect.anchoredPosition = _baseAnchoredPosition;
            clearText.gameObject.SetActive(false);
        }

        private void EnsureUI()
        {
            if (_isInitialized) return;
            if (clearText == null) CreateRuntimeUI();
            if (clearText == null) return;

            _textRect = clearText.rectTransform;
            _baseAnchoredPosition = _textRect.anchoredPosition;
            if (!clearText.TryGetComponent(out _canvasGroup))
            {
                _canvasGroup = clearText.gameObject.AddComponent<CanvasGroup>();
            }

            _canvasGroup.alpha = 0f;
            clearText.gameObject.SetActive(false);
            _isInitialized = true;
        }

        private void CreateRuntimeUI()
        {
            var canvasObject = new GameObject(
                "[Wave Clear Canvas]",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textObject = new GameObject(
                "StageClearText",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(CanvasGroup));
            textObject.transform.SetParent(canvasObject.transform, false);

            clearText = textObject.GetComponent<TextMeshProUGUI>();
            clearText.alignment = TextAlignmentOptions.Center;
            clearText.fontStyle = FontStyles.Bold;
            clearText.fontSize = 64f;
            clearText.enableAutoSizing = true;
            clearText.fontSizeMin = 42f;
            clearText.fontSizeMax = 72f;
            clearText.raycastTarget = false;

            RectTransform rect = clearText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1000f, 180f);
            rect.anchoredPosition = Vector2.zero;
        }

        private void OnValidate()
        {
            appearDuration = Mathf.Max(0.01f, appearDuration);
            holdDuration = Mathf.Max(0f, holdDuration);
            disappearDuration = Mathf.Max(0.01f, disappearDuration);
        }
    }
}
