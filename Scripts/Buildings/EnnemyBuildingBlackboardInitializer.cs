using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Initializes blackboard variables for enemy buildings when they are spawned.
/// Ensures the behavior graph has proper reference to the building component
/// for AI decision-making and action execution (such as unit spawning).
/// Executes early to ensure blackboard is ready before behavior graphs start running.
/// </summary>
[DefaultExecutionOrder(-100)] // Execute before behavior graphs to ensure early initialization
public class EnnemyBuildingBlackboardInitializer : MonoBehaviour
{
    /// <summary>Blackboard variable name for the building reference.</summary>
    public const string BB_SELF_BUILDING = "SelfBuilding";

    /// <summary>
    /// Initializes the blackboard with reference to this building.
    /// Called early in the object lifecycle to set up AI behavior tree variables.
    /// </summary>
    void Awake()
    {
        var agent = GetComponent<BehaviorGraphAgent>();
        var building = GetComponent<Building>();

        if (agent == null || agent.BlackboardReference == null || building == null)
        {
            Debug.LogError($"[{gameObject.name}] BuildingBlackboardInitializer: " +
                           "Critical components missing (Agent, Blackboard or Building)!", gameObject);
            return;
        }

        // Set the SelfBuilding variable on the blackboard for behavior tree nodes to access
        if (agent.BlackboardReference.GetVariable(BB_SELF_BUILDING, out BlackboardVariable<Building> bbSelfBuilding))
        {
            bbSelfBuilding.Value = building;
            Debug.Log($"[{gameObject.name}] Initializer: Blackboard variable '{BB_SELF_BUILDING}' initialized successfully.", gameObject);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Initializer: Blackboard variable '{BB_SELF_BUILDING}' " +
                           "(type Building) NOT FOUND on the Blackboard asset. Please create it.", gameObject);
        }
    }
}