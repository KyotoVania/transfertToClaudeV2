using UnityEngine;
using System.Collections.Generic;
using ScriptableObjects;

/// <summary>
/// Central manager for tutorial sequences in the game.
/// Handles tutorial step progression, UI element visibility control,
/// and coordination between tutorial triggers and game events.
/// Implements singleton pattern for global access and tutorial state management.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    [Header("Debug/Dev")]
    /// <summary>Enable this to completely bypass tutorial in debug mode.</summary>
    [Tooltip("Enable this to completely bypass tutorial in debug mode.")]
    [SerializeField] private bool bypassTutorial = false;

    [Header("Tutorial Configuration")]
    /// <summary>Reference to the tutorial UI manager for display control.</summary>
    [SerializeField] private TutorialUIManager uiManager;
    /// <summary>Tutorial sequence data to play during this level.</summary>
    [SerializeField] private TutorialSequence_SO sequenceToPlay;

    [Header("Tutorial HUD References (Lists)")]
    /// <summary>UI objects related to invocation/summoning mechanics.</summary>
    [SerializeField] private List<GameObject> invocationUIObjects = new List<GameObject>();
    /// <summary>UI objects related to combo system display.</summary>
    [SerializeField] private List<GameObject> comboUIObjects = new List<GameObject>();
    /// <summary>UI objects related to gold/currency display.</summary>
    [SerializeField] private List<GameObject> goldUIObjects = new List<GameObject>();
    /// <summary>UI objects related to units and spells display.</summary>
    [SerializeField] private List<GameObject> unitsAndSpellsUIObjects = new List<GameObject>();
    /// <summary>Singleton instance for global tutorial access.</summary>
    public static TutorialManager Instance { get; private set; }

    /// <summary>
    /// Indicates if a tutorial sequence is currently active.
    /// Can be consulted by other systems (like PlayerBuilding).
    /// </summary>
    public static bool IsTutorialActive { get; private set; }

    /// <summary>
    /// Event triggered when the tutorial is completely finished.
    /// </summary>
    public static event System.Action OnTutorialCompleted;

    /// <summary>
    /// Exposes the current step for observers to access.
    /// </summary>
    public TutorialStep CurrentStep => currentStep;

    /// <summary>Queue of tutorial steps to be executed.</summary>
    private Queue<TutorialStep> tutorialQueue;
    /// <summary>Currently active tutorial step.</summary>
    private TutorialStep currentStep;

    /// <summary>Counter for tracking player inputs during tutorial.</summary>
    private int inputCounter = 0;
    /// <summary>Counter for tracking beats during tutorial.</summary>
    private int beatCounter = 0;

    
    /// <summary>
    /// Initializes the tutorial manager singleton and sets up the tutorial system.
    /// </summary>
    protected void Awake() 
    {
        // Scene singleton logic
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Note : NE PAS appeler DontDestroyOnLoad(gameObject);	
        
        // Le reste de votre logique Awake/Start originale peut rester ici.
    }
    
    void Start()
    {
        if (uiManager == null || sequenceToPlay == null)
        {
            Debug.LogError("TutorialManager n'est pas configuré correctement !", this);
            enabled = false;
            return;
        }
        HideAllTutorialHUD();
        // Le tutoriel n'est pas actif par défaut au chargement de la scène.
        IsTutorialActive = false;
    }

    public void StartTutorial()
    {
        // Si le mode bypass est activé, on skip complètement le tutoriel
        if (bypassTutorial)
        {
            Debug.Log("[TutorialManager] Mode bypass activé - Skip du tutoriel");
            
            // On s'assure que le panel de tutoriel est caché
            if (uiManager != null)
            {
                uiManager.HidePanel();
            }
            
            // On affiche tous les éléments d'UI du tutoriel immédiatement
            ShowInvocationUI();
            ShowComboUI();
            ShowGoldUI();
            ShowUnitsAndSpellsUI();
            
            // On s'assure que le tutoriel n'est pas marqué comme actif
            IsTutorialActive = false;
            
            // On déclenche l'événement de fin de tutoriel pour que les autres systèmes sachent qu'on a "terminé"
            OnTutorialCompleted?.Invoke();
            
            Debug.Log("Tutoriel bypassé - Tous les éléments UI sont maintenant visibles.");
            return;
        }

        if (IsTutorialActive || (tutorialQueue != null && tutorialQueue.Count > 0)) return;
        if (TutorialBannerObserver.Instance != null) TutorialBannerObserver.Instance.ResetStatus();

        // Active l'interrupteur
        IsTutorialActive = true;

        Debug.Log("Début de la séquence de tutoriel !");
        tutorialQueue = new Queue<TutorialStep>(sequenceToPlay.steps);
        AdvanceToNextStepInternal();
    }

    /// <summary>
    /// Méthode publique pour permettre aux observers d'avancer le tutoriel
    /// </summary>
    public void AdvanceToNextStep()
    {
        AdvanceToNextStepInternal();
    }

    private void AdvanceToNextStepInternal()
    {
        UnsubscribeFromCurrentTrigger();

        if (tutorialQueue.Count > 0)
        {
            currentStep = tutorialQueue.Dequeue();
            uiManager.ShowStep(currentStep);
            TriggerStepStartAction(currentStep);
            SubscribeToCurrentTrigger();
        }
        else
        {
            EndTutorial();
        }
    }

    private void EndTutorial()
    {
        Debug.Log("Fin de la séquence de tutoriel.");
        currentStep = null;
        if (uiManager != null) uiManager.HidePanel();

        // On désactive l'interrupteur et on notifie que c'est terminé.
        IsTutorialActive = false;
        OnTutorialCompleted?.Invoke(); 
        
        
    }

    private void SubscribeToCurrentTrigger()
    {
        if (currentStep == null) return;

        inputCounter = 0;
        beatCounter = 0;

        switch (currentStep.triggerType)
        {
            case TutorialTriggerType.BeatCount:
                if (MusicManager.Instance != null) MusicManager.Instance.OnBeat += HandleBeat;
                break;
            case TutorialTriggerType.PlayerInputs:
                SequenceController.OnSequenceKeyPressed += HandlePlayerInput;
                break;
            case TutorialTriggerType.BannerPlacedOnBuilding:
                if (BannerController.Exists && TutorialBannerObserver.Instance != null)
                {
                    TutorialBannerObserver.Instance.OnBannerPlacedOnBuilding += HandleBannerPlacement;
                    BannerController.Instance.AddObserver(TutorialBannerObserver.Instance);
                }
                break;
            case TutorialTriggerType.UnitSummoned:
                SequenceController.OnCharacterInvocationSequenceComplete += HandleUnitSummoned;
                break;
            case TutorialTriggerType.MomentumSpend:
                // On s'abonne à l'événement du TutorialMomentumManager
                if (TutorialMomentumManager.Instance != null)
                {
                    TutorialMomentumManager.Instance.OnMomentumSpent += HandleMomentumSpent;
                }
                break;
            case TutorialTriggerType.FeverLevelReached:
                // Les TutorialFeverObserver se charge automatiquement via son Start()
                // Pas besoin de souscription manuelle ici
                break;
            case TutorialTriggerType.ComboCountReached:
                // Les TutorialComboObserver se charge automatiquement via son Start()
                // Pas besoin de souscription manuelle ici
                break;
            case TutorialTriggerType.SequencePanelHUD:
                // Les TutorialSequencePanelObserver se charge automatiquement via son Start()
                // Pas besoin de souscription manuelle ici
                break;
        }
    }

    private void UnsubscribeFromCurrentTrigger()
    {
        if (currentStep == null) return;

        switch (currentStep.triggerType)
        {
            case TutorialTriggerType.BeatCount:
                if (MusicManager.Instance != null) MusicManager.Instance.OnBeat -= HandleBeat;
                break;
            case TutorialTriggerType.PlayerInputs:
                SequenceController.OnSequenceKeyPressed -= HandlePlayerInput;
                break;
            case TutorialTriggerType.BannerPlacedOnBuilding:
                 if (TutorialBannerObserver.Instance != null)
                 {
                    TutorialBannerObserver.Instance.OnBannerPlacedOnBuilding -= HandleBannerPlacement;
                 }
                break;
            case TutorialTriggerType.UnitSummoned:
                SequenceController.OnCharacterInvocationSequenceComplete -= HandleUnitSummoned;
                break;
            case TutorialTriggerType.MomentumSpend:
                // On se désabonne proprement
                if (TutorialMomentumManager.Instance != null)
                {
                    TutorialMomentumManager.Instance.OnMomentumSpent -= HandleMomentumSpent;
                }
                break;
            case TutorialTriggerType.FeverLevelReached:
                // Les TutorialFeverObserver se décharge automatiquement
                // Pas besoin de désouscription manuelle ici
                break;
            case TutorialTriggerType.ComboCountReached:
                // Les TutorialComboObserver se décharge automatiquement
                // Pas besoin de désouscription manuelle ici
                break;
			
        }
    }

    /// <summary>
    /// Gère l'événement de dépense de momentum et fait avancer le tutoriel.
    /// </summary>
    private void HandleMomentumSpent()
    {
        // On n'a pas besoin de vérifier de paramètre ici, la simple dépense suffit.
        AdvanceToNextStep();
    }

    private void HandleBeat(float beatDuration) { beatCounter++; if (beatCounter >= currentStep.triggerParameter) AdvanceToNextStep(); }
    private void HandlePlayerInput(string key, Color timingColor) { inputCounter++; if (inputCounter >= currentStep.triggerParameter) AdvanceToNextStep(); }
    private void HandleBannerPlacement() { AdvanceToNextStep(); }
    private void HandleUnitSummoned(CharacterData_SO characterData, int perfectCount)
    {
        Debug.Log($"[TutorialManager] Séquence d'invocation pour '{characterData.DisplayName}' détectée, le tutoriel avance.");
        AdvanceToNextStep();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCurrentTrigger();
        if (Instance == this)
        {
            Instance = null;
        }
    }
    private void TriggerStepStartAction(TutorialStep step) { switch (step.groupToShowOnStart)
        {
            case HUDGroup.Invocation:
                ShowInvocationUI();
                break;
            case HUDGroup.Combo:
                ShowComboUI();
                break;
            case HUDGroup.Gold:
                ShowGoldUI();
                break;
            case HUDGroup.UnitsAndSpells:
                ShowUnitsAndSpellsUI();
                break;
            default:
                // Par défaut ou si None, on ne cache plus tout, on ne fait rien.
                // HideAllTutorialHUD(); // Commenté pour éviter de cacher des UI inutilement
                break;
        }
    }

    private void SetGroupVisibility(List<GameObject> uiObjects, bool visible)
    {
        foreach (var obj in uiObjects)
        {
            if (obj != null)
            {
                CanvasGroup canvasGroup = obj.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = obj.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
        }
    }

    public void HideAllTutorialHUD()
    {
        SetGroupVisibility(invocationUIObjects, false);
        SetGroupVisibility(comboUIObjects, false);
        SetGroupVisibility(goldUIObjects, false);
        SetGroupVisibility(unitsAndSpellsUIObjects, false);
    }

    public void ShowInvocationUI() { SetGroupVisibility(invocationUIObjects, true); }
    public void ShowComboUI() { SetGroupVisibility(comboUIObjects, true); }
    public void ShowGoldUI() { SetGroupVisibility(goldUIObjects, true); }
    public void ShowUnitsAndSpellsUI() { SetGroupVisibility(unitsAndSpellsUIObjects, true); }
}