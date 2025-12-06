using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;
using ScriptableObjects.Endless;
using UnityEngine;

/// <summary>
/// Contrôleur de transformation de carte pour les transitions de biome en temps réel.
/// Remplace progressivement les tuiles de la grille avec des prefabs du nouveau biome
/// en appliquant un effet de vague depuis un centre donné.
/// </summary>
public class MapTransformer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private HexGridManager hexGridManager;
    [SerializeField] private VisualMoodManager visualMoodManager;

    [Header("Transition Settings")]
    [Tooltip("Durée en secondes entre chaque anneau de propagation de la vague.")]
    [SerializeField] private float ringDelay = 0.05f;
    [Tooltip("Scale cible pour l'effet pop des nouvelles tuiles.")]
    [SerializeField] private float popTargetScale = 1f;
    [Tooltip("Scale initial des tuiles lors du pop.")]
    [SerializeField] private float popStartScale = 0f;
    [Tooltip("Durée de l'animation de pop des nouvelles tuiles.")]
    [SerializeField] private float popDuration = 0.3f;
    [Tooltip("Durée de l'animation de sink des anciennes tuiles.")]
    [SerializeField] private float sinkDuration = 0.25f;
    [Tooltip("Distance minimale à la caméra pour ignorer l'effet visuel (optimisation simple)")]
    [SerializeField] private float cullEffectDistance = 120f;

    private Coroutine _transitionRoutine;

    private void Awake()
    {
        if (hexGridManager == null)
        {
            hexGridManager = FindFirstObjectByType<HexGridManager>();
        }

        if (visualMoodManager == null)
        {
            visualMoodManager = FindFirstObjectByType<VisualMoodManager>();
        }
    }

    /// <summary>
    /// Démarre une transition vers un nouveau biome.
    /// </summary>
    /// <param name="newBiome">Profil de biome cible.</param>
    /// <param name="center">Point de départ de la vague (utiliser la position du joueur ou du centre de carte).</param>
    /// <param name="onComplete">Callback invoqué une fois la transition terminée.</param>
    public void TransitionToBiome(BiomeProfile_SO newBiome, Vector3? center = null, Action onComplete = null)
    {
        if (newBiome == null)
        {
            Debug.LogWarning("[MapTransformer] TransitionToBiome appelé avec un biome null.");
            return;
        }

        if (hexGridManager == null)
        {
            Debug.LogError("[MapTransformer] HexGridManager manquant, impossible de transformer la carte.");
            return;
        }

        if (visualMoodManager != null && newBiome.MoodData != null)
        {
            visualMoodManager.SetMood(newBiome.MoodData.moodType);
        }

        if (_transitionRoutine != null)
        {
            StopCoroutine(_transitionRoutine);
        }

        _transitionRoutine = StartCoroutine(AnimateGridTransition(newBiome, center, onComplete));
    }

    private IEnumerator AnimateGridTransition(BiomeProfile_SO biome, Vector3? center, Action onComplete)
    {
        // Récupérer toutes les tuiles valides de la grille
        var tiles = FindObjectsByType<Tile>(FindObjectsSortMode.None)
            .Where(t => t != null && t.gameObject.activeInHierarchy && hexGridManager != null && hexGridManager.GetTileAt(t.column, t.row) == t)
            .ToList();

        if (tiles.Count == 0)
        {
            Debug.LogWarning("[MapTransformer] Aucune tuile trouvée pour la transformation.");
            yield break;
        }

        Vector3 centerPos = center ?? hexGridManager.transform.position;

        // Trier par distance pour créer des anneaux
        var ordered = tiles.OrderBy(t => Vector3.SqrMagnitude(t.transform.position - centerPos)).ToList();

        // Regrouper par distance approchée (anneaux) en utilisant la distance arrondie
        var rings = new List<List<Tile>>();
        const float ringThreshold = 0.25f; // marge sur la distance au carré

        foreach (var tile in ordered)
        {
            float dist = Vector3.Distance(tile.transform.position, centerPos);
            List<Tile> ring = rings.FirstOrDefault(r => r.Count > 0 && Mathf.Abs(Vector3.Distance(r[0].transform.position, centerPos) - dist) <= ringThreshold);
            if (ring == null)
            {
                ring = new List<Tile>();
                rings.Add(ring);
            }
            ring.Add(tile);
        }

        foreach (var ring in rings)
        {
            foreach (var oldTile in ring)
            {
                if (oldTile == null) continue;

                // Choisir le prefab approprié selon le type de tuile
                GameObject prefabToSpawn = oldTile.tileType == TileType.Ground ? biome.GroundTilePrefab : biome.ObstacleTilePrefab;
                if (prefabToSpawn == null)
                {
                    // Si aucun prefab n'est disponible, on saute la transformation de cette tuile
                    continue;
                }

                Vector3 spawnPos = oldTile.transform.position;
                Quaternion spawnRot = oldTile.transform.rotation;

                // Instancier la nouvelle tuile
                GameObject newTileGO = Instantiate(prefabToSpawn, spawnPos, spawnRot, oldTile.transform.parent);
                Tile newTile = newTileGO.GetComponent<Tile>();
                if (newTile == null)
                {
                    Debug.LogWarning("[MapTransformer] Prefab de tuile sans composant Tile. Destruction de l'instance.");
                    Destroy(newTileGO);
                    continue;
                }

                // Copier les coordonnées et mettre à jour la grille
                newTile.column = oldTile.column;
                newTile.row = oldTile.row;
                newTile.SetGridManager(hexGridManager);

                Vector2Int arrayIndex = new Vector2Int(newTile.column - hexGridManager.minColumn, newTile.row - hexGridManager.minRow);
                // Sécuriser les bornes
                if (arrayIndex.x >= 0 && arrayIndex.x < (hexGridManager.maxColumn - hexGridManager.minColumn + 1) &&
                    arrayIndex.y >= 0 && arrayIndex.y < (hexGridManager.maxRow - hexGridManager.minRow + 1))
                {
                    // Mise à jour atomique de la référence dans le tableau
                    var gridField = typeof(HexGridManager).GetField("tileGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (gridField != null)
                    {
                        var grid = gridField.GetValue(hexGridManager) as Tile[,];
                        if (grid != null)
                        {
                            grid[arrayIndex.x, arrayIndex.y] = newTile;
                        }
                    }
                }

                // Reparenter les unités/bâtiments/environnements
                var occupantUnit = oldTile.currentUnit;
                var occupantBuilding = oldTile.currentBuilding;
                var occupantEnvironment = oldTile.currentEnvironment;

                // Détacher des anciennes références pour éviter les doublons
                oldTile.RemoveUnit();
                oldTile.RemoveBuilding();
                oldTile.RemoveEnvironment();

                if (occupantUnit != null)
                {
                    occupantUnit.transform.SetParent(newTile.transform);
                    occupantUnit.transform.position = newTile.transform.position + Vector3.up * 0.1f;
                    newTile.AssignUnit(occupantUnit);

                    // Petit saut visuel
                    TryJumpEffect(occupantUnit.gameObject, 0.35f);
                }

                if (occupantBuilding != null)
                {
                    occupantBuilding.transform.SetParent(newTile.transform);
                    occupantBuilding.transform.position = newTile.transform.position + Vector3.up * 0.1f;
                    newTile.AssignBuilding(occupantBuilding);
                }

                if (occupantEnvironment != null)
                {
                    occupantEnvironment.transform.SetParent(newTile.transform);
                    occupantEnvironment.transform.position = newTile.transform.position;
                    newTile.AssignEnvironment(occupantEnvironment);
                }

                // Animation: sink l'ancienne tuile puis pop la nouvelle
                AnimateTileSwap(oldTile.gameObject, newTileGO);

                // Nettoyer l'ancienne tuile
                Destroy(oldTile.gameObject, sinkDuration + 0.05f);
            }

            yield return new WaitForSeconds(ringDelay);
        }

        // Reconstruction des voisins après transformation
        hexGridManager.StartCoroutine(RebuildNeighborsNextFrame());

        onComplete?.Invoke();
    }

    private IEnumerator RebuildNeighborsNextFrame()
    {
        yield return null; // attendre la fin de frame
        var rebuildMethod = typeof(HexGridManager).GetMethod("SetupNeighborsForAllTiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        rebuildMethod?.Invoke(hexGridManager, null);
    }

    private void AnimateTileSwap(GameObject oldTile, GameObject newTile)
    {
        if (oldTile == null || newTile == null) return;

        // Simple animation via LeanTween si disponible, sinon fallback scale lerp coroutine
        bool canLean = (typeof(LeanTween) != null);
        Transform oldT = oldTile.transform;
        Transform newT = newTile.transform;

        // Initial scales
        newT.localScale = Vector3.one * popStartScale;

        if (canLean)
        {
            LeanTween.scale(oldTile, Vector3.zero, sinkDuration);
            LeanTween.scale(newTile, Vector3.one * popTargetScale, popDuration).setEase(LeanTweenType.easeOutElastic);
        }
        else
        {
            StartCoroutine(ScaleRoutine(oldT, Vector3.zero, sinkDuration));
            StartCoroutine(ScaleRoutine(newT, Vector3.one * popTargetScale, popDuration));
        }
    }

    private void TryJumpEffect(GameObject target, float height)
    {
        if (target == null) return;
        bool canLean = (typeof(LeanTween) != null);
        Vector3 startPos = target.transform.position;
        Vector3 apex = startPos + Vector3.up * height;

        if (canLean)
        {
            LeanTween.moveY(target, apex.y, 0.15f).setEase(LeanTweenType.easeOutQuad).setOnComplete(() =>
            {
                LeanTween.moveY(target, startPos.y, 0.15f).setEase(LeanTweenType.easeInQuad);
            });
        }
        else
        {
            StartCoroutine(JumpRoutine(target.transform, startPos, apex, 0.3f));
        }
    }

    private IEnumerator JumpRoutine(Transform target, Vector3 start, Vector3 apex, float duration)
    {
        if (target == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            // simple parabole
            float heightLerp = lerp <= 0.5f ? lerp * 2f : (1f - lerp) * 2f;
            float y = Mathf.Lerp(start.y, apex.y, heightLerp);
            target.position = new Vector3(start.x, y, start.z);
            yield return null;
        }
        target.position = new Vector3(start.x, start.y, start.z);
    }

    private IEnumerator ScaleRoutine(Transform target, Vector3 destScale, float duration)
    {
        if (target == null) yield break;
        Vector3 start = target.localScale;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float lerp = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
            target.localScale = Vector3.Lerp(start, destScale, lerp);
            yield return null;
        }
        target.localScale = destScale;
    }
}