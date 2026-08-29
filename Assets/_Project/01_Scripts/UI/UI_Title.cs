using ProjectOverdrive.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectOverdrive.UI
{
    public class UI_Title : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _optionButton;
        [SerializeField] private Button _exitButton;
        [SerializeField] private GameObject _optionPanel;

        [Header("Scene Settings")]
        [SerializeField] private string _nextSceneName = "01_Ready"; 
        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(OnClickStart);

            if (_exitButton != null)
                _exitButton.onClick.AddListener(OnClickExit);

            _optionButton?.onClick.AddListener(OnClickOption);
            _optionPanel?.SetActive(false);
        }

        private void Start()
        {
            SoundManager.Instance.PlayBgm("MainBGM");
        }
        private void OnClickOption()
        {
            _optionPanel?.SetActive(true);
        }

        private void OnClickStart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(_nextSceneName);
        }

        private void OnClickExit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}