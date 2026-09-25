using UnityEngine;

namespace StillHere.Core
{
    /// <summary>
    /// Adds extremely subtle breathing motion to the camera.
    /// Simulates the tiny movements of a person lying still and breathing.
    /// </summary>
    public class BreathingCamera : MonoBehaviour
    {
        public static BreathingCamera Instance { get; private set; }

        [Header("Breathing Rhythm")]
        [Tooltip("Breaths per minute (normal resting: 12-20).")]
        [Range(8f, 25f)]
        [SerializeField] private float breathsPerMinute = 14f;

        [Header("Movement Intensity (Very Subtle)")]
        [Tooltip("Vertical movement from breathing (chest rise). Keep very small!")]
        [Range(0f, 0.01f)]
        [SerializeField] private float verticalAmount = 0.0015f;

        [Tooltip("Tiny random micro-movements. Keep extremely small!")]
        [Range(0f, 0.005f)]
        [SerializeField] private float microMovementAmount = 0.0005f;

        [Tooltip("Subtle rotation sway in degrees. Keep tiny!")]
        [Range(0f, 0.5f)]
        [SerializeField] private float rotationAmount = 0.08f;

        [Header("Smoothing")]
        [Tooltip("How smooth the breathing motion is.")]
        [Range(0.5f, 3f)]
        [SerializeField] private float smoothing = 1.5f;

        [Header("Micro-Movement Speed")]
        [Tooltip("Speed of tiny unconscious movements.")]
        [Range(0.1f, 2f)]
        [SerializeField] private float microSpeed = 0.7f;

        // Internal state
        private Vector3 originalPosition;
        private Quaternion originalRotation;
        private float breathCycle = 0f;
        private float noiseOffsetX;
        private float noiseOffsetY;
        private float noiseOffsetZ;
        private Vector3 currentOffset;
        private Vector3 currentRotationOffset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Store original transform
            originalPosition = transform.localPosition;
            originalRotation = transform.localRotation;

            // Random noise offsets for variety
            noiseOffsetX = Random.Range(0f, 100f);
            noiseOffsetY = Random.Range(0f, 100f);
            noiseOffsetZ = Random.Range(0f, 100f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void LateUpdate()
        {
            UpdateBreathing();
            ApplyMovement();
        }

        private void UpdateBreathing()
        {
            // Calculate breath cycle (0 to 1, repeating)
            float breathFrequency = breathsPerMinute / 60f; // Hz
            breathCycle += Time.deltaTime * breathFrequency;
            if (breathCycle > 1f) breathCycle -= 1f;

            // Breathing curve: slower exhale than inhale (more natural)
            // Using a modified sine wave
            float breathPhase = breathCycle * Mathf.PI * 2f;
            float breathHeight = (Mathf.Sin(breathPhase) + 1f) * 0.5f; // 0 to 1
            
            // Add slight pause at bottom of breath (exhale hold)
            breathHeight = Mathf.Pow(breathHeight, 0.85f);

            // Vertical movement from breathing
            float verticalOffset = breathHeight * verticalAmount;

            // Micro-movements using Perlin noise (tiny unconscious shifts)
            float time = Time.time * microSpeed;
            float microX = (Mathf.PerlinNoise(time + noiseOffsetX, 0f) - 0.5f) * 2f * microMovementAmount;
            float microY = (Mathf.PerlinNoise(time + noiseOffsetY, 1f) - 0.5f) * 2f * microMovementAmount;
            float microZ = (Mathf.PerlinNoise(time + noiseOffsetZ, 2f) - 0.5f) * 2f * microMovementAmount * 0.5f;

            // Target offset
            Vector3 targetOffset = new Vector3(microX, verticalOffset + microY, microZ);

            // Smooth the movement
            currentOffset = Vector3.Lerp(currentOffset, targetOffset, Time.deltaTime * smoothing);

            // Tiny rotation (head micro-movements)
            float rotX = (Mathf.PerlinNoise(time * 0.5f + noiseOffsetX, 3f) - 0.5f) * rotationAmount;
            float rotY = (Mathf.PerlinNoise(time * 0.3f + noiseOffsetY, 4f) - 0.5f) * rotationAmount * 0.5f;
            float rotZ = (Mathf.PerlinNoise(time * 0.4f + noiseOffsetZ, 5f) - 0.5f) * rotationAmount * 0.3f;

            Vector3 targetRotation = new Vector3(rotX, rotY, rotZ);
            currentRotationOffset = Vector3.Lerp(currentRotationOffset, targetRotation, Time.deltaTime * smoothing);
        }

        private void ApplyMovement()
        {
            // Apply position offset
            transform.localPosition = originalPosition + currentOffset;

            // Apply rotation offset
            transform.localRotation = originalRotation * Quaternion.Euler(currentRotationOffset);
        }

        /// <summary>
        /// Temporarily intensify breathing (emotional moment).
        /// </summary>
        public void SetIntensity(float multiplier, float duration = 2f)
        {
            StartCoroutine(IntensityPulse(multiplier, duration));
        }

        private System.Collections.IEnumerator IntensityPulse(float multiplier, float duration)
        {
            float originalVertical = verticalAmount;
            float originalMicro = microMovementAmount;
            float originalRotation = rotationAmount;

            verticalAmount *= multiplier;
            microMovementAmount *= multiplier;
            rotationAmount *= multiplier;

            yield return new WaitForSeconds(duration);

            // Fade back to normal
            float elapsed = 0f;
            float fadeDuration = 1f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeDuration;
                verticalAmount = Mathf.Lerp(verticalAmount, originalVertical, t);
                microMovementAmount = Mathf.Lerp(microMovementAmount, originalMicro, t);
                rotationAmount = Mathf.Lerp(rotationAmount, originalRotation, t);
                yield return null;
            }

            verticalAmount = originalVertical;
            microMovementAmount = originalMicro;
            rotationAmount = originalRotation;
        }

        /// <summary>
        /// Reset to original position (for cutscenes).
        /// </summary>
        public void ResetToOriginal()
        {
            currentOffset = Vector3.zero;
            currentRotationOffset = Vector3.zero;
            transform.localPosition = originalPosition;
            transform.localRotation = originalRotation;
        }

        /// <summary>
        /// Update the "original" position if camera moves to new location.
        /// </summary>
        public void SetNewOrigin()
        {
            originalPosition = transform.localPosition - currentOffset;
            originalRotation = transform.localRotation * Quaternion.Inverse(Quaternion.Euler(currentRotationOffset));
        }
    }
}

