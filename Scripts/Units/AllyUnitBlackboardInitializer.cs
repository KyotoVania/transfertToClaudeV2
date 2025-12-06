using UnityEngine;
using Unity.Behavior;

/// <summary>
/// Initializes blackboard variables for ally units when they are spawned.
/// Ensures the behavior graph has proper reference to the unit component
/// for AI decision-making and action execution.
/// </summary>
public class AllyUnitBlackboardInitializer : MonoBehaviour
{
    /// <summary>Reference to the behavior graph agent component.</summary>
    private BehaviorGraphAgent m_Agent;

    /// <summary>
    /// Initializes the blackboard with reference to this ally unit.
    /// Called once when the unit is spawned to set up AI behavior tree variables.
    /// </summary>
    void Start()
    {
        m_Agent = GetComponent<BehaviorGraphAgent>();
        var allyUnit = GetComponent<Unit>();

        if (m_Agent == null) Debug.LogError($"[{gameObject.name}] Initializer: m_Agent is NULL.");
        else if (m_Agent.BlackboardReference == null) Debug.LogError($"[{gameObject.name}] Initializer: m_Agent.BlackboardReference is NULL.");

        if (allyUnit == null) Debug.LogError($"[{gameObject.name}] Initializer: allyUnit component (of type Unit) is NULL.");

        if (m_Agent == null || m_Agent.BlackboardReference == null || allyUnit == null)
        {
            Debug.LogError($"[{gameObject.name}] Initializer missing critical components! Cannot set SelfUnit.", gameObject);
            return;
        }

        var blackboardRef = m_Agent.BlackboardReference;

        // Set the SelfUnit variable on the blackboard for behavior tree nodes to access
        BlackboardVariable<Unit> bbSelfUnitForGraph;
        if (blackboardRef.GetVariable("SelfUnit", out bbSelfUnitForGraph))
        {
            bbSelfUnitForGraph.Value = allyUnit;
            Debug.Log($"[{gameObject.name}] Initializer: Successfully set 'SelfUnit' on Blackboard with component of type {allyUnit.GetType().Name}.", gameObject);
        }
        else
        {
            Debug.LogError($"[{gameObject.name}] Initializer: Blackboard variable 'SelfUnit' (expecting type Unit) NOT FOUND on the Blackboard Asset. Ensure it exists and is correctly named.", gameObject);
        }
    }
}