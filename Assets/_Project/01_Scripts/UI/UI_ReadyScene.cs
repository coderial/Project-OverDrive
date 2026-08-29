using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using ProjectOverdrive.Data;
using ProjectOverdrive.Managers;

namespace ProjectOverdrive.UI
{
    public class UI_ReadyScene : MonoBehaviour
    {
        [Header("Scene Settings")]
        [SerializeField] private string _nextSceneName = "PlayerTestScene";

        [Header("Data Pools")]
        [SerializeField] private List<PlayerData> _characterPool = new List<PlayerData>();
        [SerializeField] private List<WeaponData> _weaponPool = new List<WeaponData>();

        [Header("UI Panels")]
        [SerializeField] private GameObject _characterPanel;
        [SerializeField] private GameObject _weaponPanel;

        [Header("UI Grids")]
        [SerializeField] private Transform _characterGrid;
        [SerializeField] private Transform _weaponGrid;

        [Header("Slot Prefabs")]
        [Tooltip("캐릭터 전용 슬롯 프리팹")]
        [SerializeField] private GameObject _characterSlotPrefab;
        [Tooltip("무기 전용 슬롯 프리팹")]
        [SerializeField] private GameObject _weaponSlotPrefab;

        private void Start()
        {
            if (_characterPanel != null) _characterPanel.SetActive(true);
            if (_weaponPanel != null) _weaponPanel.SetActive(false);

            GenerateCharacterSlots();
        }

        private void GenerateCharacterSlots()
        {
            if (_characterGrid == null || _characterSlotPrefab == null) return;
            foreach (Transform child in _characterGrid) Destroy(child.gameObject);

            for (int i = 0; i < _characterPool.Count; i++)
            {
                PlayerData data = _characterPool[i];
                if (data == null || data.PlayerPrefab == null) continue;

                GameObject slot = Instantiate(_characterSlotPrefab, _characterGrid, false);
                slot.SetActive(true);
                
                Transform iconT = slot.transform.Find("Icon");
                Transform textT = slot.transform.Find("Text");

                // 프리팹에 있는 실제 스프라이트를 그대로 가져와서 UI 아이콘으로 사용
                Sprite prefabSprite = data.PlayerPrefab.GetComponentInChildren<SpriteRenderer>()?.sprite;

                if (iconT != null && prefabSprite != null)
                {
                    Image iconImg = iconT.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.sprite = prefabSprite;
                        iconImg.preserveAspect = true; // 비율 유지
                    }
                }
                
                if (textT != null)
                {
                    TextMeshProUGUI txt = textT.GetComponent<TextMeshProUGUI>();
                    if (txt != null) txt.text = data.CharacterName;
                }

                Button btn = slot.GetComponent<Button>();
                if (btn != null)
                {
                    int index = i;
                    btn.onClick.AddListener(() => OnSelectCharacter(index));
                }
            }
        }

        private void OnSelectCharacter(int index)
        {
            if (SessionManager.Instance == null)
            {
                GameObject go = new GameObject("SessionManager");
                go.AddComponent<SessionManager>();
            }

            SessionManager.Instance.SelectedPlayer = _characterPool[index];

            if (_characterPanel != null) _characterPanel.SetActive(false);
            if (_weaponPanel != null) _weaponPanel.SetActive(true);

            GenerateWeaponSlots();
        }

        private void GenerateWeaponSlots()
        {
            if (_weaponGrid == null || _weaponSlotPrefab == null) return;
            foreach (Transform child in _weaponGrid) Destroy(child.gameObject);

            for (int i = 0; i < _weaponPool.Count; i++)
            {
                WeaponData data = _weaponPool[i];
                if (data == null || data.WeaponPrefab == null) continue;

                GameObject slot = Instantiate(_weaponSlotPrefab, _weaponGrid, false);
                slot.SetActive(true); Debug.Log("Spawned weapon slot for: " + data.WeaponName);
                
                Transform iconT = slot.transform.Find("Icon");
                Transform textT = slot.transform.Find("Text");

                // 무기도 마찬가지로 프리팹의 스프라이트를 직접 가져옴 (만약 _icon이 있으면 그걸 우선 사용)
                Sprite displaySprite = data.Icon;
                if (displaySprite == null)
                {
                    displaySprite = data.WeaponPrefab.GetComponentInChildren<SpriteRenderer>()?.sprite;
                }

                if (iconT != null && displaySprite != null)
                {
                    Image iconImg = iconT.GetComponent<Image>();
                    if (iconImg != null)
                    {
                        iconImg.sprite = displaySprite;
                        iconImg.preserveAspect = true;
                    }
                }
                
                if (textT != null)
                {
                    TextMeshProUGUI txt = textT.GetComponent<TextMeshProUGUI>();
                    if (txt != null) txt.text = data.WeaponName;
                }

                Button btn = slot.GetComponent<Button>();
                if (btn != null)
                {
                    int index = i;
                    btn.onClick.AddListener(() => OnSelectWeapon(index));
                }
            }
        }

        private void OnSelectWeapon(int index)
        {
            SessionManager.Instance.SelectedWeapon = _weaponPool[index];
            SceneManager.LoadScene(_nextSceneName);
        }
    }
}
