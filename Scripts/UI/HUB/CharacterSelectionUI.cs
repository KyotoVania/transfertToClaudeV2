
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq;
using UI.HUB;
using System.Collections;
using ScriptableObjects;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Manages the character selection interface in the Hub, allowing players to browse
/// available characters, view detailed stats and previews, and add characters to their team.
/// Supports both mouse and gamepad navigation with smooth scrolling and focus management.
/// </summary>
public class CharacterSelectionUI : MonoBehaviour
{
    [Header("Panels & Containers")]
    /// <summary>Reference to the team management panel for navigation.</summary>
    [SerializeField] private GameObject teamManagementPanel;
    /// <summary>Container for available character list items.</summary>
    [SerializeField] private Transform availableCharactersContainer;
    /// <summary>Panel displaying detailed character information.</summary>
    [SerializeField] private GameObject detailPanel;

    [Header("List Item Prefab")]
    /// <summary>Prefab for creating character list items.</summary>
    [SerializeField] private GameObject availableCharacterItemPrefab;

    [Header("Detail Panel Elements")]
    /// <summary>Text component for displaying character name.</summary>
    [SerializeField] private TextMeshProUGUI detailCharacterNameText;
    /// <summary>Text component for displaying character description.</summary>
    [SerializeField] private TextMeshProUGUI detailCharacterDescriptionText;
    /// <summary>Text component for displaying health stat.</summary>
    [SerializeField] private TextMeshProUGUI statHealthText;
    /// <summary>Text component for displaying attack stat.</summary>
    [SerializeField] private TextMeshProUGUI statAttackText;
    /// <summary>Text component for displaying defense stat.</summary>
    [SerializeField] private TextMeshProUGUI statDefenseText;
    /// <summary>Text component for displaying cost stat.</summary>
    [SerializeField] private TextMeshProUGUI statCostText;
    
    [Header("Buttons")]
    /// <summary>Button for adding selected character to team.</summary>
    [SerializeField] private Button addToTeamButton;
    /// <summary>Button for returning to previous screen.</summary>
    [SerializeField] private Button backButton;
    
    [Header("3D Preview")]
    /// <summary>Component for displaying 3D character preview.</summary>
    [SerializeField] private Character3DPreview characterPreview;
    
    [Header("Gamepad Navigation")]
    /// <summary>Scroll rect for character list navigation.</summary>
    [SerializeField] private ScrollRect scrollRect;
    /// <summary>Speed for gamepad-controlled scrolling.</summary>
    [SerializeField] private float scrollSpeed = 5f;
    
    /// <summary>List of instantiated character list item UI components.</summary>
    private readonly List<AvailableCharacterListItemUI> _instantiatedListItems = new List<AvailableCharacterListItemUI>();
    /// <summary>Reference to the team manager for character assignments.</summary>
    private TeamManager _teamManager;
    /// <summary>Currently selected character data.</summary>
    private CharacterData_SO _selectedCharacter;
    /// <summary>Last selected UI object for focus restoration.</summary>
    private GameObject _lastSelectedObject;
    
    /// <summary>
    /// Initializes the character selection UI by getting manager references.
    /// </summary>
    void Awake()
    {
        _teamManager = TeamManager.Instance;
        if (_teamManager == null) Debug.LogError("[CharacterSelectionUI] TeamManager not found!");

        addToTeamButton?.onClick.AddListener(OnAddToTeamClicked);
        backButton?.onClick.AddListener(OnBackClicked);
        
        if (scrollRect == null && availableCharactersContainer != null)
        {
            scrollRect = availableCharactersContainer.GetComponentInParent<ScrollRect>();
        }
    }

    private void OnEnable()
    {
        Debug.Log("[CharacterSelectionUI] Panel activé - Préparation de la sélection");
        
        // 🚀 PAS BESOIN DE DÉSACTIVER LES CONTRÔLES DU HUB
        // Le TeamManagementUI l'a déjà fait et garde le contrôle
        
        PopulateAvailableCharactersList();
        SelectCharacter(null); // Aucun personnage sélectionné au départ
        
        StartCoroutine(SetupInitialSelection());
    }
    
    private void OnDisable()
    {
        // 🧹 NETTOYAGE SIMPLE - Plus besoin de gérer les contrôles du Hub
        _lastSelectedObject = EventSystem.current.currentSelectedGameObject;
        
        if (characterPreview != null)
        {
            characterPreview.ClearPreview();
        }
    }
    
    private void Update()
    {
        // Gérer l'action "Cancel"
        if (InputManager.Instance != null && InputManager.Instance.UIActions.Cancel.WasPressedThisFrame())
        {
            OnBackClicked();
        }
        
        // Détecter le focus de la manette/clavier pour l'affichage des détails
        GameObject currentSelected = EventSystem.current.currentSelectedGameObject;
        if (currentSelected != null && currentSelected.transform.IsChildOf(availableCharactersContainer))
        {
            var itemUI = currentSelected.GetComponent<AvailableCharacterListItemUI>();
            if (itemUI != null && itemUI.GetCharacterData() != _selectedCharacter)
            {
                SelectCharacter(itemUI.GetCharacterData());
            }
        }

        EnsureSelection();
        HandleScrolling();
    }

