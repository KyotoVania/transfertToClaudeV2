using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Gameplay;
using ScriptableObjects;
using ScriptableObjects.Endless;
using UI.Endless;
using UnityEngine;
using User_Interface.Controllers;
using Random = UnityEngine.Random;

namespace Managers
{
    /// <summary>
    /// Central brain for Endless mode: cycles objectives, tracks difficulty, spawns targets and drives reward drafts.
    /// </summary>
    public class EndlessGameManager : MonoBehaviour
    {
        public static EndlessGameManager Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private List<EndlessObjective_SO> availableObjectives = new List<EndlessObjective_SO>();
        [SerializeField] private List<PassiveBuff_SO> availableBuffs = new List<PassiveBuff_SO>();
        [SerializeField] private float baseSpawnInterval = 10f;
        [SerializeField] private float minSpawnInterval = 3f;
        [SerializeField] private int objectiveSpawnDistance = 4;
        [SerializeField] private GameObject basicPressureEnemyPrefab;

        [Header("Biome Sequencing")]
        [SerializeField] private MapTransformer mapTransformer;
        [SerializeField] private List<BiomeProfile_SO> biomesSequence = new List<BiomeProfile_SO>();
        [SerializeField] private int objectivesPerBiome = 3;

        [Header("References")]
        [SerializeField] private BuffSelectionUI buffSelectionUI;

        private HexGridManager _hexGridManager;
        private UnitSpawner _unitSpawner;
        private VisualMoodManager _visualMoodManager;

        private EndlessObjective_SO _currentObjective;
        private GameObject _spawnedTarget;
        private Coroutine _pressureRoutine;
        private Coroutine _waveRoutine;

        private int _currentBiomeIndex = 0;

        public enum EndlessState
        {
            Initialization,
            ActivePhase,
            RewardPhase,
            TransitionPhase
        }

        public EndlessState CurrentState { get; private set; } = EndlessState.Initialization;

        public int ObjectivesCompletedCount { get; private set; }
        public float DifficultyMultiplier { get; private set; } = 1f;

        /// <summary>
        /// Événement déclenché lorsqu'un nouvel objectif démarre.
        /// </summary>
        public event Action<EndlessObjective_SO> OnObjectiveStart;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _hexGridManager = HexGridManager.Instance;
            _unitSpawner = FindFirstObjectByType<UnitSpawner>();
            _visualMoodManager = FindFirstObjectByType<VisualMoodManager>();
            if (mapTransformer == null)
            {
                mapTransformer = FindFirstObjectByType<MapTransformer>();
            }

            if (_hexGridManager == null)
            {
                Debug.LogError("[EndlessGameManager] HexGridManager introuvable.", this);
                enabled = false;
                return;
            }

            if (_unitSpawner == null)
            {
                Debug.LogWarning("[EndlessGameManager] UnitSpawner introuvable - la pression ennemie sera désactivée.", this);
            }

            if (buffSelectionUI == null)
            {
                buffSelectionUI = FindFirstObjectByType<BuffSelectionUI>();
            }

            StartNextObjective();
        }

        /// <summary>
        /// Lance un nouvel objectif aléatoire.
        /// </summary>
        public void StartNextObjective()
        {
            if (availableObjectives == null || availableObjectives.Count == 0)
            {
                Debug.LogWarning("[EndlessGameManager] Aucun EndlessObjective configuré.");
                return;
            }

            _currentObjective = availableObjectives[Random.Range(0, availableObjectives.Count)];
            if (_currentObjective == null || _currentObjective.TargetPrefab == null)
            {
                Debug.LogWarning("[EndlessGameManager] Objective ou TargetPrefab manquant.");
                return;
            }

            Vector3? spawnPosition = FindObjectiveSpawnPosition();
            if (spawnPosition == null)
            {
                Debug.LogWarning("[EndlessGameManager] Impossible de trouver une tuile valide pour l'objectif.");
                return;
            }

            _spawnedTarget = Instantiate(_currentObjective.TargetPrefab, spawnPosition.Value, Quaternion.identity);

            SubscribeToTargetEvents(_spawnedTarget);

            CurrentState = EndlessState.ActivePhase;
            NotifyObjectiveStart(_currentObjective);
            StartWaveOrPressure();
            Debug.Log($"[EndlessGameManager] Nouvel objectif: {_currentObjective.Title} | Type: {_currentObjective.Type}");
        }

        /// <summary>
        /// Termine l'objectif courant et déclenche la phase de récompense.
        /// </summary>
        public void CompleteObjective()
        {
            if (_spawnedTarget != null)
            {
                Destroy(_spawnedTarget);
            }

            ObjectivesCompletedCount++;
            DifficultyMultiplier += 0.1f;

            GrantRewards();
            CurrentState = EndlessState.RewardPhase;
            StopPressureRoutine();
            StopWaveRoutine();

            OpenBuffDraft();
        }

