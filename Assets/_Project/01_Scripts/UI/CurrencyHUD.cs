using System.Globalization;
using ProjectOverdrive.Controllers;
using TMPro;
using UnityEngine;

namespace ProjectOverdrive.UI
{
    [DisallowMultipleComponent]
    public sealed class CurrencyHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text amountText;

        private PlayerController _player;

        private void Awake()
        {
            RefreshAmount(0);
        }

        private void OnEnable()
        {
            BindToPlayer();
        }

        private void Update()
        {
            if (_player == null)
            {
                BindToPlayer();
            }
        }

        private void OnDisable()
        {
            UnbindFromPlayer();
        }

        private void BindToPlayer()
        {
            PlayerController foundPlayer = FindFirstObjectByType<PlayerController>();
            if (foundPlayer == null || foundPlayer == _player)
            {
                return;
            }

            UnbindFromPlayer();
            _player = foundPlayer;
            _player.OnCurrencyChanged += RefreshAmount;
            RefreshAmount(_player.Currency);
        }

        private void UnbindFromPlayer()
        {
            if (_player != null)
            {
                _player.OnCurrencyChanged -= RefreshAmount;
            }

            _player = null;
        }

        private void RefreshAmount(int amount)
        {
            if (amountText != null)
            {
                amountText.text = amount.ToString("N0", CultureInfo.InvariantCulture);
            }
        }
    }
}
