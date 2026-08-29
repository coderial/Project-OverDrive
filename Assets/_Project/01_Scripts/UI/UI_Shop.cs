using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectOverdrive.Controllers;
using ProjectOverdrive.Data;
using ProjectOverdrive.Managers;

namespace ProjectOverdrive.UI
{
    public class UI_Shop : MonoBehaviour
    {
        [Header("Top Bar")]
        [SerializeField] private TextMeshProUGUI _waveText;
        [SerializeField] private TextMeshProUGUI _currencyText;
        [SerializeField] private Button _rerollButton;
        [SerializeField] private TextMeshProUGUI _rerollCostText;

        [Header("Shop Card Slots (4 Slots)")]
        [SerializeField] private UI_ShopItemSlot[] _itemSlots = new UI_ShopItemSlot[4];

        [Header("Equipped Weapon Slots (6 Slots)")]
        [SerializeField] private UI_EquippedWeaponSlot[] _equippedWeaponSlots = new UI_EquippedWeaponSlot[6];

        [Header("Bottom Navigation")]
        [SerializeField] private Button _nextWaveButton;

        [Header("Shop Database")]
        [Tooltip("상점에 등장할 수 있는 전체 무기 목록")]
        [SerializeField] private List<WeaponData> _weaponPool = new List<WeaponData>();
        [SerializeField] private int _baseRerollCost = 5;

        [Header("Sell Settings (판매 설정)")]
        [SerializeField] private float _level2SellBonusPercent = 20f;
        [SerializeField] private float _level3SellBonusPercent = 30f;

        [Header("Weapon Slot Popup UI")]
        [SerializeField] private GameObject _tooltipPanel;
        [SerializeField] private TextMeshProUGUI _tooltipText;
        [Tooltip("비워 두면 실행 시 팝업 내부에 자동 생성됩니다.")]
        [SerializeField] private Button _sellButton;
        [Tooltip("비워 두면 실행 시 팝업 내부에 자동 생성됩니다.")]
        [SerializeField] private Button _synthesisButton;

        private PlayerController _player;
        private WaveManager _waveManager;
        private int _currentRerollCost;
        private int _selectedWeaponSlotIndex = -1;
        private TextMeshProUGUI _sellButtonText;
        private TextMeshProUGUI _synthesisButtonText;

        private void Awake()
        {
            if (_rerollButton != null) _rerollButton.onClick.AddListener(OnClickReroll);
            if (_nextWaveButton != null) _nextWaveButton.onClick.AddListener(OnClickNextWave);

            InitializeWeaponPopup();
        }

        public void OpenShop(PlayerController player, WaveManager waveManager)
        {
            _player = player;
            _waveManager = waveManager;

            gameObject.SetActive(true);
            _currentRerollCost = _baseRerollCost;
            HideWeaponPopup();

            if (_player != null)
            {
                _player.OnCurrencyChanged += UpdateCurrencyDisplay;
            }

            UpdateWaveDisplay();
            UpdateCurrencyDisplay(_player != null ? _player.Currency : 0);
            UpdateEquippedWeaponsDisplay();
            RollShopItems();
        }

        public void CloseShop()
        {
            if (_player != null)
            {
                _player.OnCurrencyChanged -= UpdateCurrencyDisplay;
            }

            HideWeaponPopup();
            gameObject.SetActive(false);

            if (_waveManager != null)
            {
                _waveManager.CloseShop();
            }
        }

        #region Shop Item Rolling

        private void RollShopItems()
        {
            if (_weaponPool == null || _weaponPool.Count == 0) return;

            List<WeaponData> availableWeapons = new List<WeaponData>(_weaponPool);

            for (int i = 0; i < _itemSlots.Length; i++)
            {
                if (_itemSlots[i] == null) continue;

                if (availableWeapons.Count == 0)
                {
                    availableWeapons = new List<WeaponData>(_weaponPool);
                }

                int randomIndex = Random.Range(0, availableWeapons.Count);
                WeaponData randomWeapon = availableWeapons[randomIndex];
                availableWeapons.RemoveAt(randomIndex);

                int price = randomWeapon.PurchasePrice;
                _itemSlots[i].Setup(randomWeapon, price, OnBuyItem);
            }

            UpdateRerollButtonDisplay();

            // 상점 아이템이 리롤된 직후, 현재 소지금을 바탕으로 즉시 색상을 판별합니다.
            if (_player != null)
            {
                UpdateCurrencyDisplay(_player.Currency);
            }
        }

        private void OnBuyItem(UI_ShopItemSlot slot, WeaponData weapon, int price)
        {
            if (_player == null) return;
            if (!_player.TryPurchaseWeapon(weapon, price)) return;

            slot.SetSoldOut();
            UpdateEquippedWeaponsDisplay();
            HideWeaponPopup();
        }

        private void OnClickReroll()
        {
            if (_player == null || _player.Currency < _currentRerollCost) return;
            _player.SpendCurrency(_currentRerollCost);
            _currentRerollCost += 2;
            RollShopItems();
        }

        private void OnClickNextWave()
        {
            CloseShop();
        }

