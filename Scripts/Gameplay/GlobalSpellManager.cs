namespace Gameplay
{
    using UnityEngine;
using System.Collections.Generic;
using ScriptableObjects;
using System;

/// <summary>
/// Gère le chargement, les cooldowns et l'exécution des sorts globaux.
/// Centralise toute la logique liée aux sorts pour alléger le GameplayManager.
/// </summary>
public class GlobalSpellManager : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Chemin dans le dossier Resources pour charger les sorts globaux.")]
    [SerializeField] private string globalSpellsResourcePath = "Data/GlobalSpells";

    // Références aux managers
    private GoldController _goldController;
    private MusicManager _musicManager;

    // Données des sorts
    private List<GlobalSpellData_SO> _availableSpells = new List<GlobalSpellData_SO>();
    private Dictionary<string, float> _spellCooldowns = new Dictionary<string, float>();

    // Propriétés publiques pour l'UI et autres systèmes
    public IReadOnlyList<GlobalSpellData_SO> AvailableSpells => _availableSpells;
    public IReadOnlyDictionary<string, float> SpellCooldowns => _spellCooldowns;

    public static event Action<IReadOnlyList<GlobalSpellData_SO>> OnGlobalSpellsLoaded;

    void Awake()
    {
        // Initialisation des références via les singletons
        _goldController = GoldController.Instance;
        _musicManager = MusicManager.Instance;

        if (_goldController == null)
            Debug.LogError("[GlobalSpellManager] GoldController.Instance est introuvable !", this);
        if (_musicManager == null)
            Debug.LogError("[GlobalSpellManager] MusicManager.Instance est introuvable !", this);
    }

    /// <summary>
    /// Charge tous les ScriptableObjects de sorts depuis le chemin spécifié.
    /// Doit être appelée par le GameplayManager lors de l'initialisation du niveau.
    /// </summary>
    public void LoadSpells()
    {
        GlobalSpellData_SO[] spells = Resources.LoadAll<GlobalSpellData_SO>(globalSpellsResourcePath);
        _availableSpells = new List<GlobalSpellData_SO>(spells);
        Debug.Log($"[GlobalSpellManager] Chargé {_availableSpells.Count} sorts globaux depuis '{globalSpellsResourcePath}'.");
        
        // Notifie l'UI ou d'autres systèmes que les sorts sont prêts
        OnGlobalSpellsLoaded?.Invoke(_availableSpells);
    }

    /// <summary>
    /// Tente d'exécuter un sort global. Gère toutes les vérifications.
    /// </summary>
    /// <param name="spellData">Le sort à lancer.</param>
    /// <param name="perfectCount">Le nombre d'inputs parfaits pour les bonus.</param>
    public void TryExecuteSpell(GlobalSpellData_SO spellData, int perfectCount)
    {
        if (spellData == null)
        {
            Debug.LogWarning("[GlobalSpellManager] Tentative d'exécution d'un sort nul.");
            return;
        }

        // 1. Vérification du Cooldown
        if (_spellCooldowns.ContainsKey(spellData.SpellID) && Time.time < _spellCooldowns[spellData.SpellID])
        {
            Debug.Log($"[GlobalSpellManager] Le sort '{spellData.DisplayName}' est en cooldown.");
            // Feedback sonore négatif possible ici
            return;
        }
        
        // 2. Vérification de toutes les ressources AVANT de les dépenser
        bool hasSufficientMomentum = MomentumManager.Instance.CurrentCharges >= spellData.MomentumCost;
        bool hasSufficientGold = _goldController.GetCurrentGold() >= spellData.GoldCost;

        if (!hasSufficientMomentum || !hasSufficientGold)
        {
            Debug.LogWarning($"[GlobalSpellManager] Ressources insuffisantes pour lancer {spellData.DisplayName}. Momentum: {hasSufficientMomentum}, Gold: {hasSufficientGold}");
            TriggerInsufficientResourcesFeedback(hasSufficientMomentum, hasSufficientGold);
            return;
        }

        // 3. Dépenser les ressources (maintenant qu'on sait qu'on a tout)
        if (!MomentumManager.Instance.TrySpendMomentum(spellData.MomentumCost))
        {
            Debug.LogError($"[GlobalSpellManager] Erreur critique : échec de dépense de momentum après vérification réussie.");
            return;
        }
        
        _goldController.RemoveGold(spellData.GoldCost);
        Debug.Log($"[GlobalSpellManager] Ressources dépensées : {spellData.MomentumCost} momentum, {spellData.GoldCost} gold.");

        // 4. Exécuter l'effet du sort
        if (spellData.SpellEffect != null)
        {
            // L'effet du sort est un scriptable object, il est autonome.
            // On lui passe le GameObject du manager pour le contexte (ex: coroutines, sons).
            spellData.SpellEffect.ExecuteEffect(this.gameObject, perfectCount);
            Debug.Log($"[GlobalSpellManager] Sort '{spellData.DisplayName}' exécuté.");

            // Jouer le son d'activation si défini
            if (spellData.ActivationSound != null && spellData.ActivationSound.IsValid())
            {
                spellData.ActivationSound.Post(gameObject);
            }
        }

        // 5. Mettre le sort en cooldown
        float cooldownInSeconds = spellData.BeatCooldown * (_musicManager?.GetBeatDuration() ?? 1.0f);
        _spellCooldowns[spellData.SpellID] = Time.time + cooldownInSeconds;
        Debug.Log($"[GlobalSpellManager] Cooldown pour {spellData.DisplayName} défini à {cooldownInSeconds:F2} secondes.");
    }

    /// <summary>
    /// Déclenche les feedbacks visuels et audio pour les ressources insuffisantes
    /// </summary>
    /// <param name="hasSufficientMomentum">Indique si le momentum est suffisant</param>
    /// <param name="hasSufficientGold">Indique si l'or est suffisant</param>
    private void TriggerInsufficientResourcesFeedback(bool hasSufficientMomentum, bool hasSufficientGold)
    {
        // Déclencher l'événement audio via le SequenceController
        SequenceController sequenceController = FindFirstObjectByType<SequenceController>();
        if (sequenceController != null)
        {
            sequenceController.PlayInsufficientResourcesSound();
        }

        // Déclencher les feedbacks visuels seulement pour les ressources insuffisantes
        if (!hasSufficientMomentum)
        {
            MomentumDisplay momentumDisplay = FindFirstObjectByType<MomentumDisplay>();
            if (momentumDisplay != null)
            {
                momentumDisplay.ShowInsufficientMomentumFeedback();
            }
        }

        if (!hasSufficientGold)
        {
            GoldUI goldUI = FindFirstObjectByType<GoldUI>();
            if (goldUI != null)
            {
                goldUI.ShowInsufficientGoldFeedback();
            }
        }
    }
}
}