        private void GrantRewards()
        {
            int goldReward = Mathf.RoundToInt((_currentObjective?.BaseGoldReward ?? 0) * DifficultyMultiplier);
            int xpReward = Mathf.RoundToInt((_currentObjective?.BaseXPReward ?? 0) * DifficultyMultiplier);

            if (GoldController.Instance != null)
            {
                GoldController.Instance.AddGold(goldReward);
            }

            if (PlayerDataManager.Instance != null)
            {
                PlayerDataManager.Instance.AddExperience(xpReward);
            }

            Debug.Log($"[EndlessGameManager] Récompenses : {goldReward} or, {xpReward} XP.");
        }

        private void OpenBuffDraft()
        {
            if (buffSelectionUI == null)
            {
                Debug.LogWarning("[EndlessGameManager] BuffSelectionUI non assigné.");
                return;
            }

            if (availableBuffs == null || availableBuffs.Count == 0)
            {
                Debug.LogWarning("[EndlessGameManager] Aucun buff disponible pour le draft.");
                return;
            }

            var choices = availableBuffs.OrderBy(_ => Random.value).Take(3).ToList();
            buffSelectionUI.ShowSelection(choices);
        }

        /// <summary>
        /// À appeler après la sélection d'un buff pour enchaîner.
        /// </summary>
        public void ResumeAfterReward()
        {
            bool shouldChangeBiome = objectivesPerBiome > 0 && ObjectivesCompletedCount > 0 && ObjectivesCompletedCount % objectivesPerBiome == 0;

            if (shouldChangeBiome && biomesSequence != null && biomesSequence.Count > 0 && mapTransformer != null)
            {
                CurrentState = EndlessState.TransitionPhase;
                _currentBiomeIndex = (_currentBiomeIndex + 1) % biomesSequence.Count;
                var nextBiome = biomesSequence[_currentBiomeIndex];
                mapTransformer.TransitionToBiome(nextBiome, null, StartNextObjective);
            }
            else
            {
                StartNextObjective();
            }
        }

        private void SubscribeToTargetEvents(GameObject target)
        {
            if (target == null) return;

            if (target.TryGetComponent<Unit>(out var unit))
            {
                unit.OnUnitDestroyed += HandleTargetDestroyed;
            }
            else if (target.TryGetComponent<Building>(out var building))
            {
                Building.OnBuildingTeamChangedGlobal += HandleBuildingCaptured;
            }
        }

        private void UnsubscribeFromTargetEvents(GameObject target)
        {
            if (target == null) return;

            if (target.TryGetComponent<Unit>(out var unit))
            {
                unit.OnUnitDestroyed -= HandleTargetDestroyed;
            }
            else if (target.TryGetComponent<Building>(out var building))
            {
                Building.OnBuildingTeamChangedGlobal -= HandleBuildingCaptured;
            }
        }

        private void HandleTargetDestroyed()
        {
            if (CurrentState != EndlessState.ActivePhase) return;
            UnsubscribeFromTargetEvents(_spawnedTarget);
            CompleteObjective();
        }

        private void HandleBuildingCaptured(Building building, TeamType oldTeam, TeamType newTeam)
        {
            if (CurrentState != EndlessState.ActivePhase) return;

            if (_spawnedTarget != null && building != null && building.gameObject == _spawnedTarget && newTeam == TeamType.Player)
            {
                Building.OnBuildingTeamChangedGlobal -= HandleBuildingCaptured;
                CompleteObjective();
            }
        }

        private Vector3? FindObjectiveSpawnPosition()
        {
            if (_hexGridManager == null) return null;

            var allTiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
            if (allTiles == null || allTiles.Length == 0) return null;

            Vector3? playerHqPos = FindPlayerHQPosition();
            Tile candidate = null;
            foreach (var tile in allTiles.OrderBy(_ => Random.value))
            {
                if (tile == null || tile.tileType != TileType.Ground || tile.IsOccupied) continue;

                if (playerHqPos.HasValue)
                {
                    float dist = Vector3.Distance(tile.transform.position, playerHqPos.Value);
                    if (dist < objectiveSpawnDistance) continue;
                }

                candidate = tile;
                break;
            }

            return candidate != null ? candidate.transform.position : null;
        }

        private Vector3? FindPlayerHQPosition()
        {
            var hq = FindFirstObjectByType<PlayerBuilding>();
            return hq != null ? hq.transform.position : (Vector3?)null;
        }

        private void StartWaveOrPressure()
        {
            StopPressureRoutine();
            StopWaveRoutine();

            if (_currentObjective != null && _currentObjective.DefendingWaves != null && _currentObjective.DefendingWaves.Count > 0)
            {
                _waveRoutine = StartCoroutine(ExecuteObjectiveWaves(_currentObjective));
            }
            else
            {
                RestartPressureRoutine();
            }
        }

        private void RestartPressureRoutine()
        {
            StopPressureRoutine();
            if (_unitSpawner == null) return;
            _pressureRoutine = StartCoroutine(PressureSpawnLoop());
        }

        private void StopPressureRoutine()
        {
            if (_pressureRoutine != null)
            {
                StopCoroutine(_pressureRoutine);
                _pressureRoutine = null;
            }
        }

        private void StopWaveRoutine()
        {
            if (_waveRoutine != null)
            {
                StopCoroutine(_waveRoutine);
                _waveRoutine = null;
            }
        }

