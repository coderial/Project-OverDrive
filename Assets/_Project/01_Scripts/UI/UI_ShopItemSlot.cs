using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.UI
{
    public class UI_ShopItemSlot : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _statsText;
        [SerializeField] private TextMeshProUGUI _priceText;
        [SerializeField] private Button _buyButton;
        [SerializeField] private GameObject _soldOutOverlay;

        private WeaponData _currentWeapon;
        private int _price;
        private Action<UI_ShopItemSlot, WeaponData, int> _onBuyCallback;

        public bool IsSoldOut { get; private set; }

        private void Awake()
        {
            if (_buyButton != null)
            {
                _buyButton.onClick.AddListener(OnClickBuy);
            }
        }

        /// <summary>
        /// 슬롯 데이터 세팅
        /// </summary>
        public void Setup(WeaponData weapon, int price, Action<UI_ShopItemSlot, WeaponData, int> onBuyCallback)
        {
            _currentWeapon = weapon;
            _price = price;
            _onBuyCallback = onBuyCallback;
            IsSoldOut = false;

            if (_soldOutOverlay != null) _soldOutOverlay.SetActive(false);
            if (_buyButton != null) _buyButton.interactable = true;

            if (_currentWeapon != null)
            {
                if (_nameText != null) _nameText.text = _currentWeapon.WeaponName;
                if (_iconImage != null)
                {
                    _iconImage.sprite = _currentWeapon.Icon;
                    _iconImage.enabled = _currentWeapon.Icon != null;
                }
                if (_statsText != null)
                {
                    _statsText.text = $"공격력: {_currentWeapon.BaseDamage:F0}\n" +
                                      $"공격주기: {_currentWeapon.BaseAttackSpeed:F1}s\n" +
                                      $"사거리: {_currentWeapon.BaseAttackRange:F1}";
                }
                if (_priceText != null) _priceText.text = $"{_price} G";
            }
        }

        private void OnClickBuy()
        {
            if (IsSoldOut || _currentWeapon == null) return;
            _onBuyCallback?.Invoke(this, _currentWeapon, _price);
        }

        /// <summary>
        /// 구매 완료 상태로 변경
        /// </summary>
        public void SetSoldOut()
        {
            IsSoldOut = true;
            if (_soldOutOverlay != null) _soldOutOverlay.SetActive(true);
            if (_buyButton != null) _buyButton.interactable = false;
            if (_priceText != null) _priceText.text = "품절";
        }
    }
}