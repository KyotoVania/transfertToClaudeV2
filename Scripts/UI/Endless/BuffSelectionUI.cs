using System.Collections.Generic;
using Gameplay;
using ScriptableObjects.Endless;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Endless
{
    /// <summary>
    /// UI de sélection des buffs roguelike (mode Endless).
    /// Permet d'afficher des candidats et d'en appliquer un au choix du joueur.
    /// </summary>
    public class BuffSelectionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject canvasRoot;
        [SerializeField] private Transform itemsParent;
        [SerializeField] private BuffSelectionUIItem buffItemPrefab;

        private readonly List<BuffSelectionUIItem> _spawnedItems = new List<BuffSelectionUIItem>(3);

        /// <summary>
        /// Affiche l'interface de sélection avec les buffs candidats.
        /// </summary>
        /// <param name="candidates">Liste des buffs à afficher.</param>
        public void ShowSelection(List<PassiveBuff_SO> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                Debug.LogWarning("[BuffSelectionUI] Aucun buff fourni à l'affichage.");
                return;
            }

            if (buffItemPrefab == null)
            {
                Debug.LogWarning("[BuffSelectionUI] Aucun prefab d'item assigné.");
                return;
            }

            if (itemsParent == null)
            {
                Debug.LogWarning("[BuffSelectionUI] Aucun conteneur d'items assigné.");
                return;
            }

            ClearItems();

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.PauseGame();
            }

            if (canvasRoot != null)
            {
                canvasRoot.SetActive(true);
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                var buff = candidates[i];
                if (buff == null) continue;

                var item = Instantiate(buffItemPrefab, itemsParent);
                item.Setup(buff, SelectBuff);
                _spawnedItems.Add(item);
            }
        }

        /// <summary>
        /// Sélectionne un buff et l'ajoute via le GameplayManager.
        /// </summary>
        public void SelectBuff(PassiveBuff_SO buff)
        {
            if (buff == null)
            {
                Debug.LogWarning("[BuffSelectionUI] Buff sélectionné nul.");
                return;
            }

            if (GameplayManager.Instance != null)
            {
                GameplayManager.Instance.AddRoguelikeBuff(buff);
            }
            else
            {
                Debug.LogWarning("[BuffSelectionUI] GameplayManager.Instance est null, impossible d'ajouter le buff.");
            }

            Close();
        }

        /// <summary>
        /// Ferme l'UI et reprend le jeu.
        /// </summary>
        public void Close()
        {
            if (canvasRoot != null)
            {
                canvasRoot.SetActive(false);
            }

            ClearItems();

            if (PauseManager.Instance != null)
            {
                PauseManager.Instance.ResumeGame();
            }

            // Notifie le EndlessGameManager que la phase de récompense est terminée.
            if (Managers.EndlessGameManager.Instance != null)
            {
                Managers.EndlessGameManager.Instance.ResumeAfterReward();
            }
        }

        private void ClearItems()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null)
                {
                    Destroy(item.gameObject);
                }
            }

            _spawnedItems.Clear();
        }
    }
}