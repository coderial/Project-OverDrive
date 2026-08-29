using UnityEngine;
using UnityEngine.UI;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.UI
{
    public class UI_EquippedWeaponSlot : MonoBehaviour
    {
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _emptySlotBackground;

        public void SetWeapon(WeaponData weapon)
        {
            if (weapon != null && weapon.Icon != null)
            {
                _iconImage.sprite = weapon.Icon;
                _iconImage.enabled = true;
            }
            else
            {
                _iconImage.sprite = null;
                _iconImage.enabled = false;
            }
        }
    }
}