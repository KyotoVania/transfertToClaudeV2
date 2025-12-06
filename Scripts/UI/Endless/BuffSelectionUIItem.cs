using System;
using ScriptableObjects.Endless;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI.Endless
{
    /// <summary>
    /// Représente un item individuel dans l'UI de sélection de buff.
    /// Se configure à partir d'un PassiveBuff_SO et gère son bouton de sélection.
    /// </summary>
    public class BuffSelectionUIItem : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image iconImage;
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private Button selectButton;

        private PassiveBuff_SO _buff;
        private Action<PassiveBuff_SO> _onSelect;

        /// <summary>
        /// Configure l'item avec un buff et le callback de sélection.
        /// </summary>
        public void Setup(PassiveBuff_SO buff, Action<PassiveBuff_SO> onSelect)
        {
            _buff = buff;
            _onSelect = onSelect;

            if (iconImage != null)
            {
                iconImage.sprite = _buff != null ? _buff.icon : null;
                iconImage.gameObject.SetActive(iconImage.sprite != null);
            }

            if (titleText != null)
            {
                titleText.text = _buff != null ? _buff.buffName : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = _buff != null ? _buff.GetDescription() : string.Empty;
            }

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(OnSelectClicked);
            }
        }

        private void OnSelectClicked()
        {
            _onSelect?.Invoke(_buff);
        }
    }
}