        private IEnumerator PressureSpawnLoop()
        {
            while (CurrentState == EndlessState.ActivePhase)
            {
                float interval = Mathf.Max(minSpawnInterval, baseSpawnInterval / DifficultyMultiplier);
                yield return new WaitForSeconds(interval);

                if (CurrentState != EndlessState.ActivePhase) yield break;

                SpawnPressureEnemy();
            }
        }

        private IEnumerator ExecuteObjectiveWaves(EndlessObjective_SO objective)
        {
            if (objective == null)
            {
                yield break;
            }

            var waves = objective.DefendingWaves;
            if (waves == null || waves.Count == 0)
            {
                RestartPressureRoutine();
                yield break;
            }

            Wave_SO lastWave = null;
            foreach (var wave in waves)
            {
                lastWave = wave;
                yield return StartCoroutine(RunWave(wave));
                if (CurrentState != EndlessState.ActivePhase) yield break;
            }

            // Boucle sur la dernière vague tant que l'objectif n'est pas complété
            while (CurrentState == EndlessState.ActivePhase && lastWave != null)
            {
                yield return StartCoroutine(RunWave(lastWave));
            }
        }

        private IEnumerator RunWave(Wave_SO wave)
        {
            if (wave == null || wave.spawnRequests == null || wave.spawnRequests.Count == 0)
            {
                yield break;
            }

            float waveStart = Time.time;
            foreach (var request in wave.spawnRequests.OrderBy(r => r.spawnDelay))
            {
                float elapsed = Time.time - waveStart;
                float wait = Mathf.Max(0f, request.spawnDelay - elapsed);
                if (wait > 0f)
                {
                    yield return new WaitForSeconds(wait);
                }

                if (CurrentState != EndlessState.ActivePhase) yield break;

                for (int i = 0; i < request.count; i++)
                {
                    SpawnWaveEnemy(request);
                }
            }
        }

        /// <summary>
        /// Notifie le début d'un nouvel objectif aux systèmes intéressés (UI, events).
        /// </summary>
        /// <param name="objective">Objectif en cours.</param>
        private void NotifyObjectiveStart(EndlessObjective_SO objective)
        {
            OnObjectiveStart?.Invoke(objective);

            var gameUiManager = FindFirstObjectByType<GameUIManager>();
            if (gameUiManager != null)
            {
                var title = objective != null ? objective.Title : "Nouvel objectif";
                gameUiManager.SendMessage("OnObjectiveStarted", title, SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// Fait apparaître une unité d'une requête de vague sur une tuile périphérique.
        /// </summary>
        /// <param name="request">Requête de spawn de la vague.</param>
        private void SpawnWaveEnemy(Wave_SO.UnitSpawnRequest request)
        {
            if (request == null || request.unitPrefab == null)
            {
                return;
            }

            var tile = FindRandomEdgeTile();
            if (tile == null)
            {
                Debug.LogWarning("[EndlessGameManager] Aucune tuile périphérique disponible pour le spawn de vague.");
                return;
            }

            Instantiate(request.unitPrefab, tile.transform.position, Quaternion.identity);
        }

        private void SpawnPressureEnemy()
        {
            if (_unitSpawner == null) return;

            var enemyPrefab = GetBasicEnemyPrefab();
            if (enemyPrefab == null)
            {
                Debug.LogWarning("[EndlessGameManager] Aucun prefab d'ennemi basique configuré pour la pression.");
                return;
            }

            var tile = FindRandomEdgeTile();
            if (tile == null)
            {
                Debug.LogWarning("[EndlessGameManager] Aucune tuile périphérique trouvée pour le spawn de pression.");
                return;
            }

            Instantiate(enemyPrefab, tile.transform.position, Quaternion.identity);
        }

        private GameObject GetBasicEnemyPrefab()
        {
            if (basicPressureEnemyPrefab != null)
            {
                return basicPressureEnemyPrefab;
            }

            // Placeholder: try to read from a Wave_SO if provided in current objective
            var wave = _currentObjective?.DefendingWaves?.FirstOrDefault();
            if (wave != null && wave.spawnRequests.Count > 0)
            {
                return wave.spawnRequests[0].unitPrefab;
            }
            return null;
        }

        private Tile FindRandomEdgeTile()
        {
            var tiles = FindObjectsByType<Tile>(FindObjectsSortMode.None);
            if (tiles == null || tiles.Length == 0) return null;

            var edgeTiles = tiles.Where(t => t != null && t.tileType == TileType.Ground && !t.IsOccupied && IsEdgeTile(t)).ToList();
            if (edgeTiles.Count == 0) return null;

            return edgeTiles[Random.Range(0, edgeTiles.Count)];
        }

        private bool IsEdgeTile(Tile tile)
        {
            if (tile == null) return false;
            // Edge defined as having fewer than 6 neighbors in hex grid
            var neighbors = tile.Neighbors;
            return neighbors == null || neighbors.Count < 6;
        }

        private void OnDestroy()
        {
            UnsubscribeFromTargetEvents(_spawnedTarget);
            Building.OnBuildingTeamChangedGlobal -= HandleBuildingCaptured;
        }
    }
}