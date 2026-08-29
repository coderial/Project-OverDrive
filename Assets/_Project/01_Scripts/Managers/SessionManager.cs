using UnityEngine;
using ProjectOverdrive.Data;

namespace ProjectOverdrive.Managers
{
    public class SessionManager : MonoBehaviour
    {
        public static SessionManager Instance { get; private set; }

        public PlayerData SelectedPlayer { get; set; }
        public WeaponData SelectedWeapon { get; set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
