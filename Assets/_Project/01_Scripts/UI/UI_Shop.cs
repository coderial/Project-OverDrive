using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
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
        [Tooltip("2레벨 무기 판매 시 적용될 원가 대비 보너스(%)")]
        [SerializeField] private float _level2SellBonusPercent = 20f;
        [Tooltip("3레벨 무기 판매 시 적용될 원가 대비 보너스(%)")]
        [SerializeField] private float _level3SellBonusPercent = 30f;

        [Header("Tooltip UI")]
        [Tooltip("판매가 표시용 툴팁 패널 (캔버스에 새로 만들어 연결)")]
        [SerializeField] private GameObject _tooltipPanel;
        [Tooltip("판매가 텍스트")]
        [SerializeField] private TextMeshProUGUI _tooltipText;

        private PlayerController _player;
        private WaveManager _waveManager;
        private int _currentRerollCost;

        private void Awake()
        {
            if (_rerollButton != null) _rerollButton.onClick.AddListener(OnClickReroll);
            if (_nextWaveButton != null) _nextWaveButton.onClick.AddListener(OnClickNextWave);

            if (_tooltipPanel != null)
            {
                _tooltipPanel.SetActive(false);

                // [해결됨] 툴팁이 마우스를 가려서 깜빡이는 현상을 원천 차단하기 위해 
                // CanvasGroup을 동적으로 추가하고 마우스 감지(Raycast)를 꺼버립니다.
                if (!_tooltipPanel.TryGetComponent<CanvasGroup>(out var canvasGroup))
                {
                    canvasGroup = _tooltipPanel.AddComponent<CanvasGroup>();
                }
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }

        private void Update()
        {
            if (_tooltipPanel != null && _tooltipPanel.activeSelf && Mouse.current != null)
            {
                // [수정됨] 마우스 포인터가 글자를 가리지 않게 우측 하단으로 살짝(Offset) 밀어줍니다.
                Vector2 mousePos = Mouse.current.position.ReadValue();
                _tooltipPanel.transform.position = mousePos + new Vector2(30f, -30f);
            }
        }

        public void OpenShop(PlayerController player, WaveManager waveManager)
        {
            _player = player;
            _waveManager = waveManager;

            gameObject.SetActive(true);
            _currentRerollCost = _baseRerollCost;
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);

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

            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
            gameObject.SetActive(false);

            if (_waveManager != null)
            {
                _waveManager.CloseShop();
            }
        }

        #region Shop Item Rolling (구매 로직)

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
        }

        private void OnBuyItem(UI_ShopItemSlot slot, WeaponData weapon, int price)
        {
            if (_player == null) return;
            if (_player.Currency < price) return;

            int upgradeIndex = -1;
            for (int i = 0; i < _player.WeaponInfo.Length; i++)
            {
                if (_player.WeaponInfo[i] != null && _player.WeaponInfo[i].WeaponName == weapon.WeaponName)
                {
                    if (_player.WeaponLevels[i] < 3)
                    {
                        upgradeIndex = i;
                        break;
                    }
                }
            }

            if (upgradeIndex != -1)
            {
                _player.SpendCurrency(price);
                _player.UpgradeWeapon(upgradeIndex);
                slot.SetSoldOut();
                UpdateEquippedWeaponsDisplay();
                return;
            }

            int emptySlotIndex = -1;
            for (int i = 0; i < _player.WeaponInfo.Length; i++)
            {
                if (_player.WeaponInfo[i] == null)
                {
                    emptySlotIndex = i;
                    break;
                }
            }

            if (emptySlotIndex == -1) return;

            _player.SpendCurrency(price);
            _player.EquipWeapon(emptySlotIndex, weapon);
            slot.SetSoldOut();
            UpdateEquippedWeaponsDisplay();
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

        #region Sell & Tooltip Logic (판매 및 툴팁)

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
        }

        private void OnHoverWeaponEnter(int slotIndex)
        {
            if (_player == null) return;
            WeaponData weapon = _player.WeaponInfo[slotIndex];
            if (weapon == null) return;
            int level = _player.WeaponLevels[slotIndex];
            int finalPrice = GetSellPrice(weapon, level);
            if (_tooltipPanel != null) _tooltipPanel.SetActive(true);
            if (_tooltipText != null)
            {
                // 무기 타입을 판별하여 텍스트 상단에 추가했습니다!
                string typeStr = weapon.AttackType == WeaponAttackType.Thrust ? "찌르기" : "휘두르기";
                _tooltipText.text = $"[타입: {typeStr}]\n우클릭 판매: <color=yellow>+{finalPrice} G</color>";
            }
        }

        private void OnHoverWeaponExit()
        {
            if (_tooltipPanel != null) _tooltipPanel.SetActive(false);
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

                _equippedWeaponSlots[i].Setup(weapon, level, i, OnSellWeapon, OnHoverWeaponEnter, OnHoverWeaponExit);
            }
        }

        #endregion
    }
}