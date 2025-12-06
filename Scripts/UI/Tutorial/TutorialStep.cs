using UnityEngine;

/// <summary>
/// Enumeration defining UI groups that the tutorial system can control.
/// Used to show/hide specific interface elements during tutorial progression.
/// </summary>
public enum HUDGroup
{
    /// <summary>No UI group selected.</summary>
    None,
    /// <summary>UI elements related to unit invocation/summoning.</summary>
    Invocation,
    /// <summary>UI elements related to combo system.</summary>
    Combo,
    /// <summary>UI elements related to gold/currency.</summary>
    Gold,
    /// <summary>UI elements related to units and spells.</summary>
    UnitsAndSpells,
    /// <summary>Show all UI elements.</summary>
    All
}

/// <summary>
/// Represents a single step in a tutorial sequence.
/// Contains the instruction text, progression trigger, and UI visibility settings.
/// </summary>
[System.Serializable]
public class TutorialStep
{
    [Header("Step Content")]
    /// <summary>The tutorial text to display to the player.</summary>
    [Tooltip("The tutorial text to display to the player.")]
    [TextArea(3, 5)]
    public string tutorialText;

    [Header("Progression Condition")]
    /// <summary>The type of event that will advance the tutorial.</summary>
    [Tooltip("The type of event that will advance the tutorial.")]
    public TutorialTriggerType triggerType;

    /// <summary>Parameter for the trigger. Ex: '4' for PlayerInputs, '8' for BeatCount.</summary>
    [Tooltip("Parameter for the trigger. Ex: '4' for PlayerInputs, '8' for BeatCount.")]
    public int triggerParameter = 1;

    [Header("Startup Action")]
    /// <summary>Which UI element group should be shown at the start of this step.</summary>
    [Tooltip("Which UI element group should be shown at the start of this step.")]
    public HUDGroup groupToShowOnStart;
}