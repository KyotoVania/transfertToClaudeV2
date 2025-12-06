namespace ScriptableObjects
{
    using UnityEngine;
    using Sirenix.OdinInspector;

    /// <summary>
    /// ScriptableObject defining unit statistics for combat and movement.
    /// Contains all numerical parameters that define unit behavior in gameplay,
    /// including health, attack values, ranges, and timing delays.
    /// Used by the unit system for consistent stat management across different unit types.
    /// </summary>
    [CreateAssetMenu(fileName = "UnitStats_New", menuName = "GameData/Unit Stats")]
    public class UnitStats_SO : ScriptableObject
    {
        [Title("Unit Combat Stats")] 
        [MinValue(1)] 
        /// <summary>Unit health points (minimum 1).</summary>
        public int Health = 100;

        [MinValue(0)] 
        /// <summary>Unit defense value (can be 0).</summary>
        public int Defense = 10;

        [MinValue(0)] 
        /// <summary>Unit attack damage value (can be 0).</summary>
        public int Attack = 15;

        [MinValue(1)] 
        /// <summary>Attack range in number of tiles (minimum 1).</summary>
        public int AttackRange = 1;

        [MinValue(1)] 
        /// <summary>Attack delay in number of beats (minimum 1).</summary>
        public int AttackDelay = 1;

        [Title("Unit Movement & Detection")] 
        [MinValue(1)] 
        /// <summary>Movement delay in number of beats before moving (minimum 1).</summary>
        public int MovementDelay = 1;

        [MinValue(0)] 
        /// <summary>Detection range in number of tiles (0 means no detection).</summary>
        public int DetectionRange = 3;

        [Title("Unit Type")] 
        [EnumToggleButtons] 
        /// <summary>Unit type classification (Regular, Elite, or Boss).</summary>
        public UnitType Type = UnitType.Regular;


    }
}