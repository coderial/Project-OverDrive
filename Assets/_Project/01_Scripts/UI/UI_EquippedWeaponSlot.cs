using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ProjectOverdrive.Data;
using System;

namespace ProjectOverdrive.UI
{
    public class UI_EquippedWeaponSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _emptySlotBackground;

        private int _slotIndex;
        private WeaponData _currentWeapon;

        // 델리게이트 (UI_Shop과 통신)
        private Action<int> _onRightClick;
        private Action<int> _onHoverEnter;
        private Action _onHoverExit;

        public void Setup(WeaponData weapon, int level, int slotIndex, Action<int> onRightClick, Action<int> onHoverEnter, Action onHoverExit)
        {
            _currentWeapon = weapon;
            _slotIndex = slotIndex;
            _onRightClick = onRightClick;
            _onHoverEnter = onHoverEnter;
            _onHoverExit = onHoverExit;

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

        // 우클릭 판매 감지
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_currentWeapon != null && eventData.button == PointerEventData.InputButton.Right)
            {
                _onRightClick?.Invoke(_slotIndex);
                _onHoverExit?.Invoke(); // 무기가 팔리면 툴팁 강제 제거
            }
        }

        // 마우스 올렸을 때 툴팁 띄우기
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_currentWeapon != null)
            {
                _onHoverEnter?.Invoke(_slotIndex);
            }
        }

        // 마우스 내렸을 때 툴팁 지우기
        public void OnPointerExit(PointerEventData eventData)
        {
            _onHoverExit?.Invoke();
        }
    }
}