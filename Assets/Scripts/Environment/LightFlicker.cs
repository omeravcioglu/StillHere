using UnityEngine;

namespace StillHere.Environment
{
    /// <summary>
    /// Adds realistic flickering to lights.
    /// Supports various styles: fluorescent hum, dying bulb, candle, storm, etc.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        [Header("Flicker Style")]
        [SerializeField] private FlickerMode mode = FlickerMode.FluorescentSubtle;

        [Header("Intensity")]
        [Tooltip("Base intensity of the light.")]
        [SerializeField] private float baseIntensity = 1f;

        [Tooltip("How much the light can vary from base intensity.")]
        [Range(0f, 1f)]
        [SerializeField] private float flickerStrength = 0.1f;

        [Header("Timing")]
        [Tooltip("Base speed of the flicker effect.")]
        [Range(0.1f, 20f)]
        [SerializeField] private float flickerSpeed = 8f;

        [Tooltip("Random variation in timing.")]
        [Range(0f, 1f)]
        [SerializeField] private float randomness = 0.3f;

        [Header("Occasional Flicker (Fluorescent/Dying modes)")]
        [Tooltip("How often major flickers occur (seconds between).")]
        [SerializeField] private float majorFlickerInterval = 5f;

        [Tooltip("Randomness in major flicker timing.")]
        [SerializeField] private float majorFlickerVariance = 3f;

        [Tooltip("Duration of major flicker event.")]
        [SerializeField] private float majorFlickerDuration = 0.3f;

        [Header("Color Flicker (Optional)")]
        [Tooltip("Enable slight color temperature shifts.")]
        [SerializeField] private bool flickerColor = false;

        [SerializeField] private Color warmColor = new Color(1f, 0.95f, 0.9f);
        [SerializeField] private Color coolColor = new Color(0.95f, 0.98f, 1f);

        public enum FlickerMode
        {
            FluorescentSubtle,  // Barely noticeable hospital hum
            FluorescentOld,     // Occasional noticeable flicker
            DyingBulb,          // Struggling, irregular
            Candle,             // Warm, organic wavering
            Storm,              // Erratic lightning-like
            Pulse,              // Slow rhythmic pulse
            None                // Disabled (for runtime control)
        }

        private Light lightComponent;
        private float noiseOffset;
        private float nextMajorFlicker;
        private float majorFlickerTimer;
        private bool inMajorFlicker;
        private float originalIntensity;

        private void Awake()
        {
            lightComponent = GetComponent<Light>();
            originalIntensity = lightComponent.intensity;
            
            if (baseIntensity <= 0f)
            {
                baseIntensity = originalIntensity;
            }

            noiseOffset = Random.Range(0f, 1000f);
            ScheduleNextMajorFlicker();
        }

        private void Update()
        {
            if (mode == FlickerMode.None)
            {
                lightComponent.intensity = baseIntensity;
                return;
            }

            float flicker = CalculateFlicker();
            lightComponent.intensity = baseIntensity + (flicker * flickerStrength * baseIntensity);

            if (flickerColor)
            {
                float colorT = (flicker + 1f) * 0.5f; // Normalize to 0-1
                lightComponent.color = Color.Lerp(warmColor, coolColor, colorT);
            }
        }

        private float CalculateFlicker()
        {
            float time = Time.time * flickerSpeed;
            float flicker = 0f;

            switch (mode)
            {
                case FlickerMode.FluorescentSubtle:
                    flicker = CalculateFluorescentSubtle(time);
                    break;

                case FlickerMode.FluorescentOld:
                    flicker = CalculateFluorescentOld(time);
                    break;

                case FlickerMode.DyingBulb:
                    flicker = CalculateDyingBulb(time);
                    break;

                case FlickerMode.Candle:
                    flicker = CalculateCandle(time);
                    break;

                case FlickerMode.Storm:
                    flicker = CalculateStorm(time);
                    break;

                case FlickerMode.Pulse:
                    flicker = CalculatePulse(time);
                    break;
            }

            return Mathf.Clamp(flicker, -1f, 1f);
        }

        private float CalculateFluorescentSubtle(float time)
        {
            // Very subtle high-frequency hum
            float hum = Mathf.Sin(time * 12f + noiseOffset) * 0.3f;
            float noise = (Mathf.PerlinNoise(time * 0.5f + noiseOffset, 0f) - 0.5f) * 0.4f;
            
            return (hum + noise) * 0.5f;
        }

        private float CalculateFluorescentOld(float time)
        {
            // Base hum
            float hum = Mathf.Sin(time * 10f + noiseOffset) * 0.2f;
            
            // Check for major flicker
            UpdateMajorFlicker();
            
            if (inMajorFlicker)
            {
                // Erratic flickering during major event
                float erratic = Mathf.Sin(time * 50f) * Mathf.Sin(time * 37f);
                return erratic * 2f;
            }
            
            float noise = (Mathf.PerlinNoise(time + noiseOffset, 0f) - 0.5f) * 0.3f;
            return hum + noise;
        }

