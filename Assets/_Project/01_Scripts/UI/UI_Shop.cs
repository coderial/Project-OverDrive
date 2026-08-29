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
        [SerializeField] private int _baseItemPrice = 15;
        [SerializeField] private int _baseRerollCost = 5;

        private PlayerController _player;
        private WaveManager _waveManager;
        private int _currentRerollCost;

        private void Awake()
        {
            if (_rerollButton != null) _rerollButton.onClick.AddListener(OnClickReroll);
            if (_nextWaveButton != null) _nextWaveButton.onClick.AddListener(OnClickNextWave);
        }

        public void OpenShop(PlayerController player, WaveManager waveManager)
        {
            _player = player;
            _waveManager = waveManager;

            gameObject.SetActive(true);
            _currentRerollCost = _baseRerollCost;

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

            int waveNumber = _waveManager != null ? _waveManager.CurrentWave : 1;
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

                int price = _baseItemPrice + (waveNumber * 2);
                _itemSlots[i].Setup(randomWeapon, price, OnBuyItem);
            }

            UpdateRerollButtonDisplay();
        }

        private void OnBuyItem(UI_ShopItemSlot slot, WeaponData weapon, int price)
        {
            if (_player == null) return;
            if (_player.Currency < price)
            {
                Debug.Log("<color=red>[UI_Shop]</color> 재화가 부족합니다!");
                return;
            }

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
                Debug.Log($"<color=green>[UI_Shop]</color> '{weapon.WeaponName}' 레벨업! (현재 Lv.{_player.WeaponLevels[upgradeIndex]})");
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

            if (emptySlotIndex == -1)
            {
                Debug.Log("<color=yellow>[UI_Shop]</color> 무기 슬롯이 가득 찼으며, 업그레이드할 수 있는 동일 무기도 없습니다!");
                return;
            }

            _player.SpendCurrency(price);
            _player.EquipWeapon(emptySlotIndex, weapon);
            slot.SetSoldOut();
            UpdateEquippedWeaponsDisplay();
            Debug.Log($"<color=green>[UI_Shop]</color> '{weapon.WeaponName}' 구매 성공! (슬롯 [{emptySlotIndex}])");
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

                // [수정됨] 무기의 현재 레벨 정보를 추출하여 슬롯에 함께 전달합니다.
                int level = (weapon != null && i < _player.WeaponLevels.Length) ? _player.WeaponLevels[i] : 1;

                _equippedWeaponSlots[i].SetWeapon(weapon, level);
            }
        }

        #endregion
    }
}