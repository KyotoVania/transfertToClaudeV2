using UnityEngine;
using Unity.Behavior;
using Unity.Behavior.GraphFramework;

/// <summary>
/// Initializes blackboard variables for enemy units when they are spawned.
/// Ensures the behavior graph has proper reference to the unit component
/// for AI decision-making and action execution. Executes early to ensure
/// blackboard is ready before behavior graphs start running.
/// </summary>
[DefaultExecutionOrder(-100)] // Execute before default scripts to ensure early initialization
public class EnemyUnitBlackboardInitializer : MonoBehaviour
{
    /// <summary>Reference to the behavior graph agent component.</summary>
    private BehaviorGraphAgent m_Agent;
    /// <summary>Reference to the enemy unit component.</summary>
    private Unit m_EnemyUnit;

    /// <summary>
    /// Early initialization called before all Start() methods.
    /// Sets up component references and validates critical dependencies.
    /// </summary>
    void Awake()
    {
        m_Agent = GetComponent<BehaviorGraphAgent>();
        m_EnemyUnit = GetComponent<Unit>();

        if (m_Agent == null)
        {
            Debug.LogError($"[{gameObject.name}] EnemyUnitBlackboardInitializer: BehaviorGraphAgent component not found!", gameObject);
            enabled = false; // Disable if agent is missing
            return;
        }
        if (m_EnemyUnit == null)
        {
            Debug.LogError($"[{gameObject.name}] EnemyUnitBlackboardInitializer: EnemyUnit component not found!", gameObject);
            enabled = false; // Disable if unit is missing
            return;
        }

        // Initialize blackboard early to ensure readiness for behavior graphs
        InitializeBlackboard();
    }

    /// <summary>
    /// Fallback initialization method called after BehaviorGraphAgent.Start().
    /// Attempts to re-initialize blackboard if it failed during Awake.
    /// </summary>
    void Start()
    {
        // Safety net: re-attempt blackboard initialization if it failed in Awake
        if (m_Agent != null && m_Agent.BlackboardReference != null && m_Agent.BlackboardReference.GetVariable(EnemyUnit.BB_SELF_UNIT, out BlackboardVariable<EnemyUnit> temp) && temp.Value == null)
        {
            Debug.LogWarning($"[{gameObject.name}] EnemyUnitBlackboardInitializer: Re-attempting Blackboard initialization in Start().", gameObject);
            InitializeBlackboard();
        }
    }


    /// <summary>
    /// Initializes the blackboard with reference to this enemy unit.
    /// Sets up the SelfUnit variable for behavior tree nodes to access.
    /// </summary>
    void InitializeBlackboard()
    {
        if (m_Agent == null || m_EnemyUnit == null) return; // Already validated in Awake

        if (m_Agent.BlackboardReference == null)
        {
            // This can happen if blackboard is assigned late to the agent
            // BehaviorGraphAgent.Start() may not have executed yet
            Debug.LogWarning($"[{gameObject.name}] EnemyUnitBlackboardInitializer: BlackboardReference is null on BehaviorGraphAgent during InitializeBlackboard. Will retry or fail if graph starts.", gameObject);
            return;
        }

        var blackboardRef = m_Agent.BlackboardReference;

        BlackboardVariable<Unit> bbSelfUnitForGraph;

        if (blackboardRef.GetVariable(EnemyUnit.BB_SELF_UNIT, out bbSelfUnitForGraph))
        {
            if (bbSelfUnitForGraph.Value == null) // Only overwrite if null
                bbSelfUnitForGraph.Value = m_EnemyUnit;
            else if (bbSelfUnitForGraph.Value != m_EnemyUnit)
                 bbSelfUnitForGraph.Value = m_EnemyUnit;
        }
    }
}