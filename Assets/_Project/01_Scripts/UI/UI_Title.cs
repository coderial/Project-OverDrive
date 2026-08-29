using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ProjectOverdrive.UI
{
    public class UI_Title : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _exitButton;

        [Header("Scene Settings")]
        [Tooltip("Start 버튼???�르�??�어�?메인 게임 ???�름")]
        [SerializeField] private string _nextSceneName = "01_Ready"; // ?�재 메인 ?�업 중인 ???�름

        private void Awake()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(OnClickStart);

            if (_exitButton != null)
                _exitButton.onClick.AddListener(OnClickExit);
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