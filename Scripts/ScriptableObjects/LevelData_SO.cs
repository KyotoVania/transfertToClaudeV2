namespace ScriptableObjects
{
	using UnityEngine;
	using Sirenix.OdinInspector;
	using System.Collections.Generic;

	/// <summary>
	/// Enumeration of different level types in the game.
	/// Used for categorization and appropriate handling of different scene types.
	/// </summary>
	public enum LevelType
	{
    	/// <summary>System scenes like MainMenu, Hub, Core, etc.</summary>
    	SystemScene,
    	/// <summary>Playable gameplay levels.</summary>
    	GameplayLevel,
    	/// <summary>Cinematic/cutscene levels.</summary>
    	Cinematic,
	}

	/// <summary>
	/// ScriptableObject defining level/scene data for the game progression system.
	/// Contains level identification, unlock requirements, rewards, and scene information.
	/// Used by the level selection system and progression tracking.
	/// </summary>
	[CreateAssetMenu(fileName = "LevelData_New", menuName = "GameData/Level Data")]
	public class LevelData_SO : ScriptableObject
	{
    [BoxGroup("Identification")]
    [InfoBox("Unique ID used for save data and internal references.")]
    /// <summary>Unique identifier for save data and internal references.</summary>
    public string LevelID = "Level_Default";

    [BoxGroup("Identification")]
    /// <summary>Display name shown to players in level selection.</summary>
    public string DisplayName = "Default Level";
	
	[BoxGroup("Identification")]
    [Tooltip("Index for sorting levels. Smallest displays first.")]
    /// <summary>Sort order index for level display (smallest first).</summary>
    public int OrderIndex = 0;

    [BoxGroup("Scene")]
    [Required("Scene name to load is required.")]
    [SceneObjectsOnly]
    /// <summary>Unity scene name to load for this level.</summary>
    public string SceneName = "Level_Gameplay_Template";

    [TextArea(3, 5)]
    [BoxGroup("Description")]
    /// <summary>Level description for player reference.</summary>
    public string Description;

    [Title("Unlock Conditions")]
    [InfoBox("Leave empty if level is unlocked by default or via other logic.")]
    [AssetsOnly]
    /// <summary>Previous level that must be completed to unlock this level.</summary>
    public LevelData_SO RequiredPreviousLevel;

    /// <summary>Required player level to access this level (0 if not required).</summary>
    public int RequiredPlayerLevel;
    
    [Title("Display & Access")]
    /// <summary>Type classification of this level for appropriate handling.</summary>
    public LevelType TypeOfLevel = LevelType.GameplayLevel;

    [Title("Rewards")]
    /// <summary>Experience points awarded upon level completion.</summary>
    public int ExperienceReward;
    /// <summary>Currency amount awarded upon level completion.</summary>
    public int CurrencyReward;
    [AssetsOnly]
    /// <summary>Character unlocked upon completing this level.</summary>
    public CharacterData_SO CharacterUnlockReward;

    [Tooltip("Liste des équipements (items) à débloquer en récompense de victoire.")] [AssetsOnly]
    public List<EquipmentData_SO> ItemRewards;
	    
    [Title("Gameplay Settings")]
    [Range(1, 5)] public int Difficulty = 1; // Exemple d'échelle
    public float RhythmBPM = 120f; // Remplacé speed par BPM pour être clair

    [Header("Scenario Configuration")]
    [Tooltip("Scénario à exécuter pour ce niveau")]
    public LevelScenario_SO scenario;
    [Title("Audio (Wwise)")]
    public AK.Wwise.Event BackgroundMusic; // Décommenté
    public AK.Wwise.Switch MusicStateSwitch; // Potentiel Switch Wwise pour ce niveau
    // public AK.Wwise.Event VictoryMusic;
    // public AK.Wwise.Event DefeatMusic;
	}
}