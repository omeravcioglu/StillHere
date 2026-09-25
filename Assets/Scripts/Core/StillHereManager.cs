using UnityEngine;
using UnityEngine.InputSystem;
using StillHere.Input;
using StillHere.Audio;

namespace StillHere.Core
{
    /// <summary>
    /// Central manager for Still, Here. 
    /// Initializes and coordinates all core systems.
    /// Add this to a single GameObject in your scene.
    /// </summary>
    public class StillHereManager : MonoBehaviour
    {
        public static StillHereManager Instance { get; private set; }

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("References")]
        [SerializeField] private Camera mainCamera;

        [Header("Auto-Create Missing Components")]
        [Tooltip("If true, missing systems will be created automatically.")]
        [SerializeField] private bool autoCreateSystems = true;

        // System references
        private StillHereInput inputSystem;
        private AudioFocusSystem audioFocus;
        private BlinkController blinkController;
        private TimeFlowController timeFlow;
        private SpatialAudioSetup spatialAudio;

        /// <summary>
        /// True when all systems are initialized and ready.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Invoked when all systems are ready.
        /// </summary>
        public event System.Action OnSystemsReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            InitializeSystems();
        }

        private void InitializeSystems()
        {
            // Input System
            inputSystem = GetComponent<StillHereInput>();
            if (inputSystem == null && autoCreateSystems)
            {
                inputSystem = gameObject.AddComponent<StillHereInput>();
            }
            if (inputSystem != null && inputActions != null)
            {
                // Set input actions via reflection or serialized field
                SetInputActions(inputSystem, inputActions);
            }

            // Audio Focus System
            audioFocus = GetComponent<AudioFocusSystem>();
            if (audioFocus == null && autoCreateSystems)
            {
                audioFocus = gameObject.AddComponent<AudioFocusSystem>();
            }

            // Spatial Audio Setup
            spatialAudio = GetComponent<SpatialAudioSetup>();
            if (spatialAudio == null && autoCreateSystems)
            {
                spatialAudio = gameObject.AddComponent<SpatialAudioSetup>();
            }

            // Blink Controller
            blinkController = GetComponent<BlinkController>();
            if (blinkController == null && autoCreateSystems)
            {
                blinkController = gameObject.AddComponent<BlinkController>();
            }

            // Time Flow Controller
            timeFlow = GetComponent<TimeFlowController>();
            if (timeFlow == null && autoCreateSystems)
            {
                timeFlow = gameObject.AddComponent<TimeFlowController>();
            }

            IsReady = true;
            OnSystemsReady?.Invoke();

            Debug.Log("[StillHereManager] All systems initialized.");
        }

        private void SetInputActions(StillHereInput input, InputActionAsset actions)
        {
            // Use serialization to set the private field
            var field = typeof(StillHereInput).GetField("inputActions", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(input, actions);
            }
        }

        /// <summary>
        /// Update camera reference for all systems.
        /// Call this after scene transitions if camera changes.
        /// </summary>
        public void SetCamera(Camera cam)
        {
            mainCamera = cam;

            if (inputSystem != null)
            {
                inputSystem.SetCamera(cam);
            }

            if (audioFocus != null)
            {
                audioFocus.SetCamera(cam);
            }
        }

        /// <summary>
        /// Trigger a scene transition with blink.
        /// </summary>
        public void TransitionWithBlink(System.Action onDark, System.Action onComplete = null)
        {
            if (blinkController != null)
            {
                blinkController.TriggerBlink(0.5f, () =>
                {
                    onDark?.Invoke();
                });

                if (onComplete != null)
                {
                    blinkController.OnEyesOpened += OnTransitionComplete;
                    void OnTransitionComplete()
                    {
                        blinkController.OnEyesOpened -= OnTransitionComplete;
                        onComplete.Invoke();
                    }
                }
            }
            else
            {
                onDark?.Invoke();
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// Slow down time for an emotional moment.
        /// </summary>
        public void EmotionalMoment(float duration = 3f, float timeScale = 0.5f)
        {
            if (timeFlow != null)
            {
                timeFlow.TransitionToTimeScale(timeScale, 0.5f, () =>
                {
                    // Hold, then return
                    StartCoroutine(HoldAndRelease(duration));
                });
            }
        }

        private System.Collections.IEnumerator HoldAndRelease(float duration)
        {
            yield return new WaitForSecondsRealtime(duration);
            
            if (timeFlow != null)
            {
                timeFlow.ReleaseOverride();
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Validate Setup")]
        private void ValidateSetup()
        {
            bool valid = true;

            if (inputActions == null)
            {
                Debug.LogWarning("[StillHereManager] No InputActionAsset assigned!");
                valid = false;
            }

            if (mainCamera == null)
            {
                Debug.LogWarning("[StillHereManager] No camera assigned. Will try to find Camera.main at runtime.");
            }

            if (valid)
            {
                Debug.Log("[StillHereManager] Setup looks good!");
            }
        }
#endif
    }
}

