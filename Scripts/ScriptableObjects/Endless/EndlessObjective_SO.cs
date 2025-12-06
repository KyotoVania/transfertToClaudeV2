using System.Collections.Generic;
using ScriptableObjects;
using UnityEngine;

namespace ScriptableObjects.Endless
{
    /// <summary>
    /// Defines the data blueprint of an endless objective: what to spawn, rewards, and defensive waves.
    /// </summary>
    [CreateAssetMenu(fileName = "EndlessObjective_New", menuName = "GameData/Endless/Objective")]
    public class EndlessObjective_SO : ScriptableObject
    {
        public enum ObjectiveType
        {
            EliminateTarget,
            CapturePoint,
            Survive
        }

        [Header("Presentation")]
        public string Title;
        [TextArea]
        public string Description;

        [Header("Objective Data")]
        public ObjectiveType Type;
        public GameObject TargetPrefab;
        public int BaseGoldReward = 100;
        public int BaseXPReward = 50;
        [Tooltip("0 for no limit")]
        public float TimeLimit = 0f;

        [Header("Defenses")]
        public List<Wave_SO> DefendingWaves = new List<Wave_SO>();
    }
}