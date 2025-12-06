using UnityEngine;
using TMPro;
using System.Text;
using System.Collections;
using Game.Observers;

public class GoldUI : MonoBehaviour, IGoldObserver
{
    [SerializeField] private TMP_Text goldText;
    
    [Header("Insufficient Gold Feedback")]
    [Tooltip("Durée de l'effet de brillance rouge en secondes")]
    [SerializeField] private float glowDuration = 0.5f;
    
    [Tooltip("Couleur de brillance pour gold insuffisant")]
    [SerializeField] private Color insufficientGoldColor = Color.red;
    
    [Tooltip("Intensité de l'effet de brillance")]
    [SerializeField] private float glowIntensity = 2f;

    private readonly StringBuilder _stringBuilder = new StringBuilder(20);
    private const string GoldPrefix = "Gold: ";
    private Coroutine _glowAnimation;
    private Color _originalTextColor;

    private void Awake()
    {
        if (goldText == null)
        {
            Debug.LogError($"[{nameof(GoldUI)}] Le composant TMP_Text n'est pas assigné!", this);
            enabled = false;
        }
        else
        {
            _originalTextColor = goldText.color;
        }
    }

    private void OnEnable()
    {
        if (!enabled) return;

        if (GoldController.Instance == null)
        {
            Debug.LogError($"[{nameof(GoldUI)}] GoldController.Instance est null! Assurez-vous qu'il est présent dans la scène.", this);
            enabled = false;
            return;
        }

        GoldController.Instance.AddObserver(this);
        OnGoldUpdated(GoldController.Instance.GetCurrentGold());
    }

    private void OnDisable()
    {
        if (enabled && GoldController.Instance != null)
        {
            GoldController.Instance.RemoveObserver(this);
        }
    }

    public void OnGoldUpdated(int newAmount)
    {
        if (!enabled || goldText == null) return;

        _stringBuilder.Clear()
            .Append(GoldPrefix)
            .Append(newAmount);

        goldText.text = _stringBuilder.ToString();
    }

    /// <summary>
    /// Active l'effet de brillance rouge pour indiquer un gold insuffisant
    /// </summary>
    public void ShowInsufficientGoldFeedback()
    {
        if (_glowAnimation != null)
        {
            StopCoroutine(_glowAnimation);
        }
        
        _glowAnimation = StartCoroutine(AnimateInsufficientGoldGlow());
    }

    /// <summary>
    /// Coroutine qui anime l'effet de brillance rouge
    /// </summary>
    private IEnumerator AnimateInsufficientGoldGlow()
    {
        if (goldText == null) yield break;

        float elapsedTime = 0f;
        
        while (elapsedTime < glowDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / glowDuration;
            
            // Effet de pulsation (0 -> 1 -> 0)
            float pulseIntensity = Mathf.Sin(progress * Mathf.PI * 2f) * glowIntensity;
            
            // Interpolation entre la couleur originale et la couleur de brillance
            Color glowColor = Color.Lerp(_originalTextColor, insufficientGoldColor, Mathf.Abs(pulseIntensity));
            goldText.color = glowColor;
            
            yield return null;
        }

        // Retour à la couleur originale
        goldText.color = _originalTextColor;
        _glowAnimation = null;
    }
}