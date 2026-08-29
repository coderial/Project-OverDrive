using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectOverdrive.Data
{
    public enum WeaponAttackType
    {
        Thrust,
        Swing
    }

    [CreateAssetMenu(fileName = "data_weapon_default", menuName = "ProjectOverdrive/Data/Weapon Data", order = 1)]
    public class WeaponData : ScriptableObject
    {
        [Header("Weapon Identity")]
        [SerializeField] private string _weaponName = "Rusty Sword";
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _weaponPrefab;

        [Header("Weapon Appearance (Level 1~3)")]
        [Tooltip("Lv.1 무기 외형")]
        [SerializeField] private Sprite _level1Sprite;
        [Tooltip("Lv.2 무기 외형")]
        [SerializeField] private Sprite _level2Sprite;
        [Tooltip("Lv.3 무기 외형")]
        [SerializeField] private Sprite _level3Sprite;

                [Header("VFX")]
        [Tooltip("공격(휘두르기/찌르기) 시 생성될 이펙트 프리팹")]
        [SerializeField] private GameObject _attackEffectPrefab;

        [Header("Economy (상점)")]
        [SerializeField] private int _purchasePrice = 50;
        [SerializeField] private int _sellPrice = 25;

        [Header("Base Combat Stats")]
        [Tooltip("공격 방식 (찌르기/ 휘두르기) - 믹스 활성화 시 기본이 되는 모션")]
        [SerializeField] private WeaponAttackType _attackType = WeaponAttackType.Thrust;

        

        [Header("Combat Numbers")]
        [SerializeField] private float _baseDamage = 10.0f;
        [SerializeField] private float _baseAttackSpeed = 1.0f;

        [Header("Distance & Area (사거리/타격범위)")]
        [FormerlySerializedAs("_baseAttackRange")]
        [SerializeField] private float _baseAttackDistance = 1.5f;
        [SerializeField] private float _baseHitArea = 0.8f;
        [SerializeField] private float _baseKnockback = 2.0f;

        // 프로퍼티
        public string WeaponName => _weaponName;
        public Sprite Icon => _icon;
                public GameObject WeaponPrefab => _weaponPrefab;
        public GameObject AttackEffectPrefab => _attackEffectPrefab;

        public int PurchasePrice => _purchasePrice;
        public int SellPrice => _sellPrice;

        public WeaponAttackType AttackType => _attackType;
        

        public float BaseDamage => _baseDamage;
        public float BaseAttackSpeed => _baseAttackSpeed;
        public float BaseAttackDistance => _baseAttackDistance;
        public float BaseHitArea => _baseHitArea;
        public float BaseKnockback => _baseKnockback;

        public Sprite GetSpriteForLevel(int level)
        {
            switch (level)
            {
                case 1: return _level1Sprite != null ? _level1Sprite : _icon;
                case 2: return _level2Sprite != null ? _level2Sprite : _level1Sprite;
                case 3: return _level3Sprite != null ? _level3Sprite : _level2Sprite;
                default: return _icon;
            }
        }
    }
}