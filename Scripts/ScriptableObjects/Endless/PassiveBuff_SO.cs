using UnityEngine;

namespace ScriptableObjects.Endless
{
    /// <summary>
    /// ScriptableObject defining a passive roguelike buff applied permanently to allied units during endless mode.
    /// </summary>
    [CreateAssetMenu(fileName = "PassiveBuff_New", menuName = "GameData/Endless/Passive Buff")]
    public class PassiveBuff_SO : ScriptableObject
    {
        public enum StatModifierType
        {
            AttackDamage,
            Defense,
            MovementSpeed,
            AttackSpeed,
            FeverGainRate
        }

        [Header("Display")]
        public string buffName;
        [TextArea]
        public string description;
        public Sprite icon;

        [Header("Effect")]
        public StatModifierType targetStat;
        public float value = 0.1f;
        public bool isSpecialModifier;

        /// <summary>
        /// Returns the formatted description including the percentage value for clarity.
        /// </summary>
        public string GetDescription()
        {
            int percentValue = Mathf.RoundToInt(value * 100f);
            return description?.Replace("{value}", $"{percentValue}%") ?? $"+{percentValue}%";
        }
    }
}