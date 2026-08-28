using UnityEngine;

namespace ProjectOverdrive.Data
{
    [CreateAssetMenu(fileName = "data_weapon_default", menuName = "ProjectOverdrive/Data/Weapon Data", order = 1)]
    public class WeaponData : ScriptableObject
    {
        [Header("Weapon Identity")]
        [SerializeField] private string _weaponName = "Rusty Sword";
        [SerializeField] private Sprite _icon;
        [SerializeField] private GameObject _weaponPrefab;

        [Header("Base Combat Stats")]
        [Tooltip("기본 공격력")]
        [SerializeField] private float _baseDamage = 10.0f;

        [Tooltip("기본 공격 속도 계수")]
        [SerializeField] private float _baseAttackSpeed = 1.0f;

        [Tooltip("기본 사거리")]
        [SerializeField] private float _baseAttackRange = 1.5f;

        [Tooltip("적 피격 시 넉백 강도")]
        [SerializeField] private float _baseKnockback = 2.0f;

        // 프로퍼티
        public string WeaponName => _weaponName;
        public Sprite Icon => _icon;
        public GameObject WeaponPrefab => _weaponPrefab;
        public float BaseDamage => _baseDamage;
        public float BaseAttackSpeed => _baseAttackSpeed;
        public float AttackSpeed => _baseAttackSpeed;
        public float BaseAttackRange => _baseAttackRange;
        public float AttackRange => _baseAttackRange;
        public float BaseKnockback => _baseKnockback;
        public float Knockback => _baseKnockback;
    }
}