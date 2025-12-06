using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using ScriptableObjects;
using UnityEngine.EventSystems;

/// <summary>
/// Manages individual team slot UI elements in the team management interface.
/// Handles display of character information, equipment visuals, and slot interactions.
/// Supports both occupied and empty slot states with appropriate visual feedback.
/// </summary>
public class TeamSlotUI : MonoBehaviour
{
    [Header("Display Groups")]
    /// <summary>Visual elements shown when the slot contains a character.</summary>
    [SerializeField] private GameObject occupiedSlotVisuals;
    /// <summary>Visual elements shown when the slot is empty.</summary>
    [SerializeField] private GameObject emptySlotVisuals;

    [Header("Occupied Slot Elements")]
    /// <summary>Container for 3D/animated character visuals.</summary>
    [SerializeField] private Transform characterVisualsContainer;
    /// <summary>Image component for character icon display.</summary>
    [SerializeField] private Image characterImage;
    /// <summary>Text component for character name display.</summary>
    [SerializeField] private TextMeshProUGUI characterNameText;
    /// <summary>Text component for character description display.</summary>
    [SerializeField] private TextMeshProUGUI characterDescText;
    /// <summary>Button for removing character from this slot.</summary>
    [SerializeField] private Button removeButton;
        
    [Header("Interaction")]
    /// <summary>Main card button for slot interaction (assigned dynamically).</summary>
    [SerializeField] private Button mainCardButton;

    [Header("Empty Slot Elements")]
    /// <summary>Button for adding a character to this empty slot.</summary>
    [SerializeField] private Button addButton;

    /// <summary>Character data currently assigned to this slot.</summary>
    private CharacterData_SO _characterData;
    /// <summary>Index of this slot in the team array.</summary>
    private int _slotIndex;
    /// <summary>Callback executed when character is removed from slot.</summary>
    private Action<CharacterData_SO> _onRemoveCallback;
    /// <summary>Callback executed when character is added to slot.</summary>
    private Action<int> _onAddCallback;
    /// <summary>Callback executed when equipment panel should be shown.</summary>
    private Action<CharacterData_SO> _onShowEquipmentCallback;

    /// <summary>Reference to the card visual controller for visual effects.</summary>
    private CardVisualController _cardVisualController;

    /// <summary>
    /// Initializes the team slot by finding the card visual controller component.
    /// </summary>
    private void Awake()
    {
        _cardVisualController = GetComponentInParent<CardVisualController>();
        if (_cardVisualController == null)
        {
            Debug.LogError($"[TeamSlotUI] CardVisualController not found on parents of {gameObject.name}!", this);
        }
    }

    /// <summary>
    /// Sets up the team slot with character data and event callbacks.
    /// </summary>
    /// <param name="characterData">Character data to display, or null for empty slot.</param>
    /// <param name="slotIndex">Index of this slot in the team.</param>
    /// <param name="onRemove">Callback for character removal.</param>
    /// <param name="onAdd">Callback for character addition.</param>
    /// <param name="onShowEquipment">Callback for showing equipment panel.</param>
    /// <param name="level">Character level for display.</param>
    public void Setup(CharacterData_SO characterData, int slotIndex, Action<CharacterData_SO> onRemove, Action<int> onAdd, Action<CharacterData_SO> onShowEquipment, int level)
    {
        _characterData = characterData;
        _slotIndex = slotIndex;
        _onRemoveCallback = onRemove;
        _onAddCallback = onAdd;
        _onShowEquipmentCallback = onShowEquipment;

        if (_characterData != null)
        {
            // --- Slot Occupé ---
            occupiedSlotVisuals.SetActive(true);
            emptySlotVisuals.SetActive(false);

            // --- Logique d'instanciation de l'animation ---
            foreach (Transform child in characterVisualsContainer)
            {
                Destroy(child.gameObject);
            }

            if (_characterData.MenuAnimationPrefab != null)
            {
                characterImage.enabled = false;
                GameObject animInstance = Instantiate(_characterData.MenuAnimationPrefab, characterVisualsContainer);

                // --- LOGIQUE CLÉ : Liaison dynamique des événements ---
                Button newButton = animInstance.GetComponentInChildren<Button>(true);
                if (newButton != null && _cardVisualController != null)
                {
                    // Lier les Event Triggers pour les animations de sélection
                    SetupEventTriggers(newButton.gameObject);
                    
                    // Lier le clic pour ouvrir le panel d'équipement
                    newButton.onClick.AddListener(HandleShowEquipmentClick);
                    
                    // Mettre à jour la navigation
                    SetupNavigation(newButton);
                    
                    // Assigner ce nouveau bouton comme bouton principal
                    this.mainCardButton = newButton;
                }
                else
                {
                     Debug.LogError($"[TeamSlotUI] Le prefab d'animation '{_characterData.MenuAnimationPrefab.name}' n'a pas de composant Button !", this);
                }
            }
            else
            {
                characterImage.sprite = _characterData.Icon;
                characterImage.enabled = true;
            }

            // --- Fin de la nouvelle logique ---
            
            if (characterNameText != null) characterNameText.text = _characterData.DisplayName;
            if (characterDescText != null)
            {
                if (_characterData.Stats != null)
                    characterDescText.text = $"Lvl {level} / {_characterData.Stats.Type}";
                else
                    characterDescText.text = "Stats N/A";
            }

            removeButton.onClick.RemoveAllListeners();
            removeButton.onClick.AddListener(HandleRemoveClick);
        }
        else
        {
            // --- Slot Vide ---
            occupiedSlotVisuals.SetActive(false);
            emptySlotVisuals.SetActive(true);

            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(HandleAddClick);
        }
    }

