using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UI;

namespace Gameplay
{
    public class PauseManager : MonoBehaviour
    {
        public static PauseManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button defaultSelectedButton;
        [SerializeField] private List<Button> pauseButtons = new List<Button>();
        
        private GameControls gameControls;
        private bool isPaused = false;
        private UnscaledTimeEventSystem customEventSystem;

        private void Awake()
        {
            Instance = this;
            gameControls = new GameControls();
            
            // Create custom event system
            customEventSystem = gameObject.AddComponent<UnscaledTimeEventSystem>();
        }

        private void OnEnable()
        {
            gameControls.Enable();
            gameControls.Gameplay.Pause.performed += OnPausePressed;
            gameControls.UI.Cancel.performed += OnUICancel;
            
            // Debug UI actions
            gameControls.UI.Navigate.performed += OnUINavigate;
            gameControls.UI.Submit.performed += OnUISubmit;
        }

        private void OnUINavigate(InputAction.CallbackContext context)
        {
            Vector2 navigationInput = context.ReadValue<Vector2>();
            Debug.Log($"[PauseManager] UI Navigate: {navigationInput}");
            
            // Handle navigation with custom event system
            if (customEventSystem != null)
            {
                customEventSystem.HandleNavigation(navigationInput);
            }
        }

        private void OnUISubmit(InputAction.CallbackContext context)
        {
            Debug.Log("[PauseManager] UI Submit pressed");
            
            // Handle submit with custom event system
            if (customEventSystem != null)
            {
                customEventSystem.HandleSubmit();
            }
        }

        private void OnDisable()
        {
            gameControls.Gameplay.Pause.performed -= OnPausePressed;
            gameControls.UI.Cancel.performed -= OnUICancel;
            gameControls.UI.Navigate.performed -= OnUINavigate;
            gameControls.UI.Submit.performed -= OnUISubmit;
            gameControls.Disable();
        }

        private void OnPausePressed(InputAction.CallbackContext context)
        {
            if (!isPaused)
            {
                PauseGame();
            }
        }

        private void OnUICancel(InputAction.CallbackContext context)
        {
            if (isPaused)
            {
                ResumeGame();
            }
        }

        /// <summary>
        /// Met le jeu en pause et bascule les contrôles en mode UI.
        /// </summary>
        public void PauseGame()
        {
            isPaused = true;
            Time.timeScale = 0f;
            
            // Switch to UI input mode
            gameControls.Gameplay.Disable();
            gameControls.UI.Enable();
            
            Debug.Log($"[PauseManager] Input maps switched - Gameplay: {gameControls.Gameplay.enabled}, UI: {gameControls.UI.enabled}");
            
            // Activate pause panel
            if (pausePanel != null)
            {
                pausePanel.SetActive(true);
                
                // Initialize custom event system
                if (customEventSystem != null)
                {
                    // Auto-populate buttons if not manually set
                    if (pauseButtons.Count == 0)
                    {
                        pauseButtons.AddRange(pausePanel.GetComponentsInChildren<Button>());
                    }
                    
                    int defaultIndex = defaultSelectedButton != null ? pauseButtons.IndexOf(defaultSelectedButton) : 0;
                    customEventSystem.Initialize(pauseButtons, defaultIndex);
                    customEventSystem.SetActive(true);
                }
            }
            else
            {
                Debug.LogWarning("[PauseManager] pausePanel is null!");
            }
            
            Debug.Log("[PauseManager] Jeu en pause - Mode UI activé");
        }


        public void ResumeGame()
        {
            Debug.Log("[PauseManager] ResumeGame() appelé");
            
            isPaused = false;
            Time.timeScale = 1f;
            
            // Switch back to Gameplay input mode
            gameControls.UI.Disable();
            gameControls.Gameplay.Enable();
            
            // Deactivate pause panel
            if (pausePanel != null)
            {
                pausePanel.SetActive(false);
            }
            
            // Deactivate custom event system
            if (customEventSystem != null)
            {
                customEventSystem.SetActive(false);
            }
            
            // Clear UI selection
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            
            Debug.Log("[PauseManager] Jeu reprend - Mode Gameplay activé");
        }

        public void QuitToHub()
        {
            // First resume to reset time scale
            Time.timeScale = 1f;
            
            // Load hub scene
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadHub();
            }
            else
            {
                Debug.LogError("[PauseManager] GameManager.Instance est null! Impossible de retourner au hub.");
            }
        }

        public void OpenOptions()
        {
            // TODO: Implémenter l'ouverture du menu d'options
            Debug.Log("[PauseManager] Options menu - À implémenter");
        }

        public bool IsPaused => isPaused;

        private void OnDestroy()
        {
            if (gameControls != null)
            {
                gameControls.Dispose();
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}