        #endregion

        #region Sell & Synthesis Popup Logic

        private int GetSellPrice(WeaponData weapon, int level)
        {
            float basePrice = weapon.SellPrice;

            if (level == 2) basePrice *= (1f + _level2SellBonusPercent / 100f);
            if (level == 3) basePrice *= (1f + _level3SellBonusPercent / 100f);

            return Mathf.FloorToInt(basePrice);
        }

        private void OnSellWeapon(int slotIndex)
        {
            if (_player == null) return;
            WeaponData weapon = _player.WeaponInfo[slotIndex];
            if (weapon == null) return;

            int level = _player.WeaponLevels[slotIndex];
            int finalPrice = GetSellPrice(weapon, level);

            _player.RemoveWeapon(slotIndex);
            _player.AddCurrency(finalPrice);

            UpdateEquippedWeaponsDisplay();
            HideWeaponPopup();
        }

        private void OnClickWeaponSlot(int slotIndex, RectTransform slotRect)
        {
            if (_player == null || slotIndex < 0 || slotRect == null)
            {
                HideWeaponPopup();
                return;
            }

            if (_selectedWeaponSlotIndex == slotIndex &&
                _tooltipPanel != null && _tooltipPanel.activeSelf)
            {
                HideWeaponPopup();
                return;
            }

            _selectedWeaponSlotIndex = slotIndex;
            RefreshWeaponPopup();
            PositionWeaponPopup(slotRect);

            if (_tooltipPanel != null)
            {
                _tooltipPanel.transform.SetAsLastSibling();
                _tooltipPanel.SetActive(true);
            }
        }

        private void OnClickSellSelectedWeapon()
        {
            if (_selectedWeaponSlotIndex < 0) return;
            OnSellWeapon(_selectedWeaponSlotIndex);
        }

        private void OnClickSynthesizeSelectedWeapon()
        {
            if (_player == null || _selectedWeaponSlotIndex < 0) return;
            if (!_player.TrySynthesizeWeapon(_selectedWeaponSlotIndex)) return;

            UpdateEquippedWeaponsDisplay();
            HideWeaponPopup();
        }

        private void RefreshWeaponPopup()
        {
            if (_player == null ||
                _selectedWeaponSlotIndex < 0 ||
                _selectedWeaponSlotIndex >= _player.WeaponInfo.Length)
            {
                HideWeaponPopup();
                return;
            }

            WeaponData weapon = _player.WeaponInfo[_selectedWeaponSlotIndex];
            if (weapon == null)
            {
                HideWeaponPopup();
                return;
            }

            int level = _player.WeaponLevels[_selectedWeaponSlotIndex];
            int sellPrice = GetSellPrice(weapon, level);
            string typeText = weapon.AttackType == WeaponAttackType.Thrust ? "찌르기" : "휘두르기";

            if (_tooltipText != null)
            {
                float levelDamage = weapon.BaseDamage * (1f + (level - 1) * 0.5f);
                _tooltipText.text =
                    $"<b>{weapon.WeaponName}  Lv.{level}</b>\n" +
                    $"{typeText}  |  공격력 {levelDamage:F0}  |  범위 {weapon.BaseHitArea:F1}";
            }

            if (_sellButtonText != null) _sellButtonText.text = $"판매  +{sellPrice} G";

            bool canSynthesize = _player.CanSynthesizeWeapon(_selectedWeaponSlotIndex);
            if (_synthesisButton != null) _synthesisButton.interactable = canSynthesize;
            if (_synthesisButtonText != null)
            {
                _synthesisButtonText.text = level >= PlayerController.MAX_WEAPON_LEVEL
                    ? "최고 등급"
                    : "합성";
            }
        }

        private void HideWeaponPopup()
        {
            _selectedWeaponSlotIndex = -1;
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
        }

        private void InitializeWeaponPopup()
        {
            if (_tooltipPanel == null) return;

            if (!_tooltipPanel.TryGetComponent<CanvasGroup>(out var canvasGroup))
            {
                canvasGroup = _tooltipPanel.AddComponent<CanvasGroup>();
            }

            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;

            RectTransform panelRect = _tooltipPanel.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                panelRect.pivot = new Vector2(0.5f, 0.5f);
                panelRect.sizeDelta = new Vector2(380f, 230f);
            }

            if (_tooltipText != null)
            {
                _tooltipText.transform.SetParent(_tooltipPanel.transform, false);
                _tooltipText.gameObject.SetActive(true);
                _tooltipText.alignment = TextAlignmentOptions.Center;
                _tooltipText.enableAutoSizing = true;
                _tooltipText.fontSizeMin = 18f;
                _tooltipText.fontSizeMax = 30f;
                _tooltipText.raycastTarget = false;

                RectTransform textRect = _tooltipText.rectTransform;
                textRect.anchorMin = new Vector2(0.04f, 0.34f);
                textRect.anchorMax = new Vector2(0.96f, 0.96f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
            }

            if (_sellButton == null)
            {
                _sellButton = CreatePopupButton(
                    "SellButton", "판매", new Vector2(0.04f, 0.07f), new Vector2(0.48f, 0.30f));
            }

            if (_synthesisButton == null)
            {
                _synthesisButton = CreatePopupButton(
                    "SynthesisButton", "합성", new Vector2(0.52f, 0.07f), new Vector2(0.96f, 0.30f));
            }

            _sellButtonText = _sellButton != null
                ? _sellButton.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
            _synthesisButtonText = _synthesisButton != null
                ? _synthesisButton.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;

            if (_sellButton != null) _sellButton.onClick.AddListener(OnClickSellSelectedWeapon);
            if (_synthesisButton != null) _synthesisButton.onClick.AddListener(OnClickSynthesizeSelectedWeapon);

            _tooltipPanel.SetActive(false);
        }

