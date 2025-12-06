namespace ScriptableObjects
{
	using UnityEngine;
	using System.Collections.Generic;
	using Sirenix.OdinInspector;
	#if UNITY_EDITOR
	using UnityEditor;
	#endif

	/// <summary>
	/// Enumeration of input types used for character invocation sequences.
	/// Maps to keyboard/gamepad inputs for the rhythmic summoning system.
	/// </summary>
	public enum InputType { X, C, V }

	/// <summary>
	/// ScriptableObject defining character data for the strategic rhythm game.
	/// Contains all character properties including stats, invocation sequences,
	/// visual representation, and gameplay mechanics. Used by the data-driven
	/// character system for summoning and team management.
	/// </summary>
	[CreateAssetMenu(fileName = "CharacterData_New", menuName = "GameData/Character Data")]
	public class CharacterData_SO : ScriptableObject
	{
    [BoxGroup("Identification", ShowLabel = false)]
    [HorizontalGroup("Identification/Split", Width = 100)]
    [PreviewField(100, ObjectFieldAlignment.Left), HideLabel]
    /// <summary>Character icon displayed in UI elements.</summary>
    public Sprite Icon;

    [VerticalGroup("Identification/Split/Info")]
    [InfoBox("Unique ID used for save data and internal references.")]
    /// <summary>Unique identifier for save data and internal references.</summary>
    public string CharacterID = "Char_Default";

    [VerticalGroup("Identification/Split/Info")]
    /// <summary>Display name shown to players in the UI.</summary>
    public string DisplayName = "Default Character";

    [TextArea(3, 5)]
    [BoxGroup("Description")]
    /// <summary>Character description for player reference.</summary>
    public string Description;

    [Title("Gameplay")]

    [BoxGroup("Gameplay")]
    [BoxGroup("Gameplay/Stats", ShowLabel = false)]
    [InfoBox("ScriptableObject containing this character's statistics.")]
    [InlineEditor(InlineEditorModes.FullEditor)]
    /// <summary>Character statistics data (health, attack, defense, etc.).</summary>
    public StatSheet_SO Stats;
    
    /// <summary>Prefab spawned when this character is summoned in gameplay.</summary>
    public GameObject GameplayUnitPrefab;

    [BoxGroup("Gameplay/Invocation")]
    [ListDrawerSettings(NumberOfItemsPerPage = 4)]
    [InfoBox("Sequence of 4 inputs (X, C, or V) to summon this character.")]
    [ValidateInput("ValidateSequenceLength", "Invocation sequence must have exactly 4 inputs.")]
    /// <summary>4-input sequence required to summon this character rhythmically.</summary>
    public List<InputType> InvocationSequence = new List<InputType>(4);

    [BoxGroup("Gameplay/Invocation")] // Placé dans le même groupe que la séquence
    [MinValue(0)]
    [SuffixLabel("or")]
    [GUIColor(0.9f, 0.9f, 0.2f)] // Pour le rendre plus visible
    public int GoldCost = 0;
    // --- Validation pour Odin Inspector ---
    #if UNITY_EDITOR
    private bool ValidateSequenceLength(List<InputType> sequence)
    {
        return sequence != null && sequence.Count == 4;
    }
    #endif
    // ------------------------------------
	[Header("Momentum System")]
    [Tooltip("Cost in momentum charges to invoke this unit. 0 for basic units.")]
    public int MomentumCost = 0;

    [Tooltip("Momentum gained (as a fraction of a charge) when this unit is successfully invoked.")]
    [Range(0f, 1f)]
    public float MomentumGainOnInvoke = 0.2f;

    [Tooltip("Momentum gained (as a fraction of a charge) when this unit completes its primary objective (capture or defensive kill).")]
    [Range(0f, 1f)]
    public float MomentumGainOnObjectiveComplete = 0.5f;
	
    [Title("Fever Mode Buffs")]
    [InfoBox("Modificateurs appliqués lorsque le Mode Fever est actif.")]
    public FeverBuffs feverBuffs;

    [Title("Hub & UI")]
    [AssetsOnly]
    public GameObject HubVisualPrefab;
    [AssetsOnly]
    public GameObject MenuAnimationPrefab;
    [Title("Audio (Wwise)")]
    [InfoBox("Assigner les Events Wwise spécifiques à ce personnage.")]
    public AK.Wwise.Event SelectionSound;
    public AK.Wwise.Switch CharacterSwitch; // Switch pour les personnages, utile pour les sons de sélection
    // Ajouter d'autres sons si nécessaire (attaque, capacité spéciale, etc.)

    [Title("Statut Initial")]
    [InfoBox("Cocher si ce personnage est débloqué dès le début du jeu.")]
    public bool UnlockedByDefault = false;

    [Title("Cooldown d'Invocation")]
    [BoxGroup("Cooldown")]
    [MinValue(0)]
    [Tooltip("Temps en beats avant de pouvoir invoquer à nouveau ce personnage.")]
    public float InvocationCooldown = 5;
	}
	
	[System.Serializable]
	public struct FeverBuffs
	{
		[Tooltip("Multiplicateur de la vitesse d'attaque. Ex: 1.3 pour +30%. Mettre à 1 pour aucun changement.")]
		[Range(1f, 3f)]
		public float AttackSpeedMultiplier;

		[Tooltip("Nombre de projectiles supplémentaires tirés par les unités à distance.")]
		[Range(0, 5)]
		public int ExtraProjectiles;

		[Tooltip("Multiplicateur de défense. Ex: 2.0 pour doubler la défense.")]
		[Range(1f, 5f)]
		public float DefenseMultiplier;
	}
}

