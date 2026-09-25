using UnityEngine;
using UnityEngine.InputSystem;

namespace StillHere.Input
{
    /// <summary>
    /// Central input handler for Still, Here.
    /// Manages the three subtle mechanics: Focus, Blink, and Time flow.
    /// </summary>
    public class StillHereInput : MonoBehaviour
    {
        public static StillHereInput Instance { get; private set; }

        [Header("Input Asset")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Focus Settings")]
        [Tooltip("Reference to the main camera for screen-to-world conversion.")]
        [SerializeField] private Camera mainCamera;

        [Tooltip("How far into the scene to project the focus point.")]
        [SerializeField] private float focusDepth = 10f;

        private InputActionMap presenceMap;
        private InputAction focusPositionAction;
        private InputAction focusGamepadAction;
        private InputAction blinkAction;
        private InputAction timeSlowerAction;
        private InputAction timeFasterAction;

        // --- Focus (Audio Attention) ---
        /// <summary>
        /// Raw mouse position in screen coordinates.
        /// </summary>
        public Vector2 MouseScreenPosition { get; private set; }

        /// <summary>
        /// Normalized focus position (0-1 range, where 0.5,0.5 is screen center).
        /// </summary>
        public Vector2 FocusNormalized { get; private set; }

        /// <summary>
        /// Focus direction from screen center (-1 to 1 range).
        /// </summary>
        public Vector2 FocusDirection { get; private set; }

        /// <summary>
        /// The world-space point the player is focusing on.
        /// </summary>
        public Vector3 FocusWorldPoint { get; private set; }

        /// <summary>
        /// Ray from camera through the focus point.
        /// </summary>
        public Ray FocusRay { get; private set; }

        /// <summary>
        /// Gamepad stick input for focus (alternative to mouse).
        /// </summary>
        public Vector2 FocusGamepadInput { get; private set; }

        /// <summary>
        /// True if using gamepad for focus, false if using mouse.
        /// </summary>
        public bool UsingGamepadFocus { get; private set; }

        // --- Blink ---
        /// <summary>
        /// True while the player is holding the blink input.
        /// </summary>
        public bool IsBlinking { get; private set; }

        /// <summary>
        /// How long the current blink has been held (resets when released).
        /// </summary>
        public float BlinkHoldTime { get; private set; }

        /// <summary>
        /// Invoked when a blink begins (eyes closing).
        /// </summary>
        public event System.Action OnBlinkStart;

        /// <summary>
        /// Invoked when a blink ends (eyes opening). Parameter is total hold duration.
        /// </summary>
        public event System.Action<float> OnBlinkEnd;

        // --- Time Flow ---
        /// <summary>
        /// Current time influence. Negative = slower, Positive = faster, Zero = normal.
        /// Range approximately -1 to 1.
        /// </summary>
        public float TimeInfluence { get; private set; }

        // --- Thresholds ---
        [Header("Blink Settings")]
        [Tooltip("Blinks shorter than this are 'brief' (quick flutter).")]
        [SerializeField] private float briefBlinkThreshold = 0.3f;

        [Tooltip("Blinks longer than this trigger extended fade.")]
        [SerializeField] private float extendedBlinkThreshold = 1.0f;

        public float BriefBlinkThreshold => briefBlinkThreshold;
        public float ExtendedBlinkThreshold => extendedBlinkThreshold;

        /// <summary>
        /// Returns the blink type based on current hold time.
        /// </summary>
        public BlinkType CurrentBlinkType
        {
            get
            {
                if (!IsBlinking) return BlinkType.None;
                if (BlinkHoldTime < briefBlinkThreshold) return BlinkType.Brief;
                if (BlinkHoldTime < extendedBlinkThreshold) return BlinkType.Normal;
                return BlinkType.Extended;
            }
        }

        // Track last significant gamepad input time
        private float lastGamepadInputTime;
        private float lastMouseMoveTime;
        private Vector2 lastMousePosition;
        private const float InputSwitchThreshold = 0.1f;

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

            SetupInputActions();
        }

