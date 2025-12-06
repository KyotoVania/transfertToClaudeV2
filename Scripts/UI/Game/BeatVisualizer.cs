using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages the visual representation of music beats in the game UI.
/// Spawns and animates beat notes that travel along a lane to provide
/// visual timing feedback to players. Handles pulse effects and note management.
/// </summary>
public class BeatVisualizer : MonoBehaviour
{
    [Header("References")]
    /// <summary>Prefab for beat note objects that travel along the lane.</summary>
    [SerializeField] private GameObject notePrefab;
    /// <summary>Starting point for beat notes in the UI lane.</summary>
    [SerializeField] private RectTransform laneStartPoint;
    /// <summary>Target hit zone where notes should be activated.</summary>
    [SerializeField] private RectTransform hitZone;

    [Header("Timing Configuration")]
    /// <summary>Time for notes to travel from start to hit zone, measured in beats.</summary>
    [SerializeField] private float travelTimeInBeats = 2f;

    [Header("Pulse Effect")]
    /// <summary>Duration of the pulse animation in seconds.</summary>
    [Tooltip("Duration of the pulse animation in seconds.")]
    [SerializeField] private float pulseDuration = 0.15f;
    /// <summary>Magnitude of the pulse effect (e.g., 1.2 = 120% of original size).</summary>
    [Tooltip("Magnitude of the pulse effect (e.g., 1.2 = 120% of original size).")]
    [SerializeField] private float pulseMagnitude = 1.2f;

    /// <summary>Reference to the music manager for beat synchronization.</summary>
    private MusicManager musicManager;
    /// <summary>List of currently active beat notes being managed.</summary>
    private readonly List<BeatNote> activeNotes = new List<BeatNote>();

    /// <summary>
    /// Initializes the beat visualizer by connecting to the music manager and setting up event subscriptions.
    /// </summary>
    void Start()
    {
        musicManager = MusicManager.Instance;
        if (musicManager == null)
        {
            Debug.LogError("[BeatVisualizer] MusicManager.Instance not found!");
            enabled = false;
            return;
        }
        musicManager.OnBeat += SpawnNoteOnBeat;
        SequenceFlagDisplay.OnFlagStateChanged += HandleFlagStateChanged;
    }

    /// <summary>
    /// Cleans up event subscriptions when the beat visualizer is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (musicManager != null) musicManager.OnBeat -= SpawnNoteOnBeat;
        SequenceFlagDisplay.OnFlagStateChanged -= HandleFlagStateChanged;
    }

    private void SpawnNoteOnBeat(float beatDuration)
    {
        if (laneStartPoint != null)
        {
            StartCoroutine(Pulse(laneStartPoint));
        }

        // Le reste de la fonction est inchangé
        GameObject noteInstance = Instantiate(notePrefab, laneStartPoint.position, Quaternion.identity, transform);
        BeatNote noteScript = noteInstance.GetComponent<BeatNote>();
        if (noteScript != null)
        {
            float spawnBeat = musicManager.PublicBeatCount;
            noteScript.Initialize(laneStartPoint.position, hitZone.position, travelTimeInBeats, spawnBeat, () => activeNotes.Remove(noteScript));
            activeNotes.Add(noteScript);
        }
    }

    private void HandleFlagStateChanged(Color timingColor)
    {
        // Apply hit zone feedback for all hit types (including missed)
        if (hitZone != null)
        {
            StartCoroutine(PulseWithColor(hitZone, timingColor));
        }

        // Don't process notes if it's a miss (red)
        if (timingColor == Color.red) return;

        if (activeNotes.Count == 0) return;

        BeatNote bestNoteToHit = null;
        float minDistance = float.MaxValue;

        foreach (var note in activeNotes)
        {
            if (note == null) continue;
            float distance = Mathf.Abs(1.0f - note.GetCurrentProgress());
            if (distance < minDistance)
            {
                minDistance = distance;
                bestNoteToHit = note;
            }
        }

        if (bestNoteToHit != null)
        {
            bestNoteToHit.ProcessHit(timingColor);
            activeNotes.Remove(bestNoteToHit);
        }
    }

    /// <summary>
    /// Anime la taille d'un élément UI pour créer un effet de "pulsation".
    /// </summary>
    private IEnumerator Pulse(RectTransform rectTransform)
    {
        Vector3 originalScale = Vector3.one; // On part du principe que l'échelle de base est (1,1,1)
        Vector3 targetScale = originalScale * pulseMagnitude;
        float halfDuration = pulseDuration / 2;

        // Étape 1 : Agrandissement
        float timer = 0f;
        while (timer < halfDuration)
        {
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null; // Attend la prochaine frame
        }

        // Étape 2 : Rétrecissement
        timer = 0f;
        while (timer < halfDuration)
        {
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, timer / halfDuration);
            timer += Time.deltaTime;
            yield return null; // Attend la prochaine frame
        }

        // Assure que l'échelle revient exactement à sa valeur d'origine
        rectTransform.localScale = originalScale;
    }

    /// <summary>
    /// Anime la taille et la couleur d'un élément UI pour créer un effet de "pulsation" colorée.
    /// </summary>
    private IEnumerator PulseWithColor(RectTransform rectTransform, Color feedbackColor)
    {
        Vector3 originalScale = Vector3.one;
        Vector3 targetScale = originalScale * pulseMagnitude;
        float halfDuration = pulseDuration / 2;

        // Get the image component to change color
        UnityEngine.UI.Image image = rectTransform.GetComponent<UnityEngine.UI.Image>();
        Color originalColor = image != null ? image.color : Color.white;

        // Étape 1 : Agrandissement avec changement de couleur
        float timer = 0f;
        while (timer < halfDuration)
        {
            float progress = timer / halfDuration;
            rectTransform.localScale = Vector3.Lerp(originalScale, targetScale, progress);
            
            if (image != null)
            {
                image.color = Color.Lerp(originalColor, feedbackColor, progress);
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // Étape 2 : Rétrecissement avec retour à la couleur originale
        timer = 0f;
        while (timer < halfDuration)
        {
            float progress = timer / halfDuration;
            rectTransform.localScale = Vector3.Lerp(targetScale, originalScale, progress);
            
            if (image != null)
            {
                image.color = Color.Lerp(feedbackColor, originalColor, progress);
            }
            
            timer += Time.deltaTime;
            yield return null;
        }

        // Assure que tout revient exactement aux valeurs d'origine
        rectTransform.localScale = originalScale;
        if (image != null)
        {
            image.color = originalColor;
        }
    }
}