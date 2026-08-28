using UnityEngine;

namespace ProjectOverdrive.Data
{
    [CreateAssetMenu(fileName = "data_player_default", menuName = "ProjectOverdrive/Data/Player Data", order = 0)]
    public class PlayerData : ScriptableObject
    {
        [Header("Base Health")]
        [SerializeField] private int _baseMaxHp = 100;

        [Header("Base Movement")]
        [SerializeField] private float _baseMoveSpeed = 5.0f;

        [Header("Base Combat Multipliers")]
        [Tooltip("기본 공격 속도 계수 (1.0 = 100%)")]
        [SerializeField] private float _baseAttackSpeed = 1.0f;

        [Tooltip("데미지 배율 (1.0 = 100%)")]
        [SerializeField] private float _baseDmgMulti = 1.0f;

        [Tooltip("추가 공격 사거리 (반경 가산치)")]
        [SerializeField] private float _baseAdditionalRange = 0.0f;

        [Header("Utility")]
        [Tooltip("경험치 및 아이템 자석 흡수 반경")]
        [SerializeField] private float _baseMagnetRange = 2.5f;

        [Header("Initial Weapons (Max 6)")]
        [SerializeField] private WeaponData[] _initialWeapons = new WeaponData[6];

        // 외부 읽기 전용 프로퍼티
        public int BaseMaxHp => _baseMaxHp;
        public float BaseMoveSpeed => _baseMoveSpeed;
        public float BaseAttackSpeed => _baseAttackSpeed;
        public float BaseDmgMulti => _baseDmgMulti;
        public float BaseAdditionalRange => _baseAdditionalRange;
        public float BaseMagnetRange => _baseMagnetRange;
        public WeaponData[] InitialWeapons => _initialWeapons;
    }
}