    #region Gestion de l'UI (Inchangée)

    private void PopulateAvailableCharactersList()
    {
        foreach (var item in _instantiatedListItems)
        {
            if (item != null) Destroy(item.gameObject);
        }
        _instantiatedListItems.Clear();

        var available = _teamManager.AvailableCharacters;
        var activeTeam = _teamManager.ActiveTeam;
        var charactersToShow = available.Except(activeTeam).ToList();

        for (int i = 0; i < charactersToShow.Count; i++)
        {
            var character = charactersToShow[i];
            if (character == null) continue;

            GameObject itemGO = Instantiate(availableCharacterItemPrefab, availableCharactersContainer);
            var itemUI = itemGO.GetComponent<AvailableCharacterListItemUI>();
            if (itemUI != null)
            {
                int characterLevel = 1;
                if (PlayerDataManager.Instance.Data.CharacterProgressData.ContainsKey(character.CharacterID))
                {
                    characterLevel = PlayerDataManager.Instance.Data.CharacterProgressData[character.CharacterID].CurrentLevel;
                }

                itemUI.Setup(character, OnCharacterItemClicked, characterLevel);
                _instantiatedListItems.Add(itemUI);
            }
        }
        
        ConfigureGlobalNavigation();
    }

    private void ConfigureGlobalNavigation()
    {
        // Configuration de la navigation (code inchangé)
        for (int i = 0; i < _instantiatedListItems.Count - 1; i++)
        {
            Button currentButton = _instantiatedListItems[i].GetComponent<Button>();
            Button nextButton = _instantiatedListItems[i + 1]?.GetComponent<Button>();
            
            if (currentButton != null && nextButton != null)
            {
                Navigation nav = currentButton.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnDown = nextButton;
                if(i > 0)
                {
                   nav.selectOnUp = _instantiatedListItems[i - 1].GetComponent<Button>();
                }
                currentButton.navigation = nav;
            }
        }

        if(_instantiatedListItems.Count > 1)
        {
            Button lastItemButton = _instantiatedListItems[_instantiatedListItems.Count - 1].GetComponent<Button>();
            Navigation lastItemNav = lastItemButton.navigation;
            lastItemNav.mode = Navigation.Mode.Explicit;
            lastItemNav.selectOnUp = _instantiatedListItems[_instantiatedListItems.Count - 2].GetComponent<Button>();
            lastItemNav.selectOnDown = backButton;
            lastItemButton.navigation = lastItemNav;
        }
        
        foreach (var item in _instantiatedListItems)
        {
            Button itemButton = item.GetComponent<Button>();
            if (itemButton != null)
            {
                Navigation nav = itemButton.navigation;
                nav.selectOnRight = addToTeamButton;
                itemButton.navigation = nav;
            }
        }
        
        if (addToTeamButton != null)
        {
            Navigation addNav = addToTeamButton.navigation;
            addNav.mode = Navigation.Mode.Explicit;
            addNav.selectOnLeft = _instantiatedListItems.Count > 0 ? _instantiatedListItems[0].GetComponent<Button>() : backButton;
            addNav.selectOnDown = backButton;
            addToTeamButton.navigation = addNav;
        }

        if (backButton != null)
        {
            Navigation backNav = backButton.navigation;
            backNav.mode = Navigation.Mode.Explicit;
            backNav.selectOnUp = _instantiatedListItems.Count > 0 ? _instantiatedListItems.Last().GetComponent<Button>() : addToTeamButton;
            backNav.selectOnLeft = _instantiatedListItems.Count > 0 ? _instantiatedListItems.Last().GetComponent<Button>() : null;
            backNav.selectOnRight = addToTeamButton;
            backButton.navigation = backNav;
        }
    }
    
    private IEnumerator SetupInitialSelection()
    {
        yield return null;
        
        GameObject objectToSelect = _lastSelectedObject != null && _lastSelectedObject.activeInHierarchy ? _lastSelectedObject :
                                    _instantiatedListItems.Count > 0 ? _instantiatedListItems[0].gameObject :
                                    backButton != null ? backButton.gameObject : null;
        
        if (objectToSelect != null)
        {
            EventSystem.current.SetSelectedGameObject(objectToSelect);
        }
    }
    
    private void EnsureSelection()
    {
        if (EventSystem.current.currentSelectedGameObject == null)
        {
            Vector2 navInput = InputManager.Instance?.UIActions.Navigate.ReadValue<Vector2>() ?? Vector2.zero;
            if (navInput.sqrMagnitude > 0.1f)
            {
                StartCoroutine(SetupInitialSelection());
            }
        }
    }
    
