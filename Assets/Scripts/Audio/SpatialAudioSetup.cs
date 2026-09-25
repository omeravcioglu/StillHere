using UnityEngine;

namespace StillHere.Audio
{
    /// <summary>
    /// Configures Unity's audio system for optimal headphone spatialization.
    /// Attach to a GameObject in your scene (or the InputManager).
    /// </summary>
    public class SpatialAudioSetup : MonoBehaviour
    {
        [Header("Spatializer Settings")]
        [Tooltip("Target sample rate for audio (44100 or 48000 recommended).")]
        [SerializeField] private int sampleRate = 48000;

        [Tooltip("DSP buffer size. Smaller = lower latency, higher CPU.")]
        [SerializeField] private AudioDSPBufferSize dspBufferSize = AudioDSPBufferSize.Default;

        [Header("Listener Settings")]
        [Tooltip("Reference to the AudioListener (usually on the camera).")]
        [SerializeField] private AudioListener audioListener;

        public enum AudioDSPBufferSize
        {
            Default = 1024,
            LowLatency = 256,
            BestPerformance = 4096
        }

        private void Awake()
        {
            ConfigureAudioSettings();
            EnsureAudioListener();
        }

        private void ConfigureAudioSettings()
        {
            // Configure audio for headphone spatialization
            var config = AudioSettings.GetConfiguration();
            
            // Set sample rate
            config.sampleRate = sampleRate;
            
            // Set buffer size for latency
            config.dspBufferSize = (int)dspBufferSize;
            
            // Ensure stereo output (required for headphones)
            config.speakerMode = AudioSpeakerMode.Stereo;
            
            // Apply configuration
            if (!AudioSettings.Reset(config))
            {
                Debug.LogWarning("[SpatialAudioSetup] Could not apply audio configuration. " +
                                "This is normal if audio is already playing.");
            }
        }

        private void EnsureAudioListener()
        {
            if (audioListener != null) return;

            // Find existing listener
            audioListener = FindAnyObjectByType<AudioListener>();
            
            if (audioListener == null)
            {
                // Check main camera
                var mainCam = Camera.main;
                if (mainCam != null)
                {
                    audioListener = mainCam.GetComponent<AudioListener>();
                    if (audioListener == null)
                    {
                        audioListener = mainCam.gameObject.AddComponent<AudioListener>();
                        Debug.Log("[SpatialAudioSetup] Added AudioListener to main camera.");
                    }
                }
            }

            if (audioListener == null)
            {
                Debug.LogWarning("[SpatialAudioSetup] No AudioListener found! " +
                                "3D audio will not work correctly.");
            }
        }

        /// <summary>
        /// Call this if the camera/listener changes during runtime.
        /// </summary>
        public void RefreshListener()
        {
            audioListener = null;
            EnsureAudioListener();
        }
    }
}

