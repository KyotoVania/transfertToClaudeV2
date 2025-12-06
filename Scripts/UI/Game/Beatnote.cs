using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// Represents an individual beat note that travels along the visualizer lane.
/// Handles movement animation, hit detection, visual feedback for different hit qualities,
/// and automatic cleanup after processing. Provides timing feedback to players.
/// </summary>
[RequireComponent(typeof(Image))]
public class BeatNote : MonoBehaviour
{
    [Header("Visual References")]
    /// <summary>Main image component for the beat note.</summary>
    [SerializeField] private Image noteImage;
    /// <summary>Sprite to display when the note is hit with perfect timing.</summary>
    [SerializeField] private Sprite perfectHitSprite;
    /// <summary>Sprite to display when the note is hit with good timing.</summary>
    [SerializeField] private Sprite goodHitSprite;
    /// <summary>Sprite to display when the note is missed or hit poorly.</summary>
    [SerializeField] private Sprite failedHitSprite;

    [Header("Configuration")]
    /// <summary>Time window in seconds for successful hit detection.</summary>
    [SerializeField] private float hitWindow = 0.15f;
    /// <summary>Duration for the fade-out animation after processing.</summary>
    [SerializeField] private float fadeOutDuration = 0.8f;

    // Internal variables
    /// <summary>Starting position for the note's movement.</summary>
    private Vector3 startPosition;
    /// <summary>Target position for the note's movement.</summary>
    private Vector3 endPosition;
    /// <summary>Travel time for the note measured in beats.</summary>
    private float travelTimeInBeats;
    /// <summary>Beat number when this note was spawned.</summary>
    private float startBeat;
    /// <summary>Reference to the music manager for timing calculations.</summary>
    private MusicManager musicManager;
    /// <summary>Callback to execute when the note is cleaned up.</summary>
    private Action onCleanup;

    /// <summary>Flag indicating if this note has been processed (hit or missed).</summary>
    private bool hasBeenProcessed = false;
    /// <summary>Time elapsed since the note was processed.</summary>
    private float timeSinceProcessed = 0f;

    /// <summary>
    /// Initializes the beat note by ensuring the image component is assigned.
    /// </summary>
    void Awake()
    {
        if (noteImage == null) noteImage = GetComponent<Image>();
    }

    /// <summary>
    /// Initializes the beat note with movement parameters and timing information.
    /// </summary>
    /// <param name="startPos">Starting position for the note's movement.</param>
    /// <param name="endPos">Target position for the note's movement.</param>
    /// <param name="travelBeats">Time for the note to travel, measured in beats.</param>
    /// <param name="spawnBeat">Beat number when this note was spawned.</param>
    /// <param name="onCleanupCallback">Callback to execute when the note is destroyed.</param>
    public void Initialize(Vector3 startPos, Vector3 endPos, float travelBeats, float spawnBeat, Action onCleanupCallback)
    {
        this.startPosition = startPos;
        this.endPosition = endPos;
        this.travelTimeInBeats = travelBeats;
        this.startBeat = spawnBeat;
        this.onCleanup = onCleanupCallback;
        this.musicManager = MusicManager.Instance;
    }

    void Update()
    {
        if (musicManager == null) { DestroyNote(); return; }

        float progress = GetCurrentProgress();

        // --- LA CORRECTION EST ICI ---
        // On utilise LerpUnclamped pour permettre à la note de dépasser sa destination.
        transform.position = Vector3.LerpUnclamped(startPosition, endPosition, progress);

        if (hasBeenProcessed)
        {
            timeSinceProcessed += Time.deltaTime;
            float alpha = Mathf.Max(0, 1.0f - (timeSinceProcessed / fadeOutDuration));
            
            // Preserve the color but fade the alpha
            Color currentColor = noteImage.color;
            noteImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, alpha);

            if (alpha <= 0)
            {
                DestroyNote();
            }
        }
        else
        {
            if (progress > 1.0f + hitWindow)
            {
                ProcessHit(Color.red);
            }
        }
    }

    public void ProcessHit(Color timingColor)
    {
        if (hasBeenProcessed) return;
        hasBeenProcessed = true;

        // Visual feedback with sprite change
        if (timingColor == Color.green)
        {
            if (perfectHitSprite != null) 
            {
                noteImage.sprite = perfectHitSprite;
                noteImage.color = Color.green; // Add color tint for better visibility
            }
            Debug.Log("[BeatNote] PERFECT HIT!");
        }
        else if (timingColor == Color.yellow)
        {
            if (goodHitSprite != null) 
            {
                noteImage.sprite = goodHitSprite;
                noteImage.color = Color.yellow; // Add color tint
            }
            Debug.Log("[BeatNote] GOOD HIT!");
        }
        else
        {
            if (failedHitSprite != null) 
            {
                noteImage.sprite = failedHitSprite;
                noteImage.color = Color.red; // Add color tint
            }
            else
            {
                // Fallback if no sprite - just use color
                noteImage.color = Color.red;
            }
            Debug.Log("[BeatNote] MISSED!");
        }

        // Add a small scale animation for feedback
        StartCoroutine(HitFeedbackAnimation());
    }

    /// <summary>
    /// Plays a small scale animation when the note is hit for better visual feedback.
    /// </summary>
    private IEnumerator HitFeedbackAnimation()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 punchScale = originalScale * 1.3f;
        
        // Scale up quickly
        float timer = 0f;
        float punchDuration = 0.1f;
        while (timer < punchDuration)
        {
            transform.localScale = Vector3.Lerp(originalScale, punchScale, timer / punchDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        
        // Scale back down
        timer = 0f;
        while (timer < punchDuration)
        {
            transform.localScale = Vector3.Lerp(punchScale, originalScale, timer / punchDuration);
            timer += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = originalScale;
    }

    public float GetCurrentProgress()
    {
        if (musicManager == null) return -1f;
        float currentBeatPosition = musicManager.PublicBeatCount + musicManager.GetBeatProgress();
        return (currentBeatPosition - startBeat) / travelTimeInBeats;
    }

    private void DestroyNote()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        onCleanup?.Invoke();
    }
}