    private void HandleScrolling()
    {
        if (scrollRect == null || EventSystem.current.currentSelectedGameObject == null) return;
        
        RectTransform selectedRect = EventSystem.current.currentSelectedGameObject.GetComponent<RectTransform>();
        if (selectedRect == null || !selectedRect.transform.IsChildOf(availableCharactersContainer)) return;

        float containerHeight = ((RectTransform)availableCharactersContainer).rect.height;
        float viewportHeight = ((RectTransform)scrollRect.viewport).rect.height;

        if (containerHeight <= viewportHeight) return;

        float itemPosInContainer = -selectedRect.anchoredPosition.y;
        float normalizedItemPos = itemPosInContainer / (containerHeight - viewportHeight);
        
        scrollRect.verticalNormalizedPosition = Mathf.Lerp(scrollRect.verticalNormalizedPosition, 1f - normalizedItemPos, Time.deltaTime * scrollSpeed);
    }

    private void OnCharacterItemClicked(CharacterData_SO characterData)
    {
        SelectCharacter(characterData);
    }
    
    private void SelectCharacter(CharacterData_SO characterData)
    {
        _selectedCharacter = characterData;
        UpdateDetailPanel();
        
        foreach (var itemUI in _instantiatedListItems)
        {
            if (itemUI != null)
            {
                bool isFocused = EventSystem.current.currentSelectedGameObject == itemUI.gameObject;
                itemUI.SetSelected(isFocused);
            }
        }
        
        if (characterPreview != null)
        {
            if (_selectedCharacter != null)
                characterPreview.ShowCharacter(_selectedCharacter.HubVisualPrefab);
            else
                characterPreview.ClearPreview();
        }
        
        addToTeamButton.interactable = (_selectedCharacter != null);
    }

    private void UpdateDetailPanel()
    {
        if (_selectedCharacter != null)
        {
            detailPanel.SetActive(true);
            detailCharacterNameText.text = _selectedCharacter.DisplayName;
            detailCharacterDescriptionText.text = _selectedCharacter.Description;
            
            if (_selectedCharacter.Stats != null)
            {
                int level = PlayerDataManager.Instance.Data.CharacterProgressData.TryGetValue(_selectedCharacter.CharacterID, out var p) ? p.CurrentLevel : 1;
                RuntimeStats finalStats = StatsCalculator.GetFinalStats(_selectedCharacter, level, null);
                statHealthText.text = $"HP: {finalStats.MaxHealth}";
                statAttackText.text = $"ATK: {finalStats.Attack}";
                statDefenseText.text = $"DEF: {finalStats.Defense}";
                statCostText.text = $"COST: {finalStats.AttackDelay}";
            }
        }
        else
        {
            detailPanel.SetActive(false);
            addToTeamButton.interactable = false;
        }
    }

    #endregion

    #region 🚀 NOUVELLES MÉTHODES DE TRANSITION SIMPLIFIÉES

    /// <summary>
    /// Ajoute un personnage à l'équipe et retourne au TeamManagement
    /// </summary>
    private void OnAddToTeamClicked()
    {
        if (_selectedCharacter != null)
        {
            bool added = _teamManager.TryAddCharacterToActiveTeam(_selectedCharacter);
            if (added)
            {
                Debug.Log($"[CharacterSelectionUI] Personnage {_selectedCharacter.DisplayName} ajouté, retour au TeamManagement");
                ReturnToTeamManagement();
            }
        }
    }

    /// <summary>
    /// Annule la sélection et retourne au TeamManagement
    /// </summary>
    private void OnBackClicked()
    {
        Debug.Log("[CharacterSelectionUI] Retour demandé, retour au TeamManagement");
        ReturnToTeamManagement();
    }

    /// <summary>
    /// 🎯 MÉTHODE SIMPLIFIÉE : Retour propre au TeamManagement
    /// Plus besoin de transitions complexes - le TeamManagementUI gère tout
    /// </summary>
    private void ReturnToTeamManagement()
    {
        if (teamManagementPanel != null)
        {
            // ✨ LOGIQUE ULTRA-SIMPLE :
            // 1. Réactiver le panel parent
            teamManagementPanel.SetActive(true);
            //set le alpha à 1 pour éviter les problèmes de transparence
            CanvasGroup canvasGroup = teamManagementPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f; // Assurez-vous que le panel est complètement opaque
            }
            
            
            // 2. Se désactiver
            gameObject.SetActive(false);
            
            // 🎉 C'EST TOUT ! 
            // Le TeamManagementUI va automatiquement :
            // - Détecter qu'on revient d'un sous-panel (grâce à _isTransitioningToSubPanel)
            // - Restaurer le focus mémorisé dans RestoreRememberedFocus()
            // - Rafraîchir l'UI si nécessaire
        }
        else
        {
            Debug.LogError("[CharacterSelectionUI] Référence teamManagementPanel manquante !");
        }
    }

    #endregion
}