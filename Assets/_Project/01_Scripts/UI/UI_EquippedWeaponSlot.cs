using UnityEngine;
using UnityEngine.UI;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.UI
{
    public class UI_EquippedWeaponSlot : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _emptySlotBackground;

        // 레벨을 함께 전달받아 UI 이미지를 결정합니다.
        public void SetWeapon(WeaponData weapon, int level = 1)
        {
            if (weapon != null)
            {
                // 1. 레벨에 맞는 인게임 스프라이트를 먼저 가져옵니다.
                Sprite displaySprite = weapon.GetSpriteForLevel(level);

                // 2. 만약 레벨별 스프라이트가 비어있다면, 기본 Icon을 대체제로 사용합니다.
                if (displaySprite == null)
                {
                    displaySprite = weapon.Icon;
                }

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
    }
}