using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using ScriptableObjects;
using Gameplay;

public enum StatToBuff
{
    Attack,
    Defense,
    Speed
}

[CreateAssetMenu(fileName = "GlobalUnitBuffEffect_New", menuName = "GameData/Effects/Global Unit Buff Effect")]
public class GlobalUnitBuffEffect_SO : BaseSpellEffect_SO
{
    public StatToBuff Stat; 
    public float BuffMultiplier = 1.2f; 
    public float BuffDuration = 10f; 
    public int BonusDurationPerPerfectInput = 2; //
    [Header("Visual Effects")]
    public GameObject BuffVFXPrefab; // Prefab VFX à appliquer aux unités

    public override void ExecuteEffect(GameObject caster, int perfectCount) //
    {
        float totalDuration = BuffDuration + (BonusDurationPerPerfectInput * perfectCount); //

        // Utiliser AllyUnitRegistry pour obtenir les unités alliées actives
        if (AllyUnitRegistry.Instance == null)
        {
            Debug.LogWarning("[GlobalUnitBuffEffect] AllyUnitRegistry.Instance non trouvé. Impossible d'appliquer le buff.");
            return;
        }

        IReadOnlyList<AllyUnit> activeAllyUnits = AllyUnitRegistry.Instance.ActiveAllyUnits; //
        if (activeAllyUnits.Count == 0)
        {
            Debug.LogWarning("[GlobalUnitBuffEffect] Aucune unité alliée active trouvée dans AllyUnitRegistry.");
            return;
        }

        Debug.Log($"[GlobalUnitBuffEffect] Application du buff '{Stat}' (x{BuffMultiplier}) pour {totalDuration}s à {activeAllyUnits.Count} unités alliées.");

        foreach (AllyUnit allyUnit in activeAllyUnits)
        {
            if (allyUnit != null)
            {
                allyUnit.ApplyBuff(Stat, BuffMultiplier, totalDuration);
                
                // Appliquer VFX si spécifié
                if (BuffVFXPrefab != null)
                {
                    ApplyVFXToUnit(allyUnit);
                }
            }
        }
    }

    public override string GetEffectDescription() //
    {
        // Calcule la durée totale avec un exemple d'un input parfait pour la description.
        float exampleTotalDuration = BuffDuration + (BonusDurationPerPerfectInput * 1); //
        // Calcule le pourcentage d'augmentation pour l'affichage.
        int percentBuff = Mathf.RoundToInt((BuffMultiplier - 1f) * 100); //
        // Retourne la description de l'effet.
        return $"Augmente {Stat} de {percentBuff}% pour toutes les unités alliées pendant {exampleTotalDuration}s.";
    }
    
    /// <summary>
    /// Applique le VFX à une unité alliée spécifique
    /// </summary>
    /// <param name="allyUnit">L'unité alliée sur laquelle appliquer le VFX</param>
    private void ApplyVFXToUnit(AllyUnit allyUnit)
    {
        if (BuffVFXPrefab == null || allyUnit == null) return;
        
        // Créer le VFX à la position de l'unité
        GameObject vfxInstance = Object.Instantiate(BuffVFXPrefab, allyUnit.transform.position, Quaternion.identity);
        
        // Sauvegarder la taille originale du prefab avant de l'attacher
        Vector3 originalScale = vfxInstance.transform.localScale;
        
        // Attacher le VFX à l'unité pour qu'il suive ses mouvements
        vfxInstance.transform.SetParent(allyUnit.transform);
        
        // Restaurer la taille originale du prefab
        vfxInstance.transform.localScale = originalScale;
        
        // Lancer une coroutine pour détruire le VFX après 1 seconde
        if (allyUnit != null)
        {
            allyUnit.StartCoroutine(DestroyVFXAfterDelay(vfxInstance, 1f));
        }
        
        Debug.Log($"[GlobalUnitBuffEffect] VFX appliqué à {allyUnit.name} pendant 1 seconde.");
    }
    
    /// <summary>
    /// Coroutine pour détruire le VFX après un délai spécifié
    /// </summary>
    /// <param name="vfxInstance">Instance du VFX à détruire</param>
    /// <param name="delay">Délai en secondes avant destruction</param>
    /// <returns></returns>
    private IEnumerator DestroyVFXAfterDelay(GameObject vfxInstance, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (vfxInstance != null)
        {
            Object.Destroy(vfxInstance);
        }
    }
    
}