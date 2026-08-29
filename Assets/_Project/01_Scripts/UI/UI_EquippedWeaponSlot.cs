using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ProjectOverdrive.Data;
using System;

namespace ProjectOverdrive.UI
{
    public class UI_EquippedWeaponSlot : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _emptySlotBackground;

        private int _slotIndex;
        private WeaponData _currentWeapon;

        private Action<int, RectTransform> _onClick;

        public void Setup(WeaponData weapon, int level, int slotIndex, Action<int, RectTransform> onClick)
        {
            _currentWeapon = weapon;
            _slotIndex = slotIndex;
            _onClick = onClick;

            if (weapon != null)
            {
                Sprite displaySprite = weapon.GetSpriteForLevel(level);
                if (displaySprite == null) displaySprite = weapon.Icon;

                if (displaySprite != null)
                {
                    _iconImage.sprite = displaySprite;
                    _iconImage.enabled = true;
                }
                else
                {
                    _iconImage.sprite = null;
                    _iconImage.enabled = false;
                }
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_currentWeapon != null && eventData.button == PointerEventData.InputButton.Left)
            {
                _onClick?.Invoke(_slotIndex, transform as RectTransform);
            }
            else if (_currentWeapon == null && eventData.button == PointerEventData.InputButton.Left)
            {
                _onClick?.Invoke(-1, null);
            }
        }
    }
}
