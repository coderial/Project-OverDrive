using UnityEngine;
using TMPro;
using ProjectOverdrive.Managers;

namespace ProjectOverdrive.UI
{
    public class UI_WaveTimer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("씬에 배치된 WaveManager를 연결하세요")]
        [SerializeField] private WaveManager _waveManager;

        [Tooltip("시간을 표시할 TextMeshPro를 연결하세요")]
        [SerializeField] private TextMeshProUGUI _timerText;

        [Header("Settings")]
        [SerializeField] private Color _normalColor = Color.white;
        [SerializeField] private Color _dangerColor = Color.red;

        private void Update()
        {
            if (_waveManager == null || _timerText == null) return;

            // 웨이브 진행 중일 때만 시간 표시
            if (_waveManager.CurrentStatus == WaveStatus.Spawn_Progress)
            {
                float remain = _waveManager.RemainingTime;

                // 올림 처리하여 초 단위 표시 (예: 19.1초 -> 20초)
                int seconds = Mathf.CeilToInt(remain);
                _timerText.text = seconds.ToString();

                // 10초 이하일 때 빨간색으로 경고 (Brotato 연출)
                if (seconds <= 10)
                {
                    _timerText.color = _dangerColor;
                }
                else
                {
                    _timerText.color = _normalColor;
                }
            }
            else if (_waveManager.CurrentStatus == WaveStatus.Preparation)
            {
                _timerText.text = "READY";
                _timerText.color = _normalColor;
            }
            else
            {
                // 상점이나 클리어 상태일 때는 텍스트 지움
                _timerText.text = "";
            }
        }
    }
}