        private Button CreatePopupButton(
            string objectName,
            string label,
            Vector2 anchorMin,
            Vector2 anchorMax)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button));

            buttonObject.transform.SetParent(_tooltipPanel.transform, false);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = anchorMin;
            buttonRect.anchorMax = anchorMax;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;

            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.color = new Color(0.18f, 0.25f, 0.36f, 1f);

            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = buttonImage;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.30f, 0.42f, 0.58f, 1f);
            colors.pressedColor = new Color(0.12f, 0.17f, 0.25f, 1f);
            colors.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.55f);
            button.colors = colors;

            TextMeshProUGUI labelText;
            if (_tooltipText != null)
            {
                labelText = Instantiate(_tooltipText, buttonObject.transform);
                labelText.gameObject.name = "Label";
            }
            else
            {
                GameObject labelObject = new GameObject(
                    "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                labelObject.transform.SetParent(buttonObject.transform, false);
                labelText = labelObject.GetComponent<TextMeshProUGUI>();
            }

            labelText.text = label;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.enableAutoSizing = true;
            labelText.fontSizeMin = 14f;
            labelText.fontSizeMax = 26f;
            labelText.raycastTarget = false;

            RectTransform labelRect = labelText.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);

            return button;
        }

        private void PositionWeaponPopup(RectTransform slotRect)
        {
            if (_tooltipPanel == null || slotRect == null) return;

            RectTransform popupRect = _tooltipPanel.transform as RectTransform;
            RectTransform parentRect = popupRect != null ? popupRect.parent as RectTransform : null;
            if (popupRect == null || parentRect == null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, slotRect.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, screenPoint, eventCamera, out Vector2 localPoint))
            {
                return;
            }

            float gap = 16f;
            float halfPopupWidth = popupRect.rect.width * 0.5f;
            float halfPopupHeight = popupRect.rect.height * 0.5f;
            float halfSlotWidth = slotRect.rect.width * 0.5f;

            float rightPosition = localPoint.x + halfSlotWidth + halfPopupWidth + gap;
            float leftPosition = localPoint.x - halfSlotWidth - halfPopupWidth - gap;
            localPoint.x = rightPosition + halfPopupWidth <= parentRect.rect.xMax
                ? rightPosition
                : leftPosition;

            localPoint.x = Mathf.Clamp(
                localPoint.x,
                parentRect.rect.xMin + halfPopupWidth,
                parentRect.rect.xMax - halfPopupWidth);
            localPoint.y = Mathf.Clamp(
                localPoint.y,
                parentRect.rect.yMin + halfPopupHeight,
                parentRect.rect.yMax - halfPopupHeight);

            popupRect.anchoredPosition = localPoint;
        }

        #endregion

        #region Display Updates

        private void UpdateWaveDisplay()
        {
            if (_waveText != null && _waveManager != null)
                _waveText.text = $"Wave {_waveManager.CurrentWave} Cleared";
        }

        private void UpdateCurrencyDisplay(int currentCurrency)
        {
            if (_currencyText != null) _currencyText.text = $" {currentCurrency} G";
            UpdateRerollButtonDisplay();

            // 모든 상점 아이템 슬롯에 대해 현재 소지금과 비교하여 텍스트 색상을 바꿉니다.
            if (_itemSlots != null)
            {
                foreach (var slot in _itemSlots)
                {
                    if (slot != null && slot.gameObject.activeSelf)
                    {
                        slot.UpdateAffordability(currentCurrency);
                    }
                }
            }
        }

        private void UpdateRerollButtonDisplay()
        {
            if (_rerollCostText != null) _rerollCostText.text = $"Reroll ({_currentRerollCost} G)";
            if (_rerollButton != null && _player != null)
                _rerollButton.interactable = _player.Currency >= _currentRerollCost;
        }

        private void UpdateEquippedWeaponsDisplay()
        {
            if (_player == null) return;
            for (int i = 0; i < _equippedWeaponSlots.Length; i++)
            {
                if (_equippedWeaponSlots[i] == null) continue;

                WeaponData weapon = (i < _player.WeaponInfo.Length) ? _player.WeaponInfo[i] : null;
                int level = (weapon != null && i < _player.WeaponLevels.Length) ? _player.WeaponLevels[i] : 1;

                _equippedWeaponSlots[i].Setup(weapon, level, i, OnClickWeaponSlot);
            }
        }

        #endregion
    }
}
