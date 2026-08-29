using UnityEngine;

namespace ProjectOverdrive.Data
{
    [CreateAssetMenu(fileName = "data_player_default", menuName = "ProjectOverdrive/Data/Player Data", order = 0)]
    public class PlayerData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("캐릭터 이름")]
        [SerializeField] private string _characterName = "Unknown";
        [Tooltip("캐릭터 프리팹 (실제 씬에 소환될 대상)")]
        [SerializeField] private GameObject _playerPrefab;

        [Header("Base Health")]
        [SerializeField] private int _baseMaxHp = 100;

        [Header("Base Movement")]
        [SerializeField] private float _baseMoveSpeed = 5.0f;

        [Header("Base Combat Multipliers")]
        [Tooltip("기본 공격 속도 계수 (1.0 = 100%)")]
        [SerializeField] private float _baseAttackSpeed = 1.0f;

        [Tooltip("대미지 배율 (1.0 = 100%)")]
        [SerializeField] private float _baseDmgMulti = 1.0f;

        [Tooltip("추가 공격 거리(반경 가중치)")]
        [SerializeField] private float _baseAdditionalRange = 0.0f;

        [Header("Utility")]
        [Tooltip("경험치/아이템 자석 흡수 반경")]
        [SerializeField] private float _baseMagnetRange = 2.5f;

        [Header("Initial Weapons (Max 6)")]
        [SerializeField] private WeaponData[] _initialWeapons = new WeaponData[6];

        // 프로퍼티
        public string CharacterName => _characterName;
        public GameObject PlayerPrefab => _playerPrefab;
        public int BaseMaxHp => _baseMaxHp;
        public float BaseMoveSpeed => _baseMoveSpeed;
        public float BaseAttackSpeed => _baseAttackSpeed;
        public float BaseDmgMulti => _baseDmgMulti;
        public float BaseAdditionalRange => _baseAdditionalRange;
        public float BaseMagnetRange => _baseMagnetRange;
        public WeaponData[] InitialWeapons => _initialWeapons;
    }
}