        private void SetupInputActions()
        {
            if (inputActions == null)
            {
                Debug.LogError("[StillHereInput] No InputActionAsset assigned! " +
                    "Please assign StillHereInputActions in the Inspector.");
                return;
            }

            presenceMap = inputActions.FindActionMap("Presence");
            if (presenceMap == null)
            {
                Debug.LogError("[StillHereInput] Could not find 'Presence' action map!");
                return;
            }

            focusPositionAction = presenceMap.FindAction("FocusPosition");
            focusGamepadAction = presenceMap.FindAction("FocusGamepad");
            blinkAction = presenceMap.FindAction("Blink");
            timeSlowerAction = presenceMap.FindAction("TimeSlower");
            timeFasterAction = presenceMap.FindAction("TimeFaster");

            // Subscribe to blink events
            if (blinkAction != null)
            {
                blinkAction.started += OnBlinkStarted;
                blinkAction.canceled += OnBlinkCanceled;
                Debug.Log("[StillHereInput] Blink action configured: Space / Left Click / A Button");
            }
            else
            {
                Debug.LogError("[StillHereInput] Could not find 'Blink' action!");
            }

            // Enable the action map immediately
            presenceMap.Enable();
            Debug.Log("[StillHereInput] Input system ready. Presence action map enabled.");
        }

        private void OnEnable()
        {
            presenceMap?.Enable();
        }

        private void OnDisable()
        {
            presenceMap?.Disable();
        }

        private void OnDestroy()
        {
            if (blinkAction != null)
            {
                blinkAction.started -= OnBlinkStarted;
                blinkAction.canceled -= OnBlinkCanceled;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            ReadFocus();
            ReadTimeFlow();
            UpdateBlinkHold();
        }

        private void ReadFocus()
        {
            if (mainCamera == null) return;

            // Read mouse position
            Vector2 mousePos = focusPositionAction?.ReadValue<Vector2>() ?? Vector2.zero;
            
            // Read gamepad stick
            Vector2 gamepadInput = focusGamepadAction?.ReadValue<Vector2>() ?? Vector2.zero;

            // Detect which input is active
            if (gamepadInput.sqrMagnitude > 0.01f)
            {
                lastGamepadInputTime = Time.unscaledTime;
            }
            
            if ((mousePos - lastMousePosition).sqrMagnitude > 1f)
            {
                lastMouseMoveTime = Time.unscaledTime;
            }
            lastMousePosition = mousePos;

            // Switch between input methods
            UsingGamepadFocus = lastGamepadInputTime > lastMouseMoveTime + InputSwitchThreshold;

            if (UsingGamepadFocus)
            {
                // Gamepad: stick controls focus relative to center
                FocusGamepadInput = gamepadInput;
                FocusDirection = gamepadInput;
                FocusNormalized = new Vector2(0.5f + gamepadInput.x * 0.5f, 0.5f + gamepadInput.y * 0.5f);
                MouseScreenPosition = new Vector2(Screen.width * FocusNormalized.x, Screen.height * FocusNormalized.y);
            }
            else
            {
                // Mouse: position on screen
                MouseScreenPosition = mousePos;
                FocusNormalized = new Vector2(
                    Mathf.Clamp01(mousePos.x / Screen.width),
                    Mathf.Clamp01(mousePos.y / Screen.height)
                );
                FocusDirection = new Vector2(
                    (FocusNormalized.x - 0.5f) * 2f,
                    (FocusNormalized.y - 0.5f) * 2f
                );
                FocusGamepadInput = Vector2.zero;
            }

            // Calculate world-space focus point
            FocusRay = mainCamera.ScreenPointToRay(new Vector3(MouseScreenPosition.x, MouseScreenPosition.y, 0));
            FocusWorldPoint = FocusRay.GetPoint(focusDepth);
        }

        private void ReadTimeFlow()
        {
            float slower = timeSlowerAction?.ReadValue<float>() ?? 0f;
            float faster = timeFasterAction?.ReadValue<float>() ?? 0f;

            // Combine into single influence value
            TimeInfluence = Mathf.Clamp(faster - slower, -1f, 1f);
        }

        private void UpdateBlinkHold()
        {
            if (IsBlinking)
            {
                BlinkHoldTime += Time.unscaledDeltaTime;
            }
        }

        private void OnBlinkStarted(InputAction.CallbackContext context)
        {
            IsBlinking = true;
            BlinkHoldTime = 0f;
            OnBlinkStart?.Invoke();
        }

        private void OnBlinkCanceled(InputAction.CallbackContext context)
        {
            float duration = BlinkHoldTime;
            IsBlinking = false;
            OnBlinkEnd?.Invoke(duration);
            BlinkHoldTime = 0f;
        }

        /// <summary>
        /// Sets the camera used for focus calculations (call if camera changes).
        /// </summary>
        public void SetCamera(Camera cam)
        {
            mainCamera = cam;
        }
    }

    public enum BlinkType
    {
        None,
        Brief,      // Quick flutter, momentary darkness
        Normal,     // Standard blink
        Extended    // Long close, deeper darkness, time continues
    }
}