    /// <summary>
    /// Ajoute dynamiquement les listeners d'événements à un GameObject.
    /// </summary>
    private void SetupEventTriggers(GameObject targetObject)
    {
        EventTrigger trigger = targetObject.GetComponent<EventTrigger>() ?? targetObject.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        // Créer l'entrée pour l'événement 'Select' (navigation manette)
        EventTrigger.Entry onSelectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Select };
        onSelectEntry.callback.AddListener((data) => { _cardVisualController.AnimateToSelectedState(); });
        trigger.triggers.Add(onSelectEntry);

        // Créer l'entrée pour l'événement 'Deselect'
        EventTrigger.Entry onDeselectEntry = new EventTrigger.Entry { eventID = EventTriggerType.Deselect };
        onDeselectEntry.callback.AddListener((data) => { _cardVisualController.AnimateToDeselectedState(); });
        trigger.triggers.Add(onDeselectEntry);
        
        // Bonus : répliquer aussi les événements de la souris pour un comportement cohérent
        EventTrigger.Entry onPointerEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        onPointerEnter.callback.AddListener((data) => { _cardVisualController.OnPointerEnter((PointerEventData)data); });
        trigger.triggers.Add(onPointerEnter);

        EventTrigger.Entry onPointerExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        onPointerExit.callback.AddListener((data) => { _cardVisualController.OnPointerExit((PointerEventData)data); });
        trigger.triggers.Add(onPointerExit);
    }

    /// <summary>
    /// Reconfigure la navigation entre le nouveau bouton principal et le bouton poubelle.
    /// </summary>
    private void SetupNavigation(Button newMainButton)
    {
        if (newMainButton == null || removeButton == null) return;
        
        // Navigation du bouton principal -> Poubelle (Haut)
        Navigation cardNav = newMainButton.navigation;
        cardNav.mode = Navigation.Mode.Automatic;
        cardNav.selectOnUp = removeButton;
        newMainButton.navigation = cardNav;
        
        // Navigation de la poubelle -> Bouton principal (Bas)
        Navigation trashNav = removeButton.navigation;
        trashNav.mode = Navigation.Mode.Automatic;
        trashNav.selectOnDown = newMainButton;
        removeButton.navigation = trashNav;
    }

    // --- Le reste du script est majoritairement inchangé ---

    private void HandleRemoveClick()
    {
        _onRemoveCallback?.Invoke(_characterData);
    }

    private void HandleAddClick()
    {
        _onAddCallback?.Invoke(_slotIndex);
    }
    
    private void HandleShowEquipmentClick()
    {
        _onShowEquipmentCallback?.Invoke(_characterData);
    }
    
    public Button GetMainButton()
    {
        return (_characterData != null) ? mainCardButton : addButton;
    }
    
    public Button GetAddButton()
    {
        return addButton;
    }
    
    public bool HasCharacter()
    {
        return _characterData != null;
    }
}