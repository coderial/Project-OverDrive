using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectOverdrive.Controllers;
using ProjectOverdrive.Data;

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

        /// <summary>
        /// 상점 오픈 (WaveManager가 호출)
        /// </summary>
        public void OpenShop(PlayerController player, WaveManager waveManager)
        {
            _player = player;
            _waveManager = waveManager;

            gameObject.SetActive(true);
            _currentRerollCost = _baseRerollCost;

            // 플레이어 재화 변경 이벤트 구독
            if (_player != null)
            {
                _player.OnCurrencyChanged += UpdateCurrencyDisplay;
            }

            UpdateWaveDisplay();
            UpdateCurrencyDisplay(_player != null ? _player.Currency : 0);
            UpdateEquippedWeaponsDisplay();
            RollShopItems();
        }

        /// <summary>
        /// 상점 닫기 및 다음 웨이브 시작
        /// </summary>
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

        /// <summary>
        /// 4개의 상점 아이템 무작위 생성
        /// </summary>
        private void RollShopItems()
        {
            if (_weaponPool == null || _weaponPool.Count == 0)
            {
                Debug.LogWarning("[UI_Shop] Weapon Pool이 비어 있습니다.");
                return;
            }

            int waveNumber = _waveManager != null ? _waveManager.CurrentWave : 1;

            for (int i = 0; i < _itemSlots.Length; i++)
            {
                if (_itemSlots[i] == null) continue;

                // 무작위 무기 선정
                WeaponData randomWeapon = _weaponPool[Random.Range(0, _weaponPool.Count)];

                // 웨이브에 따른 가격 계산 (기본가 + 웨이브당 +2)
                int price = _baseItemPrice + (waveNumber * 2);

                _itemSlots[i].Setup(randomWeapon, price, OnBuyItem);
            }

            UpdateRerollButtonDisplay();
        }

        private void OnBuyItem(UI_ShopItemSlot slot, WeaponData weapon, int price)
        {
            if (_player == null) return;

            // 1. 재화 검사
            if (_player.Currency < price)
            {
                Debug.Log("<color=red>[UI_Shop]</color> 재화가 부족합니다!");
                return;
            }

            // 2. 무기 빈 슬롯 검사 (최대 6개)
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
                Debug.Log("<color=yellow>[UI_Shop]</color> 무기 슬롯(6개)이 가득 찼습니다!");
                return;
            }

            // 3. 재화 차감 및 무기 장착
            _player.SpendCurrency(price);
            _player.EquipWeapon(emptySlotIndex, weapon);

            // 4. 슬롯 상태 갱신
            slot.SetSoldOut();
            UpdateEquippedWeaponsDisplay();
            Debug.Log($"<color=green>[UI_Shop]</color> '{weapon.WeaponName}' 구매 성공! (슬롯 [{emptySlotIndex}])");
        }

        private void OnClickReroll()
        {
            if (_player == null || _player.Currency < _currentRerollCost)
            {
                Debug.Log("<color=red>[UI_Shop]</color> 리롤에 필요한 재화가 부족합니다!");
                return;
            }

            _player.SpendCurrency(_currentRerollCost);
            _currentRerollCost += 2; // 리롤할 때마다 비용 증가 (Brotato 공식)
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
            {
                _waveText.text = $"Wave {_waveManager.CurrentWave} Cleared";
            }
        }

        private void UpdateCurrencyDisplay(int currentCurrency)
        {
            if (_currencyText != null)
            {
                _currencyText.text = $" {currentCurrency} G";
            }
            UpdateRerollButtonDisplay();
        }

        private void UpdateRerollButtonDisplay()
        {
            if (_rerollCostText != null)
            {
                _rerollCostText.text = $"Reroll ({_currentRerollCost} G)";
            }
            if (_rerollButton != null && _player != null)
            {
                _rerollButton.interactable = _player.Currency >= _currentRerollCost;
            }
        }

        private void UpdateEquippedWeaponsDisplay()
        {
            if (_player == null) return;

            for (int i = 0; i < _equippedWeaponSlots.Length; i++)
            {
                if (_equippedWeaponSlots[i] == null) continue;

                WeaponData weapon = (i < _player.WeaponInfo.Length) ? _player.WeaponInfo[i] : null;
                _equippedWeaponSlots[i].SetWeapon(weapon);
            }
        }

        #endregion
    }
}