using UnityEngine;
using System.Collections.Generic;
using StillHere.Input;

namespace StillHere.Audio
{
    /// <summary>
    /// Manages 3D spatial audio focus. Sounds closer to where the player
    /// is looking (mouse position) become clearer; others fade to background.
    /// </summary>
    public class AudioFocusSystem : MonoBehaviour
    {
        public static AudioFocusSystem Instance { get; private set; }

        [Header("Focus Settings")]
        [Tooltip("How wide the attention cone is. Higher = more sounds are clear at once.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float focusWidth = 0.3f;

        [Tooltip("How smoothly focus transitions between sounds.")]
        [SerializeField] private float focusSmoothing = 3f;

        [Tooltip("Minimum volume multiplier for unfocused sounds.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float unfocusedVolumeMin = 0.15f;

        [Tooltip("Low-pass filter cutoff for unfocused sounds (Hz). Lower = more muffled.")]
        [SerializeField] private float unfocusedLowPassCutoff = 800f;

        [Tooltip("Low-pass cutoff for focused sounds (Hz). Higher = clearer.")]
        [SerializeField] private float focusedLowPassCutoff = 22000f;

        [Header("Spatial Audio")]
        [Tooltip("Enable HRTF-style panning for headphone spatialization.")]
        [SerializeField] private bool enableSpatialBlend = true;

        [Header("References")]
        [SerializeField] private Camera mainCamera;

        // All focusable audio sources in the scene
        private List<FocusableAudioSource> focusableSources = new List<FocusableAudioSource>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (StillHereInput.Instance == null || mainCamera == null) return;

            Vector3 focusPoint = StillHereInput.Instance.FocusWorldPoint;
            Ray focusRay = StillHereInput.Instance.FocusRay;

            UpdateAllSources(focusPoint, focusRay);
        }

        private void UpdateAllSources(Vector3 focusPoint, Ray focusRay)
        {
            foreach (var source in focusableSources)
            {
                if (source == null || source.AudioSource == null) continue;

                float focusAmount = CalculateFocusAmount(source.transform.position, focusPoint, focusRay);
                source.UpdateFocus(focusAmount, focusSmoothing, unfocusedVolumeMin, 
                                   unfocusedLowPassCutoff, focusedLowPassCutoff);
            }
        }

        private float CalculateFocusAmount(Vector3 sourcePosition, Vector3 focusPoint, Ray focusRay)
        {
            // Calculate how close this source is to the focus ray
            Vector3 toSource = sourcePosition - focusRay.origin;
            float distanceAlongRay = Vector3.Dot(toSource, focusRay.direction);
            
            if (distanceAlongRay < 0.1f) 
            {
                // Source is behind camera
                return 0f;
            }

            Vector3 closestPointOnRay = focusRay.origin + focusRay.direction * distanceAlongRay;
            float distanceFromRay = Vector3.Distance(sourcePosition, closestPointOnRay);

            // Normalize by distance (farther sounds need less precision)
            float normalizedDistance = distanceFromRay / Mathf.Max(distanceAlongRay * focusWidth, 0.5f);

            // Convert to focus amount (1 = fully focused, 0 = not focused)
            float focusAmount = 1f - Mathf.Clamp01(normalizedDistance);

            // Apply a curve for more natural falloff
            focusAmount = focusAmount * focusAmount * (3f - 2f * focusAmount); // Smoothstep

            return focusAmount;
        }

        /// <summary>
        /// Register an audio source to be affected by the focus system.
        /// </summary>
        public void Register(FocusableAudioSource source)
        {
            if (!focusableSources.Contains(source))
            {
                focusableSources.Add(source);
                
                // Configure for 3D spatial audio
                if (enableSpatialBlend && source.AudioSource != null)
                {
                    source.AudioSource.spatialBlend = 1f; // Full 3D
                    source.AudioSource.spread = 30f; // Tight spread for headphones
                    source.AudioSource.dopplerLevel = 0f; // No doppler for stationary sources
                    source.AudioSource.rolloffMode = AudioRolloffMode.Custom;
                }
            }
        }

        /// <summary>
        /// Unregister an audio source from the focus system.
        /// </summary>
        public void Unregister(FocusableAudioSource source)
        {
            focusableSources.Remove(source);
        }

        /// <summary>
        /// Get the current focus amount for a world position (0-1).
        /// </summary>
        public float GetFocusAmountAt(Vector3 worldPosition)
        {
            if (StillHereInput.Instance == null) return 0.5f;
            
            return CalculateFocusAmount(
                worldPosition, 
                StillHereInput.Instance.FocusWorldPoint, 
                StillHereInput.Instance.FocusRay
            );
        }

        public void SetCamera(Camera cam)
        {
            mainCamera = cam;
        }
    }

    /// <summary>
    /// Attach to any AudioSource to make it respond to the player's focus.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class FocusableAudioSource : MonoBehaviour
    {
        [Header("Focus Behavior")]
        [Tooltip("Base volume of this source when fully focused.")]
        [SerializeField] private float baseVolume = 1f;

        [Tooltip("If true, this sound is always somewhat audible even when unfocused.")]
        [SerializeField] private bool isAmbient = false;

        [Tooltip("Ambient sounds have a higher minimum volume when unfocused.")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientMinVolume = 0.4f;

        [Header("Character Voice")]
        [Tooltip("If true, treated as dialogue with special focus behavior.")]
        [SerializeField] private bool isVoice = false;

        [Tooltip("Voices get priority when speaking.")]
        [SerializeField] private float voiceFocusBoost = 0.3f;

        public AudioSource AudioSource { get; private set; }
        public float CurrentFocusAmount { get; private set; }
        public float BaseVolume => baseVolume;
        public bool IsVoice => isVoice;

        private AudioLowPassFilter lowPassFilter;
        private float smoothedFocus;

        private void Awake()
        {
            AudioSource = GetComponent<AudioSource>();
            
            // Add low-pass filter for muffling unfocused sounds
            lowPassFilter = GetComponent<AudioLowPassFilter>();
            if (lowPassFilter == null)
            {
                lowPassFilter = gameObject.AddComponent<AudioLowPassFilter>();
            }
            lowPassFilter.cutoffFrequency = 22000f;
        }

        private void OnEnable()
        {
            if (AudioFocusSystem.Instance != null)
            {
                AudioFocusSystem.Instance.Register(this);
            }
        }

        private void OnDisable()
        {
            if (AudioFocusSystem.Instance != null)
            {
                AudioFocusSystem.Instance.Unregister(this);
            }
        }

        private void Start()
        {
            // Ensure registration if system was already running
            if (AudioFocusSystem.Instance != null)
            {
                AudioFocusSystem.Instance.Register(this);
            }
        }

        public void UpdateFocus(float focusAmount, float smoothing, float minVolume, 
                                float unfocusedCutoff, float focusedCutoff)
        {
            CurrentFocusAmount = focusAmount;

            // Apply voice boost if this is a speaking character
            float adjustedFocus = focusAmount;
            if (isVoice && AudioSource.isPlaying)
            {
                adjustedFocus = Mathf.Min(1f, focusAmount + voiceFocusBoost);
            }

            // Smooth the transition
            smoothedFocus = Mathf.Lerp(smoothedFocus, adjustedFocus, Time.deltaTime * smoothing);

            // Calculate effective minimum volume
            float effectiveMinVolume = isAmbient ? Mathf.Max(minVolume, ambientMinVolume) : minVolume;

            // Apply volume
            float targetVolume = Mathf.Lerp(effectiveMinVolume, 1f, smoothedFocus) * baseVolume;
            AudioSource.volume = targetVolume;

            // Apply low-pass filter (muffling)
            if (lowPassFilter != null)
            {
                float targetCutoff = Mathf.Lerp(unfocusedCutoff, focusedCutoff, smoothedFocus);
                lowPassFilter.cutoffFrequency = targetCutoff;
            }
        }

        /// <summary>
        /// Set the base volume (useful for dialogue system).
        /// </summary>
        public void SetBaseVolume(float volume)
        {
            baseVolume = volume;
        }
    }
}

