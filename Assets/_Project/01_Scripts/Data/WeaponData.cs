using UnityEngine;
using UnityEngine.Serialization;

namespace ProjectOverdrive.Data
{
    public enum WeaponAttackType
    {
        Thrust, // 찌르기
        Swing   // 휘두르기(휩쓰기)
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

        [Header("Economy (상점)")]
        [Tooltip("상점에서 구매할 때 기본 가격")]
        [SerializeField] private int _purchasePrice = 50;
        [Tooltip("상점에 되팔 때 기본 가격")]
        [SerializeField] private int _sellPrice = 25;

        [Header("Base Combat Stats")]
        [Tooltip("공격 방식 (찌르기 / 휘두르기)")]
        [SerializeField] private WeaponAttackType _attackType = WeaponAttackType.Thrust;

        [Tooltip("기본 공격력")]
        [SerializeField] private float _baseDamage = 10.0f;

        [Tooltip("기본 공격 속도 계수")]
        [SerializeField] private float _baseAttackSpeed = 1.0f;

        [Header("Distance & Area (사거리 및 타격범위)")]
        [Tooltip("공격 거리: 얼마나 멀리 있는 적을 감지하고 공격할 것인가")]
        [FormerlySerializedAs("_baseAttackRange")]
        [SerializeField] private float _baseAttackDistance = 1.5f;

        [Tooltip("공격 범위: 무기가 공격 시 실제 피해가 입혀지는 피격 판정 넓이")]
        [SerializeField] private float _baseHitArea = 0.8f;

        [Header("Impact")]
        [Tooltip("타격 시 적을 밀쳐내는 넉백 강도")]
        [SerializeField] private float _baseKnockback = 2.0f;

        // 프로퍼티
        public string WeaponName => _weaponName;
        public Sprite Icon => _icon;
        public GameObject WeaponPrefab => _weaponPrefab;

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
            if (level >= 3 && _level3Sprite != null) return _level3Sprite;
            if (level == 2 && _level2Sprite != null) return _level2Sprite;
            return _level1Sprite;
        }
    }
}