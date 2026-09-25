using UnityEngine;
using StillHere.Input;

namespace StillHere.Core
{
    /// <summary>
    /// Controls the perception of time based on player input.
    /// Allows subtle speeding up or slowing down of time.
    /// This is not a pause mechanic—the world always moves, just at different paces.
    /// </summary>
    public class TimeFlowController : MonoBehaviour
    {
        public static TimeFlowController Instance { get; private set; }

        [Header("Time Flow Settings")]
        [Tooltip("Normal time scale (usually 1.0).")]
        [SerializeField] private float normalTimeScale = 1f;

        [Tooltip("Minimum time scale when slowing down.")]
        [Range(0.1f, 0.8f)]
        [SerializeField] private float minTimeScale = 0.3f;

        [Tooltip("Maximum time scale when speeding up.")]
        [Range(1.2f, 4f)]
        [SerializeField] private float maxTimeScale = 2.5f;

        [Tooltip("How smoothly time transitions between speeds.")]
        [SerializeField] private float transitionSpeed = 2f;

        [Tooltip("How quickly time returns to normal when no input.")]
        [SerializeField] private float returnSpeed = 1.5f;

        [Header("Feel")]
        [Tooltip("Curve for how input maps to time change. Allows subtle control at low input.")]
        [SerializeField] private AnimationCurve inputCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("If true, time slowing also slightly affects audio pitch.")]
        [SerializeField] private bool affectAudioPitch = true;

        [Tooltip("How much audio pitch changes with time (0 = none, 1 = matches time scale).")]
        [Range(0f, 0.5f)]
        [SerializeField] private float audioPitchInfluence = 0.15f;

        // State
        private float currentTimeScale = 1f;
        private float targetTimeScale = 1f;
        private float currentInfluence = 0f;

        /// <summary>
        /// Current time scale being applied.
        /// </summary>
        public float CurrentTimeScale => currentTimeScale;

        /// <summary>
        /// True if time is currently slowed.
        /// </summary>
        public bool IsSlowed => currentTimeScale < normalTimeScale - 0.05f;

        /// <summary>
        /// True if time is currently sped up.
        /// </summary>
        public bool IsFast => currentTimeScale > normalTimeScale + 0.05f;

        /// <summary>
        /// How much the player is influencing time (-1 to 1).
        /// </summary>
        public float CurrentInfluence => currentInfluence;

        /// <summary>
        /// Invoked when time scale changes significantly.
        /// </summary>
        public event System.Action<float> OnTimeScaleChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Initialize time scale
            currentTimeScale = normalTimeScale;
            Time.timeScale = currentTimeScale;
        }

        private void OnDestroy()
        {
            // Restore normal time when destroyed
            Time.timeScale = 1f;
            
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            ReadInput();
            UpdateTimeScale();
            ApplyTimeScale();
        }

        private void ReadInput()
        {
            if (StillHereInput.Instance == null)
            {
                currentInfluence = 0f;
                return;
            }

            // Get raw influence from input (-1 to 1)
            float rawInfluence = StillHereInput.Instance.TimeInfluence;

            // Apply curve for more nuanced control
            float sign = Mathf.Sign(rawInfluence);
            float magnitude = Mathf.Abs(rawInfluence);
            currentInfluence = sign * inputCurve.Evaluate(magnitude);
        }

        private void UpdateTimeScale()
        {
            // Calculate target time scale based on influence
            if (Mathf.Abs(currentInfluence) > 0.01f)
            {
                if (currentInfluence < 0)
                {
                    // Slowing down
                    targetTimeScale = Mathf.Lerp(normalTimeScale, minTimeScale, -currentInfluence);
                }
                else
                {
                    // Speeding up
                    targetTimeScale = Mathf.Lerp(normalTimeScale, maxTimeScale, currentInfluence);
                }
            }
            else
            {
                // Return to normal
                targetTimeScale = normalTimeScale;
            }

            // Smooth transition
            float speed = Mathf.Abs(currentInfluence) > 0.01f ? transitionSpeed : returnSpeed;
            float previousScale = currentTimeScale;
            currentTimeScale = Mathf.Lerp(currentTimeScale, targetTimeScale, speed * Time.unscaledDeltaTime);

            // Notify listeners of significant changes
            if (Mathf.Abs(currentTimeScale - previousScale) > 0.01f)
            {
                OnTimeScaleChanged?.Invoke(currentTimeScale);
            }
        }

        private void ApplyTimeScale()
        {
            Time.timeScale = currentTimeScale;
            Time.fixedDeltaTime = 0.02f * currentTimeScale; // Keep physics stable

            // Optionally affect audio pitch
            if (affectAudioPitch)
            {
                // Calculate pitch adjustment (subtle)
                float pitchDelta = (currentTimeScale - normalTimeScale) * audioPitchInfluence;
                AudioListener.volume = 1f; // Keep volume constant
                // Note: Global audio pitch would need AudioMixer control
                // This is a placeholder for the concept
            }
        }

        /// <summary>
        /// Temporarily override time scale (for cutscenes, events).
        /// Call ReleaseOverride() to return to player control.
        /// </summary>
        public void OverrideTimeScale(float scale)
        {
            targetTimeScale = scale;
            currentTimeScale = scale;
            ApplyTimeScale();
        }

        /// <summary>
        /// Release any override and return to player-controlled time.
        /// </summary>
        public void ReleaseOverride()
        {
            targetTimeScale = normalTimeScale;
        }

        /// <summary>
        /// Smoothly transition to a specific time scale over duration.
        /// </summary>
        public void TransitionToTimeScale(float targetScale, float duration, System.Action onComplete = null)
        {
            StartCoroutine(TimeScaleTransition(targetScale, duration, onComplete));
        }

        private System.Collections.IEnumerator TimeScaleTransition(float target, float duration, System.Action onComplete)
        {
            float startScale = currentTimeScale;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                
                // Smooth step
                t = t * t * (3f - 2f * t);
                
                currentTimeScale = Mathf.Lerp(startScale, target, t);
                ApplyTimeScale();
                
                yield return null;
            }

            currentTimeScale = target;
            ApplyTimeScale();
            onComplete?.Invoke();
        }

        /// <summary>
        /// Get a time-adjusted delta for manual calculations.
        /// </summary>
        public float GetPerceivedDeltaTime()
        {
            return Time.unscaledDeltaTime * currentTimeScale;
        }
    }
}

