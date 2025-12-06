using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace UI
{
    public class UnscaledTimeEventSystem : MonoBehaviour
    {
        [Header("UI Navigation")]
        [SerializeField] private List<Button> buttons = new List<Button>();
        [SerializeField] private int currentSelectedIndex = 0;
        
        [Header("Visual Effects")]
        [SerializeField] private float selectedScale = 1.1f;
        [SerializeField] private float animationDuration = 0.2f;
        
        private bool isActive = false;
        private Button currentSelectedButton;

        public void Initialize(List<Button> uiButtons, int defaultSelectedIndex = 0)
        {
            buttons.Clear();
            buttons.AddRange(uiButtons);
            currentSelectedIndex = Mathf.Clamp(defaultSelectedIndex, 0, buttons.Count - 1);
            
            // Visual feedback setup
            UpdateButtonSelection();
        }

        public void SetActive(bool active)
        {
            isActive = active;
            if (active)
            {
                UpdateButtonSelection();
            }
        }

        public void HandleNavigation(Vector2 navigationInput)
        {
            if (!isActive || buttons.Count == 0) return;

            // Horizontal navigation
            if (navigationInput.x > 0.5f)
            {
                NavigateRight();
            }
            else if (navigationInput.x < -0.5f)
            {
                NavigateLeft();
            }
            
            // Vertical navigation
            if (navigationInput.y > 0.5f)
            {
                NavigateUp();
            }
            else if (navigationInput.y < -0.5f)
            {
                NavigateDown();
            }
        }

        public void HandleSubmit()
        {
            if (!isActive || currentSelectedButton == null) return;
            
            // Simulate button click
            currentSelectedButton.onClick.Invoke();
            Debug.Log($"[UnscaledTimeEventSystem] Button clicked: {currentSelectedButton.name}");
        }

        private void NavigateRight()
        {
            currentSelectedIndex = (currentSelectedIndex + 1) % buttons.Count;
            UpdateButtonSelection();
        }

        private void NavigateLeft()
        {
            currentSelectedIndex = (currentSelectedIndex - 1 + buttons.Count) % buttons.Count;
            UpdateButtonSelection();
        }

        private void NavigateUp()
        {
            // For vertical layouts, move to previous button
            NavigateLeft();
        }

        private void NavigateDown()
        {
            // For vertical layouts, move to next button
            NavigateRight();
        }

        private void UpdateButtonSelection()
        {
            if (buttons.Count == 0) return;

            // Clear previous selection visual
            foreach (var button in buttons)
            {
                if (button != null)
                {
                    SetButtonHighlight(button, false);
                }
            }

            // Set new selection
            currentSelectedButton = buttons[currentSelectedIndex];
            if (currentSelectedButton != null)
            {
                SetButtonHighlight(currentSelectedButton, true);
                Debug.Log($"[UnscaledTimeEventSystem] Selected button: {currentSelectedButton.name}");
            }
        }

        private void SetButtonHighlight(Button button, bool highlighted)
        {
            // Color highlight effect
            var colors = button.colors;
            if (highlighted)
            {
                button.targetGraphic.color = colors.highlightedColor;
                // Scale up effect
                StartCoroutine(ScaleButton(button.transform, selectedScale));
            }
            else
            {
                button.targetGraphic.color = colors.normalColor;
                // Scale back to normal
                StartCoroutine(ScaleButton(button.transform, 1f));
            }
        }

        private System.Collections.IEnumerator ScaleButton(Transform buttonTransform, float targetScale)
        {
            Vector3 startScale = buttonTransform.localScale;
            Vector3 endScale = Vector3.one * targetScale;
            
            float elapsed = 0f;
            
            while (elapsed < animationDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / animationDuration;
                
                // Smooth animation curve
                t = Mathf.SmoothStep(0f, 1f, t);
                
                buttonTransform.localScale = Vector3.Lerp(startScale, endScale, t);
                yield return null;
            }
            
            buttonTransform.localScale = endScale;
        }
    }
}