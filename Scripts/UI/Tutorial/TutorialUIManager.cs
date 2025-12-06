using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Manages the visual presentation of tutorial content in the UI.
/// Handles tutorial panel display, text updates, and smooth fade transitions.
/// Ensures proper visibility control for tutorial instructions during gameplay.
/// </summary>
public class TutorialUIManager : MonoBehaviour
{
    /// <summary>Root panel object for the tutorial UI.</summary>
    [SerializeField] private GameObject panelRoot;
    /// <summary>Text component for displaying tutorial instructions.</summary>
    [SerializeField] private TextMeshProUGUI tutorialTextDisplay;
    /// <summary>Duration for fade in/out animations.</summary>
    [SerializeField] private float fadeDuration = 0.3f;

    /// <summary>Canvas group for controlling panel visibility and interaction.</summary>
    private CanvasGroup canvasGroup;
    /// <summary>Current fade animation coroutine reference.</summary>
    private Coroutine currentFadeCoroutine;

    /// <summary>
    /// Initializes the tutorial UI manager by setting up canvas group and initial visibility state.
    /// </summary>
    private void Awake()
    {
        // Ensure CanvasGroup component exists
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Set initial state: invisible and non-interactable but keep object active
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        // Default panel root to this game object if not assigned
        if (panelRoot == null)
        {
            panelRoot = this.gameObject;
        }
    }

    /// <summary>
    /// Displays a tutorial step with the specified content.
    /// Updates text and shows the tutorial panel with fade animation.
    /// </summary>
    /// <param name="step">Tutorial step to display, or null to hide panel.</param>
    public void ShowStep(TutorialStep step)
    {
        if (step == null)
        {
            HidePanel();
            return;
        }

        tutorialTextDisplay.text = step.tutorialText;

        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadePanel(true));
    }

    public void HidePanel()
    {
        if (currentFadeCoroutine != null)
        {
            StopCoroutine(currentFadeCoroutine);
        }
        currentFadeCoroutine = StartCoroutine(FadePanel(false));
    }

    private IEnumerator FadePanel(bool fadeIn)
    {
        // 1. On s'assure que le GameObject est actif AVANT de faire quoi que ce soit.
        // C'est la correction principale de l'erreur.
        if (fadeIn)
        {
            panelRoot.SetActive(true);
        }

        float targetAlpha = fadeIn ? 1f : 0f;
        float startAlpha = canvasGroup.alpha;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            // Utiliser unscaledDeltaTime est une bonne pratique pour les animations d'UI
            elapsedTime += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsedTime / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // Mettre à jour l'interactivité
        canvasGroup.interactable = fadeIn;
        canvasGroup.blocksRaycasts = fadeIn;

        // 2. Si on a fait un fade out, on peut maintenant désactiver le GameObject
        // en toute sécurité, une fois l'animation terminée.
        if (!fadeIn)
        {
            panelRoot.SetActive(false);
        }

        currentFadeCoroutine = null;
    }
}