        private float CalculateDyingBulb(float time)
        {
            UpdateMajorFlicker();

            if (inMajorFlicker)
            {
                // Complete dropout or intense flicker
                float dropout = Mathf.Sin(time * 80f) > 0.3f ? 1f : -0.9f;
                return dropout;
            }

            // Struggling, irregular base
            float struggle = Mathf.PerlinNoise(time * 2f + noiseOffset, 0f);
            struggle = (struggle - 0.5f) * 2f;
            
            // Occasional micro-dropouts
            float microDropout = Mathf.Sin(time * 30f + noiseOffset) > 0.95f ? -0.5f : 0f;
            
            return struggle * 0.6f + microDropout;
        }

        private float CalculateCandle(float time)
        {
            // Warm, organic wavering using multiple noise layers
            float wave1 = Mathf.PerlinNoise(time * 1.5f + noiseOffset, 0f);
            float wave2 = Mathf.PerlinNoise(time * 3f + noiseOffset + 50f, 1f);
            float wave3 = Mathf.PerlinNoise(time * 0.5f + noiseOffset + 100f, 2f);
            
            // Occasional gust
            float gust = Mathf.PerlinNoise(time * 0.2f + noiseOffset, 3f) > 0.8f 
                ? (Mathf.PerlinNoise(time * 8f, 4f) - 0.5f) * 2f 
                : 0f;

            float combined = (wave1 * 0.5f + wave2 * 0.3f + wave3 * 0.2f - 0.5f) * 2f;
            return combined + gust * 0.5f;
        }

        private float CalculateStorm(float time)
        {
            // Mostly dark with occasional bright flashes
            float flash = Mathf.PerlinNoise(time * 0.3f + noiseOffset, 0f);
            
            if (flash > 0.85f)
            {
                // Lightning flash
                float lightning = Mathf.Sin(time * 50f) > 0f ? 3f : 0.5f;
                return lightning;
            }
            
            // Dim ambient with slight variation
            return -0.7f + Mathf.PerlinNoise(time * 0.5f, 1f) * 0.3f;
        }

        private float CalculatePulse(float time)
        {
            // Slow, rhythmic pulse
            float pulse = Mathf.Sin(time * 0.3f + noiseOffset);
            
            // Smooth it
            pulse = pulse * pulse * Mathf.Sign(pulse);
            
            return pulse;
        }

        private void UpdateMajorFlicker()
        {
            if (inMajorFlicker)
            {
                majorFlickerTimer -= Time.deltaTime;
                if (majorFlickerTimer <= 0f)
                {
                    inMajorFlicker = false;
                    ScheduleNextMajorFlicker();
                }
            }
            else if (Time.time >= nextMajorFlicker)
            {
                inMajorFlicker = true;
                majorFlickerTimer = majorFlickerDuration * Random.Range(0.5f, 1.5f);
            }
        }

        private void ScheduleNextMajorFlicker()
        {
            float variance = Random.Range(-majorFlickerVariance, majorFlickerVariance);
            nextMajorFlicker = Time.time + majorFlickerInterval + variance;
        }

        /// <summary>
        /// Trigger a flicker event manually.
        /// </summary>
        public void TriggerFlicker(float duration = 0.3f)
        {
            inMajorFlicker = true;
            majorFlickerTimer = duration;
        }

        /// <summary>
        /// Set the flicker mode at runtime.
        /// </summary>
        public void SetMode(FlickerMode newMode)
        {
            mode = newMode;
        }

        /// <summary>
        /// Smoothly transition to a new base intensity.
        /// </summary>
        public void SetIntensity(float intensity, float duration = 1f)
        {
            StartCoroutine(TransitionIntensity(intensity, duration));
        }

        private System.Collections.IEnumerator TransitionIntensity(float target, float duration)
        {
            float start = baseIntensity;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                baseIntensity = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            baseIntensity = target;
        }

        /// <summary>
        /// Turn light off with a dying flicker effect.
        /// </summary>
        public void TurnOffWithFlicker(float duration = 1f)
        {
            StartCoroutine(DieSequence(duration));
        }

        private System.Collections.IEnumerator DieSequence(float duration)
        {
            FlickerMode originalMode = mode;
            mode = FlickerMode.DyingBulb;
            flickerStrength = 0.8f;

            yield return new WaitForSeconds(duration * 0.7f);

            // Final death flickers
            for (int i = 0; i < 3; i++)
            {
                lightComponent.intensity = baseIntensity * 0.3f;
                yield return new WaitForSeconds(0.05f);
                lightComponent.intensity = baseIntensity * 0.8f;
                yield return new WaitForSeconds(0.08f);
            }

            lightComponent.intensity = 0f;
            mode = FlickerMode.None;
        }
    }
}


