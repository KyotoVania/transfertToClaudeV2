using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller for displaying and updating health bars for boss units.
/// Manages the instantiation, positioning, and updating of health bar UI elements.
/// </summary>
public class HealthBarController : MonoBehaviour
{
    [Header("Health Bar Settings")]
    /// <summary>
    /// Prefab for the health bar UI canvas.
    /// </summary>
    [Tooltip("Prefab de la barre de vie (HealthBarCanvas) à instancier.")]
    [SerializeField] private GameObject healthBarPrefab;
    
    /// <summary>
    /// Height offset above the unit for positioning the health bar.
    /// </summary>
    [Tooltip("Décalage en hauteur au-dessus de l'unité pour positionner la barre de vie.")]
    [SerializeField] private float heightOffset = 2f;
    
    // Private variables
    /// <summary>
    /// Instance of the spawned health bar canvas.
    /// </summary>
    private GameObject healthBarInstance;
    
    /// <summary>
    /// Slider component for the health bar fill.
    /// </summary>
    private Slider healthSlider;
    
    /// <summary>
    /// Camera used for billboard effect (health bar always faces camera).
    /// </summary>
    private Camera mainCamera;

    /// <summary>
    /// Initializes the camera reference. Health bar spawning is now controlled by the unit.
    /// </summary>
    private void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogWarning("[HealthBarController] Main camera not found. Health bar billboard effect may not work.");
        }
    }

    /// <summary>
    /// Spawns the health bar UI above the unit with the provided health values.
    /// </summary>
    /// <param name="currentHealth">The current health value.</param>
    /// <param name="maxHealth">The maximum health value.</param>
    public void SpawnHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBarPrefab == null)
        {
            Debug.LogError("[HealthBarController] Health bar prefab is not assigned!");
            return;
        }

        Vector3 spawnPosition = transform.position + Vector3.up * heightOffset;
        healthBarInstance = Instantiate(healthBarPrefab, spawnPosition, Quaternion.identity);
        
        // Parent the health bar to this transform so it follows the unit
        healthBarInstance.transform.SetParent(transform, true);
        
        // Find the slider component in the health bar prefab
        healthSlider = healthBarInstance.GetComponentInChildren<Slider>();
        if (healthSlider == null)
        {
            Debug.LogError("[HealthBarController] Slider component not found in health bar prefab!");
            return;
        }

        // Set initial values without animation
        healthSlider.maxValue = maxHealth;
        healthSlider.value = currentHealth;
        Debug.Log($"[HealthBarController] Spawned and initialized health bar: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Updates the health bar display with current health values.
    /// </summary>
    /// <param name="currentHealth">The current health value.</param>
    /// <param name="maxHealth">The maximum health value.</param>
    public void UpdateHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider == null)
        {
            // Health bar not spawned yet, ignore silently
            return;
        }

        // Update max value immediately (no animation needed for max)
        healthSlider.maxValue = maxHealth;
        
        // Animate the current value smoothly
        AnimateHealthValue(currentHealth);
    }

    /// <summary>
    /// Animates the health bar value smoothly using LeanTween.
    /// Optimized for rapid damage with shorter duration and smooth transitions.
    /// </summary>
    /// <param name="targetValue">The target health value to animate to.</param>
    private void AnimateHealthValue(float targetValue)
    {
        if (healthSlider == null) return;

        // Cancel any existing animation on this slider
        LeanTween.cancel(healthSlider.gameObject);

        // Calculate dynamic duration based on distance to target
        float distance = Mathf.Abs(healthSlider.value - targetValue);
        float maxHealth = healthSlider.maxValue;
        float normalizedDistance = distance / maxHealth;
        
        // Shorter duration for rapid hits (0.15s to 0.25s)
        float duration = Mathf.Lerp(0.15f, 0.25f, normalizedDistance);

        // Animate the slider value smoothly
        LeanTween.value(healthSlider.gameObject, healthSlider.value, targetValue, duration)
            .setEase(LeanTweenType.easeOutQuad) // Faster curve for responsiveness
            .setOnUpdate((float val) => {
                if (healthSlider != null)
                    healthSlider.value = val;
            });
    }

    /// <summary>
    /// Updates the health bar position and rotation for billboard effect.
    /// </summary>
    private void LateUpdate()
    {
        if (healthBarInstance == null || mainCamera == null) return;

        // Position the health bar above the unit
        healthBarInstance.transform.position = transform.position + Vector3.up * heightOffset;
        
        // Billboard effect - make health bar face the camera
        Vector3 lookDirection = healthBarInstance.transform.position - mainCamera.transform.position;
        lookDirection.y = 0; // Keep it horizontal
        if (lookDirection != Vector3.zero)
        {
            healthBarInstance.transform.rotation = Quaternion.LookRotation(lookDirection);
        }
    }

    /// <summary>
    /// Cleanup when the controller is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (healthBarInstance != null)
        {
            Destroy(healthBarInstance);
